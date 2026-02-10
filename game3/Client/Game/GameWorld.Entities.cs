// ============================================================================
// [FILE] GameWorld.Entities.cs
// 说明：本文件集中管理“世界实体生命周期相关回调与工具函数”：
// - OnObjectSpawned / OnObjectDeleted / Heading / Action / Effect / Chat
// - IsPlayerSelf / SetupPlayerCamera
// 规则：不改任何功能逻辑，只从原 GameWorld.cs 搬运归类，并用注释包围段落。
// 1. 补充 _skin 字段定义。
// 2. 在 OnObjectSpawned 中自动实例化 CustomSkinLoader。
// 3. [修复] IsPlayerSelf 移除硬编码，对接 Boot.CurrentCharName。
// ============================================================================

using System;
using System.Collections.Generic;
using Godot;
using Client.Data;
using Core.Interfaces; // 引用接口命名空间
using Skins.CustomFantasy; // 【新增】引用皮肤命名空间
using Client.Utility; // 使用 ListSprLoader 檢查是否存在 8.Death 動畫

namespace Client.Game
{
	public partial class GameWorld
	{
		// 【新增】解决 CS0103: _skin 字段定义
		// 由于移除了 InitSkin 注入，这里作为私有成员自我管理
		private ISkinBridge _skin;

		// =====================================================================
		// [SECTION] 視野範圍與性能優化（三層過濾機制）
		// =====================================================================
		/// <summary>
		/// 【性能優化】三層視野範圍過濾：
		/// - 0-10格：完整處理（座標更新 + 動畫播放）- 用於邏輯計算和視覺表現
		/// - 10-15格：僅視覺（不更新座標，只播放動畫）- 用於預警，減少計算開銷
		/// - 15格外：完全跳過（不更新座標，不播放動畫）- 等待服務器自動移除
		/// </summary>
		private const int LOGIC_RANGE_CELLS = 10;   // 邏輯計算範圍（座標更新）
		private const int VISUAL_RANGE_CELLS = 15;  // 視覺範圍（動畫播放）
  		
		/// <summary>
		/// 【核心修復】計算實體與玩家的距離
		/// 使用 Euclidean distance 對齊服務器邏輯（服務器使用 Math.sqrt(dx*dx + dy*dy)）
		/// 服務器代碼：L1Object.getDistance() 使用 Euclidean distance
		/// 用於：攻擊距離檢查、視野範圍判斷（必須對齊服務器）
		/// </summary>
		public int GetEntityDistance(int mapX, int mapY)
		{
			if (_myPlayer == null) return 0;
			long dx = mapX - _myPlayer.MapX;
			long dy = mapY - _myPlayer.MapY;
			double distance = Math.Sqrt(dx * dx + dy * dy);
			return (int)distance; // 對齊服務器邏輯：Math.sqrt 後轉換為 int
		}
		
		/// <summary>實體是否在邏輯計算範圍內（0-10格）- 需要更新座標</summary>
		private bool IsEntityInLogicRange(int mapX, int mapY)
		{
			return GetEntityDistance(mapX, mapY) <= LOGIC_RANGE_CELLS;
		}
		
		/// <summary>
		/// 實體是否在視覺範圍內（0-15格）- 需要播放動畫
		/// 【核心修復】使用 Euclidean distance 對齊服務器邏輯
		/// 服務器使用 Euclidean distance 和 14格過濾，客戶端使用 15格（稍寬鬆以確保不遺漏）
		/// </summary>
		private bool IsEntityInVisualRange(int mapX, int mapY)
		{
			return GetEntityDistance(mapX, mapY) <= VISUAL_RANGE_CELLS;
		}
		
		/// <summary>實體是否在視野範圍內（兼容舊代碼，實際調用 IsEntityInVisualRange）</summary>
		private bool IsEntityInViewRange(int mapX, int mapY)
		{
			return IsEntityInVisualRange(mapX, mapY);
		}

		// =====================================================================
		// [SECTION] Packet Callbacks: Spawn (对象出现/传送支持)
		// 说明：
		// - 新对象：Instantiate EntityScene -> Init -> AddChild -> 入字典
		// - 已存在对象：视为“传送/重刷”，强制更新坐标/朝向，并对 self 中断移动
		// - 非自己且距離過遠：Init(loadVisuals: false)，延後加載圖像，進入範圍後在 _Process 中載入
		// =====================================================================
		private void OnObjectSpawned(WorldObject data)
		{
			// 【核心修复】传送支持：如果对象已存在，不再忽略，而是强制更新位置！
			if (_entities.TryGetValue(data.ObjectId, out GameEntity existingEntity))
			{
				// 【關鍵修復】檢查是否有緩存的移動包，如果有，使用緩存的移動包（可能比 S_ObjectAdd 中的座標更新）
				// 這解決了時序問題：服務器可能先發送移動包，然後才發送創建包
				if (_pendingMovePackets.TryGetValue(data.ObjectId, out var cachedMove))
				{
					existingEntity.SetMapPosition(cachedMove.x, cachedMove.y, cachedMove.heading);
					_pendingMovePackets.Remove(data.ObjectId);
					GD.Print($"[Move-Cache] Applied cached move packet for existing ObjId={data.ObjectId}: ({cachedMove.x},{cachedMove.y}) heading={cachedMove.heading}");
				}
				else
				{
					// 【徹底重構】使用 SetMapPosition 統一更新位置，確保座標一致性
					existingEntity.SetMapPosition(data.X, data.Y, data.Heading);
				}

				// 2b. 地面物品：同步數量（伺服器 S_ObjectAdd 重發時 getCount 可能已變）
				if (Client.Utility.ListSprLoader.Get(existingEntity.GfxId)?.Type == 9)
					existingEntity.ItemCount = data.Exp;

				// 3. 更新朝向
				existingEntity.SetHeading(data.Heading);

				// 4. 如果是自己，必须彻底打断所有移动逻辑！
				if (existingEntity == _myPlayer)
				{
					_isAutoWalking = false;
					_targetMapX = 0;
					_targetMapY = 0;
					_moveTimer = 0;

				// 【服務器對齊】傳送後應該設置待機動作（ACT_BREATH = 3），而不是 walk（ACT_WALK = 0）
				// 對齊服務器：傳送時實體停止移動，應該播放待機動作
				existingEntity.SetAction(GameEntity.ACT_BREATH); // 3 = Idle/Breath

					// 【座標同步修復】更新服務器確認的玩家座標（傳送時）
					_serverConfirmedPlayerX = data.X;
					_serverConfirmedPlayerY = data.Y;

					// 【玩家復活處理】如果是死亡後傳送（復活），重置死亡狀態
					// 服務器會在復活時傳送玩家到村莊，這會觸發 OnObjectSpawned
					if (_isPlayerDead)
					{
						_isPlayerDead = false;
						_resurrectionTimer = 0.0f;
						GD.PrintRich($"[color=green][Resurrection] 玩家已復活並傳送回村莊！座標: ({data.X}, {data.Y})[/color]");
					}

					Vector2 pos = CoordinateSystem.GridToPixel(data.X, data.Y);
					GD.PrintRich($"[b][color=green][Teleport] 传送成功！坐标重置为: {data.X}, {data.Y} → Position=({pos.X:F1},{pos.Y:F1})[/color][/b]");
				}
				return;
			}

			// --- 下面是原本的"新对象创建"逻辑 (保持不变) ---
			// 【核心修復】對齊服務器邏輯：服務器使用 Euclidean distance 和 14格過濾
			// 服務器代碼：L1Object.updateObject() 使用 getDistance(o, 14) 判斷是否發送 S_ObjectAdd
			// 客戶端必須使用相同的距離計算方法（Euclidean distance）和相同的過濾範圍（14格）
			// 這樣才能確保：服務器發送 S_ObjectAdd 時，客戶端一定會創建實體
			if (!IsPlayerSelf(data))
			{
				// 【關鍵修復】使用 Euclidean distance 對齊服務器邏輯
				int distance = GetEntityDistance(data.X, data.Y);
				
				// 【關鍵修復】使用 14格 過濾，對齊服務器邏輯（服務器使用 getDistance(o, 14)）
				if (distance > 14)
				{
					GD.Print($"[Performance] Skipping S_ObjectAdd for distant entity ObjId={data.ObjectId} distance={distance} (beyond 14 cells, server uses Euclidean distance)");
					return; // 超出範圍：完全跳過，不創建實體
				}
			}
			
			if (EntityScene == null) return;
			GameEntity entity = EntityScene.Instantiate<GameEntity>();
			AddChild(entity);

            // 判断是否是自己
            bool isMe = IsPlayerSelf(data);

			// 【核心修復】使用 GameWorld 的 _skinBridge，避免重複初始化
			// GameWorld 已經從 Boot 獲取了 SkinBridge，這裡直接使用它
			// 注意：_skin 是 GameWorld.Entities 的私有字段，_skinBridge 是 GameWorld 的私有字段
			// 由於是 partial class，可以直接訪問 GameWorld 的 _skinBridge
			if (_skin == null)
			{
				_skin = _skinBridge; // 使用 GameWorld 的 _skinBridge（已在 _Ready 中從 Boot 獲取）
				if (_skin == null)
				{
					GD.PrintErr("[GameWorld.Entities] _skinBridge is null! This should not happen.");
				}
			}

			// 視野優化：只對「與玩家同屏或附近」的實體載入圖像，其餘延後加載以減少壓力
			// 0-15格：載入圖像；15格外：已在上方過濾，不會到達這裡
			bool loadVisuals = isMe || IsEntityInViewRange(data.X, data.Y);
			if (!loadVisuals)
			{
				int dx = _myPlayer != null ? Math.Abs(data.X - _myPlayer.MapX) : 0;
				int dy = _myPlayer != null ? Math.Abs(data.Y - _myPlayer.MapY) : 0;
				GD.Print($"[Visual-Defer] ObjId={data.ObjectId} GfxId={data.GfxId} Map=({data.X},{data.Y}) 距離玩家過遠 (格距 dx={dx} dy={dy})，延後加載圖像");
			}
			// 初始化实体（loadVisuals=false 時不載入圖像、節點隱藏，進入範圍後由 _Process 觸發 EnsureVisualsLoaded）
			entity.Init(data, isMe, _skin, _audioProvider, loadVisuals);

			// 怪物血條：生成時若 CharPack 帶 0-100 則同步；否則等 S_HpMeter(128)。僅依伺服器數據顯示
			if (entity.HpRatio >= 0)
				entity.SetHpRatio(entity.HpRatio);
			if (!isMe)
				entity.SetHealthBarVisible(entity.ShouldShowHealthBar() && ClientConfig.ShowMonsterHealthBar);

			// 【關鍵修復】使用 SetMapPosition 統一設置位置，確保 Position 與 MapX/MapY 的一致性
			// 這樣可以避免直接設置 Position 可能導致的座標不一致問題
			// SetMapPosition 會正確處理 CurrentMapOrigin 和格心對齊（+0.5f）
			entity.SetMapPosition(data.X, data.Y, data.Heading);

			foreach (var child in entity.GetChildren())
				if (child is Control ctrl) ctrl.MouseFilter = Control.MouseFilterEnum.Ignore;

			_entities.Add(data.ObjectId, entity);
			SubscribeEntityEvents(entity); // 訂閱攻擊關鍵幀 → 被攻擊者飄字/僵硬動畫
			
			// 【關鍵修復】檢查是否有緩存的移動包，如果有，應用它
			// 這解決了時序問題：服務器可能先發送移動包，然後才發送創建包
			if (_pendingMovePackets.TryGetValue(data.ObjectId, out var cachedMovePacket))
			{
				// 應用緩存的移動包
				entity.SetMapPosition(cachedMovePacket.x, cachedMovePacket.y, cachedMovePacket.heading);
				_pendingMovePackets.Remove(data.ObjectId);
				GD.Print($"[Move-Cache] Applied cached move packet for ObjId={data.ObjectId}: ({cachedMovePacket.x},{cachedMovePacket.y}) heading={cachedMovePacket.heading}");
			}

			// 【方案 A】己方召喚：生成時依 S_ObjectAdd 的 OwnerName 標記，不依賴 Opcode 79 時序
			if (!isMe && !string.IsNullOrEmpty(data.OwnerName) && IsOwnerNameMine(data.OwnerName))
			{
				// 【寵物系統】檢查是否為寵物（通過 PetStatusChanged 信號判斷）
				// 寵物會在收到 Opcode 79 時被標記為己方寵物
				// 這裡先不處理，等待 PetStatusChanged 信號
				
				_mySummonObjectIds ??= new HashSet<int>();
				_mySummonObjectIds.Add(data.ObjectId);
			}

			if (isMe)
			{
				// 【徹底重構】使用統一的座標轉換函數計算生成位置的像素座標
				Vector2 spawnPos = CoordinateSystem.GridToPixel(data.X, data.Y);
				GD.Print($"[Pos-Diag] Spawn player ObjId={data.ObjectId} map=({data.X},{data.Y}) → Position=({spawnPos.X:F1},{spawnPos.Y:F1})");
				// 【座標同步修復】初始化服務器確認的玩家座標
				_serverConfirmedPlayerX = data.X;
				_serverConfirmedPlayerY = data.Y;
				SetupPlayerCamera(entity, data.ObjectId);
				// 與伺服器同步怪物血條開關（type=3），以便伺服器發送 Opcode 104
				SendClientOption(3, ClientConfig.ShowMonsterHealthBar ? 1 : 0);
			}
		}
		// =====================================================================
		// [SECTION END] Packet Callbacks: Spawn
		// =====================================================================


        // =====================================================================
        // [SECTION] Self Identify / Camera Lock (主角判定与摄像机锁定)
        // 说明：
        // - IsPlayerSelf：用于判断某个 WorldObject 是否为玩家自己
        // - SetupPlayerCamera：锁定 _myPlayer/_myObjectId，并将 Camera2D reparent 到主角
        // =====================================================================
        private bool IsPlayerSelf(WorldObject data)
        {
            if (Client.Boot.Instance == null) return false;
            string currentName = Client.Boot.Instance.CurrentCharName;
            if (string.IsNullOrEmpty(currentName)) return false;
            // 空名字不得視為自己：currentName.Contains("") 為 true，會誤把無名 spawn 當成自己，導致攝影機綁錯、真玩家 spawn 時跳過 SetupPlayerCamera
            if (string.IsNullOrEmpty(data.Name)) return false;
            return data.Name == currentName || data.Name.Contains(currentName) || currentName.Contains(data.Name);
        }

        /// <summary>是否為己方召喚：OwnerName（S_ObjectAdd 的 own）與己方角色名一致則視為己方召喚。</summary>
        private bool IsOwnerNameMine(string ownerName)
        {
            if (string.IsNullOrEmpty(ownerName)) return false;
            if (Client.Boot.Instance == null) return false;
            string currentName = Client.Boot.Instance.CurrentCharName;
            if (string.IsNullOrEmpty(currentName) && Client.Boot.Instance.MyCharInfo != null)
                currentName = Client.Boot.Instance.MyCharInfo.Name ?? "";
            if (string.IsNullOrEmpty(currentName)) return false;
            return ownerName == currentName || ownerName.Contains(currentName) || currentName.Contains(ownerName);
        }

        private void SetupPlayerCamera(GameEntity entity, int id)
        {
            if (_myPlayer != null) return;
            _myPlayer = entity;
            _myObjectId = id;
            if (_camera != null && IsInstanceValid(_camera))
            {
                _camera.Reparent(entity);
                _camera.Position = Vector2.Zero;
                _camera.MakeCurrent(); // 重登後確保攝影機為當前鏡頭，避免看不到角色
            }
            // 2D 音效距離衰減：聽者跟隨玩家，AudioStreamPlayer2D 依與聽者距離衰減音量
            var listener = new AudioListener2D();
            listener.Name = "AudioListener2D";
            entity.AddChild(listener);
            listener.MakeCurrent();
            GD.PrintRich($"[b][color=green][Spawn] PLAYER LOCKED! ID={_myObjectId} Name={entity.Name}[/color][/b]");
        }
        // =====================================================================
        // [SECTION END] Self Identify / Camera Lock
        // =====================================================================


		// =====================================================================
		// [SECTION] Packet Callbacks: Simple Entity Updates
		// 说明：实体的朝向/动作/特效/聊天等，属于“轻量状态更新回调”。
		// =====================================================================
		private void OnObjectHeadingChanged(int objectId, int newHeading)
		{
			if (_entities.TryGetValue(objectId, out var entity)) entity.SetHeading(newHeading);
		}

		private void OnObjectEffect(int objectId, int effectId)
		{
			// Opcode 55 / S_SkillSound(232 短包)：对象特效，藥水使用時伺服器送此包（紅水、綠水等）
			// 典型用途：护盾/增益特效（跟随角色身上）、升级光柱、传送光效、藥水使用動畫與音效
			GameEntity entity = null;
			if (_myPlayer != null && objectId == _myPlayer.ObjectId)
			{
				entity = _myPlayer;
			}
			else if (_entities.TryGetValue(objectId, out var found))
			{
				entity = found;
			}
			if (entity != null)
			{
				GD.Print($"[Effect] Obj {objectId} plays FX: {effectId}");
				// 己方使用藥水/道具：播放使用動畫（舉手施法動作）並播放音效（effectId 可作為音效 ID）
				if (entity == _myPlayer)
				{
					_myPlayer.SetAction(GameEntity.ACT_SPELL_DIR);
					Client.Boot.Instance?.PlayGlobalSound(effectId);
					// 約 0.6 秒後恢復待機
					CallDeferred(nameof(_RestoreBreathAfterPotionEffect));
				}
				// 关键修复：真正生成特效，并跟随目标实体（护盾类魔法需要）
				SpawnEffect(effectId, entity.GlobalPosition, entity.Heading, entity);
			}
		}

		private void _RestoreBreathAfterPotionEffect()
		{
			var tween = CreateTween();
			tween.TweenInterval(0.6);
			tween.TweenCallback(Callable.From(() =>
			{
				if (_myPlayer != null && !_myPlayer.IsDead)
					_myPlayer.SetAction(GameEntity.ACT_BREATH);
			}));
		}

		/// <summary>處理對象復活：Opcode 17 S_ObjectRestore，恢復實體狀態並更新外觀。</summary>
		private void OnObjectRestore(int objectId, int gfxMode, int reviverId, int gfx)
		{
			if (!_entities.TryGetValue(objectId, out var entity))
			{
				GD.Print($"[Restore] Object {objectId} not found, skip restore");
				return;
			}

			// 更新外觀
			entity.GfxId = gfx;
			// gfxMode 暫時不處理（如果需要，可以通過其他方式設置）
			entity.RefreshVisual();

			// 如果是玩家自己復活，重置死亡狀態
			if (entity == _myPlayer)
			{
				_isPlayerDead = false;
				_resurrectionTimer = 0.0f;
				_deathDialogShown = false;
				GD.PrintRich($"[color=green][Resurrection] 玩家已復活！Gfx={gfx} Mode={gfxMode}[/color]");
			}

			// 重置血量（復活後恢復滿血）
			entity.SetHpRatio(100); // 設置為 100（滿血）

			// 【服務器對齊】復活後應該設置待機動作（ACT_BREATH = 3），而不是 walk（ACT_WALK = 0）
			// 對齊服務器：復活時實體停止移動，應該播放待機動作
			entity.SetAction(GameEntity.ACT_BREATH); // 3 = Idle/Breath

			GD.Print($"[Restore] Object {objectId} restored: Gfx={gfx} Mode={gfxMode} Reviver={reviverId}");
		}

		private void OnObjectAction(int objectId, int actionId)
		{
			if (!_entities.TryGetValue(objectId, out var entity))
			{
				GD.Print($"[Action-Diag] OnObjectAction objId={objectId} actionId={actionId} -> entity not found, skip");
				return;
			}

			// [診斷] 每次收到動作都記錄，便於確認死亡動作是否進入、GfxId 是否正確
			GD.Print($"[Action-Diag] OnObjectAction objId={objectId} actionId={actionId} GfxId={entity.GfxId} name={entity.RealName}");

			// 【核心修復】收到死亡動作時，先同步將血量比例設為 0，確保選怪/任務有效性立即排除（不依賴 104 先到）
			// 這必須在檢查死亡動畫之前執行，確保後續邏輯能正確識別死亡狀態
			if (actionId == GameEntity.ACT_DEATH)
			{
				entity.SetHpRatio(0);
				
				// 【玩家死亡處理】如果是玩家自己死亡，啟動30秒復活計時器
				if (entity == _myPlayer)
				{
					_isPlayerDead = true;
					_resurrectionTimer = RESURRECTION_TIME;
					GD.PrintRich($"[color=red][Death] 玩家死亡！30秒後將自動復活並傳送回最近村莊[/color]");
					
					// 停止所有自動行為
					StopAutoActions();
					StopWalking();
					ShowDeathDialogIfNeeded();
				}
			}

			// 【特殊規則】缺少 8.Death 動畫的怪物：播放 169 傳送特效並提前清理，避免無屍體圖片卡在畫面上
			// 判定依據：list.spr 中對應 GfxId 沒有 ActionId=8 的動作定義（如 GfxId=790），或整個 GfxId 未定義（如 GfxId=841）。
			// 【核心修復】同時檢查 list.spr 定義和實際動畫資源是否存在，確保準確判斷
			if (actionId == GameEntity.ACT_DEATH)
			{
				var def = ListSprLoader.Get(entity.GfxId);
				bool hasDeathDef = def != null && def.Actions != null && def.Actions.ContainsKey(GameEntity.ACT_DEATH);
				
				// 【核心修復】不僅檢查 list.spr 定義，還要檢查實際動畫資源是否存在
				// 有些角色可能在 list.spr 中有定義，但實際動畫資源不存在（如缺少圖片文件）
				bool hasDeathResource = false;
				if (hasDeathDef && _skinBridge != null)
				{
					// 嘗試獲取死亡動畫資源，如果為 null 則表示資源不存在
					var deathFrames = _skinBridge.Character.GetBodyFrames(entity.GfxId, GameEntity.ACT_DEATH, entity.Heading);
					hasDeathResource = deathFrames != null;
				}

				bool hasDeath = hasDeathDef && hasDeathResource;

				GD.Print($"[Death-Diag] objId={objectId} GfxId={entity.GfxId} hasDeathDef={hasDeathDef} hasDeathResource={hasDeathResource} hasDeath={hasDeath} (def exists={def != null})");

				if (!hasDeath)
				{
					// 1. 在怪物當前位置播放 169「指定傳送」特效，營造被傳送飛走的視覺效果
					GD.Print($"[Death-Fallback] 播放 169 傳送特效 ObjId={objectId} GfxId={entity.GfxId} Pos={entity.GlobalPosition}");
					SpawnEffect(169, entity.GlobalPosition, entity.Heading, entity);

					// 2. 【診斷日誌，禁止移除】標記為 Death → Teleport Fallback，便於日後追蹤高頻觸發情況
					GD.PrintRich($"[color=orange][Death-Fallback] GfxId={entity.GfxId} 無 8.Death 動畫（定義={hasDeathDef} 資源={hasDeathResource}），改用 169 傳送特效並提前刪除 ObjId={objectId}[/color]");

					// 3. 提前清理實體：模擬伺服器發送刪除封包，避免一具「無圖像屍體」長時間佔據畫面
					// 【核心修復】確保在銷毀前已經設置了 HpRatio=0（已在上面執行）
					OnObjectDeleted(objectId);
					return;
				}
				GD.Print($"[Death-Diag] Playing death animation objId={objectId} GfxId={entity.GfxId}");
			}

			// 設置動作（如果有死亡動畫，會正常播放；如果沒有，已在上面 return）
			entity.SetAction(actionId);
		}

		private void OnObjectChat(int objectId, string text, int type)
		{
			_entities.TryGetValue(objectId, out var entity);
			if (entity != null) entity.ShowChat(text);
			// 自己發言已由 HUD 本地 echo 顯示（正確角色名），不再重複加入公告欄，避免出現兩條且一條為 objectId 當名稱
			if (objectId == _myObjectId) return;
			string name = entity != null ? (entity.RealName ?? objectId.ToString()) : objectId.ToString();
			_hud?.AddChatMessage(name, text);
		}
		// =====================================================================
		// [SECTION END] Packet Callbacks: Simple Entity Updates
		// =====================================================================


		// =====================================================================
		// [SECTION] Packet Callbacks: Delete (对象消失/清理)
		// 说明：
		// - 如果删除的是当前自动目标：停止自动行为
		// - QueueFree 并从字典移除
		// =====================================================================
		private void OnObjectDeleted(int objectId)
		{
			_mySummonObjectIds?.Remove(objectId);
			_myPetObjectIds?.Remove(objectId);
			
			// 【關鍵修復】清理緩存的移動包，避免內存洩漏
			_pendingMovePackets?.Remove(objectId);
			
			if (_entities.ContainsKey(objectId))
			{
				var entity = _entities[objectId];
				// 【服務器對齊】如果刪除的是拾取目標（地面物品），清理拾取任務
				// 服務器 ItemInstance.pickup 在物品全部被拾取時會調用 toDelete()，發送 S_ObjectDelete (Opcode 21)
				if (_currentTask != null && _currentTask.Target != null && _currentTask.Target.ObjectId == objectId)
				{
					// 【拾取修復】物品被刪除（全部拾取成功），清理拾取任務
					GD.Print($"[Pickup] Item {objectId} deleted (picked up successfully), clearing pickup task");
					_pickupInProgress = false;
					StopAutoActions();
				}
				UnsubscribeEntityEvents(entity);
				entity.QueueFree();
				_entities.Remove(objectId);
			}
		}
		// =====================================================================
		// [SECTION END] Packet Callbacks: Delete
		// =====================================================================

		/// <summary>每幀檢查延後加載的實體，若已進入視野範圍則載入圖像。</summary>
		private void UpdateDeferredVisuals()
		{
			if (_myPlayer == null) return;
			foreach (var kv in _entities)
			{
				var e = kv.Value;
				if (e != null && e.IsVisualsDeferred && IsEntityInViewRange(e.MapX, e.MapY))
					e.EnsureVisualsLoaded();
			}
		}
		
		/// <summary>【性能優化】清理遠距離實體：移動距離>5格時清理15格外的實體</summary>
		private void CleanupDistantEntities()
		{
			if (_myPlayer == null) return;
			
			// 檢查玩家是否移動了超過5格（使用上次記錄的位置）
			if (_lastCleanupPlayerX < 0 || _lastCleanupPlayerY < 0)
			{
				_lastCleanupPlayerX = _myPlayer.MapX;
				_lastCleanupPlayerY = _myPlayer.MapY;
				return;
			}
			
			int moveDistX = Math.Abs(_myPlayer.MapX - _lastCleanupPlayerX);
			int moveDistY = Math.Abs(_myPlayer.MapY - _lastCleanupPlayerY);
			int moveDist = Math.Max(moveDistX, moveDistY);
			
			// 如果移動距離>5格，清理15格外的實體
			if (moveDist > 5)
			{
				var toRemove = new List<int>();
				foreach (var kv in _entities)
				{
					var e = kv.Value;
					if (e == null || e == _myPlayer) continue;
					
					// 【核心修復】使用 Euclidean distance 對齊服務器邏輯
					// 服務器使用 Euclidean distance 和 14格過濾，客戶端使用 15格（稍寬鬆以確保不遺漏）
					int distance = GetEntityDistance(e.MapX, e.MapY);
					if (distance > VISUAL_RANGE_CELLS)
					{
						toRemove.Add(kv.Key);
					}
				}
				
				foreach (var objId in toRemove)
				{
					if (_entities.TryGetValue(objId, out var entity))
					{
						GD.Print($"[Performance] Cleaning up distant entity ObjId={objId} distance={GetEntityDistance(entity.MapX, entity.MapY)}");
						// 注意：UnsubscribeEntityEvents 定義在 GameWorld.Combat.cs 中，但由於是 partial class，可以直接調用
						UnsubscribeEntityEvents(entity);
						entity.QueueFree();
						_entities.Remove(objId);
					}
				}
				
				_lastCleanupPlayerX = _myPlayer.MapX;
				_lastCleanupPlayerY = _myPlayer.MapY;
			}
		}
		
		// 【性能優化】記錄上次清理時的玩家位置
		private int _lastCleanupPlayerX = -1;
		private int _lastCleanupPlayerY = -1;

		private void OnDarknessChanged(float darkness, int tier)
		{
			bool isNight = darkness >= DayNightOverlay.NightHideUiThreshold;
			foreach (var kv in _entities)
				kv.Value?.SetNightOverlayActive(isNight);
			if (!isNight)
			{
				if (_myPlayer != null && _myPlayer.HpRatio >= 0)
					_myPlayer.SetHealthBarVisible(_myPlayer.HpRatio < 100);
				RefreshMonsterHealthBars();
			}
		}
		
		// =====================================================================
		// [SECTION] 邪惡值和狀態更新處理
		// =====================================================================
		
		/// <summary>
		/// 【服務器對齊】處理邪惡值更新 (Opcode 89)
		/// 對齊服務器 S_ObjectLawful.java: writeC(89), writeD(objId), writeD(lawful)
		/// </summary>
		private void OnObjectLawfulChanged(int objectId, int lawful)
		{
			if (_entities != null && _entities.TryGetValue(objectId, out var entity))
			{
				entity.Lawful = lawful;
				// 更新名字顏色（對齊服務器規則）
				entity.UpdateColorDisplay();
				GD.Print($"[Lawful] ObjId={objectId} Lawful={lawful}");
			}
		}
		
		/// <summary>
		/// 【服務器對齊】處理中毒狀態 (Opcode 50)
		/// 對齊服務器 S_ObjectPoison.java: writeC(50), writeD(objId), writeC(poison ? 1 : 0)
		/// 中毒時實體會變色（綠色），並在 HUD 顯示狀態
		/// </summary>
		private void OnObjectPoisonReceived(int objectId, bool isPoison)
		{
			if (_entities != null && _entities.TryGetValue(objectId, out var entity))
			{
				entity.SetPoison(isPoison);
				GD.Print($"[Poison] ObjId={objectId} isPoison={isPoison}");
				if (objectId == _myPlayer?.ObjectId)
				{
					_hud?.AddSystemMessage(isPoison ? "你中毒了！" : "中毒效果已解除。");
				}
			}
		}
		
		/// <summary>
		/// 【服務器對齊】處理紫名狀態 (Opcode 106)
		/// 對齊服務器 S_PinkName.java: writeC(106), writeD(objId), writeH(time)
		/// 紫名規則：攻擊正義玩家（lawful >= 65536）時觸發，持續 10 秒
		/// </summary>
		private void OnObjectPinkNameReceived(int objectId, int duration)
		{
			if (_entities != null && _entities.TryGetValue(objectId, out var entity))
			{
				entity.SetPinkName(duration > 0, duration);
				GD.Print($"[PinkName] ObjId={objectId} duration={duration}s");
				if (objectId == _myPlayer?.ObjectId)
				{
					_hud?.AddSystemMessage($"你變成了紫名，持續 {duration} 秒。");
				}
			}
		}
	}
}
