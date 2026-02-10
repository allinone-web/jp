# Branch 對比：角色 / 魔法 / 陰影 / 服裝 邏輯與公式

用於對比三個 branch 的 GameEntity.Visuals 與 PakBrowser 邏輯。

---

## 【當前最新標準】遊戲內三層對齊（以程式碼為準）

**此功能（body 主體 / shadow 陰影 / clothes 衣服三層對齊）已測試通過，不允許修改。若需修改必須獲得許可。** 詳見 `docs/pakbrowser-development.md` 第 4 節。

| 項目 | 內容 |
|------|------|
| **Centered** | **true**（CreateLayer 內設定） |
| **(dx,dy) 語意** | 該幀圖像**左上角**在共同座標系（與 batch_merge、PakBrowser 一致） |
| **主體** | `_mainSprite.Offset = BodyOffset`，**僅用 frame 0**（UpdateAllLayerOffsets） |
| **陰影** | **僅用 frame 0** 計算 Offset，公式 `BodyOffset + (sDx - bodyDx - bodyW/2 + sW/2, ...)` |
| **服裝** | **每幀**在 OnMasterFrameChanged 內呼叫 UpdateClothesOffsetsPerFrame()，公式同陰影，用當前幀 (dx,dy,w,h) |
| **106.weapon** | 已徹底移除 |
| **幀同步** | SyncSlaveLayersToBodyFrame；陰影/服裝 SpeedScale=0，Frame 跟隨主體 |
| **Walk 快取** | 無（不區分 Walk，主體/陰影皆首幀算 Offset） |

---

## 一、jan29-magic-v2 — 遊戲 (GameEntity.Visuals) 邏輯與公式

**收集來源**：當前工作區 `Client/Game/GameEntity.Visuals.cs` 與 `GameEntity.cs`（標註為 jan29-magic-v2）。

### 1. 圖層建立

- **CreateLayer**：`Centered = false`  
  - 註解：Offset 代表**左上角**相對於 Pivot 的位置。

### 2. Offset 計算（每幀 / Walk 快取）

- **UpdateLayerOffset(layer, manualTweak, overrideAnchor?)**
  - 取得 (dx, dy)：有 `overrideAnchor` 則用其 X,Y；否則從**當前幀**紋理 meta `spr_anchor_x` / `spr_anchor_y` 讀取。
  - **公式**：`layer.Offset = (dx, dy) + manualTweak`，即 `Offset = (dx, dy) + BodyOffset`。
  - 語意：把 (dx, dy) 當成錨點/左上角數據，直接加上 BodyOffset，**每幀可變**（除非用 override）。

### 3. Walk 專用快取（GameEntity.cs）

- `_walkAnchorCache`：主體**首幀**的 (dx, dy)。
- `_walkShadowAnchorCache`：陰影**首幀**的 (dx, dy)。
- 僅在 `_currentRawAction == ACT_WALK` 時使用；非 Walk 時清空。

### 4. OnMasterFrameChanged 行為

- **Walk 時**（f==0）：寫入 _walkAnchorCache（主體首幀 dx,dy）、_walkShadowAnchorCache（陰影首幀 dx,dy）。
- **主體**：`UpdateLayerOffset(_mainSprite, BodyOffset, mainOverride)`  
  - mainOverride = Walk 時 _walkAnchorCache，否則 null → 非 Walk 時**每幀**從紋理讀 (dx,dy)。
- **武器**：同步 Frame，`UpdateLayerOffset(_weaponLayer, BodyOffset, mainOverride)`（與主體同錨點）。
- **陰影**：同步 Frame，`UpdateLayerOffset(_shadowLayer, BodyOffset, shadowOverride)`  
  - shadowOverride = Walk 時 _walkShadowAnchorCache，否則 null → 非 Walk 時每幀讀陰影紋理 (dx,dy)。
- **服裝**：同步 Frame，`UpdateLayerOffset(cl, BodyOffset, mainOverride)`（與主體同錨點，Walk 時用主體首幀）。

### 5. 服裝層去黑

- **GetClothesMaterial()**：Shader 對所有服裝層  
  - `brightness = max(r, g, b)`，若 `brightness < 0.4` 則 `discard`。  
- 服裝池中每個 layer 都套用此 Material。

### 6. 魔法

- 角色實體視覺**不負責**魔法本體顯示（魔法在 SkillEffect）。
- 幀元數據：`tex.HasMeta("sounds")` 播音效；`tex.HasMeta("effects")` 觸發 SpawnEffect。

### 7. UpdateAppearance 結尾

- 非 Walk 時：`_walkAnchorCache = null`，`_walkShadowAnchorCache = null`。
- 然後呼叫 `OnMasterFrameChanged()`。

### 8. 小結（jan29-magic-v2）

| 項目           | 內容 |
|----------------|------|
| Centered       | false |
| Offset 公式    | `(dx, dy) + BodyOffset`，dx/dy 來自紋理 meta 或 Walk 首幀快取 |
| 幀與錨點       | 非 Walk 時**每幀**重算 (dx,dy)；Walk 時主體/武器/服裝用主體首幀、陰影用陰影首幀 |
| 服裝去黑       | Shader 閾值 0.4，套用在所有服裝層 |
| Walk 快取      | 有 _walkAnchorCache、_walkShadowAnchorCache |
| 魔法透明度     | 未在 Visuals 處理（若在 SkillEffect/Provider 則另計） |

---

## 二、jan31-pakbrowser — PakBrowser 邏輯與公式 + 遊戲 (GameEntity.Visuals) 邏輯與公式

**收集來源**：當前工作區 `Client/Utility/PakBrowser.cs` 與 `Client/Game/GameEntity.Visuals.cs`（branch jan31-pakbrowser）。此 branch 中兩者對齊規則一致，以下分別列出後於「四、區別分析」對比。

---

### 二-A、PakBrowser 邏輯與公式

#### 1. 圖層建立

- **BuildMainUI** 內：`_shadowSprite` / `_bodySprite` / `_clothesSprite` / `_effectSprite` 皆  
  `Centered = true`，`Position = Vector2.Zero`。  
- 層級（由下到上）：shadow → body → clothes → effect。

#### 2. (dx, dy) 語意與 GetLayerFrameAnchor

- **sprite_offsets**：`(dx, dy)` = 該幀圖像**左上角**在共同座標系的位置（非紋理內錨點），與 batch_merge_offsets.py 一致。
- **GetLayerFrameAnchor(layer, frameIndex, out dx, dy, w, h)**：從圖層指定幀紋理讀取 meta `spr_anchor_x` / `spr_anchor_y` 與寬高；frameIndex &lt; 0 用當前幀。

#### 3. Offset 計算（僅首幀）

- **UpdateAllLayerOffsets()**：**僅用 frame 0**。
  - 主體：`_bodySprite.Offset = Vector2.Zero`（主體中心在畫面中心 (0,0)）。
  - 陰影：  
    `ox = sDx - bodyDx - bodyW/2 + sW/2`，  
    `oy = sDy - bodyDy - bodyH/2 + sH/2`，  
    `_shadowSprite.Offset = new Vector2(ox, oy)`。
  - 服裝：同式，`cDx/cDy/cW/cH` → `_clothesSprite.Offset = new Vector2(ox, oy)`。
- 呼叫時機：`PlayAnimation()` 內設定完 SpriteFrames 後呼叫一次。

#### 4. 幀同步

- 陰影/服裝：`SpeedScale = 0`，不自動播。
- **SyncShadowAndClothesToBodyFrame()**：將 _shadowSprite / _clothesSprite / _effectSprite 的 `Frame` 設為 _bodySprite.Frame。
- **OnFrameChanged**：呼叫 SyncShadowAndClothesToBodyFrame、UpdateInfoLabel；讀紋理 meta `spr_sound_ids` → PlayFrameSound，`effects` → 顯示 _effectSprite。
- **OnBodyAnimationFinished**：主體非循環結束時呼叫 SyncShadowAndClothesToBodyFrame，三層一起停。

#### 5. 服裝 / 魔法透明度

- 服裝層**無** Shader；魔法透明度由 CustomCharacterProvider 對 104.attr(8) 套用 BlackToTransparentProcessor（在 Provider 建構紋理時）。

#### 6. 小結（PakBrowser）

| 項目           | 內容 |
|----------------|------|
| Centered       | **true** |
| (dx,dy) 語意   | 該幀圖像**左上角**在共同座標系 |
| Offset 公式    | 主體 Offset=0；Shadow/Clothes `Offset = (layerDx - bodyDx - bodyW/2 + layerW/2, layerDy - bodyDy - bodyH/2 + layerH/2)` |
| 僅用首幀       | 是，frame 0 計算 Offset，避免漂移 |
| Walk 快取      | **無** |
| 服裝去黑       | 無 Shader；魔法 104.attr(8) 在 Provider 處理 |
| 幀同步         | Slave SpeedScale=0，OnFrameChanged + OnBodyAnimationFinished 同步 Frame |

---

### 二-B、遊戲 (GameEntity.Visuals) 邏輯與公式（jan31-pakbrowser 版）

#### 1. 圖層建立

- **CreateLayer**：`Centered = true`（註解：與 PakBrowser 一致，紋理以節點為中心繪製）。
- 無服裝 Shader；註解：魔法透明度由 CustomCharacterProvider 對 104.attr(8) 套用 BlackToTransparentProcessor。

#### 2. (dx, dy) 語意與 GetLayerFrameAnchor

- 與 PakBrowser 一致：從紋理 meta `spr_anchor_x` / `spr_anchor_y` 讀取 (dx, dy)=左上角，與 PngInfoLoader 一致。

#### 3. Offset 計算（僅首幀）

- **UpdateAllLayerOffsets()**：**僅用 frame 0**。
  - 主體：`_mainSprite.Offset = BodyOffset`（主體中心在 node+BodyOffset）。
  - 武器：  
    `ox = wDx - bodyDx - bodyW/2 + wW/2`，`oy = wDy - bodyDy - bodyH/2 + wH/2`，  
    `_weaponLayer.Offset = BodyOffset + new Vector2(ox, oy)`。
  - 陰影：同式，`_shadowLayer.Offset = BodyOffset + new Vector2(ox, oy)`。
  - 服裝：同式，`cl.Offset = BodyOffset + new Vector2(ox, oy)`。
- 呼叫時機：`UpdateAppearance()` 內設定完所有層 SpriteFrames 後呼叫一次，再呼叫 OnMasterFrameChanged()。

#### 4. 幀同步

- **SyncSlaveLayersToBodyFrame()**：將武器/陰影/服裝的 `Frame` 設為 _mainSprite.Frame（與 PakBrowser 的 SyncShadowAndClothesToBodyFrame 對應）。
- **OnMasterFrameChanged()**：僅呼叫 SyncSlaveLayersToBodyFrame()、ProcessFrameMetadata(f)；**不重設**各層 Offset（由 UpdateAllLayerOffsets 在 UpdateAppearance 時設定）。
- **OnUnifiedAnimationFinished()**（GameEntity.Action）：呼叫 SyncSlaveLayersToBodyFrame()，主體非循環結束時 Slave 幀同步。

#### 5. Walk 快取

- **無** _walkAnchorCache、_walkShadowAnchorCache、_walkClothesAnchorCache（此 branch 已移除，與 PakBrowser 一致：不區分 Walk，一律首幀算 Offset）。

#### 6. 小結（GameEntity.Visuals @ jan31-pakbrowser）

| 項目           | 內容 |
|----------------|------|
| Centered       | **true** |
| (dx,dy) 語意   | 該幀圖像**左上角**在共同座標系 |
| Offset 公式    | 主體 Offset=BodyOffset；武器/陰影/服裝 `Offset = BodyOffset + (layerDx - bodyDx - bodyW/2 + layerW/2, ...)` |
| 僅用首幀       | 是，frame 0 計算 Offset |
| Walk 快取      | **無** |
| 服裝去黑       | 無 Shader；魔法 104.attr(8) 在 Provider 處理 |
| 幀同步         | Slave SpeedScale=0，OnMasterFrameChanged + OnUnifiedAnimationFinished 同步 Frame |

---

### 二-C、jan31-pakbrowser 內 PakBrowser 與 GameEntity.Visuals 對齊關係

- **公式一致**：主體中心固定（PakBrowser 為 0,0；遊戲為 BodyOffset），Shadow/Clothes（遊戲含武器）相對主體中心使用同一相對公式，且**僅用首幀 (frame 0)**。
- **Centered**：皆為 true。
- **無 Walk 快取**：皆不區分動作，不維護 _walkAnchorCache 等。
- **服裝/魔法**：皆無服裝 Shader；魔法透明度在 Provider 對 104.attr(8) 處理。
- **幀同步**：Slave 皆 SpeedScale=0，每幀與動畫結束時同步到主體 Frame。

---

## 三、jan31-walkshadow-magic-ok — 遊戲 (GameEntity.Visuals) 邏輯與公式

**收集來源**：當前工作區 `Client/Game/GameEntity.Visuals.cs` 與 `GameEntity.cs`（branch jan31-walkshadow-magic-ok）。

### 1. 圖層建立

- **CreateLayer**：`Centered = false`  
  - 註解：Offset 代表**左上角**相對於 Pivot 的位置。（與 jan29-magic-v2 相同。）

### 2. Offset 計算（每幀 / Walk 快取）

- **UpdateLayerOffset(layer, manualTweak, overrideAnchor?)**
  - 取得 (dx, dy)：有 `overrideAnchor` 則用其 X,Y；否則從**當前幀**紋理 meta `spr_anchor_x` / `spr_anchor_y` 讀取。
  - **公式**：`layer.Offset = (dx, dy) + manualTweak`，即 `Offset = (dx, dy) + BodyOffset`。
  - 與 jan29-magic-v2 相同。

### 3. Walk 專用快取（GameEntity.cs）

- `_walkAnchorCache`：主體**首幀**的 (dx, dy)。
- `_walkShadowAnchorCache`：陰影**首幀**的 (dx, dy)。
- **`_walkClothesAnchorCache`**：`Vector2?[3]`，**105.clothes 各層自身首幀**的 (dx, dy)。  
  - 註解：Walk 時與下半身對齊（不跟頭部對齊），故服裝用**各自**首幀錨點，不用主體首幀。
- 僅在 `_currentRawAction == ACT_WALK` 時使用；非 Walk 時三種快取皆清空。

### 4. OnMasterFrameChanged 行為

- **Walk 時**（f==0）：
  - 寫入 _walkAnchorCache（主體首幀）、_walkShadowAnchorCache（陰影首幀）。
  - **寫入 _walkClothesAnchorCache[ci]**：對每個可見服裝層讀取該層 frame 0 的 spr_anchor_x/y，存入對應槽位。
- **主體**：`UpdateLayerOffset(_mainSprite, BodyOffset, mainOverride)`，mainOverride = Walk 時 _walkAnchorCache，否則 null。
- **武器**：同步 Frame，`UpdateLayerOffset(_weaponLayer, BodyOffset, mainOverride)`（與主體同錨點）。
- **陰影**：同步 Frame，`UpdateLayerOffset(_shadowLayer, BodyOffset, shadowOverride)`，shadowOverride = Walk 時 _walkShadowAnchorCache，否則 null。
- **服裝**：同步 Frame，**每層獨立** `clothesOverride = (isWalk && _walkClothesAnchorCache[ci].HasValue) ? _walkClothesAnchorCache[ci] : null`，再 `UpdateLayerOffset(cl, BodyOffset, clothesOverride)`。  
  - 即 Walk 時服裝用**各層自身首幀錨點**；非 Walk 時每幀讀該層紋理 (dx,dy)。

### 5. 服裝層去黑

- **GetClothesMaterial()**：與 jan29 相同，Shader `brightness < 0.4` 則 `discard`，套用在所有服裝層。

### 6. 魔法 / 幀元數據

- 角色實體視覺不負責魔法本體顯示（魔法在 SkillEffect）。
- **ProcessFrameMetadata**：`tex.HasMeta("key")` 觸發 OnAnimationKeyFrame；**無** `tex.HasMeta("sounds")` 播音效（註解：音效由 GameEntity.Audio 依 SprFrame.SoundIds 播放，此處不重複）；`tex.HasMeta("effects")` 觸發 SpawnEffect。

### 7. UpdateAppearance 結尾

- 非 Walk 時：清空 `_walkAnchorCache`、`_walkShadowAnchorCache`、**以及 `_walkClothesAnchorCache[ci]` 全部**。
- 然後呼叫 `OnMasterFrameChanged()`。

### 8. 小結（jan31-walkshadow-magic-ok）

| 項目           | 內容 |
|----------------|------|
| Centered       | false |
| Offset 公式    | `(dx, dy) + BodyOffset`，dx/dy 來自紋理 meta 或 Walk 首幀快取 |
| 幀與錨點       | 非 Walk 時**每幀**重算 (dx,dy)；Walk 時主體/武器用主體首幀、**陰影用陰影首幀**、**服裝用各層自身首幀** |
| 服裝去黑       | Shader 閾值 0.4，套用在所有服裝層 |
| Walk 快取      | _walkAnchorCache、_walkShadowAnchorCache、**_walkClothesAnchorCache[3]**（服裝各自首幀） |
| 幀音效         | Visuals 內**不**播音效（由 Audio 模組處理） |
| 魔法透明度     | 未在 Visuals 處理（若在 SkillEffect/Provider 則另計） |

---

## 四、區別分析

### 4.1 jan31-pakbrowser vs jan29-magic-v2（遊戲 Visuals）

| 項目 | jan31-pakbrowser | jan29-magic-v2 |
|------|------------------|----------------|
| **Centered** | true | false |
| **(dx,dy) 語意** | 該幀圖像**左上角**在共同座標系（與 batch_merge 一致） | 當作錨點/左上角，直接 `Offset = (dx,dy) + BodyOffset` |
| **Offset 公式** | 主體置中；Shadow/Clothes 相對主體中心：`layerDx - bodyDx - bodyW/2 + layerW/2` 等 | 每層 `Offset = (dx, dy) + BodyOffset`，dx/dy 每幀或 Walk 快取 |
| **僅用首幀** | 是，僅 frame 0 算 Offset | 否；非 Walk 時**每幀**重算 (dx,dy)；Walk 時用首幀快取 |
| **Walk 快取** | 無 | 有 _walkAnchorCache、_walkShadowAnchorCache |
| **服裝對齊** | 相對主體中心公式，首幀 | Walk 時用主體首幀錨點 (mainOverride) |
| **服裝去黑** | 無 Shader；魔法在 Provider 104.attr(8) | Shader 閾值 0.4，套用所有服裝層 |
| **幀同步** | SyncSlaveLayersToBodyFrame，OnMasterFrameChanged + AnimationFinished | 每幀 UpdateLayerOffset + 同步 Frame，無專用 SyncSlave |

**結論**：jan31-pakbrowser 採用「與 PakBrowser 一致」的對齊規則：Centered=true、僅首幀算 Offset、相對主體中心公式、無 Walk 快取、無服裝 Shader；jan29-magic-v2 為 Centered=false、每幀或 Walk 快取 (dx,dy)、服裝 Shader 去黑。

---

### 4.2 jan31-pakbrowser vs jan31-walkshadow-magic-ok（遊戲 Visuals）

| 項目 | jan31-pakbrowser | jan31-walkshadow-magic-ok |
|------|------------------|----------------------------|
| **Centered** | true | false |
| **Offset 公式** | 主體 BodyOffset；Shadow/武器/Clothes 相對主體中心，**僅首幀** | 每層 `Offset = (dx, dy) + BodyOffset`，dx/dy 每幀或 Walk 快取 |
| **僅用首幀** | 是，UpdateAllLayerOffsets 僅 frame 0 | 否；非 Walk 時每幀重算；Walk 時用各層首幀快取 |
| **Walk 快取** | 無 | 有 _walkAnchorCache、_walkShadowAnchorCache、**_walkClothesAnchorCache[3]** |
| **服裝 Walk 對齊** | 相對主體中心公式（首幀） | Walk 時服裝用**各層自身**首幀錨點 (_walkClothesAnchorCache[ci]) |
| **服裝去黑** | 無 Shader | Shader 閾值 0.4 |
| **幀同步** | SyncSlaveLayersToBodyFrame | 每幀 UpdateLayerOffset + 同步 Frame |

**結論**：jan31-pakbrowser 與 PakBrowser 完全同一套對齊（Centered=true、首幀相對公式、無快取）；jan31-walkshadow-magic-ok 保留 Walk 快取與服裝各自首幀、Centered=false、服裝 Shader。

---

### 4.3 jan29-magic-v2 vs jan31-walkshadow-magic-ok（遊戲 Visuals）

| 項目 | jan29-magic-v2 | jan31-walkshadow-magic-ok |
|------|----------------|----------------------------|
| **Walk 快取** | 主體、陰影 | 主體、陰影、**服裝各層** (_walkClothesAnchorCache) |
| **服裝 Walk 對齊** | Walk 時服裝用**主體**首幀錨點 (mainOverride) | Walk 時服裝用**各層自身**首幀錨點 |
| **幀音效** | Visuals 內 `tex.HasMeta("sounds")` 播音效 | Visuals 內不播音效（由 Audio 處理） |
| **其餘** | 同：Centered=false、UpdateLayerOffset、(dx,dy)+BodyOffset、服裝 Shader 0.4 | 同 |

**結論**：主要差別在 Walk 時服裝用主體首幀 vs 服裝各自首幀，以及幀音效是否在 Visuals 內播放。

---

### 4.4 三 branch 對照摘要

| 項目 | jan29-magic-v2 | jan31-walkshadow-magic-ok | jan31-pakbrowser |
|------|----------------|----------------------------|------------------|
| Centered | false | false | **true** |
| Offset 依據 | (dx,dy)+BodyOffset，每幀或 Walk 快取 | 同左，服裝各自快取 | **僅首幀**，相對主體中心公式 |
| Walk 快取 | 主體+陰影 | 主體+陰影+服裝各層 | **無** |
| 服裝去黑 | Shader 0.4 | Shader 0.4 | **無 Shader**（Provider 魔法） |
| 與 PakBrowser 一致 | 否 | 否 | **是**（遊戲 Visuals 與 PakBrowser 同規則） |

---

*收集順序：① jan29-magic-v2（已完成）；② jan31-pakbrowser 的 PakBrowser + GameEntity.Visuals（已完成）；③ jan31-walkshadow-magic-ok 的 GameEntity.Visuals（已完成）。*
