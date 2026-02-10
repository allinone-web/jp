# 動畫幀播放順序與音效代碼審計報告

## 一、您的定義摘要

1. **播放順序**：以 **FrameIdx=1** 作為**第一幀**開始播放，按 FrameIdx 升序，然後循環；排序後在 FrameIdx=1 **之前**的幀（例如 0、或若存在 3 等）一律跳過不當起點，但**所有幀都保留並參與循環**（先播 1→2→…→最後一幀，再接回 0 等，再回到 1）。
2. **範例**：
   - 排序 0,1,2,3,4 → 播放順序：**1→2→3→4→0→1→…**
   - 排序 6,1,2,3,4,5 → 播放順序：**1→2→3→4→5→6→1→…**
3. **音效**：原本在哪一幀定義就在哪一幀播；**不要**把被跳過幀的音效合併到第一幀、**不要**對音效做去重或額外處理。
4. **動畫定時器**：刪除動畫定時器，動畫播完就銷毀，不依賴定時器。

---

## 二、當前代碼與定義的對比

### 2.1 第一幀起點：應為「FrameIdx==1」而非「FrameIdx>=1」

**規範**：找到「**FrameIdx=1**」在排序後序列裡的**位置**，從該位置當第一幀開始播。

**當前實作**（兩處一致）：

- `Client/Utility/ListSprLoader.cs`：常數 `MinPlaybackFrameIdx = 1`，註解為「第一個 FrameIdx **>=** MinPlaybackFrameIdx 的幀」。
- `Skins/CustomFantasy/CustomCharacterProvider.cs`（約 197–202 行）與 `Client/Game/GameEntity.Audio.cs`（約 69–74 行）：

```csharp
int startIdx = 0;
while (startIdx < sortedFrames.Count && sortedFrames[startIdx].FrameIdx < SprPlaybackRule.MinPlaybackFrameIdx)
    startIdx++;
// 然後 order = [startIdx..end] + [0..startIdx-1]
```

等價於：**從第一個 FrameIdx >= 1 的幀開始**，而不是「從 FrameIdx **==** 1 的那一幀開始」。

**差異與風險**：

- 若 list.spr 中**存在 FrameIdx=1**（例如 0,1,2,3,4 或 1,2,3,4,5,6）：當前邏輯與預期一致，起點正確。
- 若**沒有 FrameIdx=1**（例如只有 0,2,3,4）：當前會從 **FrameIdx=2** 開始，違反「第一幀必須是 FrameIdx=1 的那一幀」。
- **建議**：改為「找到 **FrameIdx == MinPlaybackFrameIdx** 的幀的索引作為 startIdx」；若找不到（無 FrameIdx=1），再決定 fallback（例如從 0 開始或維持現行 >=1 並在文檔註明）。

---

### 2.2 播放順序與循環（CustomCharacterProvider / GameEntity.Audio）

- **排序**：`sortedFrames.Sort((a, b) => a.FrameIdx.CompareTo(b.FrameIdx))`，依 FrameIdx 升序，正確。
- **循環**：`order = [startIdx..末尾] + [0..startIdx-1]`，即從起點播到最後一幀再接回開頭，形成一輪循環，與「1→2→…→最後→0/其他→再回 1」一致。
- **結論**：除起點應改為「FrameIdx==1」外，順序與循環邏輯符合定義。

---

### 2.3 魔法／技能動畫（SkillEffect）的幀與音效

- **幀來源**：`SkillEffect` 透過 `GetBodyFrames(gfxId, magicActionId, heading)` 取得 `SpriteFrames`，順序由 **CustomCharacterProvider.BuildLayer** 的 `order` 決定，因此魔法動畫的幀順序與 2.1/2.2 相同；若修正 startIdx，魔法也會一併修正。
- **音效**：Provider 在每個紋理上設置 `spr_sound_ids`（來自 `SprFrame.SoundIds`），且紋理順序與 `order` 一致，因此「第幾幀」對應「order 的第幾個 SprFrame」；**未發現**「把被跳過幀的音效合併到第一幀」的程式碼，符合「原本在哪一幀定義就在哪一幀播」。
- **潛在多餘**：`SkillEffect` 內用 `_lastPlayedSoundAnim` / `_lastPlayedSoundFrame` 避免同一動畫同一幀重複觸發時重複播放。您要求「不要對音效做去重」——若嚴格解釋「不做任何去重」，可視此為多餘並刪除，改為每次 `FrameChanged` 都依該幀元數據播放；若僅禁止「被跳過幀合併」，則可保留此防重。

---

### 2.4 實體音效（GameEntity.Audio）

- **邏輯**：依 `CurrentAction` 取 seq，對 `seq.Frames` 按 FrameIdx 排序後用**同一套** startIdx/order 建 `order`，用 `_mainSprite.Frame` 作為 order 的索引，取 `order[frameIdx].SoundIds` 播放。
- **結論**：與 BuildLayer 同一規則；起點改為「FrameIdx==1」後，實體音效也會與「第一幀為 FrameIdx=1」一致。未發現合併或去重邏輯。

---

### 2.5 動畫定時器

- **SkillEffect**：僅在 `OnAnimationFinished` 時處理連環 109 與 `QueueFree()`，**沒有**用 Timer 控制動畫生命週期；註解中「可能由 AnimationFinished **或 Timer** 觸發」已過時，建議改為僅「AnimationFinished」。
- **GameEntity.Visuals**：`CreateTimer(0.2f)` 用於 **TryAsyncRetry**（資源加載失敗後延遲重試），**不是**動畫定時器，無需刪除。
- **結論**：無需刪除「動畫定時器」實作；僅需清理 SkillEffect 註解中的「或 Timer 觸發」。

---

### 2.6 其他發現

1. **GameEntity.Visuals.Debug.cs（約 73 行）**  
   - 使用 `seq.Frames[0].ActionId` 作為「序列第一幀」的 ActionId 拼檔名。  
   - `seq.Frames[0]` 是**列表第一項**，未必是**播放順序第一幀**（應為 FrameIdx=1 的那一幀）。若 list.spr 寫法為 0,1,2,3,4，則 Frames[0] 可能是 FrameIdx=0。  
   - 若希望 Debug 顯示的 .spr 與「實際播放第一幀」一致，應改為：依同一 startIdx/order 規則取**播放順序第一幀**的 ActionId（即 FrameIdx=1 所在幀的 ActionId）。

2. **GameEntity.Visuals.ProcessFrameMetadata（約 432 行）**  
   - 使用 `tex.HasMeta("sounds")` 觸發音效；而 **CustomCharacterProvider** 在紋理上設置的是 **`spr_sound_ids`**。  
   - 因此 ProcessFrameMetadata 內的音效分支很可能**從未觸發**；實體音效實際由 **GameEntity.Audio**（order + SprFrame.SoundIds）負責。  
   - 建議：若要統一由紋理元數據播實體音效，應改為讀 `spr_sound_ids`；否則可視為冗餘或鍵名不一致，並在文檔註明。

---

## 三、錯誤與臃腫／過度代碼匯總

| 項目 | 類型 | 說明 |
|------|------|------|
| startIdx 條件 | **錯誤／與定義不符** | 應從「FrameIdx**==**1」的幀開始，目前為「FrameIdx**>=**1」；若無 FrameIdx=1 會從 2 開始。 |
| ListSprLoader 註解 | 文檔 | 註解寫「>= MinPlaybackFrameIdx」，與「第一幀必須是 FrameIdx=1」不一致，建議改為「== MinPlaybackFrameIdx 的幀」。 |
| SkillEffect 註解 | 臃腫／過時 | 「可能由 AnimationFinished 或 Timer 觸發」應改為僅 AnimationFinished。 |
| _lastPlayedSoundAnim/Frame | 可選刪除 | 若嚴格「不對音效做去重」，可刪除此防重邏輯；若僅禁止合併跳過幀音效，可保留。 |
| GameEntity.Visuals.Debug | 一致性 | 使用 Frames[0] 而非「播放第一幀（FrameIdx=1）」的 ActionId，可改為與播放規則一致。 |
| ProcessFrameMetadata "sounds" | 冗餘／錯誤鍵名 | 使用 "sounds" 而 Provider 設 "spr_sound_ids"，導致此處音效從未觸發；可改鍵名或移除。 |

---

## 四、建議修改清單（待您同意後再改）

1. **CustomCharacterProvider.cs** 與 **GameEntity.Audio.cs**：將 startIdx 改為「找到 **FrameIdx == SprPlaybackRule.MinPlaybackFrameIdx** 的幀的索引」；若找不到則 fallback（例如 startIdx=0 或保持現行 >=1，由您決定）。
2. **ListSprLoader.cs**：常數註解改為「第一幀為 FrameIdx == MinPlaybackFrameIdx 的幀」。
3. **SkillEffect.cs**：註解「僅執行一次（可能由 AnimationFinished 或 Timer 觸發）」改為「僅執行一次（由 AnimationFinished 觸發）」；若您選擇嚴格不做音效去重，則刪除 _lastPlayedSoundAnim / _lastPlayedSoundFrame 的防重邏輯。
4. **GameEntity.Visuals.Debug.cs**（可選）：改為依播放順序取「第一幀（FrameIdx=1）」的 ActionId，與播放規則一致。
5. **GameEntity.Visuals.ProcessFrameMetadata**（可選）：改為讀 `spr_sound_ids` 或移除音效分支，避免冗餘與鍵名不一致。

若您同意上述方向，我可以依此給出對應檔案的具體修補片段（僅改上述幾處，不做大規模重寫）。
