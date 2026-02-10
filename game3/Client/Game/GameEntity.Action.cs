// ============================================================================
// [FILE] GameEntity.Action.cs
// [职责] 动作状态机 (State Machine)

// Visuals.cs（渲染執行）
// GameEntity.cs（狀態控制）、
// ListSprLoader.cs（數據解析）和 
// CustomCharacterProvider.cs（資源分發）

// 職責分配表 (Architect's Master Plan)
// GameEntity.cs (主文件): 存放核心數據屬性與動作常量。
// GameEntity.Action.cs (動作狀態機): 唯一的 SetAction 實作地。處理姿勢偏移（Weapon Offset）。
// GameEntity.Visuals.cs (渲染執行): 負責 AnimatedSprite2D 的圖層管理與幀同步。
// GameEntity.Movement.cs (位移邏輯): 負責坐標平滑移動，並請求 SetAction。
// GameEntity.CombatFx.cs (戰鬥表現): 負責血條、飄字，並在受擊時請求 SetAction。
// GameEntity.Shadow.cs (陰影): 專門負責腳下陰影。
// GameEntity.UI.cs (頭頂 UI): 專門負責名字、聊天氣泡。



// ============================================================================

using Godot;
using Client.Utility;

namespace Client.Game
{
	public partial class GameEntity
	{
		/// <summary>
		/// 【服務器對齊】唯一的動作設置入口
		/// 對齊服務器 ActionCodes.java 和 L1Object.getGfxMode()
		/// rawAction: 0:走, 1:攻, 2:受擊, 3:待機, 8:死亡
		/// 
		/// 【服務器對齊】動作映射規則：
		/// - 基礎動作（0,1,2,3）需要加 _visualBaseAction 偏移（對齊服務器 getGfxMode()）
		/// - 特殊動作（8.death, 15.pickup, 18.spell 等）直接使用，不偏移
		/// - 有些怪物（如弓箭手）只有武器動作（20,21,22,23），沒有基礎動作（0,1,2,3）
		///   這種情況下，_visualBaseAction 應該為 20（弓），而不是 0
		/// 
		/// 【服務器對齊】動作打斷規則（對齊服務器 isLockFreeze()）：
		/// - 可被打斷的動作：待機(3)、行走(0) - 這些動作可以隨時切換
		/// - 必須播放完成的動作：攻擊(1)、受擊(2)、拾取(15)、魔法(18)、死亡(8) - 這些動作必須完整播放
		/// - 服務器在速度檢查失敗時會設置 isLockFreeze(true)，阻止動作執行
		/// - 服務器在 HP/MP 恢復時會設置 isLockFreeze(false)，允許動作執行
		/// </summary>
		public void SetAction(int actionId)
		{
			if (_isVisualLocked) return;

			// 【服務器對齊】動作打斷規則：對齊服務器 isLockFreeze() 邏輯
			// 可被打斷的動作（待機、行走）可以隨時切換，不受 _isActionBusy 限制
			// 必須播放完成的動作（攻擊、受擊、拾取、魔法、死亡）受 _isActionBusy 保護
			// 但為了避免一幀內被刷回站立導致動作看起來閃爍，仍然保留保護
			if (_isActionBusy && actionId == ACT_BREATH)
				return;
			
			// 【服務器對齊】死亡動作必須完整播放，不能被其他動作打斷
			// 如果當前正在播放死亡動畫，不允許切換到其他動作（除了待機，因為死亡動畫結束後會保持死亡狀態）
			if (_currentRawAction == ACT_DEATH && actionId != ACT_BREATH)
				return;

			int finalSequenceId = actionId;
			// 【服務器對齊】對齊服務器動作映射規則
			// 服務器使用 getGfxMode() 來確定動作偏移：
			// - 基礎動作（0,1,2,3）需要加 gfxMode 偏移
			// - 例如：gfxMode=20（弓）時，walk(0) -> 20, attack(1) -> 21, idle(3) -> 23
			// - 特殊動作（8.death, 15.pickup, 18.spell 等）不偏移
			if (actionId >= 0 && actionId <= 3)
			{
				// 【服務器對齊】對齊服務器 getGfxMode() 邏輯
				// 有些怪物（如弓箭手）只有武器動作，_visualBaseAction 應該為武器偏移（如 20）
				finalSequenceId = actionId + _visualBaseAction;
			}
			// 注意：特殊動作（如 8.death, 15.pickup, 18.spell）不偏移，直接使用

			// 判定是否為單次動作 (One-Shot)，需要上鎖
			// 【核心修復】死亡動作不應被視為 One-Shot，不設置 _isActionBusy，確保動畫可以完整播放
			bool isOneShot = actionId == ACT_ATTACK || actionId == ACT_DAMAGE || 
							 actionId == ACT_PICKUP || actionId == ACT_SPELL_DIR || 
							 actionId == ACT_ATTACK_BOW;

			if (isOneShot)
			{
				_isActionBusy = true;
				_isAttackHitTriggered = false;
			}

			// 防止重複設置（攻擊動作除外，因為攻擊需要重置動畫）
			// 【核心修復】死亡動作也不應被重複設置檢查阻止，確保每次設置死亡動作都能正確播放
			if (CurrentAction == finalSequenceId && actionId != ACT_ATTACK && actionId != ACT_DEATH) return;

			// 【核心修復】設置 _currentRawAction 為 actionId，確保 OnUnifiedAnimationFinished 能正確識別死亡動作
			_currentRawAction = actionId;
			CurrentAction = finalSequenceId;
			
			// 【死亡動畫控制】僅在死亡動作第一次設置時重置動畫播放
			// 避免 RefreshVisual 重複重置導致只播放第一幀
			if (actionId == ACT_DEATH)
				_deathAnimationRestart = true;
			else
				_deathAnimationRestart = false;
			
			// 驅動視覺刷新
			RefreshVisual();
		}

		public void SetHeading(int heading)
		{
			if (_isVisualLocked || _heading == heading) return;
			_heading = heading;
			RefreshVisual();
		}

		/// <summary>
		/// 當動畫播放結束時的回調 (由 Visuals 層信號觸發)
		// 為什麼要設為 _lastProcessedFrame-1？
		// 強制觸發同步： 當動畫從最後一幀播放完畢「回歸到第 0 幀」時，或者你「切換了一個新動作」時，如果 _lastProcessedFrame 仍然停留在舊的數值，系統可能會誤以為「這一幀已經對齊過了」而直接 return。
		// 解決「第一幀閃爍」： 通過將其設為 -1，下一次動畫播放任何幀（從 0 開始）時，f == _lastProcessedFrame (例如 0 == -1) 必定為假。這會強制程序執行 UpdateLayerOffset，確保新動作的第一幀位置就是正確的。
		// 確保多層渲染（主體、陰影、服裝）在切換動作或循環播放時不會出現對齊錯誤或邏輯跳過
		// 有了它： 無論你如何切換動作或 GfxId，系統都會在圖片顯示出來的同時，完成最精確的坐標計算。這也是為什麼你現在能看到「陰影+角色+clothes完美對齊」的重要保障。
	
		/// </summary>
		private void OnUnifiedAnimationFinished()
		{
			_lastProcessedFrame = -1;
			_isActionBusy = false;
			// [與 PakBrowser 一致] 主體非循環動畫結束時，陰影/武器/服裝幀同步到主體當前幀
			SyncSlaveLayersToBodyFrame();

			// 死亡動畫播完：不切待機，維持死亡狀態
			if (_currentRawAction == ACT_DEATH) return;
			var def = ListSprLoader.Get(GfxId);
			var idleSeq = ListSprLoader.GetIdleSequence(def);
			if (idleSeq != null)
			{
				// 【關鍵修復】待機動作（如 28.standby）也需要加 _visualBaseAction 偏移
				// 因為這些動作有8方向，應該映射（如 28 + 3 = 31）
				// GetActionSequence 會通過語義關鍵字（standby/idle）找到對應的序列
				CurrentAction = idleSeq.ActionId + _visualBaseAction;
				// 【關鍵修復】同時更新 _currentRawAction，確保 RefreshVisual() 使用正確的值
				_currentRawAction = idleSeq.ActionId;
				RefreshVisual();
			}
			else
				StopAllLayersOnFirstFrame();
		}
	}
}
