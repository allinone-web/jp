# 為什麼會報「X does not exist in the map list」？

## 報錯原因（一句話）

**`restart_locations` 表裡有 22 筆的 `area` 值，在「地圖清單」表 `map_ids` 裡沒有對應的 id，載入時被當成無效而印出訊息並跳過。**

---

## 資料與程式流程

1. **地圖清單從哪來**  
   `MapTable` 啟動時只從 DB 表 **`map_ids`** 讀取（`SELECT * FROM map_ids`），把每一筆的 `id` 放進記憶體裡的 `_maps`。  
   所以**只有出現在 `map_ids` 裡的 id，才算「存在於 map list」**。

2. **「是否存在」怎麼判斷**  
   `MapTable.locationname(mapId)` 的實作是：
   - 若 `_maps.get(mapId)` 為 null（表示該 id 不在 `map_ids`）→ 回傳 **null**
   - 否則回傳該地圖的 name。

3. **誰在檢查、誰在報錯**  
   **RestartLocationTable** 載入 `restart_locations` 時，對每一筆做：

   ```text
   area   = 該筆的 area（玩家所在的地圖 id）
   map_id = 該筆的 map_id（重生後要傳去的地圖 id）

   if (MapTable.getInstance().locationname(area) == null)   → 印「area はマップリストに存在しません」
   if (MapTable.getInstance().locationname(mapId) == null) → 印「map_id はマップリストに存在しません」
   if (上面任一個成立) continue;  // 不載入這筆，不加入重生規則
   ```

4. **你目前的狀況**  
   - `restart_locations` 裡有 22 筆的 **area** = 84, 88, 91, 92, 95, 98, 1005, 1011, 16384, 16896, …  
   - 這些 id **沒有**出現在 `map_ids` 裡  
   - 所以 `locationname(84)`、`locationname(88)` … 都是 **null**  
   - 於是每次載入到這 22 筆時都會印一次「X does not exist in the map list」，並且這 22 筆**不會被載入**（不會生效）。

---

## 總結

| 項目 | 說明 |
|------|------|
| **報錯來源** | `RestartLocationTable.java` 載入 `restart_locations` 時 |
| **判斷依據** | `MapTable.locationname(id)` 為 null → 視為「不在 map list」 |
| **Map list 來源** | 僅 DB 表 **`map_ids`**（與是否有 .map 檔無關） |
| **根本原因** | 那 22 個 **area** 沒有在 `map_ids` 裡建檔 |
| **目前影響** | 這 22 筆重生規則被跳過、不生效；僅多 22 行 console 訊息 |

若要消除報錯，可二擇一（詳見 `missing_map_ids_restart_locations.md`）：

- **在 `map_ids` 補上這 22 個 id** → 訊息消失，且該 22 筆重生規則會正常載入並生效。  
- **刪除 `restart_locations` 中這 22 筆** → 訊息消失，這些 area 不再有重生規則。
