# 攻擊座標驗證邏輯分析

## 問題描述
客戶端攻擊怪物時，攻擊都是miss（damage=0），怪物攻擊玩家時，攻擊不在玩家角色座標。

## 服務器攻擊驗證邏輯

### 1. C_Attack 包結構
```java
// server/network/client/C_Attack.java
this.objid = readD();    // 目標ID
this.locx = readH();     // 目標X座標
this.locy = readH();     // 目標Y座標
pc.Attack(pc.getObject(this.objid), this.locx, this.locy, 1, 0);
```

### 2. PcInstance.Attack 驗證邏輯
```java
// server/world/instance/PcInstance.java:619
if ((target != null) && (getDistance(x, y, target.getMap(), this.Areaatk)) && ...)
```

**關鍵發現**：
- `x, y` 是客戶端發送的 `locx, locy`（目標座標）
- `getDistance(x, y, target.getMap(), this.Areaatk)` 的實現：
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

**驗證邏輯**：
- 服務器檢查的是：**目標座標 `(x, y)` 是否在玩家當前座標 `getX(), getY()` 的 `Areaatk` 範圍內**
- `Areaatk = 2`，表示攻擊範圍是2格

### 3. 怪物攻擊邏輯
```java
// server/world/instance/MonsterInstance.java:287
Attack(cha, cha.getX(), cha.getY(), getGfxMode() + 1, 0);
```

**關鍵發現**：
- 怪物攻擊時，使用的是**目標的實際座標** `cha.getX(), cha.getY()`
- 服務器會檢查：目標座標是否在怪物當前座標的攻擊範圍內

## 問題根源

### 客戶端攻擊座標問題
1. **客戶端發送的目標座標可能過時**：
   - 客戶端發送的是 `target.MapX, target.MapY`
   - 如果目標移動了，客戶端可能還在使用舊的座標
   - 服務器檢查時，目標座標不在玩家當前座標的2格範圍內，導致攻擊miss

2. **客戶端座標更新延遲**：
   - 客戶端依賴服務器發送的 `S_ObjectMoving` 包更新目標座標
   - 如果服務器沒有及時發送移動包，客戶端使用的目標座標可能過時

### 解決方案

#### 方案1：使用目標的實際座標（推薦）
客戶端應該使用**目標的實際座標**，而不是客戶端預測的座標。

**實現**：
- 客戶端發送攻擊包時，使用 `target.MapX, target.MapY`（目標的實際座標）
- 確保目標座標是從服務器最新更新的，而不是客戶端預測的

#### 方案2：服務器使用目標實際座標驗證（不推薦，需要修改服務器）
服務器應該使用**目標的實際座標**進行驗證，而不是客戶端發送的座標。

**實現**：
- 修改 `PcInstance.Attack`，使用 `target.getX(), target.getY()` 進行驗證
- 但這需要修改服務器代碼，不符合用戶要求

## 當前客戶端實現分析

### 客戶端發送攻擊包的邏輯
```csharp
// Client/Game/GameWorld.Combat.cs:408-410
int tx = target.MapX;
int ty = target.MapY;
if (profile.UsePredictShot) (tx, ty) = PredictRangedTarget(target);
```

**問題**：
- 客戶端使用的是 `target.MapX, target.MapY`（目標的實際座標）
- 但如果目標移動了，客戶端可能還沒有收到服務器的移動包，導致使用的座標過時

### 客戶端座標更新邏輯
```csharp
// Client/Game/GameWorld.Combat.cs:847-901
private void OnObjectMoved(int objectId, int x, int y, int heading)
{
  if (_entities.ContainsKey(objectId))
  {
    _entities[objectId].SetMapPosition(x, y, heading);
  }
}
```

**問題**：
- 客戶端依賴服務器發送的 `S_ObjectMoving` 包更新目標座標
- 如果服務器沒有及時發送移動包，客戶端使用的目標座標可能過時

## 根本原因

**客戶端發送的目標座標與服務器記錄的目標實際座標不同步**：
1. 目標移動了，但客戶端還沒有收到服務器的移動包
2. 客戶端使用過時的目標座標發送攻擊包
3. 服務器檢查時，目標座標不在玩家當前座標的2格範圍內，導致攻擊miss

## 解決方案

### 方案1：確保目標座標是最新的（推薦）
在發送攻擊包前，檢查目標座標是否是最新的：
- 如果目標最近移動過，等待服務器確認後再攻擊
- 或者，使用目標的實際座標（從服務器最新更新的）

### 方案2：攻擊前重新獲取目標座標
在發送攻擊包前，重新獲取目標的實際座標：
- 從 `_entities` 中獲取目標實體
- 使用 `target.MapX, target.MapY`（從服務器最新更新的）

### 方案3：攻擊時使用目標的實際座標
確保攻擊時使用的目標座標是從服務器最新更新的：
- 檢查目標是否在視野範圍內
- 如果目標不在視野範圍內，不發送攻擊包
- 如果目標在視野範圍內，使用目標的實際座標

## 關鍵代碼位置

1. **客戶端發送攻擊包**：`Client/Game/GameWorld.Combat.cs:705-715`
2. **服務器驗證攻擊**：`server/world/instance/PcInstance.java:619`
3. **服務器距離檢查**：`server/world/object/L1Object.java:646-657`
4. **客戶端座標更新**：`Client/Game/GameWorld.Combat.cs:847-901`

## 診斷建議

1. **添加日誌**：記錄客戶端發送的目標座標和服務器驗證時的目標座標
2. **檢查座標同步**：確保目標座標是從服務器最新更新的
3. **檢查攻擊範圍**：確保目標在攻擊範圍內（2格）
