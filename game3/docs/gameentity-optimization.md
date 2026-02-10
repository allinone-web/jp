# GameEntity 系統優化報告

## 發現的問題

### 1. 未使用的方法（GameEntity.Visuals.cs）

**問題**：
- `ApplyLayerAnchorAtOrigin` (第213行) - 定義了但從未被調用
- `ApplyLayerFeetAlignToBody` (第227行) - 定義了但從未被調用
- `GetShadowClothesHeadingTweak` (第245行) - 定義了但從未被調用
- `_anchorDiagnosticLogCount` (第286行) - 定義了但從未被使用

**影響**：
- 代碼臃腫，增加維護成本
- 容易誤導開發者認為這些方法正在使用

**優化**：
- 移除這些未使用的方法和變量

### 2. 重複定義（DebugLastSprFile）

**問題**：
- `GameEntity.cs` 第141行：`public string DebugLastSprFile { get; set; } = "";`
- `GameEntity.Visuals.Debug.cs` 第35行：`private string _debugLastSprFile = "";`
- 兩個定義重複，但實際使用的是 `_debugLastSprFile` 和 `GetDebugLastSprFile()` 方法

**影響**：
- 屬性定義了但從未被使用
- 容易造成混淆

**優化**：
- 移除 `GameEntity.cs` 中的 `DebugLastSprFile` 屬性

### 3. 重複方法（SetWeapon vs SetWeaponType）

**問題**：
- `SetWeaponType(int type)` (第113行) - 被 `GameWorld.Inventory.cs` 使用，功能完整
- `SetWeapon(int weaponIndex)` (第139行) - 沒有被調用，功能與 `SetWeaponType` 重複

**影響**：
- 兩個方法功能重複，容易造成混淆
- `SetWeapon` 方法沒有反推姿勢偏移，功能不完整

**優化**：
- 移除 `SetWeapon` 方法，統一使用 `SetWeaponType`

## 優化方案

### 方案 1：移除未使用的方法和變量

**步驟**：
1. 移除 `ApplyLayerAnchorAtOrigin` 方法
2. 移除 `ApplyLayerFeetAlignToBody` 方法
3. 移除 `GetShadowClothesHeadingTweak` 方法
4. 移除 `_anchorDiagnosticLogCount` 變量

**優點**：
- 減少代碼量
- 提高可讀性
- 避免誤導

### 方案 2：統一 DebugLastSprFile 定義

**步驟**：
1. 移除 `GameEntity.cs` 中的 `DebugLastSprFile` 屬性
2. 保留 `GameEntity.Visuals.Debug.cs` 中的 `_debugLastSprFile` 和 `GetDebugLastSprFile()` 方法

**優點**：
- 消除重複定義
- 統一接口

### 方案 3：移除重複的 SetWeapon 方法

**步驟**：
1. 移除 `SetWeapon(int weaponIndex)` 方法
2. 統一使用 `SetWeaponType(int type)` 方法

**優點**：
- 消除重複功能
- 統一接口
- 避免功能不完整的方法

## 注意事項

1. **對齊邏輯不變**：
   - 角色/陰影/衣服對齊邏輯（`UpdateAllLayerOffsets`, `UpdateClothesOffsetsPerFrame`）保持不變
   - 8方向映射邏輯保持不變

2. **功能完整性**：
   - 移除的方法都是未使用的，不會影響現有功能
   - `SetWeapon` 方法未被調用，移除不會影響功能
