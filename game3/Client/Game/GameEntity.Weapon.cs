// ============================================================================
// [FILE] GameEntity.Weapon.cs
// [职责] 外观与武器模式权威 (Weapon Authority)
// 1. 持有 _visualBaseAction (4=Sword, 20=Bow...)
// 2. 响应服务器 S_ObjectMode (Op29)
// 3. 维护逻辑武器类型 (WeaponType) 供战斗系统使用

// ============================================================================
// [FILE] GameEntity.Weapon.cs
// GameEntity.Action.cs        ✅ 动作状态机（SetAction）
// GameEntity.Weapon.cs        ✅ 外观/武器模式（gfxMode）
// GameEntity.Visuals.cs       ✅ 播放动画（只消费结果）
// GameEntity.Movement.cs      ✅ 移动 → 请求 Walk
// GameEntity.Combat.cs        ✅ 战斗 → 请求 Attack
// 修改说明：
// 1. [新增] SetVisualMode：直接响应服务器 Opcode 29 (S_ObjectMode)，是外观的绝对权威。
// 2. [逻辑] UpdateLogicalWeaponType：通过外观反推逻辑类型(WeaponType)，确保攻击距离/弹道判断正确。
// 3. [废弃] SetWeaponType：已清空逻辑。外观不再由 Inventory 决定。

// [職責說明]:
//   1. 響應 Opcode 29 (S_ObjectMode): 接收 gfxMode 並轉換為視覺偏移。
//   2. 提供邏輯判定: 為 Combat 與 Movement 系統提供 IsUsingBow() 等判定。
//   3. 姿勢重定向: 確保主體層在換裝時能自動切換正確的 Posture。
/* 
武器邏輯類型 (Item Type) 來自 Item.java:
TYPE_AXE = 2
TYPE_BOW = 3 (注意：服務器裡弓是 3)
TYPE_SPEAR = 4 (注意：服務器裡矛是 4)
TYPE_SWORD = 5 (注意：服務器裡劍是 5)
TYPE_WAND = 6 (杖)
姿勢偏移 (GfxMode) 來自 ItemsTable.java (Line 50-60):
劍(5) -> gfxmode 4
斧(2) -> gfxmode 11
弓(3) -> gfxmode 20
矛(4) -> gfxmode 24
杖(6) -> gfxmode 40
*/

// ============================================================================

using Godot;

namespace Client.Game
{
	// [姿勢文件] 負責處理武器切換與動作偏移
	public partial class GameEntity
	{
		// ==========================================
		// [核心入口] 姿勢與外觀同步
		// ==========================================

		/// <summary>
		/// 響應服務端 0x29 封包，切換角色視覺姿勢 (Base Action Offset)
		/// </summary>
		public void SetVisualMode(int gfxMode)
		{
			// 1. GM 視覺鎖定檢查
			if (_isVisualLocked) return;

			// 2. 姿勢更新檢查
			if (_visualBaseAction == gfxMode) return;
			
			_visualBaseAction = gfxMode;
			
			// 3. 核心功能：根據姿勢自動反推邏輯武器類型 (確保戰鬥系統聯通)
			UpdateLogicalWeaponType(gfxMode);

			GD.Print($"[Weapon] Posture Sync: {gfxMode} (Type:{_weaponType}) for Obj:{ObjectId}");
			
			// 4. 驅動渲染引擎刷新
			RefreshVisual();
		}

		// ==========================================
		// [邏輯判定] 供 Combat/Movement 使用 (修復 CS1061)
		// ==========================================
		/// <summary>當前視覺姿勢偏移（gfxMode）。對齊服務器 currentWeapon。</summary>
		public int GetVisualBaseAction() => _visualBaseAction;




		// ==========================================
		// [內部機制]
		// ==========================================

		/// <summary>
		/// 根據姿勢偏移反推武器類型，確保攻擊距離與彈道正確。
		/// 參考數值映射表：弓:20, 矛:24, 杖:40, 劍:4...
		/// </summary>
		private void UpdateLogicalWeaponType(int gfxMode)
		{
            // [修正] 直接操作私有字段 _weaponType
            _weaponType = gfxMode switch
            {
                4  => 5, // Gfx 4 映射為 TYPE_SWORD (5)
                11 => 2, // Gfx 11 映射為 TYPE_AXE (2)
                20 => 3, // Gfx 20 映射為 TYPE_BOW (3)
                24 => 4, // Gfx 24 映射為 TYPE_SPEAR (4)
                40 => 6, // Gfx 40 映射為 TYPE_WAND (6)
                _  => 0  // 0 為空手或無
            };

		}

        // 獲取當前姿勢對應的走路動作
        public int GetWeaponWalkAction() => ACT_WALK + _visualBaseAction;
		// 供戰鬥系統判定攻擊距離與動作
        public bool IsUsingBow() => _weaponType == 3;   // 嚴格對齊服務端 TYPE_BOW = 3
        public bool IsUsingSpear() => _weaponType == 4; // 嚴格對齊服務端 TYPE_SPEAR = 4
        public bool IsUsingStaff() => _weaponType == 6; // 嚴格對齊服務端 TYPE_SPEAR = 6



		public void SetWeaponType(int type)
		{
			// Inventory 系統調用
			_weaponType = type;
			    // 【核心修復】當裝備類型改變時，需要反推姿勢偏移，否則外觀不變
			    // 這裡我們假設 type 是服務端 Item.java 的類型 (1:劍, 3:弓...)
			    _visualBaseAction = type switch {
			        5 => 4,  // Sword -> Posture 4
			        2 => 11, // Axe -> Posture 11
			        3 => 20, // Bow -> Posture 20
			        4 => 24, // Spear -> Posture 24
			        6 => 40, // Staff -> Posture 40
			        _ => 0
			    };
			RefreshVisual();
		}

		/// <summary>
		/// 設置 GM 視覺鎖定 (修復日誌報錯)
		/// </summary>
		public void SetVisualLock(bool locked)
		{
			_isVisualLocked = locked;
			GD.Print($"[GM] Visual Lock: {locked} for {RealName}");
		}
	}
}
