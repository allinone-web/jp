# 增強版速度管理器設計文檔

## 概述

參考現代熱門網路遊戲和手機遊戲的最佳實踐，設計了一個更完善、玩家體驗更好的速度檢測和冷卻邏輯系統。

## 核心特性

### 1. 預測性冷卻（Predictive Cooldown）

**問題**：舊系統在最後一刻才檢查，導致玩家感覺"卡頓"

**解決方案**：
- 提前計算並返回剩餘冷卻時間
- 提供 `GetCooldownProgress()` 方法，返回 0.0-1.0 的進度值
- 可用於 UI 顯示冷卻進度條

**實現**：
```csharp
public static bool CanPerformAction(ActionType type, int gfxId, int actionId, out float remainingMs)
{
    // 計算剩餘時間並輸出
    remainingMs = Math.Max(0, effectiveCooldown - elapsed);
    // ...
}
```

### 2. 寬鬆時間窗口（Tolerance Window）

**問題**：嚴格的時間檢查導致因幀率波動（60fps = 16.67ms/幀）而頻繁失敗

**解決方案**：
- 允許 15ms 的寬鬆窗口，適應 60fps 的幀率波動
- 避免因微小的時間誤差導致操作失敗

**實現**：
```csharp
private const long TOLERANCE_MS = 15; // 15ms 寬鬆窗口
bool canPerform = elapsed >= (effectiveCooldown - TOLERANCE_MS);
```

### 3. 客戶端預測（Client Prediction）

**問題**：網絡延遲導致操作響應遲鈍

**解決方案**：
- 允許提前 10ms 執行，讓操作更流暢
- 服務器會做最終驗證，確保公平性

**實現**：
```csharp
private const long PREDICTION_LEAD_MS = 10; // 10ms 提前量
long effectiveCooldown = finalCooldown - PREDICTION_LEAD_MS;
```

### 4. 智能重試機制（Smart Retry）

**問題**：舊系統每幀都重試，導致性能問題和無限循環

**解決方案**：
- 根據剩餘冷卻時間動態調整重試間隔
- 剩餘時間短（<50ms）：每幀檢查（快速響應）
- 剩餘時間中等（50-200ms）：每 2-3 幀檢查
- 剩餘時間長（>200ms）：每 5-6 幀檢查（減少性能開銷）

**實現**：
```csharp
public static float GetSmartRetryInterval(float remainingMs)
{
    if (remainingMs < 50)
        return MIN_RETRY_INTERVAL; // 16ms（1幀）
    if (remainingMs < 200)
        return 0.033f; // 33ms（2幀）
    return Math.Min(MAX_RETRY_INTERVAL, remainingMs / 1000.0f * 0.3f);
}
```

### 5. 流暢的視覺反饋

**功能**：
- `GetCooldownProgress()`：返回 0.0-1.0 的進度值
- 可用於 UI 顯示冷卻進度條、技能圖標灰化等

## 與舊系統的對比

| 特性 | 舊系統 (SpeedManager) | 新系統 (EnhancedSpeedManager) |
|------|---------------------|------------------------------|
| 時間檢查 | 嚴格（0ms 誤差） | 寬鬆（15ms 誤差） |
| 客戶端預測 | 無 | 10ms 提前量 |
| 重試機制 | 每幀重試 | 智能動態調整 |
| 剩餘時間 | 不提供 | 提供（用於 UI） |
| 冷卻進度 | 不提供 | 提供（0.0-1.0） |
| 性能影響 | 高（每幀檢查） | 低（智能重試） |

## 使用方式

### 基本使用

```csharp
// 檢查是否可以攻擊，並獲取剩餘冷卻時間
if (EnhancedSpeedManager.CanPerformAction(ActionType.Attack, gfxId, actionId, out float remainingMs))
{
    // 可以攻擊
    PerformAttack();
}
else
{
    // 仍在冷卻中，remainingMs 包含剩餘時間
    float retryInterval = EnhancedSpeedManager.GetSmartRetryInterval(remainingMs);
    // 設置智能重試間隔
}
```

### 獲取冷卻進度（用於 UI）

```csharp
float progress = EnhancedSpeedManager.GetCooldownProgress(ActionType.Attack, gfxId, actionId);
// progress: 0.0 = 剛開始，1.0 = 完成
// 可用於顯示進度條：progressBar.Value = progress;
```

## 向後兼容

- 保留 `SpeedManager` 類，確保現有代碼繼續工作
- `EnhancedSpeedManager` 提供 `CanPerformAction(type, gfxId, actionId)` 重載，向後兼容
- Buff 狀態同步：`GameWorld.Buffs.cs` 同時設置兩個管理器的狀態

## 性能優化

1. **減少日誌輸出**：僅在剩餘時間 > 50ms 時記錄，避免日誌過多
2. **智能重試**：根據剩餘時間動態調整，避免每幀都檢查
3. **提前計算**：一次性計算所有需要的值，避免重複計算

## 參考遊戲

本設計參考了以下現代遊戲的最佳實踐：

1. **《原神》**：預測性冷卻 + 寬鬆時間窗口
2. **《王者榮耀》**：客戶端預測 + 服務器驗證
3. **《英雄聯盟》**：智能重試機制
4. **《和平精英》**：流暢的視覺反饋

## 未來擴展

1. **網絡延遲補償**：根據 ping 值動態調整提前量
2. **自適應寬鬆窗口**：根據幀率動態調整寬鬆窗口大小
3. **冷卻預測算法**：使用機器學習預測最佳攻擊時機
