# 速度管理器優化總結

## 優化內容

### 1. 統一使用 EnhancedSpeedManager

**問題**：
- `SpeedManager` 和 `EnhancedSpeedManager` 功能重複
- 兩者都維護獨立的時間戳和 Buff 狀態
- 需要同時更新兩個管理器，容易導致狀態不同步

**優化**：
- 將 `SpeedManager` 標記為過時，內部委託給 `EnhancedSpeedManager`
- 所有新代碼統一使用 `EnhancedSpeedManager`
- 簡化 `GameWorld.Buffs.cs`，只更新 `EnhancedSpeedManager`

**修改文件**：
- `Client/Game/SpeedManager.cs` - 改為委託模式
- `Client/Game/GameWorld.Skill.cs` - 改用 `EnhancedSpeedManager`
- `Client/Game/GameWorld.Buffs.cs` - 移除重複的 `SpeedManager` 調用

### 2. 移除 _attackCooldownTimer 重複功能

**問題**：
- `_attackCooldownTimer` 在 `UpdateCombatLogic()` 中檢查攻擊冷卻
- `EnhancedSpeedManager.CanPerformAction()` 在 `PerformAttackOnce()` 中檢查攻擊冷卻
- 兩者功能重複，可能導致不同步

**優化**：
- 移除 `_attackCooldownTimer` 及其相關邏輯
- 統一使用 `EnhancedSpeedManager.CanPerformAction()` 進行冷卻檢查
- `_attackInProgress` 僅用於標記攻擊動畫是否正在播放

**修改文件**：
- `Client/Game/GameWorld.Combat.cs` - 移除 `_attackCooldownTimer` 相關代碼

### 3. 統一時間戳更新機制

**問題**：
- `SpeedManager.CanPerformAction()` 在檢查通過時就更新時間戳
- `EnhancedSpeedManager.CanPerformAction()` 不更新時間戳，需要手動調用 `RecordActionPerformed()`
- 時間戳更新時機不一致

**優化**：
- 統一使用 `EnhancedSpeedManager.CanPerformAction()` + `RecordActionPerformed()`
- 時間戳在實際發送封包後才更新，確保與服務器同步

## 優化效果

### 代碼簡化
- 移除重複的時間戳和 Buff 狀態管理
- 統一冷卻檢查邏輯
- 減少代碼行數約 100+ 行

### 邏輯統一
- 所有速度檢查都使用 `EnhancedSpeedManager`
- 時間戳更新時機一致
- Buff 狀態管理統一

### 易於維護
- 單一職責：`EnhancedSpeedManager` 負責所有速度相關邏輯
- 減少狀態同步問題
- 更容易調試和測試

## 向後兼容

- `SpeedManager` 保留為過時類，內部委託給 `EnhancedSpeedManager`
- 現有使用 `SpeedManager` 的代碼仍可正常工作
- 建議逐步遷移到 `EnhancedSpeedManager`

## 注意事項

1. **時間戳更新**：
   - 必須在實際發送封包後調用 `RecordActionPerformed()`
   - 如果封包發送失敗，不應更新時間戳

2. **Buff 狀態**：
   - 統一使用 `EnhancedSpeedManager.SetHaste/Brave/Slow()`
   - 不再需要同時更新兩個管理器

3. **冷卻檢查**：
   - 統一使用 `EnhancedSpeedManager.CanPerformAction()`
   - 不再使用 `_attackCooldownTimer` 進行冷卻檢查
