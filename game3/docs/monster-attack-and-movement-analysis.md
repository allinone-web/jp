# 怪物攻擊與移動包分析

## 1. 怪物攻擊玩家時，服務器發送什麼包？

### 1.1 怪物攻擊流程

**服務器代碼** (`server/world/instance/MonsterInstance.java:287`):
```java
Attack(cha, cha.getX(), cha.getY(), getGfxMode() + 1, 0);
```

**關鍵點**：
- 怪物攻擊時，使用**目標的實際座標** `cha.getX(), cha.getY()`（服務器記錄的玩家座標）
- 調用 `Attack` 方法，這個方法在 `Character` 基類中定義

### 1.2 服務器發送的包

**怪物攻擊時，服務器發送 `S_ObjectAttack` 包（Opcode 35）**

**包結構** (`server/network/server/S_ObjectAttack.java`):
```
Opcode: 35
Data:
  - WriteC(action)        // 攻擊動作ID
  - WriteD(attackerId)     // 攻擊者ID（怪物ID）
  - WriteD(targetId)       // 目標ID（玩家ID）
  - WriteC(damage)         // 傷害值
  - WriteC(heading)        // 攻擊者朝向
  - WriteD(0) 或 WriteD(effectId)  // 武器效果ID（近戰）
  - WriteC(0)
```

**關鍵點**：
- **攻擊者座標**：服務器使用怪物當前的實際座標
- **目標座標**：服務器使用玩家當前的實際座標（`cha.getX(), cha.getY()`）
- **傷害值**：服務器計算的實際傷害（0表示miss）

### 1.3 客戶端如何處理怪物攻擊

**客戶端接收** (`Client/Game/GameWorld.Combat.cs:920-960`):
```csharp
private void OnObjectAttacked(int attackerId, int targetId, int actionId, int damage, ...)
{
    // 處理攻擊包
    // 如果目標是玩家自己，更新血量等
    // 如果攻擊者是怪物，播放攻擊動畫
}
```

## 2. 服務器發送怪物移動包的頻率

### 2.1 怪物AI更新頻率

**MonAi 線程** (`server/world/ai/MonAi.java:28`):
```java
private int SleepTime = 30;  // 30毫秒
```

**更新邏輯**:
1. **MonAi 線程每 30 毫秒運行一次**
2. 每次運行時，檢查每個怪物的 `isAi(time)` 方法
3. 如果 `isAi` 返回 `true`，執行AI邏輯（攻擊、移動等）

### 2.2 怪物移動時間間隔

**移動時間計算** (`server/world/instance/MonsterInstance.java:292`):
```java
this.ai_time = getMon().getModespeed(getGfxMode());
```

**關鍵點**：
- `getModespeed(getGfxMode())` 返回該動作的持續時間（毫秒）
- 不同動作有不同的速度：
  - **移動動作** (`getGfxMode()`): 通常 400-800 毫秒
  - **攻擊動作** (`getGfxMode() + 1`): 通常 600-1200 毫秒

### 2.3 怪物移動包的發送

**移動觸發** (`server/world/instance/MonsterInstance.java:291`):
```java
StartMove(cha.getX(), cha.getY());  // 開始移動
```

**移動包發送** (`server/world/object/L1Object.java:819`):
```java
// 在 updateObject() 中，當實體移動時
if ((o instanceof PcInstance))
    o.SendPacket(new S_ObjectMoving(this));  // 發送移動包給玩家
```

**關鍵點**：
- 怪物移動時，服務器會發送 `S_ObjectMoving` 包（Opcode 18）給**視野範圍內的玩家**
- **發送頻率**：取決於怪物的移動速度（通常每 400-800 毫秒一次）
- **視野範圍**：14格（`getDistance(o.getX(), o.getY(), o.getMap(), 14)`）

### 2.4 移動包結構

**S_ObjectMoving 包** (`server/network/server/S_ObjectMoving.java`):
```
Opcode: 18
Data:
  - WriteD(objectId)      // 實體ID
  - WriteH(x)            // 目標X座標（移動後的座標）
  - WriteH(y)            // 目標Y座標（移動後的座標）
  - WriteC(heading)      // 朝向
```

**注意**：
- 包中的 `x, y` 是**移動後的座標**（根據當前座標和朝向計算）
- 客戶端收到後，應該更新實體的座標

## 3. 問題分析：客戶端使用過時座標攻擊

### 3.1 問題場景

**場景描述**：
1. 玩家在客戶端看到怪物在位置 A
2. 玩家發起攻擊，使用位置 A 的座標
3. 但實際上，怪物已經移動到位置 B（服務器已更新，但客戶端還沒收到移動包）
4. 服務器收到攻擊包，檢查位置 A 的座標，發現怪物不在那裡，返回 `damage=0`（miss）

### 3.2 問題根源

**時間線**：
```
T0: 怪物在位置 A
T1: 怪物開始移動（服務器更新座標到位置 B）
T2: 玩家發起攻擊（使用位置 A 的座標）
T3: 服務器收到攻擊包，檢查位置 A，發現怪物不在，返回 miss
T4: 客戶端收到移動包，更新怪物座標到位置 B（太晚了！）
```

**問題**：
- **客戶端使用過時的目標座標**發送攻擊包
- **服務器移動包發送頻率**（400-800毫秒）可能**慢於**玩家攻擊頻率（600毫秒）
- 如果怪物在玩家發起攻擊前移動了，客戶端可能還在使用舊座標

### 3.3 解決方案

#### 方案1：發送攻擊包前重新獲取目標座標（推薦）

```csharp
private void ExecuteAttackAction(CombatProfile profile, int dist)
{
    var target = _currentTask.Target;
    int targetId = target.ObjectId;
    
    // 【修復】重新從 _entities 獲取目標實體，確保使用最新的座標
    if (!_entities.TryGetValue(targetId, out var freshTarget))
    {
        // 目標不在 _entities 中，說明已經被刪除或超出視野範圍
        GD.Print($"[Combat-Error] Target {targetId} not in _entities, canceling attack");
        FinishCurrentTask();
        return;
    }
    
    // 使用最新的目標座標（從 _entities 中獲取）
    int tx = freshTarget.MapX;  // 最新的座標！
    int ty = freshTarget.MapY;
    
    PerformAttackOnce(targetId, tx, ty);
}
```

**好處**：
- 確保使用最新的目標座標
- 如果目標不在 `_entities` 中，不發送攻擊包

#### 方案2：增加攻擊前的距離驗證

```csharp
// 發送攻擊包前，再次檢查目標是否在攻擊範圍內
int actualDist = GetGridDistance(playerX, playerY, freshTarget.MapX, freshTarget.MapY);
if (actualDist > GetAttackRange())
{
    // 目標不在攻擊範圍內，不發送攻擊包
    GD.Print($"[Combat-Error] Target {targetId} out of range: {actualDist} > {GetAttackRange()}");
    return;
}
```

## 4. 服務器移動包發送頻率總結

### 4.1 怪物AI更新頻率

| 項目 | 頻率 | 說明 |
|------|------|------|
| MonAi 線程 | 30毫秒 | AI線程每30毫秒運行一次 |
| 怪物移動間隔 | 400-800毫秒 | 根據 `getModespeed(getGfxMode())` 計算 |
| 怪物攻擊間隔 | 600-1200毫秒 | 根據 `getModespeed(getGfxMode() + 1)` 計算 |

### 4.2 移動包發送條件

**發送條件**：
1. 怪物移動時，調用 `StartMove`
2. 在 `updateObject()` 中，檢查實體是否在玩家視野範圍內（14格）
3. 如果在視野範圍內，發送 `S_ObjectMoving` 包

**發送頻率**：
- **理論頻率**：每 400-800 毫秒一次（根據怪物移動速度）
- **實際頻率**：可能更慢，因為：
  - 怪物可能不移動（攻擊、待機等）
  - 視野範圍限制（14格）
  - 網絡延遲

### 4.3 問題分析

**為什麼會出現座標不同步？**

1. **移動包發送頻率慢**：
   - 怪物移動間隔：400-800毫秒
   - 玩家攻擊間隔：600毫秒
   - 如果怪物在玩家攻擊前移動，客戶端可能還沒收到移動包

2. **網絡延遲**：
   - 移動包從服務器發送到客戶端需要時間
   - 如果網絡延遲高，客戶端可能使用過時的座標

3. **視野範圍限制**：
   - 如果怪物在14格外，不會發送移動包
   - 客戶端可能還保留著舊的座標

## 5. 建議的修復方案

### 5.1 立即修復：重新獲取目標座標

```csharp
// 在發送攻擊包前，重新從 _entities 獲取目標實體
if (!_entities.TryGetValue(targetId, out var freshTarget))
{
    // 目標不存在，取消攻擊
    return;
}

// 使用最新的座標
int tx = freshTarget.MapX;
int ty = freshTarget.MapY;
```

### 5.2 長期優化：增加攻擊前驗證

```csharp
// 發送攻擊包前，驗證目標是否在攻擊範圍內
int actualDist = GetGridDistance(playerX, playerY, freshTarget.MapX, freshTarget.MapY);
if (actualDist > GetAttackRange())
{
    // 目標不在攻擊範圍內，不發送攻擊包
    // 或者，自動移動到攻擊範圍內
    return;
}
```

### 5.3 診斷日誌

```csharp
GD.Print($"[Combat-Diag] Attack targetId={targetId} " +
         $"targetGrid=({tx},{ty}) " +
         $"playerGrid=({playerX},{playerY}) " +
         $"distance={actualDist} " +
         $"range={GetAttackRange()}");
```

## 6. 總結

### 6.1 怪物攻擊包

- **包類型**：`S_ObjectAttack` (Opcode 35)
- **發送時機**：怪物攻擊玩家時
- **包含信息**：攻擊者ID、目標ID、傷害值、座標等

### 6.2 怪物移動包頻率

- **AI更新頻率**：30毫秒
- **移動間隔**：400-800毫秒（根據怪物速度）
- **移動包發送**：每 400-800 毫秒一次（如果怪物在視野範圍內）

### 6.3 問題根源

- **客戶端使用過時的目標座標**發送攻擊包
- **移動包發送頻率可能慢於攻擊頻率**
- **網絡延遲導致座標不同步**

### 6.4 解決方案

- **發送攻擊包前，重新從 `_entities` 獲取目標實體**
- **確保使用最新的目標座標**
- **增加攻擊前的距離驗證**
