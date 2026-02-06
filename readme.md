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

---

## 4. 服務器功能更新（182 數據庫合併後）

### 4.1 物品與資料庫清理

- 已刪除 JP 專用物品（不屬於 182）與所有關聯引用  
  - item_id：`20082, 21211, 21217, 40016, 41482, 49304-49309, 140016`
  - 關聯表：`shops`, `drop_items`, `drop_rates`, `item_rates`, `resolvents`, `weapon_skills`, `pet_items`, `magic_dolls`, `cooking_ingredients`, `beginner_items`, `inventory_items`
  - 清理腳本：`jp/db/docs/remove_jp_only_items.sql`
- 已刪除 `etc_items` 範圍 `40136-41382` 以及關聯引用（`shops`, `item_rates`, `inventory_items`）
- `material` 統一：`oriharukon` -> `orichalcum`（避免 ItemTable 解析失敗）

### 4.2 Beginner Items 行為

- 已改為**完全 DB 驅動**，不再使用 `BeginnerItems.xml`
  - 新手 NPC 派發物品改用 `beginner_items` 表（依 `class_initial`）
  - `GameServer`/`ReloadConfig` 不再載入 `L1BeginnerItem` XML loader
  - `BeginnerItems.xml` 保留於專案內，但**不會被載入**

### 4.3 JP 專用功能關閉

- 清空 JP 專用 XML  
  - `data/xml/Item/WisdomPotion.xml`（智慧藥水）
  - `data/xml/Item/UnknownMaliceWeapon.xml`（マリス武器）
  - release-build 同步清空
- 移除 NPC 合成與任務中 JP 專用物品  
  - `data/xml/NpcActions/ItemMaking.xml`：移除智慧藥水（40016）配方  
  - `data/xml/NpcActions/Quest.xml`：移除 20082 獎勵  
  - release-build 同步移除
- `L1TownAdvisorInstance` 增加防護：若物品不存在則不執行合成（避免空指針）

### 4.4 TreasureBox 行為

- `L1TreasureBox` 改為**略過不存在的物品**  
  - 避免大量「does not exist in the item list」刷屏  
  - 仍保留有效的 182 物品掉落

### 4.5 啟動錯誤修正（JP 功能清理）

- `ElementalStoneGenerator`  
  - 精靈之石改用 `item_id=141`（鑽石）生成
- `L1Spawn`  
  - NPC 建立失敗時，**改用 NPC 100003（潘朵拉）代替**
- `NpcTable`  
  - 僅使用 `L1Npc` 參數構造器建立 NPC，避免 `wrong number of arguments`
- `L1BugBearRace`  
  - 競賽 NPC 不存在時**略過廣播**，避免 NPE
- `L1MapLimiter`  
  - 182 不需要，已**停用 MapLimiter**（不載入 MapLimiter.xml）
- `L1UnknownMaliceWeapon`  
  - JP 後期「マリス武器」系統，已**停用**
- `Trap System`  
  - 已**全面停用陷阱系統**（不再觸發/偵測/重置）
  - DB：刪除 `traps` / `spawn_traps` 表，移除 `impl='L1Trap'` 的 NPC 與其 spawn
  - 腳本：`jp/db/docs/remove_trap_system.sql`
- XML 物品清理  
  - 僅保留 182 存在的 item_id（etc_items / weapons / armors）
  - 工具：`jp/db/docs/clean_item_xml.py`  
  - 報告：`jp/db/docs/clean_item_xml_report.md`
- DB 清理腳本：`jp/db/docs/cleanup_missing_maps_and_npcs.sql`  
  - 移除 **不存在 NPC** 的 spawn（`spawn_npcs`, `spawn_mobs`, `spawn_boss_mobs`）  
  - 移除 **不存在地圖** 的資料（`spawn_*`, `map_timers`, `dungeons`, `return_locations`, `random_dungeons`）
- DB 清理腳本：`jp/db/docs/remove_missing_npc_spawns_100875.sql`  
  - 刪除 `npc_id=100875/100876/100877/100878/100880/100881/100883` 的 spawn 記錄

### 4.6 料理系統停用

- 已停用料理系統（Cooking）  
  - `GameServer` / `ReloadConfig` 不再載入 `CookingRecipeTable`  
  - 料理道具使用時回報「Cooking system is disabled.」  
  - 移除 DB 表：`cooking_recipes`, `cooking_ingredients`

### 4.7 地圖限制 / 魔法娃娃 / 寵物道具 / 空白卷軸移除

- `return_locations` 重新覆蓋  
  - 來源：`jpdbbackup.return_locations`  
  - 篩選：僅保留 **182 地圖集合** 的 `area_map_id` / `getback_map_id`
- MapLimiter / MapTimeController 全套移除  
  - 不再載入 MapLimiter / ResetMapTimeCycle  
  - 關閉 `startMapLimiter/stopMapLimiter` 呼叫  
  - 移除 DB 表：`map_timers`  
  - 刪除檔案：`data/xml/etc/MapLimiter.xml`, `data/xml/Cycle/ResetMapTimeCycle.xml`
- 魔法娃娃系統移除  
  - 移除 DB 表：`magic_dolls`
- 寵物道具系統移除（保留寵物功能）  
  - 移除 DB 表：`pet_items`
- BlankScroll 系統移除  
  - 刪除 `L1BlankScroll`  
  - 移除檔案：`data/xml/Item/BlankScroll.xml`

### 4.8 MapLimiter / Magic Doll / Pet Items / Blank Scroll 代碼移除

- MapLimiter 全套移除（代碼清理）  
  - 刪除 `MapTimeController` / `MapTimerTable` / `L1MapLimiter`  
  - 清理 `MapTable` / `L1PcInstance` 中所有 MapLimiter 引用
- Magic Doll 全套移除（代碼清理）  
  - 刪除 `L1MagicDoll` / `MagicDollTable` / `L1DollInstance` / `S_DollPack`  
  - 刪除 `HpRegenerationByDoll` / `MpRegenerationByDoll` / `MakeItemByDoll`  
  - 清理所有娃娃加成與召喚/傳送/倉庫/掉落等邏輯引用
- Pet Items 全套移除（代碼清理）  
  - 刪除 `PetItemTable` / `L1PetItem` / `C_UsePetItem`  
  - `L1PetInstance` / `C_GiveItem` 移除寵物裝備邏輯（保留寵物功能）
- Blank Scroll 全套移除（代碼清理）  
  - 刪除 `L1BlankScroll`  
  - `C_UseItem` 移除空白卷軸處理邏輯
