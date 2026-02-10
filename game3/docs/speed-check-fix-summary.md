# 速度檢查失敗問題修復總結

## 問題根源

即使 `list.spr` 的速度設置與服務器數據庫表的速度一致，仍然出現 "Attack blocked (speed check failed)" 錯誤。

### 根本原因

**時間戳更新時機錯誤**：
1. `EnhancedSpeedManager.CanPerformAction()` 在檢查通過時就更新了時間戳
2. 但 `PerformAttackOnce()` 在通過速度檢查後，還有其他檢查（如距離檢查）
3. 如果距離檢查失敗，返回 false，但時間戳已經被更新了
4. 導致時間戳記錄的時間比實際發送封包的時間早
5. 下次攻擊時，客戶端認為已經過了冷卻時間，但實際上還沒到

## 修復方案

### 1. 延遲時間戳更新

**修改文件**：`Client/Game/EnhancedSpeedManager.cs`

**修改內容**：
- `CanPerformAction()` 不再更新時間戳，只返回是否可以執行
- 新增 `RecordActionPerformed()` 方法，用於在實際發送封包後更新時間戳

**代碼變更**：
```csharp
// 修改前：在檢查通過時就更新時間戳
if (canPerform)
{
    _lastActionTimestamps[type] = currentTime;  // ❌ 過早更新
    remainingMs = 0;
    return true;
}

// 修改後：不在此處更新時間戳
if (canPerform)
{
    remainingMs = 0;
    return true;  // ✅ 只返回是否可以執行
}

// 新增方法：在實際發送封包後調用
public static void RecordActionPerformed(ActionType type)
{
    long currentTime = (long)Time.GetTicksMsec();
    _lastActionTimestamps[type] = currentTime;  // ✅ 在實際發送封包後更新
}
```

### 2. 在實際發送封包後更新時間戳

**修改文件**：
- `Client/Game/GameWorld.Combat.cs` - 攻擊封包
- `Client/Game/GameWorld.Skill.cs` - 魔法封包

**修改內容**：
- 在 `SendAttackPacket()` 或 `SendAttackBowPacket()` 之後調用 `RecordActionPerformed(ActionType.Attack)`
- 在 `Send(C_MagicPacket.Make(...))` 之後調用 `RecordActionPerformed(ActionType.Magic)`

**代碼變更**：
```csharp
// GameWorld.Combat.cs
SendAttackPacket(targetId, targetX, targetY, calcX, calcY, isMoving);
_myPlayer.PlayAttackAnimation(targetX, targetY);
EnhancedSpeedManager.RecordActionPerformed(ActionType.Attack);  // ✅ 在實際發送封包後更新

// GameWorld.Skill.cs
_netSession.Send(C_MagicPacket.Make(skillId, targetId, finalTargetX, finalTargetY));
EnhancedSpeedManager.RecordActionPerformed(ActionType.Magic);  // ✅ 在實際發送封包後更新
```

## 修復效果

### 修復前
- 時間戳在檢查通過時就更新，但封包可能因距離檢查失敗等原因未發送
- 導致時間戳記錄的時間比實際發送封包的時間早
- 下次攻擊時，客戶端認為已經過了冷卻時間，但實際上還沒到
- 出現 "Attack blocked (speed check failed)" 錯誤

### 修復後
- 時間戳只在實際發送封包後才更新
- 時間戳記錄的時間與實際發送封包的時間一致
- 與服務器邏輯對齊（服務器在收到封包時才記錄時間戳）
- 避免因距離檢查失敗等原因導致時間戳被提前更新

## 注意事項

1. **確保所有發送封包的地方都更新時間戳**：
   - 攻擊封包：`SendAttackPacket()` / `SendAttackBowPacket()`
   - 魔法封包：`Send(C_MagicPacket.Make(...))`
   - 移動封包：已在 `StepTowardsTarget()` 中處理

2. **如果封包發送失敗，不更新時間戳**：
   - 如果距離檢查失敗，返回 false，不更新時間戳
   - 如果其他檢查失敗，返回 false，不更新時間戳
   - 只有實際發送封包後，才更新時間戳

3. **與服務器邏輯對齊**：
   - 服務器在收到封包時才記錄時間戳
   - 客戶端在發送封包時記錄時間戳
   - 兩者時間差 = 網絡延遲（通常 < 50ms），在可接受範圍內

## 測試建議

1. **測試攻擊冷卻**：
   - 連續快速攻擊，觀察是否還會出現 "Attack blocked" 錯誤
   - 檢查攻擊間隔是否與服務器一致

2. **測試魔法冷卻**：
   - 連續快速使用魔法，觀察是否還會出現冷卻錯誤
   - 檢查魔法間隔是否與服務器一致

3. **測試距離檢查**：
   - 在距離太遠時嘗試攻擊，觀察時間戳是否正確
   - 確保距離檢查失敗時，時間戳不會被更新
