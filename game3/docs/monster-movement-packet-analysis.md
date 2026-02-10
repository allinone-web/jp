# 怪物移動包發送機制分析

## 問題描述

當玩家快速移動逃離戰場時，怪物停在原地不再移動，只有當玩家回到2格內才開始移動。

## 服務器移動包發送機制

### 1. 移動包發送流程

```
怪物移動 → StartMove() → toMove() → updateObject() → 發送 S_ObjectMoving 給玩家
```

**關鍵代碼位置**：
- `NpcInstance.StartMove()` (line 249-299): 計算路徑並調用 `toMove()`
- `L1Object.toMove()` (line 672-688): 更新位置並調用 `updateObject()`
- `L1Object.updateObject()` (line 803-851): 遍歷 `getWorldList()`，發送移動包

### 2. updateObject() 的發送邏輯

```java
// L1Object.java line 816-819
else if (getDistance(o.getX(), o.getY(), o.getMap(), 14)) {
    if (containsObject(o)) {
        if ((o instanceof PcInstance))
            o.SendPacket(new S_ObjectMoving(this));
    }
}
```

**關鍵點**：
- 只有當對象在 **14格內** 且 `containsObject(o)` 為 true 時，才發送移動包
- 移動包發送給 `PcInstance`（玩家）

### 3. 怪物AI執行機制

**MonAi 線程** (每30ms執行一次)：
```java
// MonAi.java line 72-88
if (mon.isAi(time)) {
    mon.isStatus(time);
    if (mon.isDead())
        mon.toDead(time);
    else if ((mon.isFight()) && (!mon.isRecess())) {
        mon.toFight(time);  // 戰鬥狀態
    }
    else
        mon.toWalk(time);   // 行走狀態
}
```

**toFight() 邏輯**：
```java
// MonsterInstance.java line 283-293
if ((getDistance(cha.getX(), cha.getY(), cha.getMap(), this.Areaatk)) && (LongAttackCK(cha, this.Areaatk))) {
    // 在攻擊範圍內，攻擊
    Attack(cha, cha.getX(), cha.getY(), getGfxMode() + 1, 0);
    this.ai_time = getMon().getModespeed(getGfxMode() + 1);  // 設置攻擊冷卻（例如 640ms）
} else {
    // 不在攻擊範圍內，移動
    StartMove(cha.getX(), cha.getY());
    this.ai_time = getMon().getModespeed(getGfxMode());  // 設置移動冷卻（例如 800ms）
}
```

## 問題根源

### 問題1：攻擊冷卻期間無法移動

**場景**：
1. 怪物攻擊玩家（`ai_time = 640ms`）
2. 玩家快速移動逃離（640ms內移動多格）
3. 怪物在攻擊冷卻中（`isAi(time)` 返回 false），`toFight()` 不會執行
4. 怪物停在原地，不會追擊

**原因**：
- `ai_time` 冷卻期間，`isAi(time)` 返回 false
- `toFight()` 不會被執行，因此不會調用 `StartMove()`
- 即使玩家移動觸發了 `updateObject()`，怪物也不會移動（因為不在AI執行時間）

### 問題2：移動包發送依賴玩家移動

**場景**：
1. 玩家停止移動
2. 怪物在移動（但玩家不在14格內，或 `containsObject()` 為 false）
3. 怪物移動時調用 `updateObject()`，但不會發送移動包給玩家

**原因**：
- `updateObject()` 只在以下情況被調用：
  - 怪物自己移動時（`toMove()`）
  - 玩家移動時（`toMove()`）
- 如果玩家不移動，即使怪物在移動，也不會發送移動包

### 問題3：距離檢查導致移動包丟失

**場景**：
1. 玩家快速移動逃離（例如從2格移動到5格）
2. 怪物在攻擊冷卻中，不會立即追擊
3. 當怪物冷卻結束開始追擊時，玩家已經遠離
4. 如果玩家不在14格內，`updateObject()` 不會發送移動包

**原因**：
- `updateObject()` 檢查 `getDistance(o.getX(), o.getY(), o.getMap(), 14)`
- 如果玩家不在14格內，不會發送移動包

## 解決方案

### 方案1：客戶端預測怪物移動（推薦）

**思路**：
- 當收到攻擊包（Op35）但距離 > 攻擊範圍時，客戶端預測怪物會移動
- 客戶端主動更新怪物位置（朝向目標移動）
- 等待服務器確認移動包

**實現**：
```csharp
// 在 OnObjectAttacked 中
if (damage > 0 && dist > attackRange) {
    // 怪物應該移動，但服務器還沒發送移動包
    // 客戶端預測：怪物會朝向目標移動
    int predictedX = attacker.MapX;
    int predictedY = attacker.MapY;
    // 計算朝向目標的方向
    int heading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
    // 預測下一步位置
    // ... 計算下一步位置
    // 設置 walk 動作
    attacker.SetAction(ACT_WALK);
}
```

### 方案2：定期請求怪物位置更新

**思路**：
- 客戶端定期（例如每640ms）發送位置更新請求
- 服務器返回附近怪物的位置

**問題**：
- 需要修改服務器代碼
- 增加網絡負擔

### 方案3：優化攻擊冷卻邏輯

**思路**：
- 如果目標距離 > 攻擊範圍，即使攻擊冷卻未結束，也應該移動
- 需要修改服務器代碼

## 當前客戶端修復

### 已修復的問題

1. **近戰攻擊動畫範圍**：從2格改為1格
2. **實體位置更新時動作設置**：如果位置改變，強制設置 walk 動作
3. **撤回 CleanupDistantEntities 修改**：恢復原邏輯

### 待修復的問題

1. **怪物攻擊後不會移動**：需要客戶端預測或服務器修復
2. **移動包間隔太久**：需要定期請求或服務器主動發送

## 結論

**根本原因**：
- 服務器的移動包發送機制依賴玩家移動或怪物移動時調用 `updateObject()`
- 怪物攻擊冷卻期間不會執行 `toFight()`，因此不會追擊
- 當玩家快速逃離時，怪物在冷卻中，不會立即追擊

**最佳解決方案**：
- 客戶端預測怪物移動（當距離 > 攻擊範圍時）
- 等待服務器確認移動包
- 如果服務器移動包與預測不一致，使用服務器數據
