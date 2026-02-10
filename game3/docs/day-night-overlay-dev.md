# 日夜遮罩系統開發文檔（Day/Night Overlay）

本文檔說明客戶端「日夜遮罩」功能的規則、參數、與開發細節。遮罩依伺服器遊戲世界時間驅動，提供黑夜三檔（晚上／深夜／極度黑夜）、白天色彩濾鏡（晨昏）、以及黑夜時隱藏頭頂名字與血條等行為。

---

## 一、功能概述

| 項目 | 說明 |
|------|------|
| **時間來源** | 伺服器 Opcode 33（世界時間）、Opcode 12（ParseCharacterStat 內 `writeD(WORLDTIME)`） |
| **遮罩層級** | CanvasLayer.Layer = **1**（高於預設 2D 的 0，才不會被地圖 Map_0/lowerland 遮住）；HUD 設為 **2**，視窗層 10，確保 UI 在遮罩之上 |
| **黑夜行為** | 徑向暗角 + 地面更暗（空間感）；darkness ≥ 0.5 時強制隱藏所有角色頭頂名字與血條 |
| **白天行為** | 依時段套用色彩濾鏡（早晨暖黃、中午中性、傍晚橙紅），類似攝影濾鏡／暗角 |

---

## 二、架構與檔案

### 2.1 相關檔案

| 路徑 | 職責 |
|------|------|
| `Client/Game/DayNightOverlay.cs` | 日夜邏輯：訂閱 GameTimeReceived、計算 darkness/tier、寫入 Shader、發出 DarknessChanged |
| `Client/Game/DayNightOverlay.tscn` | 場景根節點（Node + DayNightOverlay.cs），由 GameWorld 實例化 |
| `Client/Shaders/day_night.gdshader` | 全螢幕遮罩：黑夜徑向+地面暗、白天 tint 濾鏡 |
| `Client/Game/GameWorld.Setup.cs` | `SetupInitDayNightOverlay()`：載入 DayNightOverlay.tscn、訂閱 DarknessChanged |
| `Client/Game/GameWorld.Entities.cs` | `OnDarknessChanged()`：對所有實體呼叫 `SetNightOverlayActive`，白天時重設血條 |
| `Client/Game/GameEntity.CombatFx.cs` | `SetNightOverlayActive(bool isNight)`：黑夜時隱藏名字與血條 |
| `Client/Network/PacketHandler.cs` | 發出 `GameTimeReceived(int worldTimeSeconds)`，並快取 `LastWorldTimeSeconds` 供補發 |

### 2.2 節點與層級

- **DayNightOverlay** 為 GameWorld 的子節點，在 `SetupInitDayNightOverlay()` 中以 **PackedScene 實例化**（不可用 `new Node()` + `SetScript()`，否則 Godot 4 可能不呼叫 C# `_Ready()`）。
- 遮罩使用 **CanvasLayer**，`Layer = 1`；**HUD** 在 GameWorld.Setup 中設為 `Layer = 2`：
  - Godot 中 CanvasLayer 數值**越大越晚繪製**（畫在上層）。預設 2D 世界（地圖、角色）為 layer 0；若遮罩用 -1 會先於 2D 繪製，地圖會蓋住黑色層（Map_0/lowerland 故障）。
  - 繪製順序：Layer 0（2D 世界、地圖、角色）→ **Layer 1（日夜遮罩）** → Layer 2（HUD）→ Layer 10（視窗）→ Layer 128（Tooltip）。
  - 因此遮罩會蓋住地圖與角色，但不會蓋住快捷列、背包、角色欄等 UI。

---

## 三、時間與 darkness 計算

### 3.1 時間來源

- **worldTimeSeconds**：伺服器下發的「當日秒數」概念（實作上為 `writeD(WORLDTIME)`，客戶端以秒為單位使用）。
- **當天秒數**：`secInDay = ((worldTimeSeconds % 86400) + 86400) % 86400`（0～86400）。
- **小時**：`hour = secInDay / 3600f`（0～24）。

### 3.2 黎明／黃昏常數

| 常數 | 值 | 說明 |
|------|-----|------|
| `DawnHour` | 6 | 早上 6 點天亮（hour ≥ 6 且 < 18 為白天，darkness = 0） |
| `DuskHour` | 18 | 下午 6 點天黑 |
| `TransitionHours` | 1.5 | 黎明／黃昏過渡時長（小時），用於平滑 darkness |

### 3.3 darkness 公式

- **白天**（6 ≤ hour < 18）：`darkness = 0`。
- **黎明前**（hour < 6）：  
  `tDawn = (hour - (DawnHour - TransitionHours)) / TransitionHours`，  
  `darkness = Clamp(1 - tDawn, 0, 1)`。
- **黃昏後**（hour ≥ 18）：  
  `tDusk = (hour - DuskHour) / TransitionHours`，  
  `darkness = Clamp(tDusk, 0, 1)`。

---

## 四、黑夜三檔（Tier）

### 4.1 檔位定義（依 darkness 門檻）

| Tier | 常數 | darkness 範圍 | 說明 |
|------|------|----------------|------|
| 0 | `TierDay` | < 0.01 | 白天 |
| 1 | `TierEvening` | 0.5 ≤ darkness < 0.8 | 晚上（暗角） |
| 2 | `TierMidnight` | 0.8 ≤ darkness < 0.95 | 深夜 |
| 3 | `TierDeepNight` | ≥ 0.95 | 極度黑夜 |

判定順序：先判斷白天，再依 darkness 由高到低 0.95 → 0.8 → 0.5。`GetNightTier(darkness)` 僅依 darkness，不依 hour。

### 4.2 黑夜三檔設定（使用者設定，固定 darkness）

| Tier | darkness（寫入 Shader） | radius_in | radius_out | ground_darken | 備註 |
|------|--------------------------|-----------|------------|---------------|------|
| Day | 0 | 0.25 | 0.65 | 0.25 | 白天用 tint |
| **晚上** TierEvening | **0.5** | 0.2 | 0.65 | **0.85** | 固定 darkness=0.5、ground_darken 0.85 |
| **深夜** TierMidnight | **0.8** | 0.12 | **0.55** | **0.55** | 固定 darkness=0.8、ground_darken 0.55 |
| **極度黑夜** TierDeepNight | **0.95** | **0.03** | 0.4 | **0.4** | 固定 darkness=0.95、ground_darken 0.4 |

- **radius_in**：極度黑夜用 0.03（亮區極小）。
- **radius_out**：深夜檔固定 **0.55**；極度黑夜 0.4。
- **ground_darken**：晚上 **0.85**、深夜 **0.55**、極度黑夜 **0.4**。空間感方向見 4.3。

### 4.3 立體空間感（ground_darken）— 右下方較暗、左上方較亮

- **目的**：符合角色陰影朝左（光源在右）的 2.5D 感覺，營造「光從右方照到螢幕」的空間感。
- **方向**：**右下方較暗**、**左上方較亮**（與角色陰影方向一致）。
- **Shader 公式**：`t = (UV.x + (1.0 - UV.y)) * 0.5`（左上≈0、右下≈1），`height_factor = 1.0 + ground_darken * t`。左上角較亮、右下角較暗。
- **黑夜 alpha**：`night_alpha = darkness * (1.0 - night_mask) * height_factor`。
- **三檔 ground_darken**：晚上 **0.85**、深夜 **0.55**、極度黑夜 **0.4**。

---

## 五、白天色彩濾鏡（tint）

僅在 **白天**（6 ≤ hour < 18）套用；黑夜時 tint_strength 設為 0。

| 時段 | hour | tint_color (R,G,B) | tint_strength | 說明 |
|------|------|-------------------|---------------|------|
| 早晨 | 6～9 | (1, 0.95, 0.85) | 0.15 + 0.1×(1-t)，t=(hour-6)/3 | 暖黃 |
| 中午 | 11～13 | (1, 1, 1) | 0.02 | 幾乎無 |
| 傍晚 | 16～18 | (1, 0.75, 0.5) | 0.08 + 0.12×t，t=(hour-16)/2 | 橙紅 |
| 其餘白天 | 9～11, 13～16 | 未特別設定 | 0 | 使用預設白 |

白天濾鏡使用與黑夜相同的徑向概念：`tint_radius_in = 0.2`、`tint_radius_out = 0.7`，形成類似攝影暗角的色彩過渡。

---

## 六、Shader 參數總表（day_night.gdshader）

| uniform | 類型 | 預設 | 說明 |
|---------|------|------|------|
| `darkness` | float 0～1 | 0 | 黑夜強度，0=無遮罩，1=全黑遮罩 |
| `radius_in` | float 0～1 | 0.25 | 中心全亮半徑（黑夜） |
| `radius_out` | float 0～1 | 0.65 | 黑夜全黑邊界半徑 |
| `ground_darken` | float 0～1 | 0.25 | 地面更暗係數（1.0 - UV.y） |
| `night_color` | vec3 | C# 傳入 | 黑夜遮罩顏色；C# 預設 (0.02, 0.02, 0.08) 深藍黑，可改 `DayNightOverlay._nightColor` 微調 |
| `tint_color` | vec3 | (1,1,1) | 白天濾鏡顏色 |
| `tint_strength` | float 0～1 | 0 | 白天濾鏡強度 |
| `tint_radius_in` | float 0～1 | 0.2 | 白天濾鏡中心半徑 |
| `tint_radius_out` | float 0～1 | 0.7 | 白天濾鏡外緣半徑 |

Shader 邏輯摘要：

- **黑夜**：`night_alpha = darkness * (1 - radial_mask) * height_factor`，`height_factor = 1 + ground_darken * (1 - UV.y)`。
- **白天**：`tint_alpha = tint_strength * (1 - tint_radial_mask)`。
- **合併**：`out_color = mix(tint_color, black, min(1, darkness*2))`，`out_alpha = clamp(night_alpha + tint_alpha*(1-night_alpha), 0, 1)`。

---

## 七、黑夜時隱藏名字與血條

### 7.1 門檻

- **NightHideUiThreshold** = `0.5f`。  
- 當 `darkness >= NightHideUiThreshold` 時，視為「黑夜」，觸發隱藏邏輯。

### 7.2 信號與流程

1. **DayNightOverlay** 在每次 `OnGameTimeReceived` 更新後發出：  
   `DarknessChanged(float darkness, int tier)`。
2. **GameWorld** 在 `SetupInitDayNightOverlay()` 中訂閱該信號，回調 **OnDarknessChanged(float darkness, int tier)**。
3. **OnDarknessChanged**：
   - 若 `darkness >= DayNightOverlay.NightHideUiThreshold`：對所有 `_entities` 呼叫 `SetNightOverlayActive(true)`。
   - 若低於門檻：呼叫 `SetNightOverlayActive(false)`，並還原血條：主角用 `SetHealthBarVisible(ShouldShowHealthBar() && HpRatio < 100)`，其餘呼叫 **RefreshMonsterHealthBars()**。

### 7.3 GameEntity 行為

- **SetNightOverlayActive(bool isNight)**（在 `GameEntity.CombatFx.cs`）：
  - `isNight == true`：`_nameLabel.Visible = false`，`_healthBar.Visible = false`。
  - `isNight == false`：僅還原 `_nameLabel.Visible = true`；血條由 GameWorld 依上述邏輯重設，不在 Entity 內還原。

---

## 八、補發與時序

- **PacketHandler** 在 Opcode 33 與 Opcode 12（ParseCharacterStat）發送 `GameTimeReceived` 前，會寫入 **LastWorldTimeSeconds**。
- **DayNightOverlay** 在 `_Ready()` 中訂閱 `GameTimeReceived` 後，若 `ph.LastWorldTimeSeconds >= 0`，會 **CallDeferred(nameof(ApplyCachedWorldTime))**，用快取值補發一次，避免遮罩建立晚於封包而從未收到時間。
- 因此只要進世界後曾收過至少一次 Opcode 12 或 33，遮罩建立時即可立即套用正確的日夜狀態。

---

## 九、除錯預覽與參數可視化（F9 / F10 / F11 / F12）

### 9.1 按鍵

| 按鍵 | 功能 |
|------|------|
| **F9** | 下一檔：依序切換預設時段（見下表） |
| **F10** | 上一檔 |
| **F11** | 關閉強制預覽，恢復使用伺服器時間 |
| **F12** | 切換畫面上參數可視化（tier、darkness、ri、ro、ground_darken、night_color），方便微調 |

### 9.2 預設時段（DebugPresets，10 檔含黑夜三檔）

| 索引 | hour | 名稱 | 對應 Tier |
|------|------|------|-----------|
| 0 | 0 | 極度黑夜(小亮區+最強地面暗) | TierDeepNight |
| 1 | 4 | 凌晨(極度黑夜) | TierDeepNight |
| 2 | 7 | 黎明 | TierDay |
| 3 | 9 | 早晨暖黃 | TierDay |
| 4 | 12 | 中午 | TierDay |
| 5 | 16 | 下午 | TierDay |
| 6 | 18 | 黃昏橙紅 | TierDay |
| 7 | 19 | 晚上(暗角0.5~0.85) | TierEvening |
| 8 | 19.25 | 深夜(0.8~0.95) | TierMidnight |
| 9 | 20 | 極度黑夜(0.8+ ro=0.55) | TierDeepNight |

### 9.3 實作細節

- **DayNightOverlay** 內有 `_debugOverrideActive`、`_debugPresetIndex`。
- 訂閱封包時使用 **OnGameTimeReceivedFromServer**：若 `_debugOverrideActive == true`，則忽略伺服器時間。
- F9/F10 呼叫 **ApplyDebugPreset()**：以 `DebugPresets[_debugPresetIndex].Hour` 換算 `secInDay = (int)(hour * 3600)`，再呼叫 **OnGameTimeReceived(secInDay)**。
- F11 將 `_debugOverrideActive = false`，若有快取 `LastWorldTimeSeconds >= 0` 則補發 **OnGameTimeReceived**，恢復伺服器時間。

---

## 十、常數與對外介面速查

### 10.1 DayNightOverlay 常數

```csharp
DawnHour = 6f;
DuskHour = 18f;
TransitionHours = 1.5f;
TierDay = 0, TierEvening = 1, TierMidnight = 2, TierDeepNight = 3;
NightHideUiThreshold = 0.5f;
```

### 10.2 信號

```csharp
[Signal] public delegate void DarknessChangedEventHandler(float darkness, int tier);
```

### 10.3 依賴

- 父節點必須為 **GameWorld**，且 **GameWorld.PacketHandlerRef** 非 null。
- Shader 路徑：`res://Client/Shaders/day_night.gdshader`。
- 場景路徑：`res://Client/Game/DayNightOverlay.tscn`。

---

## 十一、性能

| 項目 | 說明 |
|------|------|
| **CPU** | 無每幀邏輯（無 _Process）。僅在收到 Opcode 33/12 或 F9/F10/F11/F12、視窗縮放時執行，負擔可忽略。 |
| **GPU** | 遮罩為全螢幕 ColorRect + ShaderMaterial，每幀 1 次 draw call、全解析度 fragment 一次。Shader 無貼圖、無迴圈，僅簡單數學（distance、smoothstep、mix），負擔屬輕量。 |
| **優化** | 當 `darkness < 0.001` 且 `tint_strength < 0.001`（白天且無晨昏濾鏡）時將 `_rect.Visible = false`，跳過該幀繪製；其餘時段 `Visible = true`。 |

---

## 十二、與伺服器對齊

- 伺服器：`S_WorldStatPacket()` 使用 `writeC(33)`、`writeD(Config.WORLDTIME)`；`Util.WorldTimeToHour()` 以 `Config.WORLDTIME * 1000` 為秒級時間。
- 客戶端：以 **worldTimeSeconds**（秒）換算 `secInDay`、`hour`，再驅動 darkness、tier、tint，不直接依賴伺服器小時值，僅依賴秒數一致性。

---

*文檔版本：依當前 DayNightOverlay / day_night.gdshader / GameWorld.Entities / GameEntity.CombatFx 實作整理。*
