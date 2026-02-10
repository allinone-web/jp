# 座標更新系統完整制度

## 核心原則

**確保所有角色（玩家、怪物、其他玩家）的座標都能及時更新，通過多種方式（檢查/更新/主動/被動），利用每一次機會更新屏幕所有對象的位置。**

**讓服務器和客戶端都能同步得到每一個角色的位置。**

這是 PVP 戰鬥的基礎，如果座標不正確，一切的網路遊戲基礎都毀了。

## 問題根源

### 怪物在空位置攻擊的根本原因

1. **封包丟失**：客戶端發送的移動包丟失，服務器不知道實際位置
2. **網絡阻塞**：移動包延遲，服務器收到過時位置
3. **位置更新制度缺乏**：沒有主動更新機制，只能被動等待

**結果**：服務器認為玩家在位置 A，客戶端認為玩家在位置 B，怪物根據位置 A 攻擊，但客戶端顯示玩家在位置 B → **怪物在空位置攻擊**

**解決方案**：通過主動和被動更新機制，確保服務器和客戶端都能同步得到每一個角色的位置。

## 更新機制分類

### 1. 被動更新（從服務器包中提取位置信息）

#### 1.1 S_ObjectMoving (Opcode 18) - 移動包
- **頻率**：每 400-800ms 一次
- **內容**：實體的下一步座標（`nextPos`）
- **處理**：`OnObjectMoved` → `UpdateEntityPositionFromServer`
- **適用對象**：所有實體（玩家、怪物、其他玩家、NPC）

#### 1.2 S_ObjectAttack (Opcode 35) - 攻擊包
- **近戰攻擊**：不包含座標
- **弓箭/魔法攻擊**：包含攻擊者座標和目標座標
- **處理**：`OnRangeAttackReceived` → `UpdateEntityPositionFromServer`
- **適用對象**：攻擊者和目標（如果差距 > 2 格）

#### 1.3 S_ObjectAttackMagic (Opcode 57) - 魔法攻擊包
- **內容**：攻擊者座標（`cha.getX()`, `cha.getY()`）
- **處理**：`OnObjectMagicAttacked` → `UpdateEntityPositionFromServer`
- **適用對象**：攻擊者（玩家、怪物、其他玩家）

### 2. 主動更新（客戶端主動發送位置更新）

#### 2.1 定期主動更新
- **機制**：`UpdatePositionSync` 在 `_Process` 中每 640ms 檢查一次（僅在非死亡狀態）
- **觸發條件**：玩家位置與服務器確認位置差距 > 1 格
- **發送間隔**：最小 640ms（與遊戲移動節奏同步）
- **設計原理**：根據服務器協議分析，服務器**沒有**強制要求640ms心跳，但客戶端定期更新可確保座標同步。實際移動時應根據移動速度發送封包，而非定期心跳。
- **實現位置**：`Client/Game/GameWorld.Combat.cs`

```csharp
private void UpdatePositionSync(double delta)
{
    // 每0.5秒檢查一次
    if (_positionUpdateTimer >= POSITION_UPDATE_INTERVAL)
    {
        // 檢查差距 > 1格，發送位置更新包
        if (diff > 1)
        {
            _netSession.Send(C_MoveCharPacket.Make(clientX, clientY, heading));
        }
    }
}
```

#### 2.2 攻擊時位置更新
- **機制**：`PerformAttackOnce` 在攻擊前發送位置更新
- **觸發時機**：每次攻擊前
- **實現位置**：`Client/Game/GameWorld.Combat.cs`

```csharp
public void PerformAttackOnce(int targetId, int targetX, int targetY)
{
    // 在攻擊前發送位置更新包
    SendPositionUpdateToServer("BeforeAttack");
    // ... 攻擊邏輯
}
```

#### 2.3 魔法時位置更新
- **機制**：`UseMagic` 在發送魔法包前發送位置更新
- **觸發時機**：每次釋放魔法前
- **實現位置**：`Client/Game/GameWorld.Skill.cs`

```csharp
public void UseMagic(int skillId, int targetId = 0, int targetX = 0, int targetY = 0)
{
    // 在發送魔法包前發送位置更新包（與遊戲移動節奏同步）
    if (currentTime - _lastMagicPositionUpdateTime >= MIN_MAGIC_POSITION_UPDATE_INTERVAL_MS) // 640ms
    {
        _netSession.Send(C_MoveCharPacket.Make(clientX, clientY, heading));
    }
    // ... 魔法邏輯
}
```

#### 2.4 服務器座標差距過大時的主動更新
- **機制**：`UpdateEntityPositionFromServer` 中，如果差距 > 2 格且是玩家自己，發送位置更新
- **觸發條件**：收到服務器包時，發現差距 > 2 格
- **實現位置**：`Client/Game/GameWorld.Combat.cs`

```csharp
private void UpdateEntityPositionFromServer(int objectId, int serverX, int serverY, int heading, string source)
{
    if (objectId == _myPlayer?.ObjectId && diff > 2)
    {
        // 發送客戶端的實際位置給服務器
        _netSession.Send(C_MoveCharPacket.Make(currentX, currentY, newHeading));
        // 不更新玩家位置，保持客戶端預測，等待服務器確認
    }
}
```

## 統一位置更新函數

### `UpdateEntityPositionFromServer`
- **位置**：`Client/Game/GameWorld.Combat.cs`
- **功能**：統一處理從服務器包中更新實體位置
- **核心邏輯**：
  1. 計算差距
  2. 如果差距 > 2 格且是玩家自己，發送位置更新包給服務器
  3. 否則，更新實體位置到服務器座標

## 更新時機總結

| 時機 | 觸發條件 | 更新對象 | 實現位置 |
|------|---------|---------|---------|
| S_ObjectMoving | 每 400-800ms | 所有實體 | `OnObjectMoved` |
| S_ObjectAttack (遠程) | 每次遠程攻擊 | 攻擊者和目標 | `OnRangeAttackReceived` |
| S_ObjectAttackMagic | 每次魔法攻擊 | 攻擊者 | `OnObjectMagicAttacked` |
| 定期主動更新 | 每 640ms，差距 > 1 格 | 玩家自己 | `UpdatePositionSync` |
| 攻擊前 | 每次攻擊前（間隔 >= 640ms） | 玩家自己 | `PerformAttackOnce` |
| 魔法前 | 每次魔法前（間隔 >= 640ms） | 玩家自己 | `UseMagic` |
| 差距過大 | 差距 > 2 格 | 玩家自己 | `UpdateEntityPositionFromServer` |

## 關鍵設計決策

### 1. 為什麼差距 > 2 格時不更新玩家位置？
- **原因**：服務器座標可能過時（服務器每 400-800ms 才發送一次移動包）
- **解決方案**：發送客戶端實際位置給服務器，讓服務器知道客戶端的位置
- **效果**：避免玩家被"拉回"，等待服務器在下一輪包中確認

### 2. 為什麼要主動發送位置更新？
- **原因**：確保服務器始終知道客戶端的實際位置
- **時機**：攻擊前、魔法前、定期檢查
- **效果**：減少座標不同步，提高 PVP 戰鬥的真實性

### 3. 為什麼要限制發送頻率？
- **原因**：與遊戲移動節奏同步，避免過於頻繁的網絡包，減少服務器負擔
- **限制**：最小間隔 640ms（與遊戲移動節奏同步）
- **設計原理**：根據服務器協議分析，服務器**沒有**強制要求640ms心跳。客戶端定期更新可確保座標同步，但實際移動時應根據移動速度發送封包。
- **效果**：平衡位置同步精度和網絡負擔，與遊戲節奏完美同步

## 實現檢查清單

- [x] 被動更新：S_ObjectMoving
- [x] 被動更新：S_ObjectAttack (遠程)
- [x] 被動更新：S_ObjectAttackMagic
- [x] 主動更新：定期檢查（每 0.5 秒）
- [x] 主動更新：攻擊前
- [x] 主動更新：魔法前
- [x] 主動更新：差距過大時
- [x] 統一位置更新函數
- [x] 所有實體類型的位置更新（玩家、怪物、其他玩家、NPC）

## 日誌標籤

- `[Pos-Sync]`：正常位置同步
- `[Pos-Sync-Fix]`：位置同步修復（差距過大）
- `[Pos-Active-Update]`：主動位置更新
- `[Pos-Update]`：關鍵時刻位置更新（攻擊/魔法前）

## 注意事項

1. **服務器只接受玩家自己的位置更新**（C_Moving），且距離必須 <= 1 格
2. **如果差距 > 2 格**，發送位置更新包可能被服務器拒絕，但至少讓服務器知道客戶端的位置
3. **所有實體（怪物、其他玩家）的位置更新**只能通過服務器包，客戶端不能主動更新
4. **位置更新是持續的**，應該從每一個包含座標的包中提取位置信息並更新
