# GameWorld 系統重構方案

## 一、服務器邏輯分析

### 1.1 攻擊系統（服務器端）

#### 近戰攻擊流程
1. **客戶端發包**：`C_Attack` (objid, locx, locy)
   - `locx, locy`：目標座標（客戶端發送時目標的座標）
2. **服務器處理**：`PcInstance.Attack(target, x, y, action, effectId)`
   - 檢查距離：`getDistance(x, y, target.getMap(), this.Areaatk)`
     - `x, y`：攻擊包中的目標座標
     - `this.Areaatk`：近戰=2，遠程=12
     - `getDistance`：使用 **Euclidean distance** (`Math.sqrt(dx*dx + dy*dy)`)
   - 如果距離 <= Areaatk，計算傷害
   - 發送 `S_ObjectAttack` (Opcode 35)

#### 遠程攻擊流程
1. **客戶端發包**：`C_AttackBow` (objid, locx, locy)
2. **服務器處理**：`PcInstance.AttackBow(target, x, y, action, effectId, arrow)`
   - `this.Areaatk = 12`（遠程攻擊範圍）
   - 檢查距離：`getDistance(x, y, target.getMap(), this.Areaatk)`
   - 發送 `S_ObjectAttack` (Opcode 35, bow=true)

#### 魔法攻擊流程
1. **客戶端發包**：`C_Magic` (lv, no, id)
2. **服務器處理**：`PcSkill.toMagic(lv, no, id)`
   - 發送 `S_ObjectAttackMagic` (Opcode 35 或 57)

### 1.2 移動系統（服務器端）

1. **客戶端發包**：`C_Moving` (x, y, h)
2. **服務器處理**：`PcInstance.toMove(x, y, h)`
3. **服務器發包**：`S_ObjectMoving` (objId, x, y, heading)
   - `x, y`：移動後的座標（根據 heading 計算）

### 1.3 關鍵發現

1. **距離計算**：服務器使用 **Euclidean distance** (`Math.sqrt(dx*dx + dy*dy)`)
   - 客戶端使用 **Chebyshev distance** (`Math.Max(|dx|, |dy|)`)
   - **不一致**：導致客戶端認為在範圍內，服務器認為超出範圍

2. **攻擊距離檢查**：服務器檢查的是**攻擊包中的目標座標**與**玩家當前座標**的距離
   - 如果玩家在發送攻擊包後移動，服務器使用**攻擊包發送時的目標座標**進行檢查
   - 這可能導致距離檢查失敗

3. **移動包時序**：服務器可能先發送移動包，然後才發送創建包
   - 需要緩存機制處理時序問題

## 二、重構目標

### 2.1 核心原則

1. **服務器權威**：服務器是唯一權威，客戶端必須無條件信任服務器
2. **客戶端預測**：客戶端可以預測，但必須在收到服務器確認後同步
3. **距離計算對齊**：客戶端必須使用與服務器相同的距離計算方法
4. **時序處理**：處理封包時序問題，確保邏輯正確

### 2.2 重構範圍

1. **距離計算系統**：統一使用 Euclidean distance
2. **攻擊系統**：對齊服務器邏輯，確保距離檢查一致
3. **移動系統**：簡化邏輯，去除重複代碼
4. **封包處理**：統一處理，避免重複邏輯

## 三、重構方案

### 3.1 距離計算統一

**問題**：客戶端使用 Chebyshev distance，服務器使用 Euclidean distance

**解決方案**：
- 創建統一的距離計算函數，使用 Euclidean distance
- 所有距離檢查都使用統一的函數

### 3.2 攻擊系統重構

**問題**：
1. 客戶端距離檢查與服務器不一致
2. 攻擊時使用錯誤的座標
3. 距離判斷邏輯複雜且重複

**解決方案**：
1. 統一距離計算方法
2. 簡化攻擊邏輯，去除重複代碼
3. 確保攻擊時使用正確的座標

### 3.3 移動系統重構

**問題**：
1. 移動包時序問題
2. 座標同步邏輯複雜
3. 重複的座標更新代碼

**解決方案**：
1. 實現移動包緩存機制（已完成）
2. 簡化座標同步邏輯
3. 統一位置更新函數

## 四、實施步驟

1. **階段1**：統一距離計算方法
2. **階段2**：重構攻擊系統
3. **階段3**：簡化移動系統
4. **階段4**：清理重複代碼
5. **階段5**：測試與優化
