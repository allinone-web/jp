// ============================================================================
// [FILE] GameWorld.cs
//
// [作用说明 / 拆分后的逻辑关联 - V3（增加 Setup 拆分）]
// 本文件是 GameWorld 的“总入口 / 调度层”。它只保留：
// - 共享字段与状态（Export/Runtime/Shared State）
// - Godot 生命周期入口：_Ready / _Process
// - 在 _Ready 中按固定顺序调用 Setup*() 初始化步骤（实现放在 GameWorld.Setup.cs）
//
// ────────────────────────────────────────────────────────────────────────────
// [GameWorld 拆分文件总览（同一个 partial class GameWorld）]
//
// 1) GameWorld.cs
//    - 字段与共享状态
//    - _Ready / _Process（只调度，不堆业务细节）
//
// 2) GameWorld.Setup.cs
//    - _Ready 内的初始化步骤拆分为 Setup 小函数：
//      SetupResolveRuntimeRefs / SetupEnsureUIManager / SetupBindPacketHandlerSignals
//      SetupInitCamera / SetupInitHUDAndChat / SetupNotifyBootWorldReady
//    - 只做初始化“搬家”，不改变执行顺序与逻辑。
//
// 3) GameWorld.Bindings.cs
//    - 绑定 PacketHandler 信号到回调方法，并调用各子系统 BindXXXSignals()。
//
// 4) GameWorld.Entities.cs
//    - 世界实体生命周期与轻量状态更新：Spawn/Delete/Heading/Action/Effect/Chat 等。
//
// 5) GameWorld.Input.cs
//    - 输入入口与点击分发：_UnhandledInput / HandleInput / GetClickedEntity / StopAutoActions。
//    - 输入最终调用 Combat/Inventory/Movement/Npc 等模块的方法（逻辑不变）。
//
// 6) GameWorld.UI.cs
//    - UI 刷新与桥接：RefreshWindows / OnSystemMessage / OptionsWindow 回调（Quit/Restart）。
//
// 7) GameWorld.CharacterSync.cs
//    - 角色属性同步：OnHPUpdated/OnMPUpdated、OnCharacterInfoReceived/OnCharacterStatsUpdated、UpdateBootStats。
//    - 保证 HUD 与 Boot.MyCharInfo 一致并触发窗口刷新。	
//	  - 将服务器下发的权威外观 GfxMode 应用到实体，切换武器
//
// 8) GameWorld.Chat.cs
//    - 聊天发送：SendChatPacket（HUD.ChatSubmitted -> GameWorld -> NetSession.Send）。
//
// 9) GameWorld.Movement.cs
//    - 移动与寻路：StartWalking/StopWalking/UpdateMovementLogic/FindPath 等。
//
// 10) GameWorld.Combat.cs
//    - 战斗：StartAutoAttack/UpdateCombatLogic/PerformAttackOnce/OnObjectAttacked/OnObjectMagicAttacked 等。
//
// 11) GameWorld.Inventory.cs
//    - 背包与拾取：StartPickup/UpdateInventoryLogic/OnInventoryList/OnItemAdded/OnItemRemoved 等。
//
// 12) GameWorld.Npc.cs
//    - NPC 交互：TalkToNpc/SendNpcAction/SendShopBuy + OnShowHtml/OnShopBuyOpen 等回调。
//
// 13) GameWorld.Skill.cs
//    - 技能/魔法：技能掩码、购买列表、UseMagic + 技能相关回调。
//
// 14) GameWorld.Buffs.cs
//    - Buff/状态：加速/变身等 Buff 回调与 HUD 提示（如有）。
//
// 15) GameWorld.map.cs
//    -
// 16) GameWorld.Search.cs
//    -
//
// ────────────────────────────────────────────────────────────────────────────
// [重要] 音效無聲問題：_audioProvider 必須在 _Ready 開頭與 _skinBridge 一併初始化
//
// 若 _audioProvider 延後初始化（例如放到 _Ready 後段）或漏掉，則：
// - SpawnEffect / UseMagic 創建的 SkillEffect 在 Init(..., _audioProvider, ...) 時會收到 null
// - 日誌會出現 [Magic][SkillEffect.Init] ... HasAudio:False
// - SkillEffect 內幀音效（list.spr 的 spr_sound_id）不會播放，魔法/攻擊音效全無
//
// 因此 _audioProvider 必須在 _Ready 最前面（緊接 _skinBridge 之後）就賦值，
// 且不可刪除或移到後續 BLOCK，避免後續重構再次導致無音效。
// ────────────────────────────────────────────────────────────────────────────
//
// [注意]
// - 本轮只做初始化块下沉与注释完善，不改变功能与执行顺序。
// ============================================================================

using Godot;
using System;
using System.Collections.Generic;
using Client.Network;
using Client.Data;
using Client.UI;
using Core.Interfaces; // 【新增】

namespace Client.Game
{
	// =========================================================================
	// [SECTION] Class Declaration (GameWorld 主类定义)
	// =========================================================================
	public partial class GameWorld : Node2D
	{
		// =====================================================================
		// [SECTION] Exported References (编辑器导出引用)
		// =====================================================================

		[Export] public PackedScene EntityScene;
		[Export] public PacketHandler PacketHandlerRef;
		[Export] public PackedScene HUDScene;

		/// <summary>true = 受擊/被圍毆時也允許移動；false = 受擊僵硬期間禁止移動（舊行為）。</summary>
		[Export] public bool AllowMoveWhenDamageStiffness { get; set; } = true;

		// =====================================================================
		// [SECTION END] Exported References
		// =====================================================================
		// 【已廢棄】CurrentMapOrigin 已不再使用於座標轉換
		// 所有座標轉換現在使用 CoordinateSystem.GridToPixel() 統一處理
		// 保留此變數僅為向後兼容，永遠保持為 (0,0)
		[System.Obsolete("Use CoordinateSystem.GridToPixel() instead. CurrentMapOrigin is no longer used for coordinate conversion.")]
		public static Vector2I CurrentMapOrigin { get; set; } = Vector2I.Zero;

		// =====================================================================
		// [SECTION] Runtime Components (运行期组件引用)
		// =====================================================================

		private Client.UI.HUD _hud;
		private GodotTcpSession _netSession;

		internal Dictionary<int, GameEntity> _entities = new Dictionary<int, GameEntity>();
		internal GameEntity _myPlayer;
		internal int _myObjectId;
		
		// 【關鍵修復】緩存待處理的移動包：當收到 S_ObjectMoving 但實體不存在時，緩存移動包
		// 當收到 S_ObjectAdd 創建實體時，檢查是否有緩存的移動包，如果有，應用它
		// 這解決了時序問題：服務器可能先發送移動包，然後才發送創建包
		private Dictionary<int, (int x, int y, int heading)> _pendingMovePackets = new Dictionary<int, (int, int, int)>();
		/// <summary>己方召喚物/寵物 ObjectId 集合；點擊時開 TalkWindow(moncom)，Z 自動尋怪時排除。</summary>
		internal HashSet<int> _mySummonObjectIds = new HashSet<int>();
		
		// 【診斷日誌】追蹤怪物攻擊和移動時間，用於分析故障
		// 記錄每個怪物最後一次攻擊的時間戳
		private Dictionary<int, long> _lastMonsterAttackTime = new Dictionary<int, long>();
		// 記錄每個怪物最後一次收到移動包的時間戳
		private Dictionary<int, long> _lastMonsterMoveTime = new Dictionary<int, long>();
		private Camera2D _camera;




		// 【音频】仅保留提供者，用于实体/魔法音效资源获取；播放器已移除，托管给 Boot。
		// 必須在 _Ready 開頭與 _skinBridge 一併初始化，否則 SkillEffect.Init 會收到 null（HasAudio:False），魔法/攻擊音效全無。
		private IAudioProvider _audioProvider;

		// 新增/修复】必须在这里定义皮肤接口，否则 GameEffect 无法使用！
		private ISkinBridge _skinBridge;

		private const int CELL_SIZE = 32;
		


		// =====================================================================
		// [SECTION END] Runtime Components
		// =====================================================================


		// =====================================================================
		// [SECTION] Shared State Variables (分部类共享状态变量)
		// =====================================================================

		// 战斗状态
		private GameEntity _autoTarget = null;
		private bool _isAutoAttacking = false;
		private bool _isAutoPickup = false;

		// 移动状态
		private bool _isAutoWalking = false;
		private int _targetMapX;
		private int _targetMapY;
		private float _moveInterval = 0.6f;
		private float _moveTimer = 0.0f;
		private long _lastMovePacketTime = 0; // 記錄最後一次發送移動包的時間，用於診斷丟包
		
		// 死亡復活狀態
		private float _resurrectionTimer = 0.0f; // 復活計時器（30秒）
		private const float RESURRECTION_TIME = 30.0f; // 30秒復活時間
		private bool _isPlayerDead = false; // 玩家是否死亡
		private bool _deathDialogShown = false; // 是否已顯示死亡提示視窗（避免重複彈出）

		// 保活封包 C_182：每 30 秒發送一次
		private float _keepAliveAccum = 0f;
		private const float KEEPALIVE_INTERVAL = 30f;

		// S_Bookmarks (11) 記憶座標快取；(name, mapId, id, x, y)
		internal System.Collections.Generic.List<(string name, int mapId, int id, int x, int y)> _bookmarks = new System.Collections.Generic.List<(string, int, int, int, int)>();

		// skill 
		// 【新增】特效层节点，用于统一管理特效
		private Node2D _effectLayer;

		// =====================================================================
		// [SECTION END] Shared State Variables
		// =====================================================================


		// =====================================================================
		// [SECTION] Godot Lifecycle: _Ready (初始化入口 - 执行顺序保持不变)
		// 说明：此处只负责“按顺序调度 Setup 小函数”，具体实现放在 GameWorld.Setup.cs。
		// =====================================================================
		public override void _Ready()
		{
			// 依當前角色名載入該角色專屬設定（語言、音效、黑夜、血條、快捷欄），重登後才正確套用
			Client.Data.ClientConfig.SwitchCharacter(Client.Boot.Instance?.CurrentCharName ?? "");

			// -------------------------------------------------------------
			// 【核心修復】使用 Boot 中已初始化的 SkinBridge 和 AudioProvider，避免重複初始化
			// Boot 在 _Ready 中已經初始化了 CustomSkinLoader 和 CustomAudioProvider
			// 這裡直接使用 Boot 的實例，避免創建多個實例造成資源浪費和狀態不一致
			// -------------------------------------------------------------
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			if (boot != null && boot.SkinBridge != null)
			{
				_skinBridge = boot.SkinBridge;
				// 【優化】使用 SkinBridge 的 Audio 屬性，避免重複創建 AudioProvider
				// CustomSkinLoader 內部已經創建了 AudioProvider，直接使用它
				_audioProvider = boot.SkinBridge.Audio;
				GD.Print($"[GameWorld._Ready] Using Boot's SkinBridge and AudioProvider: SkinBridge={(_skinBridge != null)} AudioProvider={(_audioProvider != null)}");
			}
			else
			{
				// 如果 Boot 不存在（不應該發生），回退到創建新實例
				GD.PrintErr("[GameWorld._Ready] Boot or SkinBridge not found! Creating new instances (this should not happen)");
				_skinBridge = new Skins.CustomFantasy.CustomSkinLoader();
				_audioProvider = _skinBridge.Audio; // 使用 SkinBridge 的 Audio 屬性
			}

			// 【新增】初始化特效系统 (调用 GameWorld.GameEffect.cs 中的方法)
			InitEffectSystem();

			
			// 受擊僵硬時是否禁止移動：由開關控制（AllowMoveWhenDamageStiffness=true 表示允許移動）
			GameEntity.DamageStiffnessBlocksMovement = !AllowMoveWhenDamageStiffness;

			// -------------------------------------------------------------
			// [BLOCK] 1) 获取运行期引用（NetSession / PacketHandlerRef）
			// -------------------------------------------------------------
			SetupResolveRuntimeRefs();
			// -------------------------------------------------------------
			// [BLOCK END] 1) 获取运行期引用
			// -------------------------------------------------------------


			// -------------------------------------------------------------
			// [BLOCK] 2) UIManager 自启动（避免 Instance 为空）
			// -------------------------------------------------------------
			SetupEnsureUIManager();
			// -------------------------------------------------------------
			// [BLOCK END] 2) UIManager 自启动
			// -------------------------------------------------------------


			// -------------------------------------------------------------
			// [BLOCK] 3) 绑定 PacketHandler 信号（Bindings 文件中实现）
			// -------------------------------------------------------------
			SetupBindPacketHandlerSignals();
			// -------------------------------------------------------------
			// [BLOCK END] 3) 绑定 PacketHandler 信号
			// -------------------------------------------------------------

			// [NEW] 4) 初始化地图提供者 (必须在 Camera 和 通知Boot 之前)
			// -------------------------------------------------------------
			SetupInitMapProvider();

			// -------------------------------------------------------------
			// [BLOCK] 4) 初始化摄像机
			// -------------------------------------------------------------
			SetupInitCamera();
			// -------------------------------------------------------------
			// [BLOCK END] 4) 初始化摄像机
			// -------------------------------------------------------------


			// -------------------------------------------------------------
			// [BLOCK] 5) 初始化 HUD + 连接聊天输入回调
			// -------------------------------------------------------------
			SetupInitHUDAndChat();
			// -------------------------------------------------------------
			// [BLOCK END] 5) 初始化 HUD + 聊天回调
			// -------------------------------------------------------------

			// -------------------------------------------------------------
			// [BLOCK] 5b) 日夜全螢幕 Shader（依伺服器遊戲世界時間）
			// -------------------------------------------------------------
			SetupInitDayNightOverlay();
			// -------------------------------------------------------------
			// [BLOCK END] 5b) 日夜 Shader
			// -------------------------------------------------------------


			// -------------------------------------------------------------
			// [BLOCK] 6) 系统在线日志（保持原顺序）
			// -------------------------------------------------------------
			GD.Print("[GameWorld] System Online.");
			// -------------------------------------------------------------
			// [BLOCK END] 6) 系统在线日志
			// -------------------------------------------------------------


			// -------------------------------------------------------------
			// [BLOCK] 7) 通知 Boot：世界场景就绪（释放缓存/切换状态）
			// -------------------------------------------------------------
			SetupNotifyBootWorldReady();
			// -------------------------------------------------------------
			// [BLOCK END] 7) 通知 Boot
			// -------------------------------------------------------------

			// 8) 角色繪製順序：下方遮住上方。關閉 YSort，改由 _Process 依 Y 降序手動排序。
			YSortEnabled = false;

			// 9) _audioProvider 已於 _Ready 開頭與 _skinBridge 一併初始化（見上方註解），此處不再重複，避免無音效。
			// [注意] 此处不再创建 BGMPlayer，所有背景音乐由 Boot 播放

			
			if (PacketHandlerRef != null)
		    {
		        PacketHandlerRef.ServerSoundReceived += OnServerSoundReceived;
		    }

		}
		// =====================================================================
		// [SECTION END] Godot Lifecycle: _Ready
		// =====================================================================


		// =====================================================================
		// [SECTION] Godot Lifecycle: _Process (主循环驱动)
		// =====================================================================
		public override void _Process(double delta)
		{
			if (_myPlayer == null) return;

			// 【玩家死亡復活處理】更新復活計時器
			if (_isPlayerDead)
			{
				_resurrectionTimer -= (float)delta;
				if (_resurrectionTimer <= 0.0f)
				{
					// 30秒到了，等待服務器發送復活封包（服務器會自動傳送到村莊）
					_resurrectionTimer = 0.0f;
					GD.PrintRich($"[color=yellow][Resurrection] 30秒復活時間已到，等待服務器復活並傳送回村莊...[/color]");
					// 注意：實際復活由服務器發送 S_ObjectRestore 或傳送封包觸發，這裡只記錄日誌
				}
			}

			// 【刪除】定期位置更新機制已刪除
			// 如果正在移動，移動包已經包含位置信息，不需要定期更新
			// 如果沒有移動，也不需要定期更新（服務器會從其他包中獲取位置信息）

			// 1) 移动逻辑 -> GameWorld.Movement.cs
			// 注意：死亡期間不允許移動
			if (!_isPlayerDead)
			{
				UpdateMovementLogic(delta);
			}

			// 2) 战斗逻辑 -> GameWorld.Combat.cs
			// 注意：死亡期間不允許戰鬥
			if (!_isPlayerDead)
			{
				UpdateCombatLogic(delta);
			}

			// 4) 拾取/背包逻辑 -> GameWorld.Inventory.cs
			UpdateInventoryLogic(delta);

			// 4b) 保活 C_182：週期發送
			_keepAliveAccum += (float)delta;
			if (_keepAliveAccum >= KEEPALIVE_INTERVAL)
			{
				_keepAliveAccum = 0f;
				_netSession?.Send(Client.Network.C_KeepAlivePacket.Make());
			}

			// 5) 視野優化：延後加載的實體進入範圍後補載圖像 -> GameWorld.Entities.cs
			UpdateDeferredVisuals();

			// 6) 【性能優化】清理遠距離實體：移動距離>5格時清理15格外的實體
			CleanupDistantEntities();

			// 7) 角色繪製順序：Y 小（螢幕下方）後畫、遮住上方
			SortEntityDrawOrder();
		}

		/// <summary>依 Position.Y 升序重排：Y 小先畫（在後）、Y 大後畫（在前），使螢幕下方（Y 大）遮住上方。死亡角色（8.death）始終在最前面（索引小，先繪製，被其他角色遮住）。不移動 MapLayer / EffectLayer。</summary>
		private void SortEntityDrawOrder()
		{
			var children = GetChildren();
			var entities = new List<GameEntity>();
			foreach (var c in children)
			{
				if (c is GameEntity e) entities.Add(e);
			}
			if (entities.Count <= 1) return;
			
			// 死亡角色（8.death）應該被其他角色遮住
			// 在 Godot 中，子節點索引小的先繪製（在後面），索引大的後繪製（在前面，會蓋住前面的）
			// 因此死亡角色應該排在前面（索引小），這樣會先繪製，被後繪製的活著角色蓋住
			// 正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0
			entities.Sort((a, b) => {
				// 死亡角色（播放 8.death 動作）排在前面（索引小，先繪製）
				bool aDead = a.IsDead;
				bool bDead = b.IsDead;
				if (aDead != bDead) return aDead ? -1 : 1; // 死亡角色排在前面（索引小），活著的排在後面（索引大）
				// 同為活著或同為死亡，按 Y 座標排序（Y 小的先繪製，Y 大的後繪製）
				return a.Position.Y.CompareTo(b.Position.Y);
			});
			
			int baseIndex = 0;
			foreach (var c in children)
			{
				if (!(c is GameEntity)) baseIndex++;
			}
			for (int i = 0; i < entities.Count; i++)
				MoveChild(entities[i], baseIndex + i);
		}

        // =====================================================================
        // [新增] 响应服务器 Opcode 29 (S_ObjectMode)
        // 核心职责：将服务器下发的权威外观 GfxMode 应用到实体
        // 此方法被 GameWorld.Bindings.cs 中的 BindPacketHandlerSignals 绑定
        // =====================================================================
        private void OnObjectVisualModeChanged(int objectId, int mode)
        {
            // 1. 处理主角 (_myPlayer)
            if (_myPlayer != null && _myPlayer.ObjectId == objectId)
            {
                // [日志] 确认收到，方便调试
                GD.Print($"[GameWorld] MyPlayer Visual Sync -> Mode: {mode}");
                _myPlayer.SetVisualMode(mode);
                return;
            }

            // 2. 处理其他实体 (NPC / 怪物 / 其他玩家)
            // 使用 GameWorld.cs 中定义的 _entities 字典
            if (_entities != null && _entities.ContainsKey(objectId))
            {
                var entity = _entities[objectId];
                if (entity != null)
                {
                    entity.SetVisualMode(mode);
                }
            }
        }

        // =====================================================================
        // [變身] 響應伺服器 Opcode 39 (S_ObjectPoly) — 變身卷軸/藥水後外觀更新
        // 封包結構: writeD(objectId), writeH(gfx), writeC(gfxMode), writeC(255), writeC(255)
        // 職責：更新實體 GfxId 與姿勢 (gfxMode)，並刷新視覺。
        // =====================================================================
        private void OnObjectVisualUpdated(int objectId, int gfxId, int gfxMode, int heading)
        {
            GameEntity entity = null;
            if (_myPlayer != null && objectId == _myPlayer.ObjectId)
            {
                entity = _myPlayer;
            }
            else if (_entities != null && _entities.TryGetValue(objectId, out var found))
            {
                entity = found;
            }
            if (entity == null) return;

            entity.GfxId = gfxId;
            if (gfxMode >= 0)
                entity.SetVisualMode(gfxMode);
            if (heading >= 0 && heading <= 7)
                entity.Heading = heading;
            entity.RefreshVisual();
        }

        // 2. [核心修改] 響應 S_OPCODE_SOUND(84) / 藥水・技能音效 -> 轉發給 Boot 播放
		private void OnServerSoundReceived(int soundId)
		{
		    if (soundId <= 0) return;
		    Boot.Instance?.PlayGlobalSound(soundId);
		}
	}
}
