# 服務器位置同步哲學

## 服務器開發者的設計思路

### 1. 服務器是權威的（Server-Authoritative）

**核心原則**：
- 服務器知道所有實體的真實位置（`cha.getX()`, `cha.getY()`）
- 服務器在發送任何包含座標的包時，都會使用實體的**當前真實位置**
- 客戶端應該**無條件信任**服務器發送的座標

### 2. 位置更新的多種途徑

服務器通過多種包來更新實體位置：

#### 2.1 移動包（S_ObjectMoving, Opcode 18）
- **頻率**：每400-800ms一次
- **內容**：實體的**下一步**座標（`nextPos`）
- **用途**：定期同步實體移動

#### 2.2 攻擊包（S_ObjectAttack, Opcode 35）
- **近戰攻擊**：不包含座標
- **弓箭/魔法攻擊**：包含攻擊者座標（`cha.getX()`, `cha.getY()`）和目標座標（`target.getX()`, `target.getY()`）
- **用途**：**每次攻擊都是一個位置更新的機會**

#### 2.3 魔法攻擊包（S_ObjectAttackMagic, Opcode 35/57）
- **內容**：攻擊者座標（`cha.getX()`, `cha.getY()`）和目標座標
- **用途**：魔法攻擊時更新位置

### 3. 服務器的設計哲學

**關鍵洞察**：
1. **每次攻擊都是一個位置更新的機會**：服務器在發送攻擊包時，會包含攻擊者的最新座標
2. **客戶端應該利用所有包含座標的包來更新位置**：不僅僅是移動包，攻擊包也應該更新位置
3. **位置同步是持續的**：客戶端應該從每一個包含座標的包中提取位置信息並更新

### 4. 為什麼客戶端會被"瞬間拉回"？

**問題根源**：
- 客戶端沒有及時從攻擊包中更新位置
- 客戶端使用過時的本地位置進行預測
- 服務器發送移動包時，發現客戶端位置與服務器位置不一致，強制同步

**解決方案**：
- **每一個包含座標的包，都要更新對應實體的位置**
- **無論是玩家、怪物、其他玩家，都要更新**
- **保持位置永遠最新**

## 客戶端實現原則

### 原則1：從所有包中提取位置信息

**實現位置**：
1. `OnRangeAttackReceived` (Op35 弓箭/魔法攻擊)
2. `OnObjectMagicAttacked` (Op57 魔法攻擊)
3. `OnObjectMoved` (Op18 移動包)
4. `OnObjectAttacked` (Op35 近戰攻擊 - 雖然不包含座標，但可以從其他途徑獲取)

### 原則2：統一的位置更新函數

**設計**：
```csharp
private void UpdateEntityPositionFromServer(int objectId, int serverX, int serverY, int heading, string source)
{
    if (!_entities.TryGetValue(objectId, out var entity))
        return;
    
    int currentX = entity.MapX;
    int currentY = entity.MapY;
    int diffX = Math.Abs(serverX - currentX);
    int diffY = Math.Abs(serverY - currentY);
    int diff = Math.Max(diffX, diffY);
    
    if (diff > 0)
    {
        GD.Print($"[Pos-Sync] Updating {objectId} position from {source}: server-confirmed ({serverX},{serverY}) client-current ({currentX},{currentY}) diff={diff}");
        entity.SetMapPosition(serverX, serverY, heading);
    }
}
```

### 原則3：所有實體都要更新

**無論是**：
- 玩家自己
- 怪物
- 其他玩家
- NPC

**都要從服務器包中更新位置**

### 原則4：優先使用服務器座標

**邏輯**：
1. 如果服務器包包含座標，**立即更新**
2. 不要等待"下一輪更新"
3. 服務器座標是**權威的**，客戶端應該無條件信任

## 實現檢查清單

- [x] `OnRangeAttackReceived` 中更新**所有攻擊者**位置（包括玩家、怪物、其他玩家）- **已完成**
- [x] `OnRangeAttackReceived` 中更新目標位置（如果差距 > 2 格）- **已完成**
- [x] `OnObjectMagicAttacked` 中更新攻擊者位置（包括玩家、怪物、其他玩家）- **已完成**
- [x] `OnObjectMoved` 中更新所有實體位置（移動包）- **已完成**
- [x] 所有實體類型的位置更新（玩家、怪物、其他玩家、NPC）- **已完成**
- [x] 統一的位置更新邏輯（服務器座標優先）- **已完成**

## 已實現的功能

### 1. `OnRangeAttackReceived` (Op35 弓箭/魔法攻擊)
- ✅ 更新攻擊者位置（無論是玩家、怪物、其他玩家）
- ✅ 更新目標位置（如果差距 > 2 格）
- ✅ 同步更新 `_serverConfirmedPlayerX/Y`（如果是玩家）

### 2. `OnObjectMagicAttacked` (Op57 魔法攻擊)
- ✅ 更新攻擊者位置（無論是玩家、怪物、其他玩家）
- ✅ 同步更新 `_serverConfirmedPlayerX/Y`（如果是玩家）

### 3. `OnObjectMoved` (Op18 移動包)
- ✅ 更新所有實體位置（已存在）

### 4. 核心原則
- ✅ 服務器座標是權威的，客戶端無條件信任
- ✅ 從每一個包含座標的包中提取位置信息並更新
- ✅ 保持位置永遠最新
