# 移動邏輯完整文檔

## 目錄
1. [座標系統](#座標系統)
2. [移動間隔計算](#移動間隔計算)
3. [移動執行邏輯](#移動執行邏輯)
4. [朝向計算](#朝向計算)
5. [加速與減速](#加速與減速)
6. [數據來源](#數據來源)
7. [關鍵參數](#關鍵參數)
8. [公式總結](#公式總結)

---

## 座標系統

### 1.1 座標類型

#### 網格座標 (Grid Coordinates)
- **定義**：`MapX`, `MapY` (int)
- **單位**：格 (Cell)
- **範圍**：整數值，對應地圖上的網格位置
- **用途**：邏輯計算、網絡協議、服務器通信

#### 世界座標 (World Coordinates)
- **定義**：`Position` (Vector2)
- **單位**：像素 (Pixel)
- **範圍**：浮點數值，對應畫面顯示位置
- **用途**：視覺渲染、平滑移動動畫

### 1.2 座標轉換公式

#### 網格 → 世界座標
```csharp
// 文件：Client/Game/GameEntity.Movement.cs
// 方法：SetMapPosition()

Vector2I origin = GameWorld.CurrentMapOrigin;  // 當前地圖原點（網格座標）
float localX = (x - origin.X + 0.5f) * GRID_SIZE;
float localY = (y - origin.Y + 0.5f) * GRID_SIZE;
Vector2 targetPos = new Vector2(localX, localY);
```

**公式說明**：
- `(x - origin.X + 0.5f)`：計算相對於地圖原點的網格偏移，`+0.5f` 表示網格中心點
- `* GRID_SIZE`：將網格單位轉換為像素單位
- **結果**：角色顯示在網格的中心位置（16, 16）

#### 關鍵常數
```csharp
private const int GRID_SIZE = 32;  // 每格 = 32 像素
private const float TELEPORT_THRESHOLD = 256.0f;  // 瞬移閾值（8格）
```

---

## 移動間隔計算

### 2.1 計算方法（雙重約定）

移動間隔的計算遵循**雙重約定**，確保視覺動畫與邏輯移動完全同步。

#### 方法 1：從 `list.spr` 計算（精確，優先使用）

**文件**：`Client/Game/GameEntity.Movement.cs`  
**方法**：`CalculateWalkDuration()`

```csharp
// 步驟 1：獲取角色的 list.spr 定義
var def = Client.Utility.ListSprLoader.Get(GfxId);

// 步驟 2：獲取 walk 動作序列
var walkSeq = Client.Utility.ListSprLoader.GetActionSequence(def, ACT_WALK);

// 步驟 3：計算所有幀的總時長
float totalDuration = 0.0f;
foreach (var frame in walkSeq.Frames)
{
    totalDuration += frame.RealDuration;
}
```

**關鍵公式**：
```csharp
// 文件：Client/Utility/ListSprLoader.cs
// 方法：ParseFrameToken()

// 單幀時長計算
RealDuration = (DurationUnit * 40.0f) / 1000.0f;  // 秒

// 總時長 = 所有幀的 RealDuration 之和
TotalDuration = Σ(RealDuration)  // 所有 walk 動作幀的時長總和
```

**公式說明**：
- `DurationUnit`：`list.spr` 文件中定義的時間單位（例如 `24.0:4` 中的 `4`）
- `40ms`：Lineage 原版的基準時間單位（與服務器完全一致）
- `RealDuration`：單幀的實際播放時長（秒）
- **總時長**：所有 walk 動作幀的 `RealDuration` 之和，即完整的移動動畫時長

**示例**：
```
假設 walk 動作有 4 幀，每幀 DurationUnit = 4：
- 單幀時長 = 4 * 40ms = 160ms = 0.16秒
- 總時長 = 4 * 0.16秒 = 0.64秒
```

#### 方法 2：從 `SprDataTable` 獲取（回退方案）

**文件**：`Client/Data/SprDataTable.cs`  
**方法**：`GetInterval(ActionType.Move, gfxId, 0)`

```csharp
// 從服務器數據庫提取的移動間隔（毫秒）
float interval = SprDataTable.GetInterval(ActionType.Move, GfxId, 0) / 1000.0f;
```

**數據來源**：
- 從 `server/datebase_182_2026-01-21.sql` 提取
- 格式：`(gfxId, actionId, interval_ms)`
- 例如：`(0, 0, 640)` 表示 gfxId=0 的 actionId=0（walk）間隔為 640ms

**回退邏輯**：
```csharp
// 如果 list.spr 計算失敗，使用 SprDataTable
if (totalDuration > 0)
    return totalDuration;
else
    return SprDataTable.GetInterval(ActionType.Move, GfxId, 0) / 1000.0f;
```

#### 最終回退值
```csharp
return interval > 0 ? interval : 0.6f;  // 如果所有方法都失敗，使用 0.6 秒
```

---

## 移動執行邏輯

### 3.1 主循環更新

**文件**：`Client/Game/GameWorld.Movement.cs`  
**方法**：`UpdateMovementLogic(double delta)`

```csharp
// 1. 每幀重置到達信號
_hasArrivedThisFrame = false;

// 2. 基礎檢查
if (!_isAutoWalking || _myPlayer == null) return;
if (GameEntity.DamageStiffnessBlocksMovement && _myPlayer.IsInDamageStiffness) return;

// 3. 獲取移動間隔（從 SprDataTable）
float baseInterval = SprDataTable.GetInterval(ActionType.Move, _myPlayer.GfxId, 0) / 1000.0f;

// 4. 應用加速邏輯（綠水）
_moveInterval = baseInterval;
if (_myPlayer.AnimationSpeed > 1.0f) 
{
    _moveInterval *= 0.75f;  // 加速：間隔縮放為 0.75
}

// 5. 累積計時器
_moveTimer += (float)delta;

// 6. 檢查是否達到移動間隔
if (_moveTimer < _moveInterval) return;

// 7. 執行一步移動
if (_targetMapX != 0 && _targetMapY != 0)
{
    StepTowardsTarget(_targetMapX, _targetMapY);
}
```

### 3.2 單步移動執行

**文件**：`Client/Game/GameWorld.Movement.cs`  
**方法**：`StepTowardsTarget(int tx, int ty)`

```csharp
// 1. 計算距離向量
int dx = tx - cx;
int dy = ty - cy;

// 2. 如果已到達，停止移動
if (dx == 0 && dy == 0)
{
    StopWalking();
    _hasArrivedThisFrame = true;
    return;
}

// 3. 【核心算法】鉗制位移量，確保只走相鄰格子
int stepX = Math.Clamp(dx, -1, 1);
int stepY = Math.Clamp(dy, -1, 1);

// 4. 計算下一步的絕對座標
int nextX = cx + stepX;
int nextY = cy + stepY;

// 5. 計算朝向
int heading = GetHeading(cx, cy, nextX, nextY, _myPlayer.Heading);

// 6. 發送移動包給服務器
_netSession.Send(C_MoveCharPacket.Make(nextX, nextY, heading));

// 7. 本地立即更新座標（平滑移動）
_myPlayer.SetMapPosition(nextX, nextY, heading);

// 8. 重置計時器
_moveTimer = 0;
```

**關鍵規則**：
- **每步移動固定 1 格**（32 像素），無論動畫時長是多少
- **移動距離** = `GRID_SIZE` (32 像素)
- **移動方向**：8 方向（N, NE, E, SE, S, SW, W, NW）

### 3.3 平滑移動動畫

**文件**：`Client/Game/GameEntity.Movement.cs`  
**方法**：`SetMapPosition(int x, int y, int h = 0)`

```csharp
// 1. 計算目標世界座標
Vector2I origin = GameWorld.CurrentMapOrigin;
float localX = (x - origin.X + 0.5f) * GRID_SIZE;
float localY = (y - origin.Y + 0.5f) * GRID_SIZE;
Vector2 targetPos = new Vector2(localX, localY);

// 2. 計算距離，判斷是否需要平滑移動
float dist = Position.DistanceTo(targetPos);
bool isSmoothMove = dist > 0 && dist < TELEPORT_THRESHOLD;  // 256 像素（8 格）

// 3. 平滑移動：使用 Tween 動畫
if (isSmoothMove)
{
    float moveDuration = CalculateWalkDuration();  // 使用動態計算的動畫時長
    SetAction(ACT_WALK);
    _moveTween = CreateTween();
    _moveTween.TweenProperty(this, "position", targetPos, moveDuration)
        .SetTrans(Tween.TransitionType.Linear);
}
else
{
    // 瞬移：直接設置座標
    Position = targetPos;
    if (!_isActionBusy) SetAction(ACT_BREATH);
}
```

**關鍵要點**：
- **動畫時長** = `CalculateWalkDuration()`（從 `list.spr` 計算或從 `SprDataTable` 獲取）
- **移動距離** = 固定 32 像素（1 格）
- **移動速度** = `32 像素 / moveDuration 秒`
- **Tween 類型**：`Linear`（線性插值，確保速度恆定）

---

## 朝向計算

### 4.1 8 方向定義

**文件**：`Client/Game/GameWorld.Movement.cs`  
**方法**：`GetHeading(int fromX, int fromY, int toX, int toY, int defaultHeading)`

```
服務器標準 Heading 定義：
0 = N   (0, -1)  正北
1 = NE  (+1, -1) 東北
2 = E   (+1, 0)  正東
3 = SE  (+1, +1) 東南
4 = S   (0, +1)  正南
5 = SW  (-1, +1) 西南
6 = W   (-1, 0)  正西
7 = NW  (-1, -1) 西北
```

### 4.2 朝向計算算法

```csharp
public static int GetHeading(int fromX, int fromY, int toX, int toY, int defaultHeading)
{
    int dx = toX - fromX;
    int dy = toY - fromY;

    // 優化後的二分查找邏輯
    if (dy < 0)  // 目標在上方
    {
        if (dx > 0) return 1;  // NE
        if (dx < 0) return 7;  // NW
        return 0;               // N
    }
    else if (dy > 0)  // 目標在下方
    {
        if (dx > 0) return 3;  // SE
        if (dx < 0) return 5;  // SW
        return 4;               // S
    }
    else  // dy == 0，目標在同一水平線
    {
        if (dx > 0) return 2;  // E
        if (dx < 0) return 6;  // W
    }

    // 如果 dx=0, dy=0（不移動），返回原本的朝向
    return defaultHeading;
}
```

**使用場景**：
- 移動時計算下一步的朝向
- 攻擊時計算面對目標的朝向
- 確保移動和攻擊使用統一的朝向計算邏輯

---

## 加速與減速

### 5.1 綠水加速（Haste）

**文件**：`Client/Game/GameWorld.Movement.cs`  
**方法**：`UpdateMovementLogic()`

```csharp
// 基礎移動間隔
float baseInterval = SprDataTable.GetInterval(ActionType.Move, _myPlayer.GfxId, 0) / 1000.0f;

// 應用加速邏輯
_moveInterval = baseInterval;
if (_myPlayer.AnimationSpeed > 1.0f) 
{
    _moveInterval *= 0.75f;  // 間隔縮放為 0.75（速度提升 33.3%）
}
```

**公式**：
```
加速後的移動間隔 = 基礎移動間隔 * 0.75
加速後的移動速度 = 基礎移動速度 / 0.75 = 基礎移動速度 * 1.333
```

**數據來源**：
- 根據服務器 `CheckSpeed.java`，加速狀態下間隔縮放為 0.75
- `AnimationSpeed > 1.0f` 表示處於加速狀態

### 5.2 減速（Slow）

**文件**：`Client/Game/SpeedManager.cs`  
**方法**：`CanPerformAction()`

```csharp
if (_isSlow && (type == ActionType.Move || type == ActionType.Attack))
{
    cooldown = (long)(cooldown / 0.75);  // 間隔放大為 1.333 倍（速度降低 25%）
}
```

**公式**：
```
減速後的移動間隔 = 基礎移動間隔 / 0.75 = 基礎移動間隔 * 1.333
減速後的移動速度 = 基礎移動速度 * 0.75
```

---

## 數據來源

### 6.1 list.spr 文件

**位置**：`Client/Utility/ListSprLoader.cs`

**格式**：
```
0.walk(1, 24.0:4 24.1:4 24.2:4 24.3:4)
```

**解析規則**：
- `0.walk`：動作 ID = 0，動作名稱 = walk
- `24.0:4`：動作 ID = 24，幀索引 = 0，時間單位 = 4
- `RealDuration = DurationUnit * 40ms / 1000.0f`

**關鍵屬性**：
- `DurationUnit`：時間單位（例如 `4`）
- `RealDuration`：實際播放時長（秒）
- `FrameIdx`：幀索引（播放順序）
- `IsStepTick`：座標位移點標記（`>` 符號）

### 6.2 SprDataTable（服務器數據庫）

**位置**：`Client/Data/SprDataTable.cs`

**數據來源**：
- 從 `server/datebase_182_2026-01-21.sql` 提取
- 格式：`(gfxId, actionId, interval_ms)`

**示例數據**：
```csharp
(0, 0, 640),   // gfxId=0, actionId=0(walk), interval=640ms
(0, 1, 840),   // gfxId=0, actionId=1(attack), interval=840ms
(1, 0, 640),   // gfxId=1, actionId=0(walk), interval=640ms
```

**回退邏輯**：
```csharp
// 優先使用 list.spr 計算
// 如果失敗，使用 SprDataTable
// 如果仍然失敗，使用默認值 0.6 秒
```

### 6.3 服務器協議

**移動包**：`C_MoveCharPacket` (Opcode 10)

**格式**：
```csharp
_netSession.Send(C_MoveCharPacket.Make(nextX, nextY, heading));
```

**參數**：
- `nextX`：下一步的網格 X 座標
- `nextY`：下一步的網格 Y 座標
- `heading`：朝向（0-7）

---

## 關鍵參數

### 7.1 常數定義

| 參數 | 值 | 單位 | 說明 | 文件位置 |
|------|-----|------|------|----------|
| `GRID_SIZE` | 32 | 像素 | 每格的像素大小 | `GameEntity.Movement.cs` |
| `TELEPORT_THRESHOLD` | 256.0 | 像素 | 瞬移閾值（8 格） | `GameEntity.Movement.cs` |
| `BASE_TIME_UNIT` | 40 | 毫秒 | Lineage 基準時間單位 | `ListSprLoader.cs` |
| `DEFAULT_MOVE_INTERVAL` | 0.6 | 秒 | 默認移動間隔（最終回退值） | `GameEntity.Movement.cs` |
| `HASTE_MULTIPLIER` | 0.75 | 倍數 | 加速倍數（間隔縮放） | `GameWorld.Movement.cs` |
| `SLOW_MULTIPLIER` | 1.333 | 倍數 | 減速倍數（間隔放大） | `SpeedManager.cs` |

### 7.2 狀態變量

| 變量 | 類型 | 說明 | 文件位置 |
|------|------|------|----------|
| `_moveTimer` | `float` | 移動計時器（累積 delta 時間） | `GameWorld.Movement.cs` |
| `_moveInterval` | `float` | 當前移動間隔（秒） | `GameWorld.Movement.cs` |
| `_isAutoWalking` | `bool` | 是否正在自動移動 | `GameWorld.Movement.cs` |
| `_targetMapX` | `int` | 目標網格 X 座標 | `GameWorld.Movement.cs` |
| `_targetMapY` | `int` | 目標網格 Y 座標 | `GameWorld.Movement.cs` |
| `_hasArrivedThisFrame` | `bool` | 本幀是否到達（用於攻擊同步） | `GameWorld.Movement.cs` |
| `_moveTween` | `Tween` | 移動動畫 Tween 對象 | `GameEntity.Movement.cs` |

---

## 公式總結

### 8.1 座標轉換

```
世界座標 X = (網格 X - 地圖原點 X + 0.5) * 32
世界座標 Y = (網格 Y - 地圖原點 Y + 0.5) * 32
```

### 8.2 移動間隔計算

```
方法 1（優先）：
  單幀時長 = DurationUnit * 40ms / 1000.0f
  總時長 = Σ(所有 walk 動作幀的 RealDuration)

方法 2（回退）：
  移動間隔 = SprDataTable.GetInterval(ActionType.Move, gfxId, 0) / 1000.0f

最終回退：
  移動間隔 = 0.6 秒
```

### 8.3 移動速度

```
基礎移動速度 = 32 像素 / 基礎移動間隔（秒）

加速後移動速度 = 32 像素 / (基礎移動間隔 * 0.75)
                = 基礎移動速度 * 1.333

減速後移動速度 = 32 像素 / (基礎移動間隔 * 1.333)
                = 基礎移動速度 * 0.75
```

### 8.4 移動距離

```
每步移動距離 = 1 格 = 32 像素（固定不變）

移動方向 = 8 方向（N, NE, E, SE, S, SW, W, NW）

下一步座標 = 當前座標 + 朝向向量（鉗制在 ±1 格內）
```

### 8.5 時間計算

```
移動計時器 += delta（每幀累積）

當 移動計時器 >= 移動間隔 時：
  執行一步移動
  重置移動計時器 = 0
```

---

## 附錄

### A. 相關文件列表

| 文件 | 職責 |
|------|------|
| `Client/Game/GameWorld.Movement.cs` | 移動邏輯主循環、單步移動執行 |
| `Client/Game/GameEntity.Movement.cs` | 座標轉換、平滑移動動畫、移動時長計算 |
| `Client/Data/SprDataTable.cs` | 服務器認可的移動間隔數據 |
| `Client/Utility/ListSprLoader.cs` | list.spr 文件解析、動畫幀時長計算 |
| `Client/Game/SpeedManager.cs` | 加速/減速狀態管理 |

### B. 服務器對應邏輯

| 客戶端邏輯 | 服務器對應 |
|-----------|-----------|
| `CalculateWalkDuration()` | `SprTable.getMoveSpeed()` |
| `GetInterval(ActionType.Move)` | 數據庫 `spr_action` 表 |
| `StepTowardsTarget()` | `L1Object.toMove()` |
| `GetHeading()` | `L1Object.calcheading()` |
| `C_MoveCharPacket` | `C_MoveChar` (Opcode 10) |

### C. 關鍵約定

1. **雙重約定**：移動間隔必須同時符合 `list.spr` 動畫時長和服務器數據庫間隔
2. **固定步長**：每步移動固定 1 格（32 像素），無論動畫時長
3. **時間基準**：`DurationUnit * 40ms` 是 Lineage 原版的基準時間單位
4. **服務器權威**：最終移動座標由服務器確認，客戶端僅做預測和視覺同步

---

**文檔版本**：1.0  
**最後更新**：2026-01-21  
**維護者**：Reverse Engineering Team
