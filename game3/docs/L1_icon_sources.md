# Lineage 1 裝備／魔法圖標來源與批次截圖

解析 TBT 等原生格式較難，可改從**網頁圖鑑**用**批次截圖**取得裝備、魔法圖標。

---

## 一、可顯示圖標的網站（供手動或自動截圖）

| 網站 | 說明 | 裝備圖標 | 魔法圖標 |
|------|------|----------|----------|
| **Lineage 1 Reborn Lookup** | 輸入道具 ID 或名稱可查單一道具，含圖示與數值。 | ✅ `?item=ID&page=lookup` | 可搜尋 spell |
| [https://www.lineage1reborn.com/?page=lookup](https://www.lineage1reborn.com/?page=lookup) |  |  |  |
| **L1.5 Server 道具分類** | 依類型列出道具（武器、防具、材料等），多數有圖。 | ✅ 各分類頁 | — |
| [https://www.l15server.com/guides/items.html](https://www.l15server.com/guides/items.html) |  |  |  |
| **台灣官方 道具查詢** | 依分類查道具，有圖片與簡介。 | ✅ | — |
| [https://tw.beanfun.com/lineage/tool.aspx?PIndex=03](https://tw.beanfun.com/lineage/tool.aspx?PIndex=03) | 需台灣 IP／帳號時可能有限制。 |  |  |
| **Lineage Compendium (archive)** | 英文 L1 資料／法術列表，部分頁有圖。 | 部分 | 部分 |
| [http://lineagecompendium.com/](http://lineagecompendium.com/) |  |  |  |
| **VG Resource 討論** | 討論如何從 L1 擷取圖素（加密、私服截圖等）。 | 參考用 | 參考用 |
| [How does one rip sprites from a dead MMO like Lineage 1?](https://www.vg-resource.com/thread-42367.html) |  |  |  |

- **裝備／道具**：最適合批次用的是 **Lineage 1 Reborn Lookup**（單一 ID 對應單一頁，易自動化）。
- **魔法**：Reborn 可搜 spell 名稱；其餘站多為列表，可手動或對列表頁做整頁截圖再裁切。

---

## 二、批次截圖思路

1. **依 ID 造訪 Reborn Lookup**  
   道具：`https://www.lineage1reborn.com/?item=<ID>&page=lookup`  
   對每個 ID 開一次頁面，等圖載完後截圖（整頁或只截圖示區塊）。

2. **ID 從哪裡來**  
   - 本專案：`Assets/ItemDesc.txt` 第一欄為道具 ID；`Assets/SpellDesc.txt` 可對應法術。  
   - 或自訂範圍，例如 0～600 對應你有的 TBT 編號。

3. **建議工具**  
   - **Playwright (Python)**：可開頭、等載入、對「整頁」或「單一元素」截圖，適合批次。  
   - 專案內腳本：`Assets/scripts/screenshot_l1_icons.py`（見下方使用說明）。

4. **注意**  
   - 請求間加短延遲（例如 1～2 秒），避免對目標站造成負載。  
   - 若網站改版，需調整選擇器（截圖目標的 CSS selector）。  
   - 截完後可依 ID 重新命名（例如 `item_0.png`）或再寫小腳本裁成固定大小圖示。

---

## 三、使用專案內批次截圖腳本

```bash
# 1. 安裝 Playwright 與瀏覽器
pip install playwright
playwright install chromium

# 2. 只截裝備（從 ItemDesc.txt 讀 ID，預設輸出到 Assets/icon_screenshots/items）
python3 Assets/scripts/screenshot_l1_icons.py --items --out Assets/icon_screenshots/items

# 3. 指定 ID 範圍（例如 0～299）
python3 Assets/scripts/screenshot_l1_icons.py --range 0-299 --out Assets/icon_screenshots/items

# 4. 延遲（每頁等 2 秒再截圖）
python3 Assets/scripts/screenshot_l1_icons.py --items --delay 2 --out Assets/icon_screenshots/items
```

截圖結果為**整頁**；若日後要只保留圖示，可再依網頁結構改腳本中的 selector 做「元素截圖」。

---

## 四、若不想寫腳本：手動批次截圖

1. 用瀏覽器開 [Lineage 1 Reborn Lookup](https://www.lineage1reborn.com/?page=lookup)。  
2. 依序在搜尋框輸入 ID（0, 1, 2, …）或道具名稱，Enter。  
3. 每筆結果出現後用系統或擴充功能截圖（例如只截圖示區塊），另存為 `item_0.png`、`item_1.png` 等。  
4. 魔法圖標可同上，改搜尋法術名稱並截圖。

以上方式可避開 TBT 解析，用「網頁圖鑑 + 批次截圖」取得裝備與魔法圖標。
