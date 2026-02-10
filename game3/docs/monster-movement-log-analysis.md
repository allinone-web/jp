# 怪物移動日誌分析

## 日誌分析：服務器如何處理怪物移動和攻擊

### 關鍵發現

#### 1. 玩家位置
- **玩家ID**: 10005
- **玩家座標**: (32915, 32922) - 這是服務器確認的座標

#### 2. 怪物移動模式

**怪物5959795的移動軌跡**：
```
[Move-Packet] Received S_ObjectMoving objectId=5959795 nextPos=(32915,32917) heading=4 currentPos=(32915,32916)
[Move-Packet] Received S_ObjectMoving objectId=5959795 nextPos=(32915,32918) heading=4 currentPos=(32915,32917)
[Move-Packet] Received S_ObjectMoving objectId=5959795 nextPos=(32915,32919) heading=4 currentPos=(32915,32918)
```

**分析**：
- 怪物5959795在Y軸上從32916 → 32917 → 32918 → 32919，持續向南移動（heading=4）
- 玩家在Y=32922，怪物在Y=32916-32919，**怪物正在向南移動，朝向玩家**
- 每次移動1格，符合正常移動模式

**怪物5959803的移動軌跡**：
```
[Move-Packet] Received S_ObjectMoving objectId=5959803 nextPos=(32912,32921) heading=3 currentPos=(32911,32920)
```

**分析**：
- 怪物5959803從(32911,32920)移動到(32912,32921)
- heading=3（東南方向）
- 玩家在(32915,32922)，怪物在(32912,32921)
- **怪物正在向東北移動，朝向玩家**

**怪物5958445的移動軌跡**：
```
[Move-Packet] Received S_ObjectMoving objectId=5958445 nextPos=(32920,32917) heading=1 currentPos=(32921,32918)
[Move-Packet] Received S_ObjectMoving objectId=5958445 nextPos=(32921,32916) heading=1 currentPos=(32920,32917)
[Move-Packet] Received S_ObjectMoving objectId=5958445 nextPos=(32922,32915) heading=1 currentPos=(32921,32916)
[Move-Packet] Received S_ObjectMoving objectId=5958445 nextPos=(32923,32914) heading=1 currentPos=(32922,32915)
```

**分析**：
- 怪物5958445從(32921,32918) → (32920,32917) → (32921,32916) → (32922,32915) → (32923,32914)
- heading=1（東北方向）
- 玩家在(32915,32922)，怪物在(32923,32914)
- **怪物正在向西南移動，但似乎偏離了玩家位置**

#### 3. 怪物攻擊模式

**怪物5958499攻擊玩家**：
```
[Combat-Packet] Received Op35 attacker=5958499 target=10005 action=1 damage=5
[Combat-Fix] Monster 5958499 attacking player at server-confirmed (32915,32922) client-predicted (32915,32922), adjusted heading to 1
```

**分析**：
- 怪物5958499攻擊玩家10005
- 調整朝向為heading=1（東北方向）
- 使用服務器確認的玩家座標(32915,32922)

**怪物5959803攻擊玩家**：
```
[Combat-Packet] Received Op35 attacker=5959803 target=10005 action=25 damage=9
[Combat-Fix] Monster 5959803 attacking player at server-confirmed (32915,32922) client-predicted (32915,32922), adjusted heading to 3
```

**分析**：
- 怪物5959803攻擊玩家10005
- 調整朝向為heading=3（東南方向）
- 使用服務器確認的玩家座標(32915,32922)

**怪物5959795攻擊玩家**：
```
[Combat-Packet] Received Op35 attacker=5959795 target=10005 action=25 damage=10
[Combat-Fix] Monster 5959795 attacking player at server-confirmed (32915,32922) client-predicted (32915,32922), adjusted heading to 4
```

**分析**：
- 怪物5959795攻擊玩家10005
- 調整朝向為heading=4（南方向）
- 使用服務器確認的玩家座標(32915,32922)

### 服務器移動邏輯分析

#### S_ObjectMoving包的結構

**服務器發送** (`server/network/server/S_ObjectMoving.java`):
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
  writeH(x);  // 下一步座標X
  writeH(y);  // 下一步座標Y
  writeC(o.getHeading());  // 當前朝向
}
```

**關鍵點**：
- 服務器根據**當前座標**和**當前朝向**，計算**下一步座標**
- 包中的 `x, y` 是**下一步座標**，不是當前座標
- 客戶端收到包後，應該更新實體到這個新座標

#### 怪物移動決策邏輯

**服務器代碼** (`server/world/instance/MonsterInstance.java:291`):
```java
StartMove(cha.getX(), cha.getY());  // cha是玩家A
```

**分析**：
- 服務器使用 `cha.getX(), cha.getY()`（玩家A的服務器記錄的座標）
- 調用 `StartMove` 計算路徑
- 使用A*尋路算法計算移動路徑
- 每次移動一步，發送一個 `S_ObjectMoving` 包

### 日誌中的異常情況

#### 1. Entity_NOT_FOUND

**多次出現**：
```
[Pos-Sync] ObjID:5959795 | Server_Grid:(32915,32917) | Entity_NOT_FOUND
[Pos-Sync] ObjID:5959803 | Server_Grid:(32912,32921) | Entity_NOT_FOUND
[Pos-Sync] ObjID:5958445 | Server_Grid:(32920,32917) | Entity_NOT_FOUND
```

**分析**：
- 服務器發送移動包，但客戶端找不到對應的實體
- 可能的原因：
  1. 實體已經被刪除（超出視野範圍）
  2. 實體還沒有被創建（剛進入視野範圍）
  3. 實體ID不匹配

**但奇怪的是**：
- 客戶端仍然收到移動包，說明服務器認為實體存在
- 客戶端記錄了 `[Move-Packet]`，說明移動包被處理了
- 但 `Entity_NOT_FOUND` 表示實體不在 `_entities` 字典中

#### 2. 怪物5958445的移動方向

**移動軌跡**：
```
(32921,32918) → (32920,32917) → (32921,32916) → (32922,32915) → (32923,32914)
heading=1 (東北方向)
```

**分析**：
- 玩家在(32915,32922)
- 怪物從(32921,32918)開始，應該向西南移動（朝向玩家）
- 但實際移動方向是東北（heading=1），**遠離玩家**
- 這可能是：
  1. 路徑被阻擋，怪物繞路
  2. 怪物在追擊其他目標
  3. 服務器計算的路徑有誤

### 服務器移動和攻擊的關係

#### 時間線分析

**場景1：怪物5959795攻擊玩家**
```
T1: [Move-Packet] 怪物5959795移動到(32915,32917) heading=4
T2: [Combat-Packet] 怪物5959795攻擊玩家，調整朝向為heading=4
T3: [Move-Packet] 怪物5959795移動到(32915,32918) heading=4
T4: [Combat-Packet] 怪物5959795攻擊玩家，調整朝向為heading=4
```

**分析**：
- 怪物在移動過程中攻擊玩家
- 攻擊時調整朝向，確保面向玩家
- 移動方向（heading=4）與攻擊朝向（heading=4）一致

**場景2：怪物5959803攻擊玩家**
```
T1: [Move-Packet] 怪物5959803移動到(32912,32921) heading=3
T2: [Combat-Packet] 怪物5959803攻擊玩家，調整朝向為heading=3
```

**分析**：
- 怪物移動到(32912,32921)，距離玩家(32915,32922)只有3格
- 攻擊時調整朝向為heading=3（東南方向），面向玩家
- 移動方向（heading=3）與攻擊朝向（heading=3）一致

### 關鍵結論

#### 1. 服務器移動邏輯

**服務器如何決定怪物移動**：
1. 怪物AI決定攻擊目標（玩家）
2. 調用 `StartMove(cha.getX(), cha.getY())`，使用玩家的服務器記錄座標
3. 使用A*尋路算法計算移動路徑
4. 每次移動一步，發送一個 `S_ObjectMoving` 包
5. 包中的 `nextPos` 是下一步座標，`heading` 是當前朝向

**服務器如何計算移動方向**：
- 根據當前座標和目標座標，計算朝向
- 使用 `calcheading` 方法計算朝向
- 根據朝向，計算下一步座標（當前座標 + 朝向向量）

#### 2. 客戶端處理邏輯

**客戶端如何處理移動包**：
1. 收到 `S_ObjectMoving` 包
2. 從 `_entities` 獲取實體當前座標
3. 驗證移動距離（應該只移動1格）
4. 更新實體到新座標
5. 播放移動動畫（如果距離合理）

**客戶端如何處理攻擊包**：
1. 收到 `S_ObjectAttack` 包（Op35）
2. 從 `_entities` 獲取攻擊者和目標實體
3. 如果是怪物攻擊玩家，使用服務器確認的玩家座標調整朝向
4. 播放攻擊動畫

#### 3. 故障分析

**為什麼有些怪物走向錯誤的方向？**

**可能的原因**：
1. **路徑被阻擋**：怪物無法直接走向玩家，需要繞路
2. **多目標衝突**：怪物在追擊多個目標，優先級混亂
3. **座標不同步**：服務器記錄的玩家座標與實際座標不一致
4. **尋路算法問題**：A*算法計算的路徑有誤

**從日誌看**：
- 怪物5958445從(32921,32918)開始，應該向西南移動到(32915,32922)
- 但實際向東北移動，遠離玩家
- 這可能是路徑被阻擋，或者服務器計算的路徑有誤

### 建議

#### 1. 添加更詳細的日誌

**服務器端**（如果可能）：
- 記錄怪物AI決策過程
- 記錄 `StartMove` 的目標座標
- 記錄A*尋路算法的計算結果

**客戶端**：
- 記錄實體當前座標和移動包中的座標
- 記錄移動方向與目標方向的關係
- 記錄 `Entity_NOT_FOUND` 的詳細原因

#### 2. 驗證移動方向

**客戶端** (`Client/Game/GameWorld.Combat.cs:868-918`):
```csharp
private void OnObjectMoved(int objectId, int x, int y, int heading)
{
  // ... 現有邏輯 ...
  
  // 【新增】驗證移動方向是否朝向玩家
  if (_myPlayer != null && _entities.TryGetValue(objectId, out var entity))
  {
    int dx = x - _myPlayer.MapX;
    int dy = y - _myPlayer.MapY;
    int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
    
    // 計算應該的朝向
    int expectedHeading = GetHeading(entity.MapX, entity.MapY, _myPlayer.MapX, _myPlayer.MapY, heading);
    
    if (expectedHeading != heading && distance > 1)
    {
      GD.Print($"[Move-Direction] ObjId={objectId} moved heading={heading} but expected={expectedHeading} towards player at ({_myPlayer.MapX},{_myPlayer.MapY})");
    }
  }
}
```

#### 3. 處理Entity_NOT_FOUND

**客戶端** (`Client/Game/GameWorld.Combat.cs:868-918`):
```csharp
private void OnObjectMoved(int objectId, int x, int y, int heading)
{
  // ... 現有邏輯 ...
  
  if (!_entities.ContainsKey(objectId))
  {
    // 【新增】如果實體不存在，嘗試從服務器數據創建
    // 或者記錄更詳細的錯誤信息
    GD.Print($"[Move-Error] ObjId={objectId} not found in _entities. Last known position may be outdated.");
    // 可以選擇：創建實體，或者忽略移動包
  }
}
```
