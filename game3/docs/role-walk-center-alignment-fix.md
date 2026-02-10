# 角色 Walk / Shadow / Attack 對齊修復 — 完整技術文檔

---

## 【代碼保護說明】請務必遵守

**與主體 / 陰影 / 衣服三層對齊相關的代碼已調適正確，不允許擅自修改。**

- **範圍**：`GameEntity.Visuals.cs` 中的 **UpdateAllLayerOffsets**（主體+陰影，僅首幀）、**UpdateClothesOffsetsPerFrame**（服裝每幀）、**OnMasterFrameChanged**（幀同步+服裝 Offset）、**CreateLayer**（Centered=true），以及 `Client/Utility/PakBrowser.cs` 中的 **UpdateAllLayerOffsets**、**GetLayerFrameAnchor**。`GameEntity.cs` 的 **BodyOffset** 為全身偏移基準。
- **當前標準**：主體與陰影**僅用首幀 (frame 0)** 計算 Offset，與 PakBrowser、batch_merge 公式一致；服裝在遊戲內**每幀**計算以修復 gfx=240 等錯位。詳見 `docs/pakbrowser-development.md` 第 4 節「主體/陰影/衣服三層對齊」。
- **約定**：上述代碼實現「主體/陰影/衣服三層對齊、角色中心穩定、陰影在腳下不晃動」。**如需修改，必須獲得許可**，並在修改後回歸測試對齊表現。
- **文檔**：完整錯誤原因、正確做法見本文檔與 `pakbrowser-development.md`；代碼內亦有對應註釋標記「已測試通過，不允許修改」。

---

## 1. 任務概述

本任務分多階段修復「遊戲內角色、陰影、地圖圖層」相關問題：

| 階段 | 現象 | 結果 |
|------|------|------|
| 一 | 角色出生/傳送後位置與移動不一致；攻擊/移動點空地座標可能錯誤 | 統一實體 Position 公式、點擊換算地圖格時加上 `CurrentMapOrigin` |
| 二 | **僅 Walk 動作**時，角色圖片中心會「走出中心再退回」、亂飄；其他動作正常 | Walk 時主體/武器/服裝固定使用主體首幀錨點 `_walkAnchorCache` |
| 三 | Walk 時陰影跑到**角色頭頂**對齊 | 陰影層不傳 `mainOverride`，改為用陰影自身每幀錨點（後續再優化為陰影首幀錨點） |
| 四 | Walk 第 2–3 幀陰影**左右晃動**、與角色錯位；第一幀對齊 | 陰影專用 `_walkShadowAnchorCache`，Walk 時陰影固定使用陰影自身首幀錨點 `shadowOverride` |
| 五 | Map 4、Map 69 角色陰影被 **lowerland** 層遮住；Map 0 正常 | Map_4.tscn、Map_69.tscn 根節點增加 `z_index = -100`，與 Map_0 一致 |

---

## 2. 故障現象（用戶描述）

- 遊戲內：角色播放時**會走出中心位，然後退回**，即角色中心沒有永遠固定在畫面中心。
- PakBrowser 預覽：角色中心始終在畫面中心，表現正常。
- 僅限 **Walk 動作**有問題，其他動作（攻擊、待機、受擊等）正常。
- 修復主體後：Walk 時**陰影**曾出現在頭頂；改為陰影用自身錨點後，第一幀對齊腳下，但第 2–3 幀陰影左右晃動、未與角色對齊。
- Map 0 地圖正常；Map 4、Map 69 陰影被 lowerland 遮住。

---

## 3. 根因分析

### 3.1 為何 PakBrowser 正常、遊戲內異常？

| 項目 | PakBrowser | 遊戲內 GameEntity.Visuals |
|------|------------|---------------------------|
| AnimatedSprite2D | `new AnimatedSprite2D()`，未設置 `Centered`、`Offset` | `CreateLayer()` 中 `Centered = false`，`Offset = (dx, dy)` |
| Godot 預設 | `Centered = true` → 紋理以節點為中心繪製 | `Centered = false` → 紋理左上角在 `Position + Offset` |
| 結果 | 視覺中心始終在 charRoot 位置 | 每幀從紋理讀取 `spr_anchor_x/y`，Offset 隨幀變化 |

**結論**：遊戲內為與 list.spr/PAK 錨點對齊，採用 `Centered = false` + 每幀 `Offset = (dx, dy) + BodyOffset`。當某動作每幀 dx/dy 不同時，視覺中心就會隨幀移動。

### 3.2 為何只有 Walk 會亂飄？

- **攻擊、待機、受擊等**：同動作內各幀錨點較一致，視覺中心穩定。
- **Walk**：左右腳、重心隨幀變化，**每幀 (dx, dy) 差異大**，導致「中心隨幀來回移動」。

### 3.3 為何 Walk 時陰影曾跑到頭頂？

- 修復主體亂飄時，對**主體、武器、陰影、服裝**都傳入了**主體首幀錨點** `mainOverride`。
- 主體的 (dx, dy) 對應**身體中心/錨點**，陰影的 (dx, dy) 在美術上對應**腳下**。陰影被強制用主體錨點後，視覺上就跑到頭頂。

**錯誤**：陰影層使用 `UpdateLayerOffset(_shadowLayer, BodyOffset, mainOverride)`。  
**正確**：陰影不可使用主體錨點；應使用陰影**自身**的錨點（見下節）。

### 3.4 為何 Walk 第 2–3 幀陰影左右晃動？

- 改為陰影不傳 `mainOverride` 後，陰影改為用**自身每幀紋理**的 (dx, dy)。
- Walk 動畫中陰影各幀的 (dx, dy) **不一致**，導致陰影相對角色左右晃動、錯位。

**錯誤**：Walk 時陰影仍每幀讀取當前幀紋理錨點。  
**正確**：Walk 時陰影也應**固定使用陰影自身首幀錨點**，與主體策略一致但使用陰影專用快取 `_walkShadowAnchorCache`。

### 3.5 為何 Map 4、Map 69 陰影被 lowerland 遮住？

- **Map_0.tscn**：根節點 `map0` 設有 **`z_index = -100`**，整張地圖（含 lowerland）繪製在後，角色與陰影在前。
- **Map_4.tscn / Map_69.tscn**：根節點未設 `z_index`，預設為 0，lowerland 與角色/陰影同層或更前，會蓋住陰影。

---

## 4. 修復方案概述

- **主體/武器/服裝（Walk）**：僅 Walk 時使用主體首幀錨點 `_walkAnchorCache`，非 Walk 時清空，其餘動作每幀讀當前幀錨點。
- **陰影（Walk）**：  
  - 不可使用主體錨點（避免陰影跑到頭頂）。  
  - Walk 時使用**陰影自身首幀錨點** `_walkShadowAnchorCache`，避免第 2–3 幀左右晃動；非 Walk 時陰影用每幀錨點（Attack 等保持不變）。
- **地圖**：Map_4、Map_69 根節點增加 `z_index = -100`，與 Map_0 一致。

---

## 5. 具體代碼修改

### 5.1 新增欄位（GameEntity.cs）

**檔案**：`Client/Game/GameEntity.cs`  
**位置**：私有變量區，與 `_lastSpawnedEffectFrame` 相鄰。

**代碼**：

```csharp
/// <summary>Walk 動作專用：僅在走路時使用首幀 (dx,dy) 固定錨點，避免每幀 dx/dy 不同導致角色中心晃動。</summary>
internal Vector2? _walkAnchorCache = null;
/// <summary>Walk 動作專用：陰影層使用自身首幀錨點，避免第 2–3 幀左右晃動、與角色錯位。</summary>
internal Vector2? _walkShadowAnchorCache = null;
```

**參數說明**：  
- `_walkAnchorCache`：主體首幀 (dx, dy)，供主體、武器、服裝在 Walk 時使用。  
- `_walkShadowAnchorCache`：陰影層首幀 (dx, dy)，**僅**供陰影層在 Walk 時使用；與主體錨點分離，避免陰影跑到頭頂。  
- 非 Walk 時兩者均清空。

**相關常數**：`ACT_WALK = 0`。

---

### 5.2 UpdateLayerOffset 支援「覆寫錨點」（GameEntity.Visuals.cs）

**檔案**：`Client/Game/GameEntity.Visuals.cs`  
**方法**：`UpdateLayerOffset`

**簽名與邏輯要點**：

```csharp
/// <param name="overrideAnchor">Walk 專用：傳入時強制使用此 (dx,dy)，不讀當前幀紋理。</param>
private void UpdateLayerOffset(AnimatedSprite2D layer, Vector2 manualTweak, Vector2? overrideAnchor = null)
{
    if (layer == null || !layer.Visible || layer.SpriteFrames == null) return;

    int dx = 0;
    int dy = 0;
    if (overrideAnchor.HasValue)
    {
        dx = (int)overrideAnchor.Value.X;
        dy = (int)overrideAnchor.Value.Y;
    }
    else
    {
        var tex = layer.SpriteFrames.GetFrameTexture(layer.Animation, layer.Frame);
        if (tex == null) return;
        if (tex.HasMeta("spr_anchor_x")) dx = (int)tex.GetMeta("spr_anchor_x");
        if (tex.HasMeta("spr_anchor_y")) dy = (int)tex.GetMeta("spr_anchor_y");
    }

    Vector2 finalPos = new Vector2(dx, dy) + manualTweak;
    layer.Offset = finalPos;
    // ... 日誌等
}
```

**參數**：  
- `overrideAnchor == null`：從當前幀紋理讀取 `spr_anchor_x/y`（非 Walk 或未使用快取時）。  
- `overrideAnchor != null`：強制使用 `overrideAnchor.Value.X/Y`，不讀紋理（Walk 時主體用 `_walkAnchorCache`，陰影用 `_walkShadowAnchorCache`）。

---

### 5.3 OnMasterFrameChanged：Walk 主體 + 陰影首幀錨點（GameEntity.Visuals.cs）

**檔案**：`Client/Game/GameEntity.Visuals.cs`  
**方法**：`OnMasterFrameChanged()`

**邏輯要點**：

- 僅在 **Walk**（`_currentRawAction == ACT_WALK`）且 **f == 0** 時寫入快取：  
  - 主體第 0 幀紋理 → `_walkAnchorCache`。  
  - 陰影層第 0 幀紋理 → `_walkShadowAnchorCache`（陰影用**自身**首幀，與主體分離）。
- 非 Walk 時：`_walkAnchorCache = null`，`_walkShadowAnchorCache = null`。
- 計算 override：  
  - `mainOverride = (isWalk && _walkAnchorCache.HasValue) ? _walkAnchorCache : null`  
  - `shadowOverride = (isWalk && _walkShadowAnchorCache.HasValue) ? _walkShadowAnchorCache : null`
- 各層呼叫：  
  - 主體：`UpdateLayerOffset(_mainSprite, BodyOffset, mainOverride)`  
  - 武器：`UpdateLayerOffset(_weaponLayer, BodyOffset, mainOverride)`  
  - **陰影**：`UpdateLayerOffset(_shadowLayer, BodyOffset, shadowOverride)`（不可傳 `mainOverride`，否則陰影會到頭頂；必須用 `shadowOverride` 才能腳下對齊且不晃）  
  - 服裝：`UpdateLayerOffset(cl, BodyOffset, mainOverride)`

**代碼段**：

```csharp
// [Walk 專用] 僅走路時固定使用首幀錨點
bool isWalk = _currentRawAction == ACT_WALK;
if (isWalk)
{
    if (f == 0)
    {
        var tex = _mainSprite.SpriteFrames?.GetFrameTexture(_mainSprite.Animation, 0);
        if (tex != null)
        {
            int dx = tex.HasMeta("spr_anchor_x") ? (int)tex.GetMeta("spr_anchor_x") : 0;
            int dy = tex.HasMeta("spr_anchor_y") ? (int)tex.GetMeta("spr_anchor_y") : 0;
            _walkAnchorCache = new Vector2(dx, dy);
        }
        // 陰影層用自身首幀錨點，避免 Walk 第 2–3 幀左右晃動、與角色錯位
        if (_shadowLayer != null && _shadowLayer.Visible && _shadowLayer.SpriteFrames != null)
        {
            var stex = _shadowLayer.SpriteFrames.GetFrameTexture(_shadowLayer.Animation, 0);
            if (stex != null)
            {
                int sdx = stex.HasMeta("spr_anchor_x") ? (int)stex.GetMeta("spr_anchor_x") : 0;
                int sdy = stex.HasMeta("spr_anchor_y") ? (int)stex.GetMeta("spr_anchor_y") : 0;
                _walkShadowAnchorCache = new Vector2(sdx, sdy);
            }
        }
    }
}
else
{
    _walkAnchorCache = null;
    _walkShadowAnchorCache = null;
}

Vector2? mainOverride = (isWalk && _walkAnchorCache.HasValue) ? _walkAnchorCache : null;
Vector2? shadowOverride = (isWalk && _walkShadowAnchorCache.HasValue) ? _walkShadowAnchorCache : null;

// 1. 主體對齊 (Walk 時用首幀錨點，其餘用當前幀)
UpdateLayerOffset(_mainSprite, BodyOffset, mainOverride);

// 2. 武器對齊 (Walk 時同用首幀錨點，保持與主體一致)
if (_weaponLayer.Visible) {
    _weaponLayer.Frame = f % _weaponLayer.SpriteFrames.GetFrameCount(_weaponLayer.Animation);
    UpdateLayerOffset(_weaponLayer, BodyOffset, mainOverride);
}

// 3. 陰影對齊（Walk 時用陰影自身首幀錨點，避免第 2–3 幀左右晃動；非 Walk 用每幀錨點）
if (_shadowLayer.Visible) {
    _shadowLayer.Frame = f % _shadowLayer.SpriteFrames.GetFrameCount(_shadowLayer.Animation);
    UpdateLayerOffset(_shadowLayer, BodyOffset, shadowOverride);
}

// 4. 服裝層對齊 (Walk 時亦用首幀錨點)
foreach (var cl in _clothesPool) {
    if (cl.Visible) {
        cl.Frame = f % cl.SpriteFrames.GetFrameCount(cl.Animation);
        UpdateLayerOffset(cl, BodyOffset, mainOverride);
    }
}
```

**參數與含義**：

| 符號 | 含義 |
|------|------|
| `_currentRawAction` | 當前原始動作 ID（0=Walk, 1=Attack, 2=Damage, 3=Breath…） |
| `ACT_WALK` | 常數 0 |
| `f` | `_mainSprite.Frame`，當前主體動畫幀索引 |
| `_walkAnchorCache` | 主體首幀 (dx, dy)，僅供主體/武器/服裝 |
| `_walkShadowAnchorCache` | 陰影首幀 (dx, dy)，**僅供陰影**，與主體分離 |
| `mainOverride` | Walk 且快取有效時供主體/武器/服裝使用 |
| `shadowOverride` | Walk 且陰影快取有效時供陰影使用；非 Walk 為 null，陰影用每幀錨點（Attack 等不變） |

---

### 5.4 UpdateAppearance 清空 Walk 快取（GameEntity.Visuals.cs）

**檔案**：`Client/Game/GameEntity.Visuals.cs`  
**方法**：`UpdateAppearance(...)` 末尾，呼叫 `OnMasterFrameChanged()` 前。

**代碼**：

```csharp
// 7. 觸發偏移位置矯正（非 Walk 時清掉 walk 錨點快取）
_lastProcessedFrame = -1;
if (_currentRawAction != ACT_WALK) { _walkAnchorCache = null; _walkShadowAnchorCache = null; }
OnMasterFrameChanged();
```

**說明**：切換到非 Walk 時清空主體與陰影的 Walk 快取，避免殘留影響其它動作。

---

### 5.5 Map 4、Map 69 根節點 z_index（場景文件）

**錯誤**：Map_4.tscn、Map_69.tscn 根節點未設 `z_index`，lowerland 預設繪製順序與角色/陰影重疊或更前，遮住陰影。

**正確**：與 Map_0 一致，根節點設 `z_index = -100`，整張地圖繪製在後。

**檔案與修改**：

- `Skins/CustomFantasy/Maps/Map_4.tscn`  
  在 `[node name="Map4" type="Node2D"]` 下一行加入：
  ```text
  z_index = -100
  ```
- `Skins/CustomFantasy/Maps/Map_69.tscn`  
  在 `[node name="Map69" type="Node2D"]` 下一行加入：
  ```text
  z_index = -100
  ```

---

## 6. 第一階段修復摘要（實體 Position 與點擊座標）

- **OnObjectSpawned（GameWorld.Entities.cs）**  
  - 錯誤：`Position = new Vector2(data.X * CELL_SIZE, data.Y * CELL_SIZE)`（無 origin、無 +0.5f）。  
  - 正確：`(data.X - CurrentMapOrigin.X + 0.5f) * CELL_SIZE`（Y 同理），與 `SetMapPosition` 一致。
- **SetMapPosition（GameEntity.Movement.cs）**  
  - 公式：`localX = (x - origin.X + 0.5f) * GRID_SIZE`，`GRID_SIZE = 32`。
- **點擊空地（GameWorld.Input.cs）**  
  - 錯誤：`(int)(globalMousePos.X / CELL_SIZE)` 未加 `CurrentMapOrigin`。  
  - 正確：目標格/移動格換算時加上 `CurrentMapOrigin.X` / `CurrentMapOrigin.Y`。

---

## 7. 涉及檔案與符號一覽

| 檔案 | 修改內容 |
|------|----------|
| `Client/Game/GameEntity.cs` | 新增 `_walkAnchorCache`、`_walkShadowAnchorCache`；**與 Walk/Shadow/Attack 對齊相關，調適正確，不允許修改** |
| `Client/Game/GameEntity.Visuals.cs` | `UpdateLayerOffset` 增加 `overrideAnchor`；`OnMasterFrameChanged` 中 Walk 主體/陰影首幀錨點與 `mainOverride`/`shadowOverride`；`UpdateAppearance` 清空兩快取；**與 Walk/Shadow/Attack 對齊相關，調適正確，不允許修改** |
| `Client/Game/GameWorld.Entities.cs` | 出生/傳送 Position 公式（第一階段） |
| `Client/Game/GameWorld.Input.cs` | 點空地座標換算（第一階段） |
| `Skins/CustomFantasy/Maps/Map_4.tscn` | 根節點 `z_index = -100` |
| `Skins/CustomFantasy/Maps/Map_69.tscn` | 根節點 `z_index = -100` |

**關鍵常數**：`ACT_WALK = 0`，`CELL_SIZE = 32`，`GRID_SIZE = 32`，`CurrentMapOrigin`。

---

## 8. 驗證結果

- 角色 Walk：全程穩定，中心不亂飄。  
- 陰影 Walk：在腳下對齊，第 1–3 幀均不晃動、不錯位。  
- Attack 等其它動作：主體與陰影對齊正常，未改動。  
- Map 0 / 4 / 69：陰影不再被 lowerland 遮住。

---

## 9. 總結與錯誤/正確對照

| 問題 | 錯誤原因 | 正確做法 |
|------|----------|----------|
| Walk 角色中心亂飄 | 每幀用當前幀紋理 (dx,dy) 設 Offset，Walk 各幀錨點差異大 | Walk 時主體/武器/服裝僅用主體首幀錨點 `_walkAnchorCache` |
| Walk 陰影跑到頭頂 | 陰影層使用主體錨點 `mainOverride` | 陰影不使用 `mainOverride`；Walk 時使用陰影自身首幀錨點 `_walkShadowAnchorCache`（`shadowOverride`） |
| Walk 第 2–3 幀陰影左右晃動 | 陰影每幀讀自身紋理錨點，Walk 各幀 (dx,dy) 不同 | Walk 時陰影固定使用陰影首幀錨點 `_walkShadowAnchorCache` |
| Map 4/69 陰影被 lowerland 遮住 | 地圖根節點無 `z_index`，預設 0 | 根節點設 `z_index = -100`，與 Map_0 一致 |

**代碼保護**：與角色 Walk、Shadow、Attack 對齊相關的代碼已調適正確，**不允許修改**；如需修改，必須獲得許可，並在修改後回歸測試 Walk / Shadow / Attack 的對齊表現。

以上為「角色 Walk / Shadow / Attack 對齊」任務的完整過程、錯誤原因、正確做法、參數與代碼段文檔。
