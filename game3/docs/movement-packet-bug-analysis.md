# 移動包協議故障分析

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

## 服務器移動處理邏輯分析

### 1. C_Moving (Opcode 10) 協議

**服務器期望** (`C_Moving.java`):
```java
this.x = readH();      // 目標 X 座標
this.y = readH();      // 目標 Y 座標
this.h = readC();      // 朝向
pc.toMove(this.x, this.y, this.h);
```

**客戶端發送** (`C_MoveCharPacket.cs`):
```csharp
writer.WriteByte(10);       // Opcode: 10
writer.WriteUShort(x);       // 下一步的 X 座標
writer.WriteUShort(y);       // 下一步的 Y 座標
writer.WriteByte(heading);   // 朝向 (0-7)
```

**協議對齊**：✅ 正確

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
        case 0: y--; break;      // N
        case 1: x++; y--; break; // NE
        case 2: x++; break;      // E
        case 3: x++; y++; break; // SE
        case 4: y++; break;      // S
        case 5: x--; y++; break; // SW
        case 6: x--; break;      // W
        case 7: x--; y--; break; // NW
        }
        
        // 4. 調用父類 toMove
        super.toMove(x, y, h);
    }
}
```

**關鍵發現**：
1. **速度檢查**：如果移動太快，會增加 `_injusticeCount`，如果 >= 10 會觸發懲罰
2. **距離檢查**：`getDistance(x, y, getMap(), 1)` 檢查目標座標與當前座標的距離，必須 <= 1 格
3. **座標調整**：服務器會根據 `heading` **調整**客戶端發送的座標
4. **如果任何檢查失敗，不會調用 `super.toMove()`，也不會發送 `S_ObjectMoving`**

### 3. 服務器速度檢查邏輯

**CheckSpeed.getRightInterval(ACT_TYPE.MOVE)**:
```java
case MOVE:
    interval = SprTable.getInstance().getAttackSpeed(this._pc.getGfx(), this._pc.getGfxMode() + 1);
    break;
```

**注意**：服務器使用 `getAttackSpeed` 來檢查移動速度，這可能是代碼錯誤或特殊設計。

**速度檢查邏輯**：
```java
long interval = now - ((Long)this._actTimers.get(type)).longValue();
int rightInterval = getRightInterval(type);
interval = ()(interval * ((Config.CHECK_STRICTNESS - 5) / 100.0D));

if ((0L < interval) && (interval < rightInterval)) {
    // 移動太快，增加 injusticeCount
    this._injusticeCount += 1;
    if (this._injusticeCount >= 10) {
        doPunishment(type, Config.PUNISHMENT);  // 懲罰：凍結、傳送回起點、踢下線
        return 2;
    }
    return 1;  // 檢查失敗，但不懲罰
}
```

**關鍵**：如果 `checkInterval()` 返回 1 或 2，`toMove()` 可能不會處理移動。

### 4. 服務器距離檢查邏輯

**L1Object.getDistance(int tx, int ty, int tm, int loc)**:
```java
public boolean getDistance(int tx, int ty, int tm, int loc) {
    long dx = tx - getX();
    long dy = ty - getY();
    double distance = Math.sqrt(dx * dx + dy * dy);
    if (loc < (int)distance) {
        return false;  // 距離 > loc，返回 false
    }
    if (getMap() != tm) {
        return false;
    }
    return true;
}
```

**關鍵**：
- 服務器檢查的是客戶端發送的座標 `(x, y)` 與服務器當前座標的距離
- 如果距離 > 1 格，返回 `false`，不會處理移動

## 問題根源分析

### 可能原因1：速度檢查失敗

**分析**：
- 客戶端移動間隔可能不符合服務器要求
- 服務器使用 `getAttackSpeed(gfx, gfxMode + 1)` 來檢查移動速度
- 如果客戶端發送移動包太快，服務器會拒絕處理

**檢查點**：
- 客戶端移動間隔是否 >= 服務器要求的間隔？
- 是否考慮了 Haste/Slow 的影響？

### 可能原因2：距離檢查失敗

**分析**：
- 客戶端發送的是 `(nextX, nextY)`，這是目標座標
- 服務器檢查的是目標座標與服務器當前座標的距離
- 如果客戶端預測的座標與服務器實際座標不一致，距離可能 > 1 格

**檢查點**：
- 客戶端發送的座標是否與服務器當前座標距離 <= 1 格？
- 如果服務器沒有確認之前的移動，客戶端預測可能已經偏離

### 可能原因3：座標調整後不一致

**分析**：
- 服務器會根據 `heading` 調整客戶端發送的座標
- 如果調整後的座標與服務器當前座標距離 > 1 格，會失敗

**檢查點**：
- 客戶端計算的 `heading` 是否正確？
- 服務器調整後的座標是否合理？

## 解決方案

### 方案1：確保移動間隔符合服務器要求

**需要檢查**：
1. 客戶端移動間隔必須 >= 服務器要求的間隔
2. 服務器使用 `getAttackSpeed(gfx, gfxMode + 1)` 來檢查移動速度
3. 考慮 Haste/Slow 的影響

### 方案2：確保座標與服務器一致

**需要檢查**：
1. 客戶端發送的座標必須與服務器當前座標距離 <= 1 格
2. 如果服務器沒有確認之前的移動，客戶端應該等待確認
3. 不要發送多個未確認的移動包

### 方案3：添加診斷和重試機制

**需要實現**：
1. 記錄每次移動包的發送時間和內容
2. 如果長時間沒有收到確認，記錄警告
3. 如果服務器拒絕處理，客戶端應該如何響應
