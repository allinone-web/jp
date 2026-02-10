// ==================================================================================
// [FILE] Client/Game/GameEntity.cs
// [ARCHITECTURE] 2D Layered Sprite Animation System (Lineage Style)
//
// [類功能說明 - 開發規範]
// 本類為遊戲世界所有實體（玩家、NPC、怪物）的邏輯與視覺容器。
// 遵循以「主體姿勢(Master)」驅動「組件圖層(Slave)」的核心準則。
//
// 1. 動作模組 (Base Action Sets):
//    實體不直接決定 GfxId，而是通過 _visualBaseAction 偏移量切換姿勢。
//    公式：FinalActionId = RawAction (0:走, 1:攻) + _visualBaseAction (0:空手, 4:劍...)
//
// 2. 渲染管線 (Rendering Pipeline):
//    - 數據源: 依賴 ListSprLoader 解析 list.spr 提供的 105(服裝), 106(武器), 109(特效)。
//    - 同步機制: 必須調用 InitializeVisualSystem() 初始化多層 AnimatedSprite2D。
//    - 幀同步: 以主體層為 Master，強制同步所有 Slave 層的 Frame 索引。
//
// 3. 層級順序 (Z-Order Standard):
//    陰影 (-1) -> 主體 (0) -> 武器 (1) -> 服裝 (2+) -> 臨時特效 (最上層)
//
// 4. 攻速權威 (Framerate):
//    所有動畫組件的 SpeedScale 必須由 list.spr 中的 110 字段計算所得。
//
// [注意事項]: 
// - 禁止在主類中寫死資源路徑。
// - 任何視覺更新必須通過 UpdateAppearance() 接口觸發。
// [職責分配]:
//   1. 本文件為唯一變量聲明處，禁止在其他 partial 文件聲明任何 private 字段。
//   2. 負責處理公共屬性的 Getter/Setter 以及生命週期初始化。
//   3. 統一管理動作常量，修復外部系統引用缺失。


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

// ==================================================================================

using Godot;
using System;
using System.Collections.Generic;
using Core.Interfaces;
using Client.Utility;

namespace Client.Game
{
	// [主文件] 職責：數據存儲中心與生命週期
	public partial class GameEntity : Node2D
	{
		// ==========================================
        // [SECTION] 核心動作常量 (對齊服務器 ActionCodes.java 和 SprTable.java)
		// ==========================================
		// 【服務器對齊】參考 server/check/ActionCodes.java
		// 基礎動作：
		public const int ACT_WALK = 0;        // ACTION_Walk = 0 (對齊 ActionCodes.java)
		public const int ACT_ATTACK = 1;      // ACTION_Attack = 1
		public const int ACT_DAMAGE = 2;      // ACTION_Damage = 2
		public const int ACT_BREATH = 3;      // ACTION_Idle = 3 (待機/呼吸)
		// 武器動作偏移（對齊 ActionCodes.java）：
		// - 劍 (Sword): 4=walk, 5=attack, 6=damage, 7=idle
		// - 斧 (Axe): 11=walk, 12=attack, 13=damage, 14=idle
		// - 弓 (Bow): 20=walk, 21=attack, 22=damage, 23=idle
		// - 矛 (Spear): 24=walk, 25=attack, 26=damage, 27=idle
		// - 杖 (Staff): 40=walk, 41=attack, 42=damage, 43=idle
		// - 匕首 (Dagger): 46=walk, 47=attack, 48=damage, 49=idle
		// - 雙手劍 (TwoHandSword): 50=walk, 51=attack, 52=damage, 53=idle
		public const int ACT_DEATH = 8;       // ACTION_Die = 8
		public const int ACT_PICKUP = 15;     // ACTION_Pickup = 15
		public const int ACT_SPELL_DIR = 18; // ACTION_SkillAttack = 18 (有方向魔法)
		// ACTION_SkillBuff = 19 (無方向魔法，對齊 SprTable.java nodirSpellSpeed)
		public const int ACT_ATTACK_BOW = 21; // ACTION_BowAttack = 21
		
		// 【服務器對齊】特殊動作（對齊 ActionCodes.java）
		// ACTION_HideDamage = 13 (隱身受擊，與 ACTION_AxeDamage 共用 ID)
		// ACTION_HideIdle = 14 (隱身待機，與 ACTION_AxeIdle 共用 ID)
		// ACTION_On = 28, ACTION_Off = 29 (門/開關動作)
		// ACTION_AltAttack = 30 (替代攻擊)
		// ACTION_SpellDirectionExtra = 31 (有方向魔法額外動作)
		
		// 【服務器對齊】SprTable.java 動作分類（用於速度檢查）：
		// moveSpeed: 0, 4, 11, 20, 24, 40, 46, 50
		// attackSpeed: 1, 5, 12, 21, 25, 30, 31, 41, 47, 51
		// dirSpellSpeed: 18
		// nodirSpellSpeed: 19

        // ==========================================
        // [SECTION] 视觉微调配置 (编辑器可调)
        // ==========================================
        [ExportGroup("Visual Adjustments")]
        // 將默認值設為您測試出的黃金數值。-30 -20
        // [Export] public Vector2 BodyOffset = new Vector2(-30, -20);   // 全身偏移：帶動所有圖層同步移動
        [Export] public Vector2 BodyOffset = new Vector2(0, 0);   // 全身偏移：帶動所有圖層同步移動

        // ==========================================
        // [SECTION] 公共属性
        // ==========================================
        [Export] public int ObjectId { get; set; }
        [Export] public int GfxId { get; set; }
        public int MapX { get; set; }
        public int MapY { get; set; }
        /// <summary>地面物品數量，來自 S_ObjectAdd writeD(o.getCount())；非地面物品為 0。拾取時送此值即「全部拾取」。</summary>
        public int ItemCount { get; set; }
        public int Lawful { get; set; } = 0;
        public float AnimationSpeed { get; set; } = 1.0f;

		/// <summary>血量比例 0–100，來自伺服器 Opcode 104 (ObjectHitRatio)。未收到前預設 100。</summary>
		public int HpRatio => _hpRatio;

		/// <summary>是否死亡：正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0（隱藏/變身怪物可能血量為0但未死亡）。</summary>
		public bool IsDead => _currentRawAction == ACT_DEATH;

		/// <summary>是否顯示血條：僅依伺服器數據。伺服器送過 S_HpMeter(0-100) 或 CharPack 的 HP 字節在 0-100 時為 true；0xFF/未送過為 false。不依 list.spr（圖層）判斷邏輯。</summary>
		public bool ShouldShowHealthBar()
		{
			return HpRatio >= 0 && HpRatio <= 100;
		}

		/// <summary>是否有血量（可計算傷害）：與 ShouldShowHealthBar 同源，僅依伺服器數據。用於 Z 鍵選怪/對話邏輯。</summary>
		public bool HasServerHp()
		{
			return HpRatio >= 0 && HpRatio <= 100;
		}

        // 讓外部（GameWorld）可以讀取當前是否正在執行不可中斷的動作
        public bool IsActionBusy => _isActionBusy;
		/// <summary>是否正在播放受擊僵硬動畫 (ActionId=2.damage)；全專案唯一依此判定「受擊期間禁止移動」。</summary>
		public bool IsInDamageStiffness => _currentRawAction == ACT_DAMAGE;
		/// <summary>開關：受擊僵硬期間是否禁止移動，預設 true。</summary>
		public static bool DamageStiffnessBlocksMovement { get; set; } = true;
		// 戰鬥關鍵幀事件
        public event Action<int, int> OnAttackKeyFrameHit;



        /// 當前最終動作 ID = 基礎動作 + 姿勢偏移
        public int CurrentAction 
        { 
            get => _currentRawAction + _visualBaseAction; 
            set => _currentRawAction = value - _visualBaseAction; 
        }

        /// <summary>
        /// 當前朝向 (0-7)
        /// </summary>
        public int Heading 
        { 
            get => _heading; 
            set => _heading = value; 
        }
        // [修正 CS0200] 允許寫入，修復 Weapon.cs 報錯
        public int WeaponType { get => _weaponType; set => _weaponType = value; }
		// public int WeaponType => _weaponType;
				
		public int VisualBaseAction => _visualBaseAction;



		// ==========================================
		// [SECTION] 內部私有變量 (分部類共享)
		// ==========================================
        // 狀態標記
        private int _currentRawAction = 3; // 默认为 Breath
		private int _heading = 4;
		private int _hpRatio = 100; // 血量比例 0–100，伺服器 104 更新；預設 100 視為存活
		private int _visualBaseAction = 0; 
		private int _weaponType = 0; // 0=空手, 1=劍, 2=斧, 3=弓, 4=矛, 5=杖

		// [待機規則] 唯一邏輯：ActionId=3 或 Name(breath/idle)；無則不做待機
		public void SetDefaultStandAction()
		{
			var def = ListSprLoader.Get(GfxId);
			var idleSeq = ListSprLoader.GetIdleSequence(def);
			if (idleSeq == null) return;
			// 【關鍵修復】待機動作（如 28.standby）也需要加 _visualBaseAction 偏移
			// 因為這些動作有8方向，應該映射（如 28 + 3 = 31）
			// GetActionSequence 會通過語義關鍵字（standby/idle）找到對應的序列
			CurrentAction = idleSeq.ActionId + _visualBaseAction;
			// 【關鍵修復】同時更新 _currentRawAction，確保 RefreshVisual() 使用正確的值
			_currentRawAction = idleSeq.ActionId;
			RefreshVisual();
		}

		/// <summary>
		/// 幀播放規則：非循環動作播完後回到第一幀並停止（全局唯一封裝，勿重複定義）
		/// </summary>
		internal void StopAllLayersOnFirstFrame()
		{
			if (_mainSprite != null) { _mainSprite.Frame = 0; _mainSprite.Stop(); }
			if (_shadowLayer != null) { _shadowLayer.Frame = 0; _shadowLayer.Stop(); }
			foreach (var cl in _clothesPool) { cl.Frame = 0; cl.Stop(); }
		}

		// 讓外部（GameWorld）可以讀取當前是否正在執行不可中斷的動作
		private bool _isActionBusy = false;       // 【修復】宣告動作忙碌鎖

		// [關鍵修復] 這裡聲明後，Visuals.cs 就不可以再聲明
		private bool _isVisualLocked = false;     // GM 視覺鎖
		private bool _isVisualLoading = false;    // 異步重試鎖
		// 【死亡動畫控制】僅在死亡動作第一次設置時重置動畫播放，避免 RefreshVisual 重複重置導致只播放第一幀
		private bool _deathAnimationRestart = false;
		// [重試上限] 同一實體 Visual-Missing 超過此次數後不再 TryAsyncRetry，避免刷屏；日誌不刪，僅減少重試與重複輸出
		private int _visualMissingRetryCount = 0;
		private const int VisualMissingRetryMax = 5;
		// [視野優化] 距離過遠時延後加載圖像，進入範圍後由 GameWorld 觸發 EnsureVisualsLoaded
		private bool _visualsDeferred = false;
		private int _lastProcessedFrame = -1;
		private bool _isAttackHitTriggered = false; // 防止單次動作多次命中
		// [隱身術] S_ObjectInvis (Op 52)：isSelf 時半透明，他人時完全隱藏
		private bool _isInvisible = false;
		private bool _isInvisibilitySelf = false;
		// 【新增】中毒狀態 (Opcode 50)
		private bool _isPoison = false;
		// 【新增】紫名狀態 (Opcode 106)
		private bool _isPinkName = false;
		// [109.effect / 幀特效] 每 (anim,frame) 只觸發一次 SpawnEffect，避免重複
		private string _lastSpawnedEffectAnim = "";
		private int _lastSpawnedEffectFrame = -1;
		// [PakBrowser 一致] 顯示改為 Centered = true、僅 BodyOffset，已移除 walk 錨點快取

        // [核心修復] 增加真實名稱存儲，防止顯示 @node2d
        private string _realName = "Unknown";
        public string RealName => _realName;
		/// <summary>S_OPCODE_CHANGENAME (81) 時由 GameWorld 呼叫，更新顯示名稱。</summary>
		internal void SetDisplayName(string name)
		{
			_realName = name ?? "";
			UpdateNameDisplay();
		} 
        // [修正 CS0103] 統一在此聲明 UI 組件，供 UI.cs 和 CombatFx.cs 使用
        private Label _nameLabel;
        private ProgressBar _healthBar;
        private Label _chatBubble;
        private ColorRect _body;

        // [修正 CS0103] 統一在此聲明渲染組件，供 Visuals.cs 和 Audio.cs 使用。106.weapon 層已徹底移除。
        private AnimatedSprite2D _mainSprite;      
        private AnimatedSprite2D _shadowLayer;    
        private readonly List<AnimatedSprite2D> _clothesPool = new(); 
        private readonly List<(int TargetId, int Damage)> _pendingAttacks = new();

		protected ISkinBridge _skinBridge;

		// ==========================================
		// [SECTION] 基礎接口 (修復外部調用)
		// ==========================================
		public override void _Ready()
		{
			// 獲取全局資源網橋
			_skinBridge = Boot.Instance.SkinBridge;

			// 1. 初始化視覺系統 (實作於 GameEntity.Visuals.cs)
			InitializeVisualSystem(); // 實作於 Visuals.cs
			SetupUI();               // 實作於 UI.cs
			// 2. 初始渲染 (默認待機)
			RefreshVisual();
		}

        /// 實體初始化入口，由 GameWorld 調用
		/// <param name="loadVisuals">若為 false，不載入圖像（延後至進入視野範圍），並隱藏節點以減輕客戶端壓力。</param>
		public void Init(Client.Data.WorldObject data, bool isMe, ISkinBridge skin, IAudioProvider audio, bool loadVisuals = true)
		{
			this.ObjectId = data.ObjectId;
			this.GfxId = data.GfxId;
			this.MapX = data.X;
			this.MapY = data.Y;
			this.ItemCount = data.Exp;
			this.Lawful = data.Lawful;
			this._heading = data.Heading;
			this._realName = data.Name; // [核心修復] 存儲真實名字
			this._skinBridge = skin;
			SetupAudio(audio);
			// 血條數據僅依伺服器：0-100 為有效比例；0xFF(255) 或其它表示「無血條」，記為 -1
			_hpRatio = (data.HpRatio >= 0 && data.HpRatio <= 100) ? data.HpRatio : -1;
			UpdateNameDisplay(); // 立即刷新名字
			if (!loadVisuals)
			{
				_visualsDeferred = true;
				Visible = false; // 不載入圖像時先隱藏，避免顯示替身或空白
				return;
			}
			// [修復] _Ready 在 AddChild 時已觸發，當時 GfxId 仍為 0，故首次 RefreshVisual 會用 gfx 0 當替身。Init 內 GfxId 已設好，必須再刷一次。
			RefreshVisual();
		}

		/// <summary>是否隱身（來自 S_ObjectInvis Op 52）。</summary>
		internal bool IsInvisible => _isInvisible;

		/// <summary>隱身術：由 GameWorld 呼叫。invis=true 且 isSelf=true 時自己半透明；invis=true 且 isSelf=false 時他人完全不可見。</summary>
		internal void SetInvisible(bool invis, bool isSelf)
		{
			_isInvisible = invis;
			_isInvisibilitySelf = isSelf;
			ApplyInvisibilityVisual();
		}

		/// <summary>套用隱身視覺：非隱身恢復正常；自己隱身時半透明；他人隱身時完全不可見。</summary>
		internal void ApplyInvisibilityVisual()
		{
			if (!_isInvisible)
			{
				Modulate = Colors.White;
				Visible = true;
				return;
			}
			if (_isInvisibilitySelf)
			{
				Modulate = new Color(1f, 1f, 1f, 0.5f);
				Visible = true;
			}
			else
			{
				Modulate = new Color(1f, 1f, 1f, 0f);
				Visible = false;
			}
		}
		
		/// <summary>【服務器對齊】設置中毒狀態 (Opcode 50)</summary>
		internal void SetPoison(bool poison)
		{
			_isPoison = poison;
			UpdateColorDisplay(); // 更新名字顏色（中毒時會變綠）
		}
		
		/// <summary>【服務器對齊】設置紫名狀態 (Opcode 106)</summary>
		internal void SetPinkName(bool pinkName, int duration = 0)
		{
			_isPinkName = pinkName;
			UpdateColorDisplay(); // 更新名字顏色（紫名時會變紫）
			// 【TODO】如果 duration > 0，可以啟動計時器，在 duration 秒後自動清除紫名狀態
		}

		/// <summary>是否尚未載入圖像（因距離過遠而延後）。由 GameWorld 依視野範圍決定何時呼叫 EnsureVisualsLoaded。</summary>
		internal bool IsVisualsDeferred => _visualsDeferred;

		/// <summary>進入視野範圍時由 GameWorld 呼叫，載入圖像並顯示。</summary>
		internal void EnsureVisualsLoaded()
		{
			if (!_visualsDeferred) return;
			_visualsDeferred = false;
			Visible = true;
			GD.Print($"[Visual-Defer-Show] ObjId={ObjectId} GfxId={GfxId} name={RealName} now visible at grid ({MapX},{MapY}) pos={Position}");
			RefreshVisual();
			ApplyInvisibilityVisual(); // 延遲顯示後仍須尊重隱身狀態
		}

        public void RefreshVisual()
        {
            // 視野優化：延後加載的實體不在此處載入，由 GameWorld 在進入範圍時呼叫 EnsureVisualsLoaded
            if (_visualsDeferred) return;
            // 【服務器對齊】動作映射規則與 SetAction 一致：
            // - 基礎動作（0,1,2,3）需要加 _visualBaseAction 偏移
            // - 特殊動作（8.death, 15.pickup, 18.spell 等）不偏移
            int finalActionId = _currentRawAction;
            if (_currentRawAction >= 0 && _currentRawAction <= 3)
                finalActionId = _currentRawAction + _visualBaseAction;
            // 這裡傳入 _weaponType，確保 Visuals 層拿到的是正確的武器索引
            UpdateAppearance(GfxId, finalActionId, _heading, _weaponType);
			
			// 【死亡動畫控制】只在第一次設置死亡動作時重置播放，避免每次刷新都重播第一幀
			if (_currentRawAction == ACT_DEATH && _deathAnimationRestart)
				_deathAnimationRestart = false;
        }


    }
}
