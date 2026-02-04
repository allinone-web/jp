# 專案目錄與關聯關係（優先級第一）

## 1. 關聯關係與規範（必須遵守）

- **game2 + jp = 一套完整的 服務器 + 客戶端**
- **/jp**：Lineage JP 開源服務器代碼，是**協議與邏輯的標準（Source of Truth）**
- **/game2**：Lineage 客戶端代碼（Godot 4.x + C#），**必須與 /jp 對齊**
- **/linserver182**：**已廢棄，不再使用**；後續開發**不得**與 linserver182 對齊

**約定**：後續所有開發以 **/jp** 為準。客戶端封包、opcode、變數命名、位元對齊均以 jp 服務器代碼為依據。

---

## 2. 三個目錄說明

### 2.1 jp — Lineage JP 開源服務器（標準）

- **位置**：`/Users/airtan/Documents/GitHub/jp/`
- **角色**：服務器端，協議與遊戲邏輯的**唯一標準**
- **結構**：Java 服務器代碼，`src/jp/l1j/`，標準目錄 config/, data/, db/, lib/ 等

### 2.2 game2 — Lineage 客戶端（與 jp 對齊）

- **位置**：`/Users/airtan/Documents/GitHub/game2/`
- **角色**：客戶端，與 **jp** 服務器對齊，組成完整 服務器+客戶端
- **結構**：Godot 4.x + C#，Client/Network、Client/Game、Client/UI 等；協議與封包以 jp 為準

### 2.3 linserver182 — 已廢棄

- **位置**：`/Users/airtan/Documents/GitHub/linserver182/`
- **狀態**：**廢棄，不再使用**
- **說明**：原為 Lineage 182 版服務器代碼；現已改為 **game2 與 jp 對齊**，不再與 linserver182 對齊

---

## 3. 開發約定摘要

| 項目       | 約定                         |
|------------|------------------------------|
| 協議標準   | **/jp** 為唯一標準           |
| 客戶端對齊 | **/game2** 必須與 **/jp** 對齊 |
| 廢棄目錄   | **/linserver182** 僅供參考，不得作為對齊依據 |
