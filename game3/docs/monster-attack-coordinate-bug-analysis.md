# 怪物攻擊座標錯誤故障分析

## 故障場景描述

1. **玩家A** 正在攻擊 **怪物B**，在座標 **123**
2. **怪物C** 也攻擊 **玩家A**，但到座標 **456** 發起播放攻擊動作

**問題**：怪物C攻擊玩家A時，為什麼會到座標456播放攻擊動作，而不是玩家A的實際座標？

## 數據包流程分析

### 1. 服務器端流程

#### 1.1 怪物C決定攻擊玩家A

**代碼位置**：`server/world/instance/MonsterInstance.java:283-288`

```java
if ((getDistance(cha.getX(), cha.getY(), cha.getMap(), this.Areaatk)) && (LongAttackCK(cha, this.Areaatk))) {
  if (this.Areaatk > 2)
    AttackBow(cha, cha.getX(), cha.getY(), getGfxMode() + 1, 66, true);
  else {
    Attack(cha, cha.getX(), cha.getY(), getGfxMode() + 1, 0);
  }
}
```

**關鍵點**：
- 服務器使用 `cha.getX(), cha.getY()`（玩家A的**服務器記錄的座標**）
- 如果玩家A正在移動，服務器記錄的座標可能**不是最新的**

#### 1.2 服務器發送攻擊包

**代碼位置**：`server/network/server/S_ObjectAttack.java:9-26`

```java
public S_ObjectAttack(Character cha, L1Object target, int action, int dmg, int effectId, boolean bow, boolean arrow)
{
  writeC(35);
  writeC(action);
  writeD(cha.getObjectId());        // 攻擊者ID（怪物C）
  writeD(target.getObjectId());     // 目標ID（玩家A）
  writeC(dmg);
  writeC(cha.getHeading());         // 攻擊者朝向
  // ... 武器效果等
}
```

**關鍵點**：
- **Opcode 35 包不包含目標座標**（只有攻擊者ID和目標ID）
- 如果是弓箭攻擊，才會包含座標（`bow` 方法中的 `target.getX(), target.getY()`）

### 2. 客戶端流程

#### 2.1 客戶端收到攻擊包

**代碼位置**：`Client/Game/GameWorld.Combat.cs:926-962`

```csharp
private void OnObjectAttacked(int attackerId, int targetId, int actionId, int damage)
{
  // attackerId = 怪物C的ID
  // targetId = 玩家A的ID
  
  if (_entities.TryGetValue(attackerId, out var attacker))
  {
    attacker.SetAction(actionId);  // 播放攻擊動畫
    if (targetId > 0 && !isOp35Magic)
    {
      attacker.PrepareAttack(targetId, damage);  // 準備攻擊（加入待處理列表）
    }
  }
}
```

**關鍵問題**：
- `SetAction(actionId)` 會播放攻擊動畫，但**沒有調整朝向**
- `PrepareAttack` 只是將攻擊加入待處理列表，**不播放動畫**

#### 2.2 攻擊動畫播放

**代碼位置**：`Client/Game/GameEntity.CombatFx.cs:44-62`

```csharp
public void PlayAttackAnimation(int tx, int ty)
{
  // 根據目標座標調整朝向
  if (tx != 0 && ty != 0 && (tx != MapX || ty != MapY))
  {
    int newHeading = GameWorld.GetHeading(MapX, MapY, tx, ty, Heading);
    SetHeading(newHeading);
  }
  SetAction(ACT_ATTACK);  // 播放攻擊動畫
}
```

**關鍵問題**：
- `PlayAttackAnimation` 需要目標座標 `(tx, ty)` 來調整朝向
- 但 `OnObjectAttacked` **沒有調用 `PlayAttackAnimation`**，只是調用了 `SetAction(actionId)`
- 這意味著怪物C的攻擊動畫**沒有調整朝向**，使用的是怪物C當前的朝向

#### 2.3 攻擊關鍵幀觸發

**代碼位置**：`Client/Game/GameEntity.CombatFx.cs:168-181`

```csharp
public void OnAnimationKeyFrame()
{
  if (!_isAttackHitTriggered && _pendingAttacks.Count > 0)
  {
    _isAttackHitTriggered = true;
    foreach (var atk in _pendingAttacks)
    {
      OnAttackKeyFrameHit?.Invoke(atk.TargetId, atk.Damage);
    }
    _pendingAttacks.Clear();
  }
}
```

**關鍵點**：
- 當攻擊動畫播放到關鍵幀時，觸發 `OnAttackKeyFrameHit`
- 這會調用 `HandleEntityAttackHit(targetId, damage)`
- 但這裡**沒有使用目標座標**，只是處理傷害

## 故障根源分析

### 問題1：客戶端沒有調整怪物攻擊朝向

**當前邏輯**：
- `OnObjectAttacked` 只調用 `SetAction(actionId)`，不調整朝向
- 怪物C的攻擊動畫使用**當前朝向**，而不是面向玩家A

**正確邏輯應該是**：
- 收到攻擊包時，應該調用 `PlayAttackAnimation(targetX, targetY)`
- 但 `S_ObjectAttack` 包**不包含目標座標**，需要從 `_entities` 獲取

### 問題2：目標座標可能過時

**場景**：
1. 玩家A在座標123攻擊怪物B
2. 玩家A移動到座標456（客戶端預測）
3. 服務器還沒發送移動確認包，服務器記錄的玩家A座標還是123
4. 怪物C攻擊玩家A，服務器使用座標123
5. 客戶端收到攻擊包，從 `_entities` 獲取玩家A的座標，可能是456（客戶端預測）

**時間線**：
```
T0: 玩家A在座標123（服務器和客戶端一致）
T1: 玩家A開始移動（客戶端預測到座標456）
T2: 怪物C決定攻擊玩家A（服務器使用座標123）
T3: 服務器發送攻擊包（不包含座標）
T4: 客戶端收到攻擊包，從 _entities 獲取玩家A座標（可能是456）
T5: 怪物C播放攻擊動畫，朝向座標456（錯誤！）
T6: 服務器發送移動確認包，玩家A實際在座標123
```

### 問題3：客戶端使用過時的目標座標

**當前邏輯**：
- `OnObjectAttacked` 沒有獲取目標座標
- 如果調用 `PlayAttackAnimation`，需要從 `_entities` 獲取目標座標
- 但 `_entities` 中的座標可能是**客戶端預測的座標**，不是服務器確認的座標

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
    if (targetId > 0 && _entities.TryGetValue(targetId, out var target))
    {
      // 使用目標的當前座標（從 _entities 獲取）
      int targetX = target.MapX;
      int targetY = target.MapY;
      
      // 調整攻擊者朝向
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

**問題**：
- 使用 `target.MapX, target.MapY` 可能是**客戶端預測的座標**，不是服務器確認的座標
- 如果玩家A正在移動，客戶端預測的座標可能與服務器不一致

### 方案2：服務器在攻擊包中包含目標座標

**修改位置**：`server/network/server/S_ObjectAttack.java`

```java
public S_ObjectAttack(Character cha, L1Object target, int action, int dmg, int effectId, boolean bow, boolean arrow)
{
  writeC(35);
  writeC(action);
  writeD(cha.getObjectId());
  writeD(target.getObjectId());
  writeC(dmg);
  writeC(cha.getHeading());
  
  // 【新增】包含目標座標（服務器確認的座標）
  if (target != null)
  {
    writeH(target.getX());  // 目標X座標
    writeH(target.getY());  // 目標Y座標
  }
  else
  {
    writeH(0);
    writeH(0);
  }
  
  // ... 現有邏輯 ...
}
```

**優點**：
- 客戶端可以使用**服務器確認的目標座標**
- 避免客戶端預測座標與服務器不一致的問題

**缺點**：
- 需要修改服務器協議（可能影響其他客戶端）

### 方案3：客戶端使用服務器確認的玩家座標

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

**優點**：
- 不需要修改服務器協議
- 對於玩家目標，使用服務器確認的座標
- 對於其他目標，使用當前座標

**缺點**：
- 如果玩家正在移動，服務器確認的座標可能還是過時的

## 推薦方案

**推薦使用方案3**，因為：
1. 不需要修改服務器協議
2. 對於玩家目標，優先使用服務器確認的座標
3. 如果服務器確認座標不可用，回退到客戶端座標

## 測試場景

1. **場景1**：玩家A在座標123攻擊怪物B，怪物C攻擊玩家A
   - 預期：怪物C應該面向玩家A的實際座標（123或456，取決於移動狀態）

2. **場景2**：玩家A正在移動，怪物C攻擊玩家A
   - 預期：怪物C應該面向玩家A的服務器確認座標（如果可用）

3. **場景3**：玩家A和怪物C同時移動，怪物C攻擊玩家A
   - 預期：怪物C應該面向玩家A的服務器確認座標（如果可用）
