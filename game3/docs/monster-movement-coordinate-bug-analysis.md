# 怪物移動座標錯誤故障分析

## 故障場景描述

1. **玩家A** 在座標 **a**
2. **怪物B** 正在跟玩家A作戰，在座標 **a**
3. **怪物C** 距離玩家A有5格距離，應該走向座標 **a**，但卻走向座標 **999**（錯誤！）

**問題**：為什麼怪物C沒有走到玩家A的座標a，而走到座標999攻擊？

## 關鍵問題分析

### 問題1：T2時服務器使用什麼座標？

**用戶說**："正確的話，服務器應該是用座標456"

**實際情況**：
- 在T1時，玩家A攻擊怪物B（座標456），但**玩家A自己還在座標123**
- 在T2時，怪物C決定攻擊玩家A，服務器調用：
  ```java
  StartMove(cha.getX(), cha.getY());  // cha是玩家A
  ```
- `cha.getX(), cha.getY()` 是**玩家A的服務器記錄的座標**（123），不是456

**結論**：
- **服務器使用座標123**（玩家A的服務器記錄的座標）
- **不是456**（456是怪物B的座標，不是玩家A的座標）

### 問題2：S_ObjectMoving包的內容

**服務器代碼** (`server/network/server/S_ObjectMoving.java:7-48`):
```java
public S_ObjectMoving(L1Object o)
{
  int x = o.getX();  // 當前座標X
  int y = o.getY();  // 當前座標Y
  switch (o.getHeading()) {
  case 0: y++; break;  // 向北移動，Y+1
  case 1: x--; y++; break;  // 向東北移動
  case 2: x--; break;  // 向東移動，X-1
  case 3: x--; y--; break;  // 向東南移動
  case 4: y--; break;  // 向南移動，Y-1
  case 5: x++; y--; break;  // 向西南移動
  case 6: x++; break;  // 向西移動，X+1
  case 7: x++; y++; break;  // 向西北移動
  }
  writeC(18);
  writeD(o.getObjectId());
  writeH(x);  // 【關鍵】這是下一步的座標，不是當前座標！
  writeH(y);  // 【關鍵】這是下一步的座標，不是當前座標！
  writeC(o.getHeading());
}
```

**關鍵點**：
- `S_ObjectMoving` 包中的 `x, y` 是**下一步的座標**，不是當前座標
- 服務器根據當前座標和朝向，計算下一步座標
- 客戶端收到包後，應該更新實體到這個新座標

### 問題3：怪物C走向座標999的原因

**可能的原因**：

#### 原因1：服務器在決定移動時使用了錯誤的目標座標

**服務器代碼** (`server/world/instance/MonsterInstance.java:291`):
```java
StartMove(cha.getX(), cha.getY());  // cha是玩家A
```

**分析**：
- 如果玩家A在座標a，`cha.getX(), cha.getY()` 應該是座標a
- 但如果服務器記錄的玩家A座標是999（錯誤），怪物C就會走向999

**可能的情況**：
- 玩家A移動了，但服務器還沒更新座標
- 或者服務器座標被錯誤設置為999

#### 原因2：客戶端收到移動包時解析錯誤

**客戶端代碼** (`Client/Game/GameWorld.Combat.cs:868-918`):
```csharp
private void OnObjectMoved(int objectId, int x, int y, int heading)
{
  // x, y 是服務器發送的下一步座標
  if (_entities.ContainsKey(objectId))
  {
    _entities[objectId].SetMapPosition(x, y, heading);
  }
}
```

**分析**：
- 客戶端收到移動包後，直接使用包中的 `x, y` 更新實體座標
- 如果服務器發送的座標是999，客戶端就會更新到999

#### 原因3：客戶端在處理移動包時使用了錯誤的座標

**可能的情況**：
- 客戶端收到移動包時，從 `_entities` 獲取了錯誤的目標座標
- 或者客戶端在計算移動路徑時使用了錯誤的目標座標

## 時間線分析

### T0: 玩家A在座標a（服務器和客戶端一致）

```
服務器狀態：
- 玩家A: getX() = a, getY() = a
- 怪物B: getX() = a, getY() = a（正在跟玩家A作戰）
- 怪物C: getX() = c, getY() = c（距離玩家A 5格）

客戶端狀態：
- 玩家A: MapX = a, MapY = a
- 怪物B: MapX = a, MapY = a
- 怪物C: MapX = c, MapY = c
```

### T1: 玩家A攻擊怪物B

**服務器處理**：
- 玩家A發送攻擊包（Opcode 23），目標座標是怪物B的座標（a）
- 服務器驗證通過，發送攻擊響應包（Opcode 35）
- **玩家A的座標沒有改變**，仍然是a

### T2: 怪物C決定攻擊玩家A

**服務器處理** (`server/world/instance/MonsterInstance.java:291`):
```java
StartMove(cha.getX(), cha.getY());  // cha是玩家A
// cha.getX() = a, cha.getY() = a
```

**關鍵問題**：
- 如果 `cha.getX(), cha.getY()` 返回的是座標999（錯誤），怪物C就會走向999
- 這可能是因為：
  1. 服務器記錄的玩家A座標是999（錯誤）
  2. 或者 `cha` 對象指向了錯誤的目標

### T3: 服務器發送移動包

**服務器發送** (`server/network/server/S_ObjectMoving.java`):
```
Opcode: 18 (S_ObjectMoving)
Data:
  - WriteD(objectId)      // 怪物C的ID
  - WriteH(x)             // 下一步座標X（根據當前座標和朝向計算）
  - WriteH(y)             // 下一步座標Y（根據當前座標和朝向計算）
  - WriteC(heading)       // 怪物C的朝向
```

**關鍵點**：
- 如果怪物C的當前座標是c，朝向是面向座標999的方向
- 服務器會計算下一步座標，可能是朝向999的方向移動一步
- 客戶端收到包後，會更新怪物C到這個新座標

### T4: 客戶端收到移動包

**客戶端處理** (`Client/Game/GameWorld.Combat.cs:868-918`):
```csharp
private void OnObjectMoved(int objectId, int x, int y, int heading)
{
  if (_entities.ContainsKey(objectId))
  {
    _entities[objectId].SetMapPosition(x, y, heading);
  }
}
```

**關鍵點**：
- 客戶端收到移動包後，直接使用包中的 `x, y` 更新實體座標
- 如果服務器發送的座標是朝向999的方向，客戶端就會更新到這個座標

## 故障根源分析

### 根本原因：服務器在決定移動時使用了錯誤的目標座標

**可能的情況**：

1. **服務器記錄的玩家A座標是999（錯誤）**
   - 玩家A移動了，但服務器還沒更新座標
   - 或者服務器座標被錯誤設置為999

2. **`cha` 對象指向了錯誤的目標**
   - `cha` 應該是玩家A，但實際上指向了其他對象（座標999）

3. **客戶端預測的玩家A座標是999（錯誤）**
   - 客戶端預測玩家A移動到座標999
   - 但服務器記錄的玩家A座標是a
   - 如果服務器使用了客戶端預測的座標（不應該），就會導致錯誤

### 關鍵發現：S_ObjectMoving包中的座標是下一步座標

**重要**：
- `S_ObjectMoving` 包中的 `x, y` 是**下一步的座標**，不是當前座標
- 服務器根據當前座標和朝向，計算下一步座標
- 如果怪物C的朝向是面向座標999的方向，服務器會計算朝向999的方向移動一步
- 客戶端收到包後，會更新怪物C到這個新座標

**這意味著**：
- 如果怪物C的朝向是錯誤的（面向座標999），服務器會計算錯誤的下一步座標
- 客戶端收到包後，會更新怪物C到錯誤的座標

## 解決方案

### 方案1：添加日誌追蹤服務器座標

**服務器端**（如果可能）：
- 在 `StartMove` 時，記錄目標座標
- 在發送 `S_ObjectMoving` 時，記錄包內容

**客戶端**：
- 在收到 `S_ObjectMoving` 時，記錄包內容和實體當前座標
- 在更新實體座標時，記錄更新前後的座標

### 方案2：驗證目標座標

**客戶端** (`Client/Game/GameWorld.Combat.cs:868-918`):
```csharp
private void OnObjectMoved(int objectId, int x, int y, int heading)
{
  if (_entities.ContainsKey(objectId))
  {
    var entity = _entities[objectId];
    int oldX = entity.MapX;
    int oldY = entity.MapY;
    
    // 【修復】驗證移動距離是否合理
    int dx = Math.Abs(x - oldX);
    int dy = Math.Abs(y - oldY);
    int distance = Math.Max(dx, dy);
    
    if (distance > 1)
    {
      // 移動距離超過1格，可能是錯誤的座標
      GD.Print($"[Move-Error] ObjId={objectId} moved from ({oldX},{oldY}) to ({x},{y}) distance={distance} (expected <= 1)");
      // 可以選擇：跳過這個移動包，或者順移到新座標
    }
    
    entity.SetMapPosition(x, y, heading);
  }
}
```

### 方案3：使用服務器確認的玩家座標

**客戶端** (`Client/Game/GameWorld.Combat.cs:926-962`):
```csharp
private void OnObjectAttacked(int attackerId, int targetId, int actionId, int damage)
{
  // ... 現有邏輯 ...
  
  // 【修復】如果是怪物攻擊玩家，調整朝向面向目標（使用服務器確認的玩家座標）
  if (targetId > 0 && targetId == _myPlayer?.ObjectId)
  {
    // 目標是玩家自己，使用服務器確認的座標（優先）
    int targetX = _serverConfirmedPlayerX >= 0 ? _serverConfirmedPlayerX : _myPlayer.MapX;
    int targetY = _serverConfirmedPlayerY >= 0 ? _serverConfirmedPlayerY : _myPlayer.MapY;
    
    // 調整攻擊者朝向
    int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
    attacker.SetHeading(newHeading);
  }
}
```

## 日誌格式

### 需要添加的日誌

1. **服務器端**（如果可能）：
   ```
   [Server-Move] Monster {monsterId} StartMove target={targetId} targetPos=({targetX},{targetY}) monsterPos=({monsterX},{monsterY})
   [Server-Move] Sending S_ObjectMoving objectId={objectId} nextPos=({x},{y}) heading={heading}
   ```

2. **客戶端**：
   ```
   [Move-Packet] Received S_ObjectMoving objectId={objectId} nextPos=({x},{y}) heading={heading} currentPos=({currentX},{currentY})
   [Move-Error] ObjId={objectId} moved from ({oldX},{oldY}) to ({x},{y}) distance={distance} (expected <= 1)
   [Combat-Fix] Monster {attackerId} attacking player at server-confirmed ({targetX},{targetY}) client-predicted ({clientX},{clientY})
   ```

## 測試場景

1. **場景1**：玩家A在座標a，怪物C距離5格，應該走向座標a
   - 預期：怪物C走向座標a
   - 實際：檢查日誌，看服務器發送的目標座標是什麼

2. **場景2**：玩家A移動了，怪物C應該跟隨
   - 預期：怪物C跟隨玩家A移動
   - 實際：檢查日誌，看服務器是否使用了最新的玩家A座標

3. **場景3**：多個怪物同時攻擊玩家A
   - 預期：所有怪物都走向玩家A的座標
   - 實際：檢查日誌，看每個怪物收到的目標座標是什麼
