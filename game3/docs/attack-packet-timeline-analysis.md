# 攻擊數據包時間線分析

## 場景描述

1. **玩家A** 在座標 **123** 攻擊 **怪物B**
2. **怪物C** 也攻擊 **玩家A**，但到座標 **456** 發起播放攻擊動作

## 時間線分析

### T0: 玩家A在座標123（服務器和客戶端一致）

```
服務器狀態：
- 玩家A: getX() = 123, getY() = 123
- 怪物B: getX() = 456, getY() = 456
- 怪物C: getX() = 789, getY() = 789

客戶端狀態：
- 玩家A: MapX = 123, MapY = 123
- 怪物B: MapX = 456, MapY = 456
- 怪物C: MapX = 789, MapY = 789
```

### T1: 玩家A開始移動，攻擊怪物B

#### 1.1 客戶端向服務器發送攻擊包

**客戶端發送** (`Client/Game/GameWorld.Combat.cs:705-714`):
```
Opcode: 23 (C_Attack)
Data:
  - WriteD(targetId)      // 怪物B的ID
  - WriteH(targetX)       // 456 (客戶端發送的目標座標)
  - WriteH(targetY)       // 456 (客戶端發送的目標座標)
```

**關鍵點**：
- 客戶端發送的是**怪物B的座標**（456），不是玩家A的座標
- 客戶端在 `PerformAttackOnce` 中從 `_entities` 獲取目標座標

#### 1.2 服務器接收並驗證攻擊

**服務器接收** (`server/network/client/C_Attack.java:30-34`):
```java
this.objid = readD();      // 怪物B的ID
this.locx = readH();       // 456 (客戶端發送的目標座標)
this.locy = readH();       // 456 (客戶端發送的目標座標)

pc.Attack(pc.getObject(this.objid), this.locx, this.locy, 1, 0);
```

**服務器驗證** (`server/world/instance/PcInstance.java:619`):
```java
if ((target != null) && (getDistance(x, y, target.getMap(), this.Areaatk)) && ...)
{
  // x, y = 456, 456 (客戶端發送的目標座標)
  // target.getX(), target.getY() = 456, 456 (服務器記錄的怪物B座標)
  // 驗證通過
}
```

**服務器狀態更新**：
- 玩家A: getX() = 123, getY() = 123（**未改變**，因為玩家A沒有移動）
- 怪物B: getX() = 456, getY() = 456（未改變）

#### 1.3 服務器發送攻擊響應包

**服務器發送** (`server/world/instance/PcInstance.java:650`):
```
Opcode: 35 (S_ObjectAttack)
Data:
  - WriteC(action)        // 動作ID (攻擊動作)
  - WriteD(attackerId)     // 玩家A的ID
  - WriteD(targetId)       // 怪物B的ID
  - WriteC(dmg)           // 傷害值
  - WriteC(heading)       // 玩家A的朝向
  - WriteD(0) 或 WriteD(effectId)  // 武器效果ID
  - WriteC(0)
```

**關鍵點**：
- **近戰攻擊不包含座標**（只有弓箭攻擊才包含座標）
- 客戶端收到包後，從 `_entities` 獲取攻擊者和目標的座標

### T2: 怪物C決定攻擊玩家A

#### 2.1 服務器使用什麼座標？

**服務器代碼** (`server/world/instance/MonsterInstance.java:287`):
```java
Attack(cha, cha.getX(), cha.getY(), getGfxMode() + 1, 0);
```

**關鍵分析**：
- `cha.getX(), cha.getY()` 是**玩家A的服務器記錄的座標**
- 在T1時，玩家A**沒有移動**，只是發送了攻擊包
- 所以 `cha.getX(), cha.getY()` = **123**（不是456！）

**結論**：
- **服務器使用座標123**（玩家A的服務器記錄的座標）
- **不是456**（456是怪物B的座標，不是玩家A的座標）

#### 2.2 服務器驗證攻擊距離

**服務器代碼** (`server/world/instance/MonsterInstance.java:283`):
```java
if ((getDistance(cha.getX(), cha.getY(), cha.getMap(), this.Areaatk)) && (LongAttackCK(cha, this.Areaatk)))
{
  // cha.getX(), cha.getY() = 123, 123 (玩家A的服務器記錄的座標)
  // this.getX(), this.getY() = 789, 789 (怪物C的座標)
  // 驗證距離是否在攻擊範圍內
}
```

**關鍵點**：
- 服務器使用玩家A的**當前服務器記錄的座標**（123）進行距離驗證
- 如果玩家A在T1時移動了，服務器會先處理移動包，更新玩家A的座標

### T3: 服務器發送攻擊包 Op35

#### 3.1 單個怪物攻擊玩家

**服務器發送** (`server/world/instance/MonsterInstance.java:287` → `server/world/object/Character.java:875` → `server/network/server/S_ObjectAttack.java:9-26`):
```
Opcode: 35 (S_ObjectAttack)
Data:
  - WriteC(action)        // 動作ID (攻擊動作)
  - WriteD(attackerId)     // 怪物C的ID
  - WriteD(targetId)       // 玩家A的ID
  - WriteC(dmg)           // 傷害值 (0表示miss)
  - WriteC(heading)       // 怪物C的朝向（服務器計算的，面向玩家A在座標123）
  - WriteD(0) 或 WriteD(effectId)  // 武器效果ID
  - WriteC(0)
```

**關鍵點**：
- **不包含目標座標**（只有ID）
- 客戶端收到包後，需要從 `_entities` 獲取目標座標
- 如果 `_entities` 中的玩家A座標是456（客戶端預測），怪物C會朝向456（錯誤！）

#### 3.2 4個怪物同時攻擊玩家

**場景**：4個怪物（C, D, E, F）同時攻擊玩家A

**服務器發送**（每個怪物一個包）：
```
包1: Opcode 35
  - attackerId = 怪物C的ID
  - targetId = 玩家A的ID
  - heading = 怪物C面向玩家A在座標123的朝向
  - dmg = 傷害值

包2: Opcode 35
  - attackerId = 怪物D的ID
  - targetId = 玩家A的ID
  - heading = 怪物D面向玩家A在座標123的朝向
  - dmg = 傷害值

包3: Opcode 35
  - attackerId = 怪物E的ID
  - targetId = 玩家A的ID
  - heading = 怪物E面向玩家A在座標123的朝向
  - dmg = 傷害值

包4: Opcode 35
  - attackerId = 怪物F的ID
  - targetId = 玩家A的ID
  - heading = 怪物F面向玩家A在座標123的朝向
  - dmg = 傷害值
```

**關鍵點**：
- 每個怪物發送一個獨立的Op35包
- 所有包中的 `targetId` 都是玩家A的ID
- 所有包中的 `heading` 都是服務器計算的，面向玩家A在座標123的朝向
- **所有包都不包含目標座標**

**客戶端處理**：
- 客戶端收到每個包後，調用 `OnObjectAttacked(attackerId, targetId, actionId, damage)`
- 從 `_entities` 獲取目標座標（可能是456，客戶端預測）
- 所有怪物都會朝向座標456（錯誤！應該是123）

### T4: 客戶端收到攻擊包

**客戶端代碼** (`Client/Game/GameWorld.Combat.cs:926-962`):
```csharp
private void OnObjectAttacked(int attackerId, int targetId, int actionId, int damage)
{
  if (_entities.TryGetValue(attackerId, out var attacker))
  {
    attacker.SetAction(actionId);  // 播放攻擊動畫，但不調整朝向
    if (targetId > 0 && !isOp35Magic)
    {
      attacker.PrepareAttack(targetId, damage);
    }
  }
}
```

**問題**：
- `SetAction(actionId)` 只播放攻擊動畫，**不調整朝向**
- 怪物C的攻擊動畫使用**當前朝向**，而不是面向玩家A

### T5: 怪物C播放攻擊動畫

**當前邏輯**：
- 怪物C播放攻擊動畫，使用**當前朝向**（可能是錯誤的）
- 如果客戶端要調整朝向，需要從 `_entities` 獲取目標座標
- 如果 `_entities` 中的玩家A座標是456（客戶端預測），怪物C會朝向456（錯誤！）

### T6: 服務器發送移動確認包

**服務器發送** (`server/network/server/S_ObjectMoving.java`):
```
Opcode: 18 (S_ObjectMoving)
Data:
  - WriteD(objectId)      // 玩家A的ID
  - WriteH(x)             // 123 (服務器確認的座標)
  - WriteH(y)             // 123 (服務器確認的座標)
  - WriteC(heading)       // 玩家A的朝向
```

**客戶端處理** (`Client/Game/GameWorld.Combat.cs:868-918`):
```csharp
private void OnObjectMoved(int objectId, int x, int y, int heading)
{
  if (objectId == _myPlayer?.ObjectId)
  {
    _serverConfirmedPlayerX = x;  // 123
    _serverConfirmedPlayerY = y;  // 123
    // 如果服務器座標與客戶端預測不一致，強制同步
    if (_myPlayer.MapX != x || _myPlayer.MapY != y)
    {
      _myPlayer.SetMapPosition(x, y, heading);
    }
  }
}
```

## 故障根源

### 問題1：服務器使用座標123，客戶端使用座標456

**時間線**：
```
T1: 玩家A攻擊怪物B（座標456），但玩家A自己還在座標123
T2: 怪物C攻擊玩家A，服務器使用座標123
T3: 服務器發送Op35包（不包含座標）
T4: 客戶端收到包，從 _entities 獲取玩家A座標（可能是456，客戶端預測）
T5: 怪物C朝向座標456（錯誤！應該是123）
T6: 服務器發送移動確認包，玩家A實際在座標123
```

**根本原因**：
- 服務器使用玩家A的**服務器記錄的座標**（123）
- 客戶端使用玩家A的**客戶端預測的座標**（456）
- 兩者不一致，導致怪物攻擊朝向錯誤

### 問題2：Op35包不包含目標座標

**當前協議**：
- Op35包只包含攻擊者ID和目標ID
- 不包含目標座標
- 客戶端需要從 `_entities` 獲取目標座標

**問題**：
- `_entities` 中的座標可能是**客戶端預測的座標**，不是服務器確認的座標
- 導致怪物攻擊朝向錯誤

## 解決方案

### 方案1：在 `OnObjectAttacked` 中調整怪物攻擊朝向

**修改位置**：`Client/Game/GameWorld.Combat.cs:926-962`

```csharp
private void OnObjectAttacked(int attackerId, int targetId, int actionId, int damage)
{
  // ... 現有邏輯 ...
  
  if (_entities.TryGetValue(attackerId, out var attacker))
  {
    attacker.SetAction(actionId);
    
    // 【修復】如果是怪物攻擊玩家，調整朝向面向目標
    if (targetId > 0 && targetId == _myPlayer?.ObjectId)
    {
      // 目標是玩家自己，使用服務器確認的座標
      int targetX = _serverConfirmedPlayerX >= 0 ? _serverConfirmedPlayerX : _myPlayer.MapX;
      int targetY = _serverConfirmedPlayerY >= 0 ? _serverConfirmedPlayerY : _myPlayer.MapY;
      
      // 調整攻擊者朝向
      int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
      attacker.SetHeading(newHeading);
      
      GD.Print($"[Combat-Fix] Monster {attackerId} attacking player at server-confirmed ({targetX},{targetY}), adjusted heading to {newHeading}");
    }
    else if (targetId > 0 && _entities.TryGetValue(targetId, out var target))
    {
      // 目標是其他實體，使用當前座標
      int targetX = target.MapX;
      int targetY = target.MapY;
      
      int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
      attacker.SetHeading(newHeading);
      
      GD.Print($"[Combat-Fix] Monster {attackerId} attacking target {targetId} at ({targetX},{targetY}), adjusted heading to {newHeading}");
    }
    
    if (targetId > 0 && !isOp35Magic)
    {
      attacker.PrepareAttack(targetId, damage);
    }
  }
}
```

### 方案2：如果角色不在正確位置，直接順移

**修改位置**：`Client/Game/GameEntity.Movement.cs:62-120`

```csharp
public void SetMapPosition(int x, int y, int h = 0)
{
  if (x < 0 || y < 0) return;

  int oldMapX = MapX;
  int oldMapY = MapY;

  if (_moveTween != null && _moveTween.IsValid())
  {
    _moveTween.Kill();
    _moveTween = null;
  }

  int dx = x - MapX;
  int dy = y - MapY;
  MapX = x;
  MapY = y;
  SetHeading(h);

  Vector2I origin = GameWorld.CurrentMapOrigin;
  float localX = (x - origin.X + 0.5f) * GRID_SIZE;
  float localY = (y - origin.Y + 0.5f) * GRID_SIZE;
  Vector2 targetPos = new Vector2(localX, localY);
  float dist = Position.DistanceTo(targetPos);
  
  // 【修復】如果距離過大（>256像素 = 8格），直接順移，不拉過去
  bool isSmoothMove = dist > 0 && dist < TELEPORT_THRESHOLD;
  
  // 【新增】如果距離過大，直接順移
  if (dist >= TELEPORT_THRESHOLD)
  {
    Position = targetPos;  // 直接順移
    GD.Print($"[Pos-Teleport] ObjId={ObjectId} teleported from grid ({oldMapX},{oldMapY}) to ({x},{y}) dist={dist:F0}px (threshold={TELEPORT_THRESHOLD})");
    return;  // 不播放動畫
  }

  // ... 現有邏輯 ...
}
```

## 日誌分析

### 需要添加的日誌

1. **服務器端**（如果可能）：
   - 怪物攻擊時，記錄目標座標
   - 發送Op35包時，記錄包內容

2. **客戶端**：
   - 收到Op35包時，記錄攻擊者和目標ID
   - 調整朝向時，記錄使用的目標座標
   - 順移時，記錄距離和原因

### 日誌格式

```
[Combat-Packet] Received Op35 attacker={attackerId} target={targetId} action={actionId} damage={damage}
[Combat-Fix] Monster {attackerId} attacking target {targetId} at server-confirmed ({targetX},{targetY}), adjusted heading to {newHeading}
[Pos-Teleport] ObjId={ObjectId} teleported from grid ({oldMapX},{oldMapY}) to ({x},{y}) dist={dist:F0}px
```
