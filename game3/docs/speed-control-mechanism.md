# 速度控制機制完整文檔

## 核心理解確認

**✅ 您的理解完全正確！**

控制一切速度（移動、攻擊、魔法）的關鍵文件確實是：
1. **客戶端**：`list.spr` 文件
2. **服務器**：`sprite_frame` 數據庫表

兩者都基於**統一的 40ms 基準時間單位**。

---

## 1. 全局基準：40ms

### 1.1 定義

**40ms** 是 Lineage 原版的基準時間單位，應用於所有動作類型：
- ✅ 移動 (Walk)
- ✅ 攻擊 (Attack)
- ✅ 魔法 (Magic/Spell)

### 1.2 客戶端實現

**文件**：`Client/Utility/ListSprLoader.cs`  
**方法**：`ParseFrameToken()`

```csharp
// 換算時長:
// - 基準單位: DurationUnit * 40ms (與原版 Lineage 完全一致)
// - 110.framerate 只用來控制「全局加速/減速」，不再在這裡二次放大（避免整體過快）
// [核心修復] 確保所有動作（包括 walk）嚴格遵循 DurationUnit * 40ms
// 攻擊速度的 幀速度，和整體速度，此公式和代碼 正確無誤。不准修改。
f.RealDuration = (f.DurationUnit * 40.0f) / 1000.0f;
```

**公式**：
```
單幀時長（秒）= DurationUnit * 40ms / 1000.0f
總動作時長（秒）= Σ(所有幀的 RealDuration)
```

**示例**：
```
假設 walk 動作有 4 幀，每幀 DurationUnit = 4：
- 單幀時長 = 4 * 40ms = 160ms = 0.16秒
- 總時長 = 4 * 0.16秒 = 0.64秒
```

---

## 2. 客戶端速度控制：list.spr

### 2.1 文件位置

**文件**：`Client/Utility/ListSprLoader.cs`  
**數據來源**：`list.spr` 文件（從遊戲資源包解析）

### 2.2 格式定義

**格式**：
```
0.walk(1, 24.0:4 24.1:4 24.2:4 24.3:4)
```

**解析規則**：
- `0.walk`：動作 ID = 0，動作名稱 = walk
- `24.0:4`：動作 ID = 24，幀索引 = 0，時間單位 = 4
- `RealDuration = DurationUnit * 40ms / 1000.0f`

### 2.3 動作類型

`list.spr` 定義了所有動作的動畫幀序列：

| 動作類型 | 動作 ID 示例 | 說明 |
|---------|------------|------|
| **移動 (Walk)** | 0, 4, 11, 20, 24, 40, 46, 50 | 不同武器的走路動作 |
| **攻擊 (Attack)** | 1, 5, 12, 21, 25, 30, 31, 41, 47, 51 | 不同武器的攻擊動作 |
| **魔法 (Spell)** | 18 (有向), 19 (無向) | 魔法施放動作 |

### 2.4 計算流程

**文件**：`Client/Game/GameEntity.Movement.cs`  
**方法**：`CalculateWalkDuration()`

```csharp
// 方法 1：從 list.spr 計算（精確，符合雙重約定）
var def = Client.Utility.ListSprLoader.Get(GfxId);
var walkSeq = Client.Utility.ListSprLoader.GetActionSequence(def, ACT_WALK);

float totalDuration = 0.0f;
foreach (var frame in walkSeq.Frames)
{
    totalDuration += frame.RealDuration; // RealDuration = DurationUnit * 40ms / 1000.0f
}
```

**關鍵要點**：
- ✅ 優先使用 `list.spr` 計算（精確）
- ✅ 所有動作（walk、attack、magic）都使用相同的 40ms 基準
- ✅ 總時長 = 所有幀的 `RealDuration` 之和

---

## 3. 服務器速度控制：sprite_frame 表

### 3.1 數據庫結構

**表名**：`sprite_frame`  
**位置**：`server/datebase_182_2026-01-21.sql`

**表結構**：
```sql
CREATE TABLE `sprite_frame` (
  `name` varchar(255) NOT NULL DEFAULT '',
  `gfx` int(10) unsigned NOT NULL DEFAULT '0',
  `action` int(10) unsigned NOT NULL DEFAULT '0',
  `action_name` varchar(255) NOT NULL DEFAULT '',
  `frame` int(10) unsigned NOT NULL DEFAULT '0'
);
```

**字段說明**：
- `gfx`：角色外觀 ID（對應客戶端的 GfxId）
- `action`：動作 ID（0=walk, 1=attack, 18=有向魔法, 19=無向魔法）
- `action_name`：動作名稱（walk, attack, spell_dir, spell_nodir）
- `frame`：動作間隔（毫秒）

### 3.2 數據示例

```sql
INSERT INTO `sprite_frame` (`name`, `gfx`, `action`, `action_name`, `frame`)
VALUES
    ('王子', 0, 0, 'walk', 640),      -- gfxId=0, walk, 間隔=640ms
    ('王子', 0, 1, 'attack', 840),    -- gfxId=0, attack, 間隔=840ms
    ('騎士', 1, 0, 'walk', 640),      -- gfxId=1, walk, 間隔=640ms
    ('騎士', 1, 1, 'attack', 880),    -- gfxId=1, attack, 間隔=880ms
    ('法師', 37, 18, 'spell_dir', 880), -- gfxId=37, 有向魔法, 間隔=880ms
    ('法師', 37, 19, 'spell_nodir', 800); -- gfxId=37, 無向魔法, 間隔=800ms
```

### 3.3 服務器讀取邏輯

**文件**：`server/database/SprTable.java`  
**方法**：`loadSprAction()`

```java
st = con.prepareStatement("SELECT * FROM sprite_frame");
rs = st.executeQuery();
while (rs.next()) {
    int key = rs.getInt("gfx");
    int actid = rs.getInt("action");
    int speed = rs.getInt("frame");  // 間隔（毫秒）
    
    switch (actid) {
    case 0: case 4: case 11: case 20: case 24: case 40: case 46: case 50:
        spr.moveSpeed.put(Integer.valueOf(actid), Integer.valueOf(speed));
        break;
    case 18:
        spr.dirSpellSpeed.put(Integer.valueOf(actid), Integer.valueOf(speed));
        break;
    case 19:
        spr.nodirSpellSpeed.put(Integer.valueOf(actid), Integer.valueOf(speed));
        break;
    case 1: case 5: case 12: case 21: case 25: case 30: case 31: case 41: case 47: case 51:
        spr.attackSpeed.put(Integer.valueOf(actid), Integer.valueOf(speed));
        break;
    }
}
```

### 3.4 服務器速度查詢方法

**文件**：`server/database/SprTable.java`

```java
// 移動速度
public int getMoveSpeed(int sprid, int actid)

// 攻擊速度
public int getAttackSpeed(int sprid, int actid)

// 有向魔法速度
public int getDirSpellSpeed(int sprid, int actid)

// 無向魔法速度
public int getNodirSpellSpeed(int sprid, int actid)
```

### 3.5 服務器速度檢查

**文件**：`server/check/CheckSpeed.java`  
**方法**：`getRightInterval()`

```java
private int getRightInterval(ACT_TYPE type)
{
    int interval = 0;
    switch (type) {
    case MOVE:
        interval = SprTable.getInstance().getMoveSpeed(this._pc.getGfx(), this._pc.getGfxMode());
        break;
    case ATTACK:
        interval = SprTable.getInstance().getAttackSpeed(this._pc.getGfx(), this._pc.getGfxMode() + 1);
        break;
    case SPELL_DIR:
        interval = SprTable.getInstance().getDirSpellSpeed(this._pc.getGfx(), 18);
        break;
    case SPELL_NODIR:
        interval = SprTable.getInstance().getNodirSpellSpeed(this._pc.getGfx(), 19);
        break;
    }
    
    // 應用加速/減速
    if (this._pc.isSpeed()) {
        interval = (int)(interval * 0.75D);  // 加速：間隔縮放為 0.75
    }
    if (this._pc.isSlow()) {
        interval = (int)(interval / 0.75D);  // 減速：間隔放大為 1.333
    }
    if (this._pc.isBrave()) {
        interval = (int)(interval * 0.75D);  // 勇敢：攻擊間隔縮放為 0.75
    }
    return interval;
}
```

---

## 4. 客戶端與服務器的對應關係

### 4.1 數據同步

**客戶端**：`Client/Data/SprDataTable.cs`

```csharp
// 數據從服務器數據庫提取
// Data extracted from server/datebase_182_2026-01-21.sql
var rawData = new List<(int gfx, int action, int frame)>
{
    (0, 0, 640),   // gfxId=0, actionId=0(walk), interval=640ms
    (0, 1, 840),   // gfxId=0, actionId=1(attack), interval=840ms
    (1, 0, 640),   // gfxId=1, actionId=0(walk), interval=640ms
    // ...
};
```

**對應關係**：
- 客戶端的 `SprDataTable` 數據 = 服務器的 `sprite_frame` 表數據
- 兩者必須完全一致，否則會導致速度不同步

### 4.2 雙重約定機制

**客戶端實現**：`Client/Game/GameEntity.Movement.cs`

```csharp
private float CalculateWalkDuration()
{
    // 方法 1：從 list.spr 計算（精確，符合雙重約定）
    var def = Client.Utility.ListSprLoader.Get(GfxId);
    var walkSeq = Client.Utility.ListSprLoader.GetActionSequence(def, ACT_WALK);
    if (walkSeq != null && walkSeq.Frames.Count > 0)
    {
        float totalDuration = 0.0f;
        foreach (var frame in walkSeq.Frames)
        {
            totalDuration += frame.RealDuration; // RealDuration = DurationUnit * 40ms / 1000.0f
        }
        if (totalDuration > 0)
        {
            return totalDuration;  // ✅ 優先使用 list.spr 計算
        }
    }
    
    // 方法 2：從 SprDataTable 獲取（服務器認可的移動間隔，作為回退）
    float interval = SprDataTable.GetInterval(ActionType.Move, GfxId, 0) / 1000.0f;
    return interval > 0 ? interval : 0.6f;  // 最終回退值
}
```

**雙重約定**：
1. ✅ **優先**：從 `list.spr` 計算（精確，符合動畫定義）
2. ✅ **回退**：從 `SprDataTable`（服務器數據庫）獲取（服務器認可的間隔）

**關鍵要點**：
- `list.spr` 的總時長應該等於 `sprite_frame` 表的 `frame` 值（毫秒轉換為秒）
- 如果兩者不一致，優先使用 `list.spr`（客戶端視覺優先）
- 服務器會根據 `sprite_frame` 表進行速度檢查和反作弊驗證

---

## 5. 速度控制流程圖

```
┌─────────────────────────────────────────────────────────────┐
│                    速度控制機制流程                            │
└─────────────────────────────────────────────────────────────┘

【客戶端】
  list.spr 文件
    ↓
  解析動畫幀序列 (24.0:4 24.1:4 ...)
    ↓
  計算 RealDuration = DurationUnit * 40ms / 1000.0f
    ↓
  總時長 = Σ(所有幀的 RealDuration)
    ↓
  應用於動畫播放和移動間隔
    ↓
  ┌─────────────────────────────────┐
  │ 如果 list.spr 計算失敗，回退到：  │
  └─────────────────────────────────┘
    ↓
  SprDataTable (從服務器數據庫提取)
    ↓
  獲取間隔值（毫秒）→ 轉換為秒

【服務器】
  sprite_frame 數據庫表
    ↓
  SprTable.loadSprAction() 讀取
    ↓
  存儲到內存映射表
    ↓
  CheckSpeed.getRightInterval() 查詢
    ↓
  應用加速/減速倍數
    ↓
  速度檢查和反作弊驗證
```

---

## 6. 關鍵參數總結

### 6.1 基準時間單位

| 參數 | 值 | 單位 | 說明 |
|------|-----|------|------|
| **基準時間單位** | 40 | 毫秒 | Lineage 原版基準，應用於所有動作 |
| **公式** | `DurationUnit * 40ms` | 毫秒 | 單幀時長計算 |

### 6.2 客戶端數據來源

| 數據來源 | 文件位置 | 用途 | 優先級 |
|---------|---------|------|--------|
| **list.spr** | `Client/Utility/ListSprLoader.cs` | 動畫幀定義和時長計算 | ⭐ 優先 |
| **SprDataTable** | `Client/Data/SprDataTable.cs` | 服務器認可的間隔值（回退） | 回退 |

### 6.3 服務器數據來源

| 數據來源 | 文件位置 | 用途 |
|---------|---------|------|
| **sprite_frame 表** | `server/datebase_182_2026-01-21.sql` | 速度間隔數據庫 |
| **SprTable** | `server/database/SprTable.java` | 速度查詢接口 |
| **CheckSpeed** | `server/check/CheckSpeed.java` | 速度檢查和反作弊 |

### 6.4 動作類型對應

| 動作類型 | 客戶端動作 ID | 服務器動作 ID | 數據庫 action 值 |
|---------|-------------|--------------|----------------|
| **移動 (Walk)** | 0, 4, 11, 20, 24, 40, 46, 50 | 0, 4, 11, 20, 24, 40, 46, 50 | 0, 4, 11, 20, 24, 40, 46, 50 |
| **攻擊 (Attack)** | 1, 5, 12, 21, 25, 30, 31, 41, 47, 51 | 1, 5, 12, 21, 25, 30, 31, 41, 47, 51 | 1, 5, 12, 21, 25, 30, 31, 41, 47, 51 |
| **有向魔法** | 18 | 18 | 18 |
| **無向魔法** | 19 | 19 | 19 |

---

## 7. 驗證機制

### 7.1 客戶端驗證

**文件**：`Client/Game/GameEntity.Movement.cs`

```csharp
// 確保動畫播放時間 = 移動間隔，實現視覺和邏輯同步
float moveDuration = CalculateWalkDuration();
_moveTween.TweenProperty(this, "position", targetPos, moveDuration)
    .SetTrans(Tween.TransitionType.Linear);
```

**驗證點**：
- ✅ 動畫播放時長 = 移動間隔
- ✅ 視覺移動速度 = 邏輯移動速度

### 7.2 服務器驗證

**文件**：`server/check/CheckSpeed.java`

```java
public int checkInterval(ACT_TYPE type) {
    long now = System.currentTimeMillis();
    long interval = now - ((Long)this._actTimers.get(type)).longValue();
    int rightInterval = getRightInterval(type);  // 從 sprite_frame 表獲取
    
    if ((0L < interval) && (interval < rightInterval)) {
        this._injusticeCount += 1;  // 速度過快，記錄違規
        if (this._injusticeCount >= 10) {
            doPunishment(type, Config.PUNISHMENT);  // 處罰
            return 2;
        }
    }
    return result;
}
```

**驗證點**：
- ✅ 客戶端動作間隔必須 >= 服務器認可的間隔
- ✅ 如果速度過快，觸發反作弊機制

---

## 8. 結論

### ✅ 核心確認

1. **40ms 是全局基準**：所有動作（walk、attack、magic）都使用 `DurationUnit * 40ms` 計算時長
2. **list.spr 是客戶端權威**：定義動畫幀序列和時長，優先使用
3. **sprite_frame 是服務器權威**：定義服務器認可的速度間隔，用於反作弊驗證
4. **雙重約定機制**：客戶端優先使用 `list.spr`，失敗時回退到 `SprDataTable`（服務器數據）

### 📋 關鍵文件清單

| 文件 | 職責 |
|------|------|
| `Client/Utility/ListSprLoader.cs` | 解析 list.spr，計算 RealDuration |
| `Client/Data/SprDataTable.cs` | 存儲服務器認可的速度間隔（回退） |
| `Client/Game/GameEntity.Movement.cs` | 計算移動動畫時長（雙重約定） |
| `server/database/SprTable.java` | 從 sprite_frame 表讀取速度數據 |
| `server/check/CheckSpeed.java` | 速度檢查和反作弊驗證 |
| `server/datebase_182_2026-01-21.sql` | sprite_frame 數據庫表定義 |

---

**文檔版本**：1.0  
**最後更新**：2026-01-21  
**維護者**：Reverse Engineering Team
