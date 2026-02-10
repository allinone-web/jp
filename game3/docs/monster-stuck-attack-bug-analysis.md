# 怪物卡在一個地方攻擊的故障分析

## 故障場景描述

**問題**：怪物卡在一個地方攻擊，而玩家已經移動了。

**日誌證據**：
```
[Combat-Fix] Monster 5957213 attacking player at server-confirmed (32964,32676) client-predicted (32965,32677), adjusted heading to 3
[Combat-Fix] Monster 5957213 attacking player at server-confirmed (32964,32676) client-predicted (32966,32676), adjusted heading to 3
[Combat-Fix] Monster 5957213 attacking player at server-confirmed (32964,32676) client-predicted (32964,32678), adjusted heading to 3
[Combat-Fix] Monster 5957213 attacking player at server-confirmed (32964,32676) client-predicted (32963,32678), adjusted heading to 3
```

**關鍵發現**：
- **服務器確認的玩家座標**：一直保持 `(32964,32676)`（過時！）
- **客戶端預測的玩家座標**：從 `(32965,32677)` → `(32966,32676)` → `(32964,32678)` → `(32963,32678)`（持續更新）
- **怪物攻擊朝向**：一直使用過時的服務器確認座標 `(32964,32676)`，導致攻擊朝向錯誤

## 問題根源

### 1. 服務器確認座標更新頻率低

**當前邏輯**：
- `_serverConfirmedPlayerX/Y` 只在收到 `S_ObjectMoving` 包時更新
- 但服務器可能不經常發送玩家移動確認包
- 如果玩家移動了但服務器沒發送確認包，`_serverConfirmedPlayerX/Y` 就會過時

**從日誌看**：
- 玩家從 `(32965,32677)` 移動到 `(32963,32678)`
- 但 `_serverConfirmedPlayerX/Y` 一直保持 `(32964,32676)`
- 這表示服務器很久沒有發送玩家移動確認包了

### 2. 怪物攻擊時使用過時的座標

**當前邏輯** (`Client/Game/GameWorld.Combat.cs:960-971`):
```csharp
if (targetId > 0 && targetId == _myPlayer?.ObjectId)
{
    // 目標是玩家自己，使用服務器確認的座標（優先）
    int targetX = _serverConfirmedPlayerX >= 0 ? _serverConfirmedPlayerX : _myPlayer.MapX;
    int targetY = _serverConfirmedPlayerY >= 0 ? _serverConfirmedPlayerY : _myPlayer.MapY;
    
    // 調整攻擊者朝向
    int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
    attacker.SetHeading(newHeading);
}
```

**問題**：
- 如果 `_serverConfirmedPlayerX/Y` 過時（與客戶端預測差距很大），怪物會使用過時的座標
- 導致怪物攻擊朝向錯誤，看起來像"卡在一個地方攻擊"

### 3. 魔法攻擊的座標差異

**從日誌看**：
```
[Magic][Range-Coord-Fix] 目標是玩家，使用實際座標！ServerTarget:(32966,32671) PlayerActual:(32965,32677) Diff:(1,6) -> 使用玩家實際座標
[Magic][Range-Coord-Fix] 目標是玩家，使用實際座標！ServerTarget:(32966,32671) PlayerActual:(32966,32676) Diff:(0,5) -> 使用玩家實際座標
[Magic][Range-Coord-Fix] 目標是玩家，使用實際座標！ServerTarget:(32966,32671) PlayerActual:(32964,32678) Diff:(2,7) -> 使用玩家實際座標
```

**分析**：
- 服務器提供的目標座標 `(32966,32671)` 一直不變（過時！）
- 玩家實際座標在持續變化
- 魔法攻擊已經修復，使用玩家實際座標
- 但物理攻擊（Op35）還沒有修復

## 解決方案

### 方案1：如果服務器確認座標過時，使用客戶端預測

**修改位置**：`Client/Game/GameWorld.Combat.cs:960-971`

```csharp
if (targetId > 0 && targetId == _myPlayer?.ObjectId)
{
    // 目標是玩家自己，優先使用服務器確認的座標
    int targetX = _serverConfirmedPlayerX >= 0 ? _serverConfirmedPlayerX : _myPlayer.MapX;
    int targetY = _serverConfirmedPlayerY >= 0 ? _serverConfirmedPlayerY : _myPlayer.MapY;
    
    // 【修復】如果服務器確認座標與客戶端預測差距過大（>1格），使用客戶端預測
    // 這表示服務器確認座標過時，客戶端預測更準確
    int serverDiffX = Math.Abs(targetX - _myPlayer.MapX);
    int serverDiffY = Math.Abs(targetY - _myPlayer.MapY);
    int serverDiff = Math.Max(serverDiffX, serverDiffY);
    
    if (serverDiff > 1)
    {
        // 服務器確認座標過時，使用客戶端預測
        targetX = _myPlayer.MapX;
        targetY = _myPlayer.MapY;
        GD.Print($"[Combat-Fix] Server confirmed player position too old (diff={serverDiff}), using client prediction. Server:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY}) Client:({_myPlayer.MapX},{_myPlayer.MapY})");
    }
    
    // 調整攻擊者朝向
    int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
    attacker.SetHeading(newHeading);
    
    GD.Print($"[Combat-Fix] Monster {attackerId} attacking player at ({targetX},{targetY}) (server-confirmed:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY}) client-predicted:({_myPlayer.MapX},{_myPlayer.MapY})), adjusted heading to {newHeading}");
}
```

### 方案2：直接使用客戶端預測（更簡單）

**修改位置**：`Client/Game/GameWorld.Combat.cs:960-971`

```csharp
if (targetId > 0 && targetId == _myPlayer?.ObjectId)
{
    // 【修復】直接使用客戶端預測的玩家座標（因為它更準確，持續更新）
    // 服務器確認座標可能過時，導致怪物攻擊朝向錯誤
    int targetX = _myPlayer.MapX;
    int targetY = _myPlayer.MapY;
    
    // 調整攻擊者朝向
    int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
    attacker.SetHeading(newHeading);
    
    GD.Print($"[Combat-Fix] Monster {attackerId} attacking player at client-predicted ({targetX},{targetY}) server-confirmed:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY}), adjusted heading to {newHeading}");
}
```

**優點**：
- 更簡單，直接使用客戶端預測
- 客戶端預測持續更新，更準確
- 與魔法攻擊的修復邏輯一致

**缺點**：
- 如果客戶端預測錯誤，怪物攻擊朝向也會錯誤
- 但這比使用過時的服務器確認座標更好

## 推薦方案（已更新）

**最終方案**：根據差距大小選擇策略

### 服務器攻擊包分析

**關鍵發現**：
1. **近戰攻擊的 Op35 包不包含目標座標**（只有弓箭攻擊才包含）
2. **服務器不會在攻擊包中更新玩家座標**（對於近戰攻擊）
3. **服務器會在 `S_ObjectMoving` 包中更新玩家座標**（每400-800ms一次）
4. **玩家移動一格需要640ms**（32像素 / 50像素/秒）

### 策略選擇

**修改位置**：`Client/Game/GameWorld.Combat.cs:991-1023`

**邏輯**：
1. **差距 <= 2格**：使用服務器確認座標，等待下一輪 `S_ObjectMoving` 包更新（約640ms後）
   - 這樣可以保持服務器權威性，避免客戶端預測錯誤
   - 服務器會在下一輪移動包中更新玩家座標

2. **差距 > 2格**：立即使用客戶端預測
   - 這表示服務器確認座標已經嚴重過時，不能等待下一輪更新
   - 避免怪物"卡在一個地方攻擊"

**優點**：
1. 平衡服務器權威性和客戶端響應性
2. 如果差距不大，等待服務器更新（更準確）
3. 如果差距很大，立即使用客戶端預測（更響應）

## 測試場景

1. **場景1**：玩家移動，服務器確認座標更新及時
   - 預期：怪物使用服務器確認座標攻擊

2. **場景2**：玩家移動，服務器確認座標過時
   - 預期：怪物使用客戶端預測座標攻擊

3. **場景3**：玩家快速移動，服務器確認座標差距>1格
   - 預期：怪物使用客戶端預測座標攻擊，朝向正確
