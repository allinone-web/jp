# Centered 與對齊差異分析：jan29-magic-v2 vs jan31-pakbrowser

**當前最新標準**：主體/陰影/衣服三層對齊的規則、公式與代碼保護說明見 **docs/pakbrowser-development.md 第 4 節**。該功能已測試通過，不允許修改；若需修改必須獲得許可。

---

## 一、Centered 的視覺差異（你的觀察）

| Centered | 角色整體與螢幕中心的關係 |
|----------|---------------------------|
| **true** | 角色**整體中心**自動在螢幕中心。 |
| **false** | 角色整體**不在**螢幕中心；角色的**左上角**（或 (dx,dy) 參考點）在螢幕中心，角色從該點延伸出去。 |

- **jan29-magic-v2**：`Centered = false`，每層 `Offset = (dx, dy) + BodyOffset`，所以每張圖的**左上角**被放在 node + (dx,dy) + BodyOffset。若 node 是螢幕中心且 BodyOffset=(0,0)，則「參考點」在螢幕中心，角色會往右下延伸，整體中心偏離。
- **jan31-pakbrowser**：`Centered = true`，主體 `Offset = BodyOffset`，所以主體**中心**在 node + BodyOffset（螢幕中心），角色整體置中。

---

## 二、對齊現象整理

| Branch | gfx=240 | gfx=53 |
|--------|---------|--------|
| **jan29-magic-v2** | clothes 與身體對齊；陰影與身體對齊 | 陰影與身體對齊 |
| **jan31-pakbrowser** | clothes 與身體**無法**對齊；陰影與身體對齊 | 陰影與身體**無法**對齊 |
| 其他角色 | 兩版差別很小 | 兩版差別很小 |

---

## 三、原因分析

### 3.1 公式與 (dx,dy) 語意

- **sprite_offsets** 的 (dx, dy)：該幀圖像**左上角**在「某個共同座標系」的位置（與 batch_merge 一致時，即拼圖畫布上的座標）。
- **jan29**：每層 `Offset = (dx, dy) + BodyOffset`，且 **非 Walk 時每幀**從紋理讀**當前幀**的 (dx, dy)。  
  → 等於「每幀把每張圖的左上角放到 (dx,dy)+BodyOffset」，直接重現拼圖時每幀的相對位置。  
  → 只要 body / shadow / clothes 的 (dx,dy) 來自**同一套**座標系（同一 merge、同一畫布），就會對齊。
- **jan31**：主體中心固定在 BodyOffset（螢幕中心），Shadow/Clothes 用**僅首幀 (frame 0)** 的 (dx,dy) 算相對偏移：  
  `Offset = BodyOffset + (layerDx - bodyDx - bodyW/2 + layerW/2, ...)`  
  → 等於「假設 body 中心在 (bodyDx+bodyW/2, bodyDy+bodyH/2)，其它層相對這個中心放」。  
  → 依賴兩件事：  
  1）body / shadow / clothes 的 (dx,dy) 都在**同一個**共同座標系；  
  2）**只用 frame 0** 的 (dx,dy) 就足以代表整段動畫的相對關係。

### 3.2 為何 jan31 在 gfx=240（clothes）、gfx=53（shadow）會錯位？

可能原因（可並存）：

1. **sprite_offsets 座標系不一致**  
   - PngInfoLoader 的 key 是 `GfxId_FileActionId`（例如 240_0、242_0、53_0）。  
   - 若 240 與 242（clothes）的 (dx,dy) 來自**不同**批次／不同畫布（例如 242 單獨 merge、原點在 242 自己的左上角），則 242 的 (dx,dy) 與 240 不在同一「共同座標系」。  
   - jan29：每層各自 `Offset=(dx,dy)+BodyOffset`，若檔案裡 240/242 剛好都寫成「同一畫布」的絕對座標，就會對齊。  
   - jan31：用 `cDx - bodyDx - bodyW/2 + cW/2` 等公式，一旦 242 的 (cDx,cDy) 是另一套原點（例如 0,0），減去 bodyDx/bodyDy 會得到錯誤的相對量，clothes 就會偏掉。  
   - 同理，gfx=53 的陰影若與 53 本體不在同一座標系，jan31 的相對公式就會讓陰影錯位；jan29 若 53 與陰影的 (dx,dy) 來自同一 merge，就會對齊。

2. **僅用 frame 0 導致與「每幀 (dx,dy)」不一致**  
   - jan29：**每幀**用當前幀的 (dx,dy)，所以若動畫每幀相對位置有變化，也會跟著動，對齊穩定。  
   - jan31：**只**用 frame 0 的 (dx,dy) 算 Shadow/Clothes 的 Offset。  
   - 若 gfx=240 的 clothes 或 gfx=53 的 shadow 在**第 0 幀**的 (dx,dy) 與共同座標系不一致（例如該幀缺值、或該 GfxId 的 frame 0 用了不同原點），就會只在這兩個 gfx 上出現明顯錯位；其它角色若 frame 0 一致，差別就小。

3. **個別 GfxId 的 sprite_offsets 產出方式不同**  
   - 部分角色可能是「body+shadow+clothes 一起 batch_merge」產出，所以 (dx,dy) 天然同一座標系。  
   - 240 的 clothes（242）、53 的 shadow 若來自**單獨** merge 或別的工具，座標系就可能和 body 不同，jan31 的相對公式就會把這些差異放大；jan29 的「每層直接 (dx,dy)+BodyOffset」在檔案若寫成同一空間時仍可對齊。

### 3.3 小結（為何 jan29 對齊、jan31 不對齊）

- **jan29**：每幀、每層各自 `Offset = (dx, dy) + BodyOffset`，不假設「共同原點」，只要檔案裡各層 (dx,dy) 是同一畫布座標，就能對齊；且**每幀**更新，不會被 frame 0 單一幀綁死。
- **jan31**：假設「主體中心 + 相對公式」且**只用 frame 0**；當某 GfxId 的 shadow/clothes 的 (dx,dy) 座標系或 frame 0 與 body 不一致時，就會在該 gfx（如 240、53）上錯位，其它 gfx 差別小是因為它們的 frame 0 與座標系較一致。

---

## 四、優缺點與結合方式

### 4.1 jan29-magic-v2

| 優點 | 缺點 |
|------|------|
| 多數 gfx（含 240、53）body/shadow/clothes 對齊穩定 | Centered=false → 角色**整體中心**不在螢幕中心，是「左上角/參考點」在中心 |
| 每幀 (dx,dy)，動畫若有每幀微調也能對齊 | Walk 需快取首幀錨點，邏輯較多 |
| 不強依「共同座標系」的假設，各層 (dx,dy) 同空間即可 | 服裝用 Shader 去黑，與 PakBrowser/Provider 魔法透明分工不同 |

### 4.2 jan31-pakbrowser

| 優點 | 缺點 |
|------|------|
| Centered=true → 角色**整體中心**在螢幕中心，視覺直觀 | 依賴「所有層 (dx,dy) 同一共同座標系」且 frame 0 具代表性；240 clothes、53 shadow 易錯位 |
| 無 Walk 快取，邏輯單純 | 僅首幀算 Offset，若某 gfx 的 frame 0 或座標系異常就會偏 |
| 與 batch_merge 數學一致（主體中心 + 相對公式） | 對 sprite_offsets 產出方式較敏感 |

### 4.3 結合兩者優點的可行做法

目標：  
- 角色**整體中心**在螢幕中心（保留 jan31 的 Centered=true）。  
- 陰影/服裝與身體**每幀對齊**（吸收 jan29 的「每幀 (dx,dy)」）。

建議方案（擇一或分階段實作）：

1. **保留 Centered=true，改為每幀算相對 Offset（推薦）**  
   - 主體：`Offset = BodyOffset`（中心在 node+BodyOffset）。  
   - Shadow/武器/Clothes：**每幀**用**當前幀**的 (bodyDx, bodyDy, bodyW, bodyH) 與 (layerDx, layerDy, layerW, layerH) 計算：  
     `Offset = BodyOffset + (layerDx - bodyDx - bodyW/2 + layerW/2, layerDy - bodyDy - bodyH/2 + layerH/2)`  
   - 這樣既「角色中心在螢幕中心」，又「每幀相對位置與 sprite_offsets 一致」，避免 frame 0 單一幀或座標系不一致造成的 240/53 類錯位。  
   - 缺點：每幀讀紋理 meta 並做一次運算，成本略增（通常可接受）。

2. **可選：依 GfxId 或資源 fallback**  
   - 若某 gfx 偵測到「frame 0 與當前幀 (dx,dy) 差異過大」或「已知錯位」（如 240、53），可對該 gfx 強制用「每幀相對公式」；其餘仍用「僅 frame 0」以省算力。  
   - 或：sprite_offsets 產出時強制「body+shadow+clothes 同一畫布」，從源頭統一座標系，再沿用 jan31 的僅首幀公式。

3. **維持 Centered=false 但補償整體中心（若必須保留 jan29 對齊）**  
   - 保留 jan29 的 `Offset = (dx, dy) + BodyOffset` 與每幀 (dx,dy)。  
   - 另算「主體中心」：例如 bodyCenter = (bodyDx + bodyW/2, bodyDy + bodyH/2)，令 `BodyOffset = -bodyCenter`（或 node 的 position 再減 bodyCenter），使整體中心落在螢幕中心。  
   - 效果：對齊行為與 jan29 相同，但視覺上角色中心在螢幕中心。

實作時建議：**Shadow 維持 jan31 方案（僅首幀計算 Offset）**，因多數 gfx 陰影與主體對齊正常；**Clothes 改為每幀計算 Offset**（公式不變），修復 gfx=240 等 clothes 錯位。若日後發現某 gfx（如 53）陰影錯位，可再改為陰影也每幀計算。

---

## 五、總結

| 項目 | jan29-magic-v2 | jan31-pakbrowser |
|------|-----------------|-------------------|
| 角色整體在螢幕中心 | 否（左上/參考點在中心） | 是 |
| 240 clothes / 53 shadow 對齊 | 可對齊 | 易錯位 |
| 原因 | 每幀 (dx,dy)、直接放置，不依賴單一 frame 0 與嚴格共同座標系 | 僅 frame 0 + 相對公式，對座標系與 frame 0 敏感 |
| 建議 | 保留「每幀 (dx,dy)」的對齊能力 | 保留「Centered=true + 相對公式」，改為**每幀**計算 Offset，結合兩者優點 |

上述分析與結合方案已寫入本文件，可作為修改 GameEntity.Visuals 與 PakBrowser 對齊邏輯的依據。
