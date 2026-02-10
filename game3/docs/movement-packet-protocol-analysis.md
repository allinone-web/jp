# 移動包協議分析與故障診斷

## 問題描述

**現象**：
- 玩家移動2-3秒（3-4格）後，被突然拉回
- 客戶端發送了多個移動包，但沒有收到服務器確認（`S_ObjectMoving`）

**日誌證據**：
```
[Move-Packet] Sending C_MoveChar(10) -> From:(33195,33443) To:(33194,33444) Heading:5 ServerConfirmed:(33195,33443)
[Move-Packet] Sending C_MoveChar(10) -> From:(33194,33444) To:(33193,33445) Heading:5 ServerConfirmed:(33195,33443)
[Pos-Sync-Fix] Server confirmed too old! Using client prediction. Server:(33195,33443) Client:(33192,33446)
```

**關鍵發現**：
- 客戶端發送了多個移動包，但 `ServerConfirmed` 一直停留在 `(33195,33443)`
- 這表示服務器沒有發送 `S_ObjectMoving` 確認包

## 服務器移動處理邏輯

### 1. C_Moving (Opcode 10) 協議

**服務器期望** (`C_Moving.java`):
```java
this.x = readH();      // 目標 X 座標
this.y = readH();      // 目標 Y 座標
this.h = readC();      // 朝向
pc.toMove(this.x, this.y, this.h);
```

### 2. PcInstance.toMove() 處理流程

```java
public void toMove(int x, int y, int h)
{
    // 1. 速度檢查
    if (Config.CHECK_SPEED_TYPE == 2)
        getCheckSped().checkInterval(CheckSpeed.ACT_TYPE.MOVE);
    else
        this.move.check();
    
    // 2. 距離檢查（必須 <= 1 格）
    if (getDistance(x, y, getMap(), 1)) {
        // 3. 根據 heading 調整座標
        switch (h) {
        case 0: y--; break;
        case 1: x++; y--; break;
        case 2: x++; break;
        case 3: x++; y++; break;
        case 4: y++; break;
        case 5: x--; y++; break;
        case 6: x--; break;
        case 7: x--; y--; break;
        }
        
        // 4. 調用父類 toMove
        super.toMove(x, y, h);
    }
}
```

**關鍵發現**：
- 服務器會根據 `heading` **調整**客戶端發送的座標
- 例如：如果客戶端發送 `(x=33194, y=33444, heading=5)`，服務器會調整為 `(x=33193, y=33445)`
- 這表示服務器期望客戶端發送的是**當前座標**，然後根據 heading 計算下一步

### 3. L1Object.toMove() 處理流程

```java
public void toMove(int x, int y, int h) {
    setMove(true);
    setHeading(h);
    setX(x);  // 設置為調整後的座標
    setY(y);
    
    if (!getDistance(getTempX(), getTempY(), getMap(), 12)) {
        setTempX(x);
        setTempY(y);
        updateWorld();
    }
    updateObject();  // 這裡會發送 S_ObjectMoving
}
```

### 4. updateObject() 發送確認

```java
public void updateObject()
{
    for (L1Object o : getWorldList()) {
        if (getDistance(o.getX(), o.getY(), o.getMap(), 14)) {
            if (containsObject(o)) {
                if ((o instanceof PcInstance))
                    o.SendPacket(new S_ObjectMoving(this));  // 發送確認
            }
        }
    }
}
```

**關鍵發現**：
- `S_ObjectMoving` 包含的是**下一步**座標（根據當前座標和 heading 計算）
- 只有在 `toMove()` 成功執行後，才會發送確認

## 問題根源分析

### 可能原因1：速度檢查失敗

**服務器速度檢查**：
- `CheckSpeed.checkInterval(ACT_TYPE.MOVE)` 會檢查移動間隔
- **重要**：服務器**沒有**640ms心跳機制，只檢查移動速度（間隔時間）
- 如果移動太快（間隔 < `getRightInterval(MOVE)`），會：
  - 增加 `_injusticeCount`
  - 如果 `_injusticeCount >= 10`，會觸發懲罰（凍結、傳送回起點、踢下線）

**客戶端移動間隔**：
- 從 `SprDataTable` 獲取移動間隔（約 600ms）
- 如果客戶端發送移動包的頻率太快，服務器會拒絕處理

### 可能原因2：距離檢查失敗

**服務器距離檢查**：
- `getDistance(x, y, getMap(), 1)` 檢查目標座標與當前座標的距離
- 如果距離 > 1 格，不會處理移動

**客戶端發送的座標**：
- 客戶端發送的是 `(nextX, nextY)`，這是**目標座標**
- 但服務器會根據 `heading` 調整座標
- 如果調整後的座標與服務器當前座標距離 > 1 格，會失敗

### 可能原因3：座標計算不一致

**服務器座標調整邏輯**：
```java
switch (h) {
case 0: y--; break;      // N
case 1: x++; y--; break; // NE
case 2: x++; break;      // E
case 3: x++; y++; break; // SE
case 4: y++; break;      // S
case 5: x--; y++; break; // SW
case 6: x--; break;      // W
case 7: x--; y--; break; // NW
}
```

**客戶端座標計算**：
- 客戶端計算 `nextX = cx + stepX`, `nextY = cy + stepY`
- 然後計算 `heading = GetHeading(cx, cy, nextX, nextY, heading)`
- 發送 `(nextX, nextY, heading)`

**問題**：
- 如果客戶端計算的 `heading` 與服務器期望的不一致
- 或者客戶端發送的座標與服務器根據 heading 調整後的座標不一致
- 服務器可能會拒絕處理移動

## 解決方案

### 方案1：確保移動間隔符合服務器要求

**檢查點**：
1. 客戶端移動間隔必須 >= 服務器要求的間隔
2. 考慮 Haste/Slow 的影響
3. 不要發送移動包太快

### 方案2：確保座標與 heading 一致

**檢查點**：
1. 客戶端發送的座標必須與 heading 一致
2. 服務器會根據 heading 調整座標，確保調整後的座標是合理的

### 方案3：添加診斷日誌

**需要記錄**：
1. 客戶端發送的移動包內容
2. 服務器是否處理了移動包（需要服務器日誌）
3. 如果服務器拒絕處理，原因是什麼（速度檢查失敗？距離檢查失敗？）

## 建議的修復步驟

1. **檢查客戶端移動間隔**：確保符合服務器要求
2. **驗證座標與 heading 的一致性**：確保客戶端發送的座標與 heading 匹配
3. **添加服務器端日誌**：記錄移動包處理結果
4. **處理服務器拒絕的情況**：如果服務器拒絕處理，客戶端應該如何響應
