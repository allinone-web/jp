# "Attack blocked (speed check failed)" 問題分析

## 問題現象

```
[Combat] Attack blocked (speed check failed). Target:6265409 Dist:1 - Smart retry active (interval=100ms)
[SpeedManager] Attack on cooldown. remaining=480ms
[Combat] Attack blocked (speed check failed). Target:6265409 Dist:1 - Smart retry active (interval=100ms)
[SpeedManager] Attack on cooldown. remaining=380ms
[Combat] Attack blocked (speed check failed). Target:6265409 Dist:1 - Smart retry active (interval=100ms)
[SpeedManager] Attack on cooldown. remaining=274ms
[Combat] Attack blocked (speed check failed). Target:6265409 Dist:1 - Smart retry active (interval=82ms)
```

## 根本原因分析

### 問題 1：時間戳記錄時機錯誤

**當前邏輯**：
1. `PerformAttackOnce()` 調用 `EnhancedSpeedManager.CanPerformAction()` 檢查速度
2. 如果通過檢查，`CanPerformAction()` **立即更新時間戳**（第108行）
3. 然後 `PerformAttackOnce()` 繼續執行其他檢查（距離檢查等）
4. 如果距離太遠，返回 false，但時間戳已經被更新了

**問題**：
- 時間戳記錄的時間比實際發送封包的時間早
- 如果距離檢查失敗，時間戳已經被更新，但封包沒有發送
- 導致下次攻擊時，客戶端認為已經過了冷卻時間，但實際上還沒到

### 問題 2：雙重冷卻檢查機制衝突

**當前邏輯**：
1. `EnhancedSpeedManager.CanPerformAction()` 檢查冷卻時間
2. `GameWorld.Combat.cs` 中的 `_attackCooldownTimer` 也在檢查冷卻時間（第514-515行）

**問題**：
- 兩個機制可能不同步
- `EnhancedSpeedManager` 使用寬鬆窗口（`PREDICTION_LEAD_MS = 10ms`, `TOLERANCE_MS = 15ms`）
- `_attackCooldownTimer` 使用嚴格的冷卻時間
- 可能導致 `EnhancedSpeedManager` 允許攻擊，但 `_attackCooldownTimer` 還在冷卻中

### 問題 3：寬鬆窗口與服務器不匹配

**當前邏輯**：
- 客戶端使用 `PREDICTION_LEAD_MS = 10ms` 和 `TOLERANCE_MS = 15ms`
- 允許提前 10ms 執行，並給予 15ms 的寬鬆窗口
- 但服務器沒有這些寬鬆窗口，它嚴格按照間隔時間檢查

**問題**：
- 如果客戶端在間隔時間 - 10ms 時就記錄了時間戳，但服務器在收到封包時才記錄
- 就會導致客戶端認為已經過了冷卻時間，但服務器還沒到
- 服務器可能會拒絕封包，導致攻擊失敗

### 問題 4：時間戳更新時機不正確

**當前邏輯**：
- `CanPerformAction()` 在檢查通過時就更新時間戳（第108行）
- 但實際發送封包的時間可能更晚（因為還有其他檢查和處理）

**正確邏輯**：
- 時間戳應該在**實際發送封包時**更新，而不是在檢查通過時更新
- 這樣可以確保時間戳記錄的時間與服務器收到封包的時間一致

## 解決方案

### 方案 1：延遲時間戳更新（推薦）

**修改**：
1. `CanPerformAction()` 不再更新時間戳，只返回是否可以執行
2. 在 `PerformAttackOnce()` 中，**實際發送封包後**才更新時間戳
3. 如果距離檢查失敗或其他原因導致封包未發送，不更新時間戳

**優點**：
- 時間戳記錄的時間與實際發送封包的時間一致
- 與服務器邏輯對齊（服務器在收到封包時才記錄時間戳）

### 方案 2：移除寬鬆窗口

**修改**：
1. 移除 `PREDICTION_LEAD_MS` 和 `TOLERANCE_MS`
2. 使用嚴格的冷卻時間檢查，與服務器完全一致

**優點**：
- 與服務器邏輯完全一致
- 避免因寬鬆窗口導致的不同步

**缺點**：
- 可能導致操作感覺不夠流暢（因為沒有提前量）

### 方案 3：統一冷卻檢查機制

**修改**：
1. 移除 `_attackCooldownTimer` 機制
2. 只使用 `EnhancedSpeedManager` 進行冷卻檢查
3. 確保兩個機制不會衝突

**優點**：
- 簡化邏輯，避免雙重檢查
- 減少不同步的可能性

## 推薦修復方案

**組合方案 1 + 3**：
1. 延遲時間戳更新：在實際發送封包後才更新時間戳
2. 統一冷卻檢查機制：移除 `_attackCooldownTimer`，只使用 `EnhancedSpeedManager`
3. 保留寬鬆窗口：但調整為更小的值（如 `PREDICTION_LEAD_MS = 5ms`, `TOLERANCE_MS = 10ms`）

這樣可以：
- 確保時間戳記錄的時間與實際發送封包的時間一致
- 避免雙重檢查機制衝突
- 保持操作的流暢性，同時與服務器邏輯對齊
