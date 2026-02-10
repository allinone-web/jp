# 攻擊系統完整分析文檔

## 1. Areaatk 攻擊範圍定義

### 1.1 近戰攻擊 (Melee Attack)
**位置**: `server/world/instance/PcInstance.java:613`
```java
this.Areaatk = 2;  // 硬編碼，近戰攻擊範圍 = 2格
```

**驗證邏輯**: `server/world/instance/PcInstance.java:619`
```java
if ((target != null) && (getDistance(x, y, target.getMap(), this.Areaatk)) && ...)
```
- `getDistance(x, y, target.getMap(), 2)` 檢查目標座標 `(x, y)` 是否在玩家當前座標的 **2格範圍內**
- 這是**硬編碼**的值，不是從數據庫讀取

### 1.2 弓箭攻擊 (Bow Attack)
**位置**: `server/world/instance/PcInstance.java:673`
```java
this.Areaatk = 12;  // 硬編碼，弓箭攻擊範圍 = 12格
```

**驗證邏輯**: `server/world/instance/PcInstance.java:691`
```java
if ((target != null) && (!target.isDead()) && (!target.isDelete()) && (arrow) && (LongAttackCK(target, this.Areaatk)))
```
- 使用 `LongAttackCK(target, 12)` 檢查，不僅檢查距離，還檢查**路徑是否被阻擋**

### 1.3 怪物攻擊範圍
**位置**: `server/world/instance/MonsterInstance.java:73`
```java
this.Areaatk = mon.getAreaatk();  // 從數據庫讀取
```
- 怪物的 `Areaatk` 是從**數據庫** (`Monster` bean) 讀取的
- 不同怪物有不同的攻擊範圍（近戰=2，遠程>2）

## 2. 攻擊驗證邏輯詳解

### 2.1 近戰攻擊驗證流程

**步驟1: 距離檢查**
```java
// server/world/object/L1Object.java:646
public boolean getDistance(int tx, int ty, int tm, int loc) {
  long dx = tx - getX();  // tx是客戶端發送的目標座標，getX()是玩家當前座標
  long dy = ty - getY();
  double distance = Math.sqrt(dx * dx + dy * dy);
  if (loc < (int)distance) return false;  // 如果距離大於攻擊範圍，返回false
  return true;
}
```
- **檢查**: 目標座標 `(x, y)` 是否在玩家當前座標的 `Areaatk` 範圍內
- **Areaatk = 2**: 表示攻擊範圍是2格（切比雪夫距離）

**步驟2: 遠程攻擊路徑檢查** (僅用於弓箭)
```java
// server/world/object/Character.java:923
public boolean LongAttackCK(L1Object temp, int loc) {
  // 從玩家位置到目標位置，逐格檢查路徑是否被阻擋
  // 最多檢查12格
  int count = 12;
  do {
    h = calcheading(myx, myy, tax, tay);
    if (!WorldMap.getInstance().IsThroughAttack(myx, myy, map, h)) {
      return false;  // 路徑被阻擋，返回false
    }
    // 移動到下一格
    switch (h) { ... }
    count--;
  } while (count > 0);
  return true;
}
```
- **檢查**: 從玩家位置到目標位置的路徑是否被阻擋
- **用途**: 弓箭攻擊必須通過此檢查，確保沒有障礙物阻擋

### 2.2 魔法攻擊驗證流程

**位置**: `server/world/instance/skill/Magic.java:215`
```java
if ((longCheck) && (!this.operator.LongAttackCK(o, 12))) {
  return 0;  // 路徑被阻擋，傷害為0
}
```

**魔法攻擊範圍**: 
- **硬編碼為 12格**，使用 `LongAttackCK(target, 12)` 檢查
- **不使用 `Areaatk` 變量**，直接使用固定值12

**魔法攻擊驗證步驟**:
1. 檢查目標是否死亡、鎖定等基本狀態
2. 檢查 `LongAttackCK(target, 12)` - 路徑是否被阻擋（12格範圍）
3. 檢查 `WorldMap.getInstance().AttackZone(this.operator, o)` - 是否在攻擊區域內
4. 計算傷害（考慮MR減傷等）

## 3. 攻擊數據包流程

### 3.1 客戶端發送攻擊包

#### 近戰攻擊 (C_Attack, Opcode 23)
**客戶端發送**:
```
Opcode: 23
Data:
  - WriteD(targetId)      // 目標ID
  - WriteH(targetX)       // 目標X座標（客戶端發送的目標座標）
  - WriteH(targetY)       // 目標Y座標（客戶端發送的目標座標）
```

**服務器接收**: `server/network/client/C_Attack.java:34`
```java
pc.Attack(pc.getObject(this.objid), this.locx, this.locy, 1, 0);
```

#### 弓箭攻擊 (C_AttackBow, Opcode 24)
**客戶端發送**:
```
Opcode: 24
Data:
  - WriteD(targetId)      // 目標ID
  - WriteH(targetX)       // 目標X座標
  - WriteH(targetY)       // 目標Y座標
```

**服務器接收**: `server/network/client/C_AttackBow.java:37`
```java
pc.AttackBow(obj, this.locx, this.locy, 1, 66, false);
```

#### 魔法攻擊 (C_Magic, Opcode 25)
**客戶端發送**:
```
Opcode: 25
Data:
  - WriteC(lv - 1)        // 技能等級 (0-based)
  - WriteC(no)            // 技能編號
  - WriteD(targetId)      // 目標ID
```

**服務器接收**: `server/network/client/C_Magic.java:46`
```java
pc.getSkill().toMagic(this.lv, this.no, this.id);
```

### 3.2 服務器驗證並響應

#### 近戰/弓箭攻擊響應 (S_ObjectAttack, Opcode 35)

**服務器發送**: `server/network/server/S_ObjectAttack.java`
```
Opcode: 35
Data:
  - WriteC(action)        // 動作ID (攻擊動作)
  - WriteD(attackerId)     // 攻擊者ID
  - WriteD(targetId)      // 目標ID (如果為null則為0)
  - WriteC(damage)         // 傷害值 (0表示miss)
  - WriteC(heading)        // 攻擊者朝向
  
  // 如果是弓箭攻擊 (bow=true)
  if (arrow) {
    - WriteD(Config.getObjectID_ETC())  // 箭矢物品ID
    - WriteH(effectId)                  // 箭矢效果ID
    - WriteC(0)
    - WriteH(attackerX)                  // 攻擊者X座標
    - WriteH(attackerY)                  // 攻擊者Y座標
    - WriteH(targetX)                   // 目標X座標（服務器實際座標）
    - WriteH(targetY)                   // 目標Y座標（服務器實際座標）
    - WriteH(0)
    - WriteC(0)
  } else {
    // 近戰攻擊
    - WriteD(0) 或 WriteD(effectId)     // 武器效果ID
    - WriteC(0)
  }
```

**關鍵點**:
- **damage = 0** 表示攻擊miss（目標不在攻擊範圍內，或路徑被阻擋）
- **targetX, targetY** 是**服務器實際的目標座標**，不是客戶端發送的座標
- 客戶端應該使用服務器返回的座標來同步目標位置

#### 魔法攻擊響應 (S_ObjectAttackMagic, Opcode 35 或 57)

**單體魔法** (Opcode 35):
```
Opcode: 35
Data:
  - WriteC(action)        // 魔法動作ID
  - WriteD(attackerId)     // 攻擊者ID
  - WriteD(targetId)      // 目標ID
  - WriteC(damage)        // 傷害值
  - WriteC(heading)        // 攻擊者朝向
  - WriteD(Config.getObjectID_ETC())
  - WriteH(gfx)           // 魔法效果ID
  - WriteC(6)
  - WriteH(attackerX)      // 攻擊者X座標
  - WriteH(attackerY)      // 攻擊者Y座標
  - WriteH(targetX)       // 目標X座標（服務器實際座標）
  - WriteH(targetY)       // 目標Y座標（服務器實際座標）
  - WriteH(0)
  - WriteC(0)
```

**群體魔法** (Opcode 57):
```
Opcode: 57
Data:
  - WriteC(action)        // 魔法動作ID
  - WriteD(attackerId)     // 攻擊者ID
  - WriteH(attackerX)      // 攻擊者X座標
  - WriteH(attackerY)      // 攻擊者Y座標
  - WriteC(heading)        // 攻擊者朝向
  - WriteD(Config.getObjectID_ETC())
  - WriteH(gfx)           // 魔法效果ID
  - WriteC(0 或 8)         // 0=無方向，8=有方向
  - WriteH(0)
  - WriteH(targetCount)    // 目標數量
  - for each target:
      - WriteD(targetId)   // 目標ID
      - WriteC(damage)     // 對該目標的傷害
```

## 4. 攻擊驗證邏輯總結

### 4.1 近戰攻擊驗證
1. **距離檢查**: `getDistance(x, y, target.getMap(), 2)` - 目標座標是否在玩家2格範圍內
2. **路徑檢查**: `LongAttackCK(target, 2)` - 路徑是否被阻擋（近戰通常不需要，但代碼中有檢查）
3. **目標狀態檢查**: 目標是否死亡、刪除等
4. **傷害計算**: `DmgSystem(target, false, 1)` - 計算傷害

### 4.2 弓箭攻擊驗證
1. **箭矢檢查**: `arrow = inv.Arrow(true)` - 是否有箭矢
2. **距離檢查**: `LongAttackCK(target, 12)` - 目標是否在12格範圍內
3. **路徑檢查**: `LongAttackCK` 內部檢查路徑是否被阻擋
4. **目標狀態檢查**: 目標是否死亡、刪除等
5. **傷害計算**: `DmgSystem(target, true, 1)` - 計算傷害（弓箭類型）

### 4.3 魔法攻擊驗證
1. **距離檢查**: `LongAttackCK(target, 12)` - 目標是否在12格範圍內
2. **路徑檢查**: `LongAttackCK` 內部檢查路徑是否被阻擋
3. **目標狀態檢查**: 目標是否死亡、鎖定等
4. **攻擊區域檢查**: `WorldMap.getInstance().AttackZone(this.operator, o)` - 是否在攻擊區域內
5. **傷害計算**: `Damage(o, true)` - 計算傷害（考慮MR減傷等）

## 5. 關鍵發現

### 5.1 攻擊範圍硬編碼
- **近戰**: `Areaatk = 2` (硬編碼在 `PcInstance.Attack`)
- **弓箭**: `Areaatk = 12` (硬編碼在 `PcInstance.AttackBow`)
- **魔法**: `12格` (硬編碼在 `Magic.Damage`，使用 `LongAttackCK(target, 12)`)
- **怪物**: `Areaatk` 從數據庫讀取 (`Monster.getAreaatk()`)

### 5.2 服務器驗證邏輯
- **服務器檢查的是**: 目標座標 `(x, y)` 是否在玩家當前座標的攻擊範圍內
- **關鍵**: 客戶端發送的 `(x, y)` 是**目標的座標**，不是玩家的座標
- **問題**: 如果目標移動了，客戶端發送的目標座標可能過時，導致服務器判定攻擊miss

### 5.3 服務器響應包內容
- **S_ObjectAttack** 包含:
  - `damage`: 傷害值（0表示miss）
  - `targetX, targetY`: **服務器實際的目標座標**（用於同步）
  - `attackerX, attackerY`: 攻擊者座標（服務器確認的）

### 5.4 客戶端應該做的
1. **發送攻擊包前**: 確保目標座標是最新的（從 `_entities` 獲取）
2. **收到攻擊響應後**: 使用服務器返回的 `targetX, targetY` 同步目標位置
3. **如果 damage = 0**: 表示攻擊miss，可能是目標座標過時或目標不在攻擊範圍內

## 6. 問題根源

### 6.1 攻擊miss的原因
1. **目標座標過時**: 客戶端發送的目標座標與服務器實際座標不一致
2. **目標不在攻擊範圍內**: 目標移動了，不在攻擊範圍內
3. **路徑被阻擋**: 弓箭/魔法攻擊路徑被障礙物阻擋
4. **目標狀態異常**: 目標已死亡、刪除等

### 6.2 解決方案
1. **發送攻擊包前**: 重新從 `_entities` 獲取目標實體，確保使用最新的目標座標
2. **收到攻擊響應後**: 使用服務器返回的 `targetX, targetY` 同步目標位置
3. **如果目標不在 `_entities` 中**: 不發送攻擊包，等待服務器同步
