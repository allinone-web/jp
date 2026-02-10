// ==================================================================================
// [FILE] Client/Game/GameEntity.Visuals.cs
// [ROLE] 視覺渲染執行模組 (The Visual Engine)
// [DESCRIPTION] 
//   負責處理多層渲染(主體, 武器, 陰影, 服裝)的同步播放，
//   並偵聽每一幀的元數據觸發音效、特效與打擊事件。

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

// [架構設計]:
//   1. Master-Slave 同步: 只有 _mainLayer 有計時器。其餘所有圖層(Slave)通過
//      信號強行與主體對齊幀索引，徹底解決多圖層「幀漂移」與「溜冰」現象。
//   2. 數據驅動事件: 偵聽幀元數據觸發 戰鬥判定(!)、位移點(>)、音效([)、特效(])。
//   3. 異步健壯性: 包含 TryAsyncRetry 機制，解決 PAK 異步加載時的資源空窗期。
//   4. 內存優化: 服裝層採用對象池(Object Pool)，避免更換裝備時的內存抖動。

// 實裝對齊公式：Offset = (dx + w/2, dy + h/2) + ManualTwea
// 1. 修正對齊公式：(sprDx - w/2, sprDy - h/2)。
//   1. 坐標對齊：修正公式為 Offset = Anchor，解決主體與陰影/服裝分離的問題。
//   2. Shader：採用用戶提供的去黑色邏輯，並強制應用於所有服裝層。
//   3. 105層同步：服裝層強制應用 BodyOffset，確保三層合一。
//   4. 詳細日誌：實時輸出每一層的物理坐標數據。
//   1. 角色中心對齊：使用 Offset = -Anchor 邏輯，將圖片腳底點拉回 Node 中心 (0,0)。
//   2. 黑色背景去除：Shader 閾值調高至 0.4，確保黑色完全透明。
//   3. 多層強制對齊：主體、陰影、服裝各層均使用各自的 Anchor 進行負向抵消。
//   4. 數據監控：強制輸出每一幀的坐標計算過程。

//   1. 角色偏移：使用 Offset = (-Anchor.X, Anchor.Y) 邏輯，將角色往上提拉。
//   2. 強制合一：陰影與服裝層「放棄」自己的 Anchor，強制使用「主體」的 Anchor 數據。
//   3. 黑色去除：Shader 閾值固定為 0.4，徹底去除黑邊。
//   4. 坐標日誌：輸出 Master 權威坐標，確保數據可視化。

// [修復] 
//   1. 獨立偏移：Character, Clothes, Shadow 各自讀取 dx/dy，不再強制統一 Offset。
//   2. 中心對齊：Centered=false，Offset 直接採用原始 dx/dy，實現 Pivot 歸零。
//   3. 黑色去除：Shader 閾值 0.4 + discard。
//   4. 數據監控：詳細打印每一層的獨立座標數據。

// (-30, -20) 是「黃金數值」？
// 從日誌中我們可以看到計算過程：

// 主體 (MainBody): Data:(19, -38) + Tweak:(-30, -20) = FinalOffset:(-11, -58)
// 陰影 (Shadow): Data:(14, 5) + Tweak:(-30, -20) = FinalOffset:(-16, -15)
// ==================================================================================

using Godot;
using System;
using System.Collections.Generic;
using Client.Utility;
using Core.Interfaces;

namespace Client.Game
{
	// [渲染執行文件] 職責：AnimatedSprite2D 的操作與幀同步
	public partial class GameEntity
	{
		// [與 PakBrowser 一致] 服裝層不再使用 Shader 去黑；魔法透明度由 CustomCharacterProvider 對 104.attr(8) 套用 BlackToTransparentProcessor。

		// ====================================================================
		// 初始化系統
		// ====================================================================
		// 注意：此處不再定義任何變量 (如 _mainSprite 等)

		/// <summary>
		/// 初始化視覺系統。Z軸順序由底至頂：陰影(-1) -> 主體(0) -> 服裝(2+)。106.weapon 層已徹底移除。
		/// </summary>
		public void InitializeVisualSystem()
		{
			if (_mainSprite != null) return; // 單例保護

			// 1. 初始化陰影 (Z = -1)
			_shadowLayer = CreateLayer("Shadow", -1);

			// 2. 初始化主體 (Z = 0, Master)
			_mainSprite = CreateLayer("MainBody", 0);
			_mainSprite.FrameChanged += OnMasterFrameChanged;
			_mainSprite.AnimationFinished += OnUnifiedAnimationFinished;

			// 3. 初始化服裝對象池 (Z = 2..4, Slave)
			// 支持多件衣服疊加，預建池提升性能，防止 GC 抖動
						// 這些層通常是 105.clothes，用於發光武器、魔法裝飾

			for (int i = 0; i < 3; i++) {
				var c = CreateLayer($"Clothes_{i}", 2 + i);
				c.Visible = false;
				_clothesPool.Add(c);
			}

			GD.Print($"[Visual] {RealName} 渲染系統初始化完成 (與 PakBrowser 對齊規則一致).");
		}

		/// 【已測試通過，不允許修改】與 PakBrowser 一致：Centered=true，三層對齊依賴此設定。
		private AnimatedSprite2D CreateLayer(string name, int z) {
			// [PakBrowser 一致] Centered = true：紋理以節點為中心繪製，角色中心固定於畫面中心，無抖動、無左上角對齊問題
			var s = new AnimatedSprite2D { 
				Name = name, 
				ZIndex = z, 
				TextureFilter = TextureFilterEnum.Nearest,
				Centered = true
			};
			AddChild(s);
			return s;
		}

		// ---------------------------------------------------------
		// 【已測試通過，不允許修改】此功能（body 主體 / shadow 陰影 / clothes 衣服三層對齊）已調適正確。
		// 對齊規則：Centered=true，主體 Offset=BodyOffset（角色整體中心在螢幕中心）。
		// Shadow：僅首幀(frame 0)計算 Offset，與主體對齊。Clothes：每幀計算 Offset，修復 gfx=240 等錯位。
		// 106.weapon 層已徹底移除。若需修改必須獲得許可。詳見 docs/pakbrowser-development.md 第 4 節。
		// ---------------------------------------------------------
		private static bool DEBUG_ANCHOR_ALIGN = false;

		/// 從圖層指定幀紋理讀取 sprite_offsets (dx,dy)=左上角 與尺寸 (w,h)；與 PakBrowser.GetLayerFrameAnchor / PngInfoLoader 一致。
		/// frameIndex &lt; 0 用圖層當前幀；&gt;= 0 用指定幀（對齊僅用 frame 0）。
		private static bool GetLayerFrameAnchor(AnimatedSprite2D layer, int frameIndex, out int dx, out int dy, out int w, out int h)
		{
			dx = dy = w = h = 0;
			if (layer == null || layer.SpriteFrames == null) return false;
			int useFrame = frameIndex >= 0 ? frameIndex : layer.Frame;
			int count = layer.SpriteFrames.GetFrameCount(layer.Animation);
			if (count <= 0) return false;
			useFrame = useFrame % count;
			var tex = layer.SpriteFrames.GetFrameTexture(layer.Animation, useFrame);
			if (tex == null) return false;
			dx = tex.HasMeta("spr_anchor_x") ? (int)tex.GetMeta("spr_anchor_x") : 0;
			dy = tex.HasMeta("spr_anchor_y") ? (int)tex.GetMeta("spr_anchor_y") : 0;
			w = tex.GetWidth();
			h = tex.GetHeight();
			return true;
		}

		/// 【已測試通過，不允許修改】主體與 Shadow 的 Offset：僅用首幀(frame 0)。Shadow 與主體對齊。
		private void UpdateAllLayerOffsets()
		{
			const int useFrame = 0;
			if (!GetLayerFrameAnchor(_mainSprite, useFrame, out int bodyDx, out int bodyDy, out int bodyW, out int bodyH))
			{
				_mainSprite.Offset = BodyOffset;
				if (_shadowLayer.Visible && _shadowLayer.SpriteFrames != null) _shadowLayer.Offset = BodyOffset;
				return;
			}
			_mainSprite.Offset = BodyOffset;

			if (_shadowLayer.Visible && _shadowLayer.SpriteFrames != null &&
			    GetLayerFrameAnchor(_shadowLayer, useFrame, out int sDx, out int sDy, out int sW, out int sH))
			{
				float ox = sDx - bodyDx - bodyW / 2f + sW / 2f;
				float oy = sDy - bodyDy - bodyH / 2f + sH / 2f;
				_shadowLayer.Offset = BodyOffset + new Vector2(ox, oy);
			}
			else if (_shadowLayer.Visible && _shadowLayer.SpriteFrames != null)
				_shadowLayer.Offset = BodyOffset;
			// Clothes 不在此處設定，改由 UpdateClothesOffsetsPerFrame() 每幀計算
		}

		/// 【已測試通過，不允許修改】Clothes 每幀計算 Offset，用當前幀的 (dx,dy,w,h) 修復 gfx=240 等錯位。
		private void UpdateClothesOffsetsPerFrame()
		{
			if (_mainSprite?.SpriteFrames == null) return;
			const int useFrame = -1; // 當前幀
			if (!GetLayerFrameAnchor(_mainSprite, useFrame, out int bodyDx, out int bodyDy, out int bodyW, out int bodyH))
			{
				foreach (var cl in _clothesPool) { if (cl.Visible && cl.SpriteFrames != null) cl.Offset = BodyOffset; }
				return;
			}
			foreach (var cl in _clothesPool)
			{
				if (!cl.Visible || cl.SpriteFrames == null) continue;
				if (GetLayerFrameAnchor(cl, useFrame, out int cDx, out int cDy, out int cW, out int cH))
				{
					float ox = cDx - bodyDx - bodyW / 2f + cW / 2f;
					float oy = cDy - bodyDy - bodyH / 2f + cH / 2f;
					cl.Offset = BodyOffset + new Vector2(ox, oy);
				}
				else
					cl.Offset = BodyOffset;
			}
		}

		/// 主體動畫結束時（非循環如 1.attack、3.breath）強制同步陰影/服裝到主體當前幀。
		internal void SyncSlaveLayersToBodyFrame()
		{
			if (_mainSprite?.SpriteFrames == null) return;
			int bodyFrame = _mainSprite.Frame;

			if (_shadowLayer.Visible && _shadowLayer.SpriteFrames != null && _shadowLayer.SpriteFrames.HasAnimation(_shadowLayer.Animation))
			{
				int count = _shadowLayer.SpriteFrames.GetFrameCount(_shadowLayer.Animation);
				if (count > 0) _shadowLayer.Frame = bodyFrame % count;
			}
			foreach (var cl in _clothesPool)
			{
				if (!cl.Visible || cl.SpriteFrames == null || !cl.SpriteFrames.HasAnimation(cl.Animation)) continue;
				int count = cl.SpriteFrames.GetFrameCount(cl.Animation);
				if (count > 0) cl.Frame = bodyFrame % count;
			}
		}

		/// 【已測試通過，不允許修改】同步 Slave 層幀；Shadow Offset 依首幀設定，Clothes 每幀重算 Offset。
		private void OnMasterFrameChanged()
		{
			if (_mainSprite == null) return;
			int f = _mainSprite.Frame;
			if (f == _lastProcessedFrame) return;

			SyncSlaveLayersToBodyFrame();
			UpdateClothesOffsetsPerFrame();
			ProcessFrameMetadata(f);
			_lastProcessedFrame = f;
		}

		// ---------------------------------------------------------
		// 外觀更新邏輯 (保持不變，僅確保調用正確)
		/// 唯一的視覺刷新出口。整合了：基礎姿勢、105服裝、110攻速。106.weapon 層已徹底移除。
		// ---------------------------------------------------------
		public void UpdateAppearance(int gfxId, int finalAction, int head, int weaponType)
		{
			// 安全檢查：若資源網橋、組件未就緒或視覺被 GM 鎖定，跳過
			if (_skinBridge == null || _mainSprite == null || _isVisualLocked) return;

			var def = ListSprLoader.Get(gfxId);
			if (def == null) {
				// 【診斷日誌，禁止刪除】用於偵測 list.spr 缺少對應 GfxId 的定義，後續排查資源缺失必須依賴此輸出。
				GD.PrintRich($"[color=red][Visual-Missing] list.spr 無定義 ObjId={ObjectId} GfxId={gfxId} action={finalAction} head={head} name={RealName} -> TryAsyncRetry[/color]");
				if (_visualMissingRetryCount < VisualMissingRetryMax) { _visualMissingRetryCount++; TryAsyncRetry(); }
				return;
			}

			// 1. 獲取主體資源 (若為 null 則啟動異步重試，達上限後停止以免刷屏)
			var bodyFrames = _skinBridge.Character.GetBodyFrames(gfxId, finalAction, head);
			if (bodyFrames == null) {
				// 【診斷日誌，禁止刪除】用於偵測 Spr 檔存在但缺少對應動作/方向幀（例如 8.Death 未提供實際圖片）。
				GD.PrintRich($"[color=red][Visual-Missing] 動畫檔取得為空 ObjId={ObjectId} GfxId={gfxId} action={finalAction} head={head} name={RealName} -> TryAsyncRetry[/color]");
				if (_visualMissingRetryCount < VisualMissingRetryMax) { _visualMissingRetryCount++; TryAsyncRetry(); }
				return;
			}
			_visualMissingRetryCount = 0; // 成功取得則重置，後續若再缺可再重試

			// 2. 主體層播放速率：根據 AnimationSpeed 設置
			// - 加速術（Haste）：AnimationSpeed = 1.333...（1/0.75）
			// - 緩速（Slow）：AnimationSpeed = 0.75
			// - 正常：AnimationSpeed = 1.0
			// 動作間隔由 SpeedManager 控制，動畫播放速度由 SpeedScale 控制
			float bodySpeed = AnimationSpeed;

			// 3. 渲染主體 (Master 層控制播放進度)
			ApplyFramesToLayer(_mainSprite, bodyFrames, head.ToString(), bodySpeed);

			// 4. [105 服裝圖層處理] - 使用對象池 (Pool)。106.weapon 層已徹底移除。
			UpdateClothesOverlay(def.ClothesIds, gfxId, finalAction, head);

			// 5. [101 陰影處理]
			// 【速度同步修復】Shadow 層也需要應用 AnimationSpeed，確保與主體同步播放
			if (def.ShadowId > 0) {
				var sfs = _skinBridge.Character.GetBodyFrames(def.ShadowId, gfxId, finalAction, head);
				if (sfs != null) {
					_shadowLayer.Visible = true;
					ApplyFramesToLayer(_shadowLayer, sfs, head.ToString(), bodySpeed);
				}
			} else _shadowLayer.Visible = false;

			// 6. 主體與 Shadow 依首幀(frame 0)計算 Offset；Clothes 在 OnMasterFrameChanged 每幀計算
			UpdateAllLayerOffsets();
			_lastProcessedFrame = -1;
			OnMasterFrameChanged();

			// 7. 更新附屬 UI
			UpdateColorDisplay();
			UpdateNameDisplay();

			// 8. 隱身術(Op52)：每次外觀更新後重新套用，避免被覆蓋
			ApplyInvisibilityVisual();
		}



		private void ApplyFramesToLayer(AnimatedSprite2D layer, SpriteFrames frames, string anim, float speed)
		{
			// 【核心修復】死亡動畫（8.death）必須強制重新設置 SpriteFrames 和播放，確保動畫完整播放
			// 若資源引用未變，則不重複賦值，防止重置播放進度
			// 但死亡動畫例外：即使資源引用相同，也必須強制重新播放，避免動畫被提前停止
			bool isDeathAction = _currentRawAction == ACT_DEATH;
			bool forceDeathReset = isDeathAction && _deathAnimationRestart;
			if (layer.SpriteFrames != frames || forceDeathReset) 
			{
				layer.SpriteFrames = frames;
				// [核心修復] 當更換 SpriteFrames 時，強制重置當前動畫名稱
				// 否則如果新舊 SpriteFrames 的動畫名稱相同，Godot 可能不會觸發重新播放
				if (frames.HasAnimation(anim)) 
				{
					layer.Animation = anim;
					if (forceDeathReset)
						layer.Frame = 0; // 死亡動畫從第一幀開始播放
				}
			}
			
			// 【核心修復】死亡動畫必須強制播放，即使動畫名稱相同且正在播放
			// 這是因為死亡動畫可能被其他邏輯提前停止（例如受擊僵硬、移動等）
			if (forceDeathReset || layer.Animation != anim || !layer.IsPlaying()) {
				if (frames.HasAnimation(anim)) 
				{
					layer.Play(anim);
					if (isDeathAction)
					{
						GD.Print($"[Death-Play] 強制播放死亡動畫 ObjId={ObjectId} GfxId={GfxId} action={CurrentAction} head={anim} layer={layer.Name}");
					}
				}
				else
				{
					// 【兜底替身圖像 & 診斷日誌，禁止刪除】
					// 說明：Spr 檔存在且有此動作，但缺少當前方向(head)的動畫，系統退回播放 "0" 並輸出紅字以協助定位錯誤資料。
					GD.PrintRich($"[color=red][Visual-Fallback] 缺方向動畫 ObjId={ObjectId} name={RealName} GfxId={GfxId} action={CurrentAction} head={anim} layer={layer.Name} -> fallback to \"0\"[/color]");
					layer.Play("0"); // 向下兼容的方向
				}
			}

			// 【速度同步修復】所有層（主體、Shadow、Clothes）都需要應用相同的 SpeedScale
			// 這樣可以確保在加速術/緩速術時，所有層都能同步播放
			// Shadow/Clothes 雖然沒有自己的 list.spr，但它們的動畫播放速度必須與主體一致
			// 雙重同步機制：
			// 1. SpeedScale 控制播放速度（與主體一致）
			// 2. OnMasterFrameChanged() 手動同步幀數（防止累積誤差）
			// 【核心修復】死亡動畫不受加速/緩速影響，使用正常速度播放
			if (speed > 0) layer.SpeedScale = isDeathAction ? 1.0f : speed; 
			else layer.SpeedScale = 0;
		}

		private void UpdateClothesOverlay(List<int> ids, int refId, int act, int head) {
			// 隱藏所有池內組件，準備重分配
			foreach (var l in _clothesPool) l.Visible = false;

			// 【速度同步修復】Clothes 層也需要應用 AnimationSpeed，確保與主體同步播放
			float clothesSpeed = AnimationSpeed;
			for (int i = 0; i < ids.Count && i < _clothesPool.Count; i++) {
		var sf = _skinBridge.Character.GetBodyFrames(ids[i], refId, act, head);
				if (sf != null) {
					_clothesPool[i].Visible = true;
					ApplyFramesToLayer(_clothesPool[i], sf, head.ToString(), clothesSpeed);
				}
			}
		}

		// ... (保留 ProcessFrameMetadata, TryAsyncRetry 等其他輔助方法) ...
 

		private void TryAsyncRetry() 
		{
			if (_isVisualLoading) return;
			_isVisualLoading = true;

			// 0.2s 後重試，給 PAK 加載器呼吸時間
			GetTree().CreateTimer(0.2f).Timeout += () => {
				_isVisualLoading = false;
				if (IsInstanceValid(this)) RefreshVisual(); // 刷新當前視覺
			};
		}



		private void ProcessFrameMetadata(int frameIdx)
		{
			if (_mainSprite.SpriteFrames == null) return;
			var tex = _mainSprite.SpriteFrames.GetFrameTexture(_mainSprite.Animation, frameIdx);
			if (tex == null) return;

			if (tex.HasMeta("key"))
			{
				GD.Print($"[HitChain] ProcessFrameMetadata ObjId={ObjectId} frame={frameIdx} HAS key -> OnAnimationKeyFrame");
				OnAnimationKeyFrame();
			}
			// 動畫幀音效僅依 SprFrame.SoundIds，由 GameEntity.Audio 依 order[frameIdx].SoundIds 播放；此處不重複播音效。
			// [109.effect / 幀內特效] list.spr 幀的 ]effectId（EffectIds）在此觸發，與魔法共用 SpawnEffect，支援連環 109
			if (tex.HasMeta("effects"))
			{
				string curAnim = _mainSprite.Animation;
				if (curAnim != _lastSpawnedEffectAnim || frameIdx != _lastSpawnedEffectFrame)
				{
					_lastSpawnedEffectAnim = curAnim;
					_lastSpawnedEffectFrame = frameIdx;
					var arr = tex.GetMeta("effects").AsGodotArray();
					for (int i = 0; i < arr.Count; i++)
					{
						int eid = arr[i].AsInt32();
						if (eid <= 0) continue;
						var gw = GetTree().CurrentScene as GameWorld;
						if (gw != null)
						{
							gw.SpawnEffect(eid, GlobalPosition, Heading, this);
							GD.Print($"[FrameEffect] ObjId={ObjectId} anim={curAnim} frame={frameIdx} -> SpawnEffect GfxId:{eid} (支援 109 連環)");
						}
					}
				}
			}
		}
	}
}
