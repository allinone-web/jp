# PakBrowser 開發文檔

遊戲素材瀏覽與調適工具，用於預覽 list.spr 定義的角色動畫、多層疊加（主體 / 陰影 / 服裝 / 幀內特效）及對齊除錯。本文檔說明 PakBrowser 的全部功能、開發過程中曾發生的錯誤與正確邏輯規則、以及所有依賴檔案與必備功能。

---

## 0. 檔案位置與入口

| 項目 | 路徑 |
|------|------|
| 主類別 | `Client/Utility/PakBrowser.cs`，`namespace Client.Utility`，繼承 `Control` |
| 場景入口 | `Client/UI/Scenes/PakBrowser.tscn`，根節點掛載 `PakBrowser.cs` |
| 啟動方式 | 從 Godot 執行該場景，或由 Boot/主選單切換到 PakBrowser 場景 |

---

## 1. 功能概述

| 功能 | 說明 |
|------|------|
| **角色列表** | 從 list.spr 載入所有 GfxId，可依元數據篩選（all / 101 / 102 / 104 / 105 / 106 / 109） |
| **多層預覽** | 第 1 層主體、第 2 層 101.shadow、第 3 層 105.clothes、第 4 層 ]effectId 幀內特效，中心對齊 |
| **動作區** | 依當前角色 SprActionSequence 列出動作按鈕（0.walk、1.attack 等），點選後播放 |
| **對齊規則** | 依 sprite_offsets 與 batch_merge 規則，僅用首幀 (frame 0) 計算 Offset，三層穩定置中 |
| **幀同步** | 陰影/服裝/特效僅跟隨主體幀，SpeedScale=0；主體非循環結束時 AnimationFinished 強制同步 |
| **命名與實際檔名** | 資訊區顯示「命名規則」(gfxId, refGfxId, act, head → 首幀檔名) 與「當前實際播放」檔名，便於除錯 |
| **幀效果** | 播放到對應幀時：`[` / `<` 觸發音效；`]212` 觸發加載並顯示 gfxId=212 的動畫疊加 |

---

## 2. PakBrowser 類成員與方法一覽

### 2.1 常數與路徑

- `LIST_SPR_PATH` = `"res://Assets/list.spr"`
- `PAK_OPTIONS` = `["res://Assets/sprites-138-new2.pak", "res://Assets/sprites-138.pak"]`

### 2.2 核心元件（欄位）

| 欄位 | 類型 | 說明 |
|------|------|------|
| `_provider` | CustomCharacterProvider | 負責從 PAK + list.spr 建構 SpriteFrames |
| `_bodySprite` | AnimatedSprite2D | 主體層，唯一 SpeedScale 受 _isPlaying 控制，訂閱 FrameChanged / AnimationFinished |
| `_shadowSprite` | AnimatedSprite2D | 第 2 層 101.shadow，Modulate 半透明，SpeedScale=0 |
| `_clothesSprite` | AnimatedSprite2D | 第 3 層 105.clothes，SpeedScale=0 |
| `_effectSprite` | AnimatedSprite2D | 第 4 層 ]effectId 幀內特效，預設 Visible=false，SpeedScale=0 |
| `_sfxPlayer` | AudioStreamPlayer | 幀音效 [ / < 播放，Bus 可為 "SFX" |
| `_infoLabel` | Label | 預覽區左上角顯示 Gfx/Act/Dir/幀數/命名規則/實際播放檔名 |
| `_gfxList` | ItemList | 左側角色列表，ItemMetadata 存 GfxId |
| `_actionGrid` | GridContainer | 動作按鈕區，Columns=8 |
| `_listSprText` | TextEdit | 右側 list.spr 對應區塊原文 |

### 2.3 狀態欄位

- `_currentGfxId`：當前選中角色 GfxId，-1 表示未選
- `_currentActionId`：當前動作 ID（預設 0 = Walk）
- `_currentHeading`：當前朝向 0..7（預設 4 = 正下）
- `_isPlaying`：是否自動播放
- `_currentPakIndex`：0 或 1，對應 PAK_OPTIONS
- `_currentParamFilter`：0=all，或 101/102/104/105/106/109 篩選角色列表

### 2.4 主要方法與流程

| 方法 | 說明 |
|------|------|
| `_Ready()` | 非同步啟動：SetupBaseUI → 延遲 0.1s → InitializeSystems（ListSprLoader.Load + new CustomCharacterProvider）→ BuildMainUI → RefreshGfxList |
| `InitializeSystems()` | 檢查 list.spr 與 PAK 檔案存在後，呼叫 ListSprLoader.Load(listPath)、new CustomCharacterProvider(pakPath) |
| `BuildMainUI()` | 建左/右分割、預覽區（charRoot 內 _shadowSprite → _bodySprite → _clothesSprite → _effectSprite）、控制列、動作區、list.spr 區、PAK 選擇、參數篩選 |
| `RefreshGfxList()` | 從 ListSprLoader.GetAllGfxIds() 取 ID，依 _currentParamFilter 篩選 ParsedMetadataIds，填入 _gfxList |
| `OnGfxSelected(long)` | 設定 _currentGfxId，RefreshActionButtons、RefreshListSprContent、PlayAnimation |
| `PlayAnimation()` | 取 body/shadow/clothes SpriteFrames（GetBodyFrames 多重重試），設動畫名、SpeedScale（主體依 _isPlaying，其餘 0），Frame=0，UpdateAllLayerOffsets，UpdateInfoLabel |
| `GetLayerFrameAnchor(layer, frameIndex, out dx, dy, w, h)` | 從圖層指定幀紋理讀取 meta spr_anchor_x/y 與寬高；(dx,dy) 語意為該幀圖像左上角在共同座標系 |
| `UpdateAllLayerOffsets()` | 僅用 frame 0：主體 Offset=0；Shadow/Clothes 依相對主體中心公式計算 Offset |
| `SyncShadowAndClothesToBodyFrame()` | 將 _shadowSprite / _clothesSprite / _effectSprite 的 Frame 設為 _bodySprite.Frame |
| `OnFrameChanged()` | SyncShadowAndClothesToBodyFrame、UpdateInfoLabel；讀紋理 meta spr_sound_ids → PlayFrameSound，effects → 載入並顯示 _effectSprite |
| `OnBodyAnimationFinished()` | 主體非循環動畫結束時呼叫 SyncShadowAndClothesToBodyFrame，三層一起停 |
| `PlayFrameSound(int soundId)` | 載入 res://Assets/CustomFantasy/sound/{soundId}.wav 或 .WAV 並播放 |
| `GetListSprBlock(path, gfxId)` | 從 list.spr 讀取以 #gfxId 開頭的區塊字串，供 _listSprText 顯示 |

---

## 3. 介面與元件

- **左側**：角色列表 (ItemList)，篩選按鈕（all, 101, 102, 104, 105, 106, 109）、PAK 選擇（sprites-138.pak / sprites-138-new2.pak）。
- **右側**：預覽區（多層 AnimatedSprite2D + 資訊 Label）、控制列（⏮/⏯/⏭、8 方向）、動作區 (GridContainer)、list.spr 原文 (TextEdit)。
- **預覽層級**（由下到上）：`_shadowSprite` → `_bodySprite` → `_clothesSprite` → `_effectSprite`，皆 `Centered=true`、`Position=Vector2.Zero`，Offset 由 `UpdateAllLayerOffsets()` 依 sprite_offsets 計算。

---

## 4. 主體 / 陰影 / 衣服三層對齊（當前最新標準）

**【代碼保護】此功能（body 主體 / shadow 陰影 / clothes 衣服三層對齊）已測試通過，不允許修改。若需修改必須獲得許可。**

### 4.0 對齊規則總覽

| 項目 | 說明 |
|------|------|
| **座標語意** | sprite_offsets 與紋理 meta 的 `(dx, dy)` = 該幀圖像**左上角**在共同座標系的位置（與 batch_merge_offsets.py 一致），**非**紋理內錨點。 |
| **Centered** | 主體、陰影、服裝層皆 `Centered = true`，紋理以節點為中心繪製，角色視覺中心穩定。 |
| **主體** | 主體中心置於「節點原點 + BodyOffset」；PakBrowser 為 `Offset = Vector2.Zero`（畫面中心），遊戲為 `Offset = BodyOffset`。 |
| **陰影** | **僅用首幀 (frame 0)** 的 (dx,dy,w,h) 計算 Offset，與主體對齊；公式見下。若每幀重算會導致 Walk 時陰影左右晃動。 |
| **服裝** | **遊戲內**：每幀用當前幀 (dx,dy,w,h) 計算 Offset，修復 gfx=240 等錯位。**PakBrowser**：僅首幀計算（預覽用）。 |
| **幀同步** | 陰影/服裝 `SpeedScale = 0`，不自動播；每幀與動畫結束時將 Frame 設為主體當前幀。 |

### 4.0.1 正確公式（與 batch_merge 一致）

- **主體**  
  - PakBrowser：`_bodySprite.Offset = Vector2.Zero`（主體中心在畫面 (0,0)）。  
  - 遊戲：`_mainSprite.Offset = BodyOffset`（主體中心在 node + BodyOffset）。

- **陰影 / 服裝（相對主體中心）**  
  共同座標系下層的左上在 `(layerDx, layerDy)`，主體中心在 `(bodyDx + bodyW/2, bodyDy + bodyH/2)`。  
  以主體中心為原點時，層左上應在 `(layerDx - bodyDx - bodyW/2, layerDy - bodyDy - bodyH/2)`。  
  `Centered = true` 時紋理中心在 Offset，故紋理左上在 `(Offset.x - layerW/2, Offset.y - layerH/2)`。令其等於上式得：

  **Offset = (layerDx - bodyDx - bodyW/2 + layerW/2, layerDy - bodyDy - bodyH/2 + layerH/2)**

  遊戲內再統一加上 BodyOffset：`layer.Offset = BodyOffset + new Vector2(ox, oy)`。

### 4.0.2 之前無法對齊的錯誤原因與正確做法

| 錯誤做法 | 後果 | 正確做法 |
|----------|------|----------|
| 將 (dx,dy) 當成「紋理內錨點」，使用 `Offset = (w/2-dx, h/2-dy)` 或每幀重算 | 主體/魔法球往右下漂移、角色走出中心再退回 | (dx,dy)=左上角在共同座標系；主體置中、Shadow/Clothes 用相對主體中心公式 |
| 陰影/服裝**每幀**用當前幀 (dx,dy) 計算 Offset（且 Centered=false） | Walk 時陰影左右晃動、服裝與主體錯位 | 陰影**僅首幀 (frame 0)** 算 Offset；服裝在遊戲內可每幀算以修復部分 gfx 錯位 |
| 陰影使用主體的首幀 (dx,dy) 作為陰影的錨點 | 陰影跑到角色頭頂 | 陰影使用**陰影自身**首幀 (dx,dy)，與主體公式一致但數據來自陰影紋理 |
| 服裝/陰影用 targetDef 動作序列或不同幀序 | 錯圖、方向 6 等錯位 | 服裝/陰影**僅用 refDef（主體）動作序列**，檔名後綴與主體一致 |

### 4.0.3 遊戲 (GameEntity.Visuals) 與 PakBrowser 對齊對應關係

- **CreateLayer**：皆 `Centered = true`。  
- **UpdateAllLayerOffsets()**（遊戲）：僅設定**主體 + 陰影**，且**僅用 frame 0**；主體 `Offset = BodyOffset`，陰影 `Offset = BodyOffset + (ox, oy)`，公式同上。服裝**不**在此設定，改由 **UpdateClothesOffsetsPerFrame()** 每幀計算。  
- **UpdateClothesOffsetsPerFrame()**（遊戲）：每幀用主體與各服裝層**當前幀**的 (dx,dy,w,h) 套用同一相對公式，修復 gfx=240 等錯位。  
- **OnMasterFrameChanged**：呼叫 SyncSlaveLayersToBodyFrame（幀同步）+ UpdateClothesOffsetsPerFrame + ProcessFrameMetadata。  
- **PakBrowser**：UpdateAllLayerOffsets 一次計算主體/陰影/服裝，皆僅首幀；預覽場景無 BodyOffset，主體 Offset=0。

---

### 4.1 對齊規則（與 batch_merge_offsets.py 一致）

- **sprite_offsets 語意**：文件中 `(dx, dy)` = 該幀圖像**左上角**在共同座標系的位置（非紋理內錨點）。batch_merge 拼圖時用此座標貼圖，檔名後綴 `-{fileAct}-{frameIdx}.png` 相同，僅前綴 SpriteId 不同（如 734 / 736 / 242）。
- **PakBrowser 對齊**：
  - 主體中心置於畫面中心 (0,0) → `_bodySprite.Offset = Vector2.Zero`。
  - Shadow/Clothes 相對主體：陰影左上應在 `(sDx - bodyDx - bodyW/2, sDy - bodyDy - bodyH/2)`，故  
    `Offset = (sDx - bodyDx - bodyW/2 + sW/2, sDy - bodyDy - bodyH/2 + sH/2)`。
  - **僅用首幀 (frame 0)** 的 (dx, dy, w, h) 計算 Offset，避免每幀漂移（否則會出現「魔法球往右下依次播放」「主體偏離中心」）。

### 4.2 服裝/陰影幀順序（唯一規則）

- **list.spr 設計**：服裝/陰影不在 list.spr 定義動作序列，與主體整體設計、分層輸出，動作與幀序完全相同。
- **CustomCharacterProvider.BuildLayer**：當 `gfxId != refGfxId`（服裝/陰影/武器）時，**必須且僅能用 refDef（主體）的動作序列**取得幀順序，再從 targetDef 的 SpriteId 載入對應 `(fileAct, frameIdx)` 的圖。無 fallback 到 targetDef 的動作序列。
- **檔名**：`loadId = targetDef.SpriteId`（圖片前綴），`fileAct = f.ActionId + offset`（與主體同），檔名 `{loadId}-{fileAct}-{frameIdx:D3}.png` 與 batch_merge 後綴一致。

### 4.3 幀同步與停止

- 陰影/服裝：`SpeedScale = 0`，不自動播，僅在 `OnFrameChanged` 與 `OnBodyAnimationFinished` 中依主體當前幀設定 `Frame`，使 1.attack、3.breath 等非循環結束時三層一起停。
- 效果層：同為 SpeedScale=0，幀由 `SyncShadowAndClothesToBodyFrame` 與主體同步。

### 4.4 幀效果三類

| 符號 | 含義 | 資料來源 | 實作 |
|------|------|----------|------|
| `[86` | 幀音效 ID 86 | SprFrame.SoundIds → 紋理 meta `spr_sound_ids` | `PlayFrameSound(86)`，路徑 `res://Assets/CustomFantasy/sound/{id}.wav` |
| `<97` | 幀音效 ID 97 | 同上（ListSprLoader 已將 `<` 解析進 SoundIds） | 同上 |
| `]212` | 幀內特效 gfxId=212 | SprFrame.EffectIds → 紋理 meta `effects` | 載入 GfxId 212 的 SpriteFrames 顯示於 `_effectSprite`，與主體同 action/heading |

---

## 5. 開發過程中曾發生的錯誤與修正

### 5.1 對齊錯誤（已修正）

- **錯誤**：沿用「紋理內錨點」解讀，使用 `Offset = (w/2 - dx, h/2 - dy)` 且**每幀**重算，導致主體/魔法球往右下漂移、主體偏離中心。
- **正確**：採用與 batch_merge 一致之「(dx,dy)=左上角在共同座標系」；主體 Offset=(0,0)，Shadow/Clothes 用相對主體中心的公式；且**僅用首幀 (frame 0)** 計算 Offset。

### 5.2 陰影/服裝幀不同步（已修正）

- **錯誤**：陰影使用固定 action 0 (walk)，且陰影 SpeedScale=1 自播，導致 1.attack、3.breath 結束後陰影仍循環播放。
- **正確**：陰影與主體同 action（優先 _currentActionId，再 fallback 0）；陰影/服裝一律 SpeedScale=0，僅在 OnFrameChanged 與 OnBodyAnimationFinished 中依主體幀寫入 Frame。

### 5.3 服裝錯圖 / 錯幀（已修正）

- **錯誤**：BuildLayer 時 `seq = GetActionSequence(targetDef, actionId) ?? GetActionSequence(refDef, actionId)`，若服裝在 list.spr 有自定義動作且幀數/順序與主體不同，會導致 body.Frame 與 clothes.Frame 對齊到錯的邏輯幀，出現錯圖（尤其方向 6）。
- **正確**：服裝/陰影**僅用 refDef 的動作序列**，無 targetDef fallback；breath fallback 的 walk 序列亦僅用 refDef。

### 5.4 ListSprLoader 註解錯誤（已修正）

- **錯誤**：`RedirectId` 註解寫成 `'<' 符號: 動作跳轉`，但實際 `<` 為幀音效，已解析進 SoundIds。
- **正確**：RedirectId 註解改為「動作跳轉（若有其他符號定義）；'<' 與 '[' 均為幀音效，已解析進 SoundIds」；SoundIds 註解為「'[' 與 '<' 符號: 幀音效（如 [86、<97）」；EffectIds 註解為「']' 符號: 幀內觸發特效，如 ]212 表示加載 gfxId=212」。

### 5.5 魔法球/主體偏離中心（同上對齊錯誤）

- 與 5.1 同一根因：每幀重算 Offset 且誤解 (dx,dy) 語意，修正後僅用首幀、主體置中、其他層相對主體中心。

---

## 6. 依賴與必備檔案

PakBrowser 依賴以下模組與資源，缺一不可。

### 6.1 程式依賴與必備 API

| 檔案 | 必備功能 |
|------|----------|
| **Client/Utility/ListSprLoader.cs** | 解析 list.spr：`Load(string path)`；型別 `SprDefinition`（GfxId, SpriteId, Name, ShadowId, ClothesIds, Actions, ParsedMetadataIds 等）、`SprActionSequence`（ActionId, Name, DirectionFlag, Frames）、`SprFrame`（ActionId, FrameIdx, SoundIds, EffectIds 等）；靜態方法 `GetAllGfxIds()`、`Get(int gfxId)`、`GetActionSequence(SprDefinition def, int actionId)`。 |
| **Skins/CustomFantasy/CustomCharacterProvider.cs** | 建構函數 `CustomCharacterProvider(string globalPakPath)`；方法 `GetBodyFrames(int gfxId, int actionId, int heading)`、`GetBodyFrames(int gfxId, int refGfxId, int actionId, int heading)`。服裝/陰影僅用 refDef 動作序列；建構紋理時寫入 meta：`spr_anchor_x`、`spr_anchor_y`、`spr_file_name`、`spr_sound_ids`（Array&lt;int&gt;）、`effects`（Array&lt;int&gt;）。 |
| **Client/Utility/PngInfoLoader.cs** | 靜態 `Load(string path)` 載入 sprite_offsets 檔；`TryGetFrame(int gfxId, int fileActionId, int frameIdx, out PngFrameInfo info)`，PngFrameInfo 含 Dx、Dy。Provider 建構紋理時以此覆寫 dx/dy 並寫入紋理 `spr_anchor_x`/`spr_anchor_y`，PakBrowser 的 GetLayerFrameAnchor 讀取此 meta。 |
| **Skins/CustomFantasy 內 PAK 載入** | Sprite138PakLoader + PakArchiveReader 讀取 .pak 圖檔（由 CustomCharacterProvider 使用），需能依 (loadId, fileAct, frameIdx) 取得紋理。 |

### 6.2 資源路徑（PakBrowser 與 Provider 使用）

| 路徑 | 用途 |
|------|------|
| `res://Assets/list.spr` | 角色與動作定義（#GfxId、101.shadow、105.clothes、動作幀 token）。 |
| `res://Assets/sprites-138.pak` / `res://Assets/sprites-138-new2.pak` | 圖像資源包（任選其一存在即可）。 |
| `res://Assets/sprite_offsets-138_update.txt` | 每幀 (dx, dy)，格式見下節；Provider 建構時經 PngInfoLoader 載入並注入紋理 meta。 |
| `res://Assets/CustomFantasy/sound/{soundId}.wav` 或 `.WAV` | 幀音效檔（[ / < 對應的 ID）；若不存在則僅打 log 不崩潰。 |

### 6.3 sprite_offsets 檔案格式（PngInfoLoader 用）

- 標題行：`#GfxId-FileActionId`，例如 `#240-0`。PngInfoLoader 內部 key 為 `"GfxId_FileActionId"`（如 `"240_0"`）。
- 幀行：`FRAME &lt;frameIdx&gt; dx=&lt;dx&gt; dy=&lt;dy&gt; bmp=&lt;filename&gt;`，例如 `FRAME 0 dx=12 dy=-34 bmp=240-0-000.png`。
- TryGetFrame(gfxId, fileActionId, frameIdx) 用此 key 與 frameIdx 查詢 Dx、Dy，供 Provider 寫入紋理 meta。

### 6.4 參考工具（對齊規則對照）

- **batch_merge_offsets.py**（專案外）：以檔名後綴匹配 body/weapon/clothes，拼圖與座標計算；PakBrowser 對齊公式與其「(dx,dy)=左上角、同後綴」一致。

---

## 7. 資料流摘要

1. **啟動**：`_Ready` → SetupBaseUI → 延遲 0.1s → InitializeSystems（檢查 list.spr + PAK，ListSprLoader.Load，new CustomCharacterProvider(pakPath)，Provider 內 PngInfoLoader.Load(sprite_offsets)）→ BuildMainUI → RefreshGfxList。
2. **選角色**：OnGfxSelected → RefreshActionButtons、RefreshListSprContent、PlayAnimation。
3. **PlayAnimation**：依 _currentGfxId / _currentActionId / _currentHeading 取 body/shadow/clothes SpriteFrames（Provider.GetBodyFrames），設動畫名、SpeedScale（主體依 _isPlaying，陰影/服裝=0），隱藏 _effectSprite，設三層 Frame=0，UpdateAllLayerOffsets，UpdateInfoLabel。
4. **每幀**：OnFrameChanged → SyncShadowAndClothesToBodyFrame、UpdateInfoLabel；讀取當前幀紋理 meta：spr_sound_ids → PlayFrameSound，effects → 顯示 _effectSprite 並載入對應 GfxId。
5. **對齊**：UpdateAllLayerOffsets 僅用 frame 0 的 (dx,dy,w,h)，主體 Offset=0，Shadow/Clothes 用相對主體中心公式。

---

## 8. 修改記錄與相關文檔

- **docs/pakbrowser-clothes-sync-changelog.md**：服裝/陰影幀順序、對齊、唯一規則等代碼級變更說明。
- **docs/role-walk-center-alignment-fix.md**：遊戲內 Walk 對齊與 PakBrowser 一致策略（Centered=true、首幀錨點等）。

---

## 9. 小結

- **對齊**：與 batch_merge 一致，(dx,dy)=左上角；僅首幀算 Offset；主體置中，Shadow/Clothes 相對主體。
- **幀序**：服裝/陰影僅用主體(refDef)動作序列，檔名後綴與主體一致。
- **同步**：陰影/服裝/特效僅跟隨主體幀，非循環結束時一併停止。
- **效果**：`[`/`<` 音效實播，`]effectId` 實顯示對應 GfxId 動畫。
- **除錯**：資訊區顯示命名規則與當前實際播放檔名，便於對照 list.spr 與 PAK 檔名。
