# Lineage 182 與 JP 資料庫整合方案

---

## 方案對比與採納結論（2025-02 更新）

### 兩份方案對比

| 維度 | 原方案（本文件初版） | 你比對後的方案（採納） |
|------|----------------------|------------------------|
| **ID 策略** | 列為「待決策」，允許「保留 182 id 或重映射」 | **明確**：不能把 182 的 item_id/skill_id/npc_id 整包覆蓋；必須以 **JP ID 為唯一運行 ID** |
| **硬編碼依據** | 僅提到「JP 若在程式內寫死區段」 | **具體指出**：`C_UseItem.java`、`L1ItemId.java`、`L1SkillId.java` 等有大量硬編碼 ID 與範圍判斷 |
| **映射方式** | 以「欄位對照 + 可選 ID 重映射」為主 | **以語義/名稱映射**：物品用 `nameid` 優先配對、NPC/怪物用 `name_id + gfxid`、技能用名稱/等級/效果對到 JP skill id |
| **產出物** | ETL 直接產 JP 用 INSERT/CSV | **先產映射表**（`map_*_182_to_jp.csv`，含 confidence/method/note），再產合併 CSV 到 **新目錄** `tw_182merge/`，不覆蓋現有 |
| **依賴表** | 有列 spawn_mobs、drop_items | **完整列舉**：drop_items、spawn_mobs、spawn_npcs、shops、mob_skills 等，並做 referential check |
| **環境** | 可寫入「JP DB」 | **先 staging DB**（如 `l1jdb_182merge`），通過後再切正式，保留回滾 |
| **驗證標準** | 啟動無錯、遊戲內抽查 | **三層**：DB 檢查（引用完整、無 orphan）、Server 檢查（30 分鐘壓測）、Client 檢查（核心循環可連續執行） |
| **決策選項** | 未明確二選一 | **明確二選一**：① 安全映射（保持 JP ID，推薦） ② 高還原（改 JP 程式相容 182 ID，風險高） |

### 採納結論

- **關鍵結論（與你一致）**
  1. **不能**直接把 182 的 `item_id / skill_id / npc_id` 整包覆蓋到 JP；JP 伺服器有大量硬編碼 ID（如 `L1ItemId.POTION_OF_HEALING = 40010`、`L1SkillId.HEAL = 1`），改用 182 ID 會導致邏輯錯誤。
  2. 目標「JP server + 182 時代資料」**可行**，但必須以 **JP 結構與 ID 契約為骨架**，182 僅當**資料來源**做**映射**（名稱、nameid、gfx、效果等）。
  3. 只改主表（etc_items / npcs / skills）**不夠**，須同步處理依賴表（掉落、出生點、商店、怪物技能等），否則會出現孤兒資料與引用錯誤。

- **推薦採納**：你的 **「安全映射方案」**  
  - 以 JP schema 為唯一結構、**JP ID 為唯一運行 ID**。  
  - 建立映射表（map_*_182_to_jp.csv）→ 產出 tw_182merge → 一致性檢查 → 匯入 **staging DB** → 伺服器／客戶端驗證 → 通過後再切正式。

- **需要審批的決策**（依你原文）
  1. **走「安全映射方案（推薦）」**：保持 JP ID 契約，不追求 182 原始 ID。  
  2. **或**走「高還原方案」：允許修改 JP server 程式以相容 182 ID（風險高、工時大）。

以下保留原文件的欄位對照與細節，**ID 相關建議已改為「以 JP ID 為準、182 僅作內容映射」**；流程與 TODO 已與你的建議流程對齊並在後文更新。

---

## 一、目標與約束

- **目標**：使用 **JP 伺服器** + **182 時代的遊戲資料**（物品、怪物/NPC、魔法）供客戶端連線。
- **約束**：
  - 資料庫**結構以 JP 為準**（表名、欄位名、型別），伺服器程式依 JP 的 `ItemTable`、`NpcTable`、`SkillTable` 讀取 MySQL。
  - **資料內容**來自 182：`game2/docs/lineage182-db/`（items、npc、monster、skill_list、monster_spawnlist、monster_item_drop 等）。

---

## 二、兩版差異摘要

| 項目 | 182 | JP |
|------|-----|-----|
| **物品** | 單表 `items`（武器/防具/雜項混在一起，`type`: sword/axe/light/none…） | 分表 `etc_items`、`weapons`、`armors`，各有 JP 專用欄位 |
| **NPC/怪物** | `npc`（友好 NPC）+ `monster`（怪物），表結構不同 | 單表 `npcs`，以 `impl` 區分（L1Monster、L1Guard、L1Dwarf…） |
| **技能** | `skill_list`（skill_id, skill_no, mp_consume, min_dmg/max_dmg…） | `skills`（id, skill_number, consume_mp, damage_value/damage_dice…） |
| **出生點** | monster_spawnlist（monster id, loc…） | spawn_mobs（npc_id, loc_x, loc_y, map_id, respawn…） |
| **掉落** | monster_item_drop（monid, itemid, count_min/max, chance） | drop_items（npc_id, item_id, min, max, chance） |
| **資料載入** | 182 為 MySQL dump（.sql） | JP 從 MySQL 讀取；`jp/db/csv/tw` 為可選的 CSV 來源（需匯入 DB） |

---

## 三、欄位對照與轉換要點

### 3.1 物品（182 `items` → JP `etc_items` / `weapons` / `armors`）

- 182 的 `type` 決定歸屬：
  - **weapons**：sword, axe, dagger, bow, spear, blunt, staff, twohand 等 → 對應 JP `weapons.type` 與 `weaponTypes`。
  - **armors**：依 182 的防具型別對應 JP `armors.type`（helm, armor, cloak, glove, boots, shield, ring, amulet…）。
  - **etc_items**：light, none, scroll, material 等 → 對應 JP `item_type` / `use_type`（light, potion, scroll, other…）。
- 關鍵欄位對照（182 → JP etc_items 為例）：

| 182 (items) | JP (etc_items) | 備註 |
|-------------|-----------------|------|
| item_id | **不直接使用** | 輸出時使用 **JP 既有 id**；182 僅提供 name/nameid/gfx 等，透過 map_items_182_to_jp 映射到 JP id |
| name | name | 直接 |
| nameid | unidentified_name_id, identified_name_id | 可同值 |
| type | item_type / use_type | 需對照 JP 的 _etcItemTypes / _useTypes 字串 |
| inv_gfx_id | inv_gfx_id | 直接 |
| grd_gfx_id | grd_gfx_id | 直接 |
| weight | weight | 直接 |
| material | material | 需為 JP 的 material 字串（iron, wood, glass…） |
| piles | stackable | 0/1 → 0/1 |
| trade | tradable | 0/1 → 0/1 |
| minlvl / maxlvl | min_level / max_level | 直接 |
| 無 | delay_id, delay_time, delay_effect, save_at_once, charge_time, expiration_time, sealable, deletable, bless, loc_x, loc_y, map_id, item_desc_id, food_volume | 182 無則填 JP 預設（0、1、\N 等） |

- **weapons / armors**：同樣需依 JP 的 `ItemTable` 與 schema 補齊 JP 專有欄位（safe_enchant, use_royal, use_knight, use_elf… 等），182 無則給預設值。

### 3.2 NPC / 怪物（182 `npc` + `monster` → JP `npcs`）

- **ID 策略（採納安全映射）**：
  - **不以 182 的 npcid/uid 覆蓋 JP**。JP 的 `npcs.id` 為唯一運行 ID；182 的 NPC/怪物僅作為**內容來源**（name, name_id, gfx_id, hp, 等），透過 `map_npcs_182_to_jp.csv` 映射到既有或預留的 JP npc id。
  - 182：`npc.npcid`、`monster.uid` 僅用於 182 側識別與映射表鍵值。
- **impl**：182 無此欄位，需依 182 的 `npc.type`（Guard, Shop, Npc, Dwarf…）/ 是否為怪物 對應到 JP 的 impl（L1Guard, L1Shop, L1Monster, L1Dwarf…）。怪物一律 `L1Monster`。
- 關鍵欄位對照（182 monster → JP npcs）：

| 182 (monster) | JP (npcs) | 備註 |
|---------------|-----------|------|
| uid | **不直接作為 JP id** | 映射表鍵值；JP npcs.id 由 map_npcs_182_to_jp 決定（JP 既有或新 id） |
| name | name | 直接 |
| name_id | name_id | 直接 |
| gfx | gfx_id | 直接 |
| level | level | 直接 |
| hp, mp | hp, mp | 直接 |
| min_dmg, max_dmg | 無直接欄位 | 部分 JP 用 mob_skills；可先 0 |
| ac, mr | ac, mr | 直接 |
| exp | exp | 直接 |
| lawful | lawful | 直接 |
| size | size | 直接（small/large） |
| die | 無 | 可忽略或對應 JP 行為 |
| 無 | impl | 固定 L1Monster |
| 無 | str, con, dex, wis, int, family, agro, tamable, move_speed, atk_speed… | 182 無則填 JP 預設（0, -1, false…） |

- 182 `npc` 欄位對 JP `npcs`：npcid→id, name→name, nameid→name_id, gfxid→gfx_id, hp→hp, type→ 對應 impl（如 Guard→L1Guard, Shop→L1Shop）。其餘 JP 專有欄位填預設。

### 3.3 技能（182 `skill_list` → JP `skills`）

| 182 (skill_list) | JP (skills) | 備註 |
|------------------|-------------|------|
| skill_id | **不直接覆蓋 id** | 用名稱/等級/效果做語義映射到 JP skill id（見 map_skills_182_to_jp）；JP 的 skills.id 保持不變 |
| name | name | 直接 |
| skill_level | skill_level | 直接 |
| skill_no | skill_number | 直接 |
| mp_consume | consume_mp | 直接 |
| hp_consume | consume_hp | 直接 |
| item_consume | consume_item_id | 直接 |
| item_consume_count | consume_amount | 直接 |
| reuse_delay | reuse_delay | 直接 |
| buff_duration | buff_duration | 直接 |
| type (字串) | target (字串) + type (整數) | 需對照 JP 的 target / type 定義 |
| min_dmg, max_dmg | damage_value, damage_dice, damage_dice_count | 換算方式需與 JP 邏輯一致 |
| id | skill_id | 注意 JP 有 id 與 skill_id 兩欄 |
| cast_gfx | cast_gfx | 直接 |
| range | ranged | 直接 |
| lawful_consume | lawful | 直接 |
| attr | attr | 直接 |
| magic (pc/npc) | 無 | 可過濾只匯入 pc 或同時匯入 npc |
| 無 | target_to, probability_*, name_id, action_id, cast_gfx2, sys_msg_*, can_cast_with_invis, ignores_counter_magic, is_buff, impl | 182 無則填 JP 預設 |

---

## 四、推薦流程（最佳方案 — 與你方案一致）

### 1. 建立中繼層與映射表

- `map_items_182_to_jp.csv`（182 item_id / nameid → JP item id，含 confidence / method / note）
- `map_npcs_182_to_jp.csv`（182 npcid 或 monster uid / name_id + gfx → JP npc id）
- `map_skills_182_to_jp.csv`（182 skill_id / 名稱·等級·效果 → JP skill id）
- 配對優先：物品以 **nameid** 優先；NPC/怪物以 **name_id + gfxid**，再 fallback name_id，最後人工映射。

### 2. 產生合併後 CSV（新資料夾，不覆蓋現有）

- 新目錄：`jp/db/csv/tw_182merge/`
- 先產主表：`etc_items.csv`、`npcs.csv`、`skills.csv`（內容來自 182，**id 一律為 JP id**，由映射表決定）
- 再產依賴表：`drop_items.csv`、`spawn_mobs.csv`、`spawn_npcs.csv`、`shops.csv`、`mob_skills.csv` 等，所有引用 id 皆為 JP id。

### 3. 一致性檢查（自動）

- 所有引用 ID 必須可解析（item_id/npc_id/skill_id 在對應主表存在）
- 無重複主鍵
- `npcs.impl` 必須是 JP 可識別類型（L1Monster、L1Guard、L1Shop…）
- skills 必要欄位完整（含 impl 等）

### 4. 匯入 Staging DB（先不動正式 DB）

- 建立 staging 庫，例如 `l1jdb_182merge`
- 用 JP schema 建表，匯入 `tw_182merge` 的 CSV

### 5. 伺服器啟動驗證

- 無啟動期 parser/loader 錯誤
- 無大量 null 的 skill/item/npc 查找失敗

### 6. 客戶端驗證

- 登入、地圖切換、NPC 對話、商店購買
- 打怪掉寶、技能施放、變身、捲軸/藥水使用
- 封包與顯示對應正常（名稱、圖示、效果）

### 如何判定「整合成功」

1. **DB 檢查全綠**：引用完整、無 orphan、無重複 key  
2. **Server 檢查全綠**：啟動與約 30 分鐘壓測無關鍵錯誤  
3. **Client 檢查全綠**：核心循環（打怪→掉寶→買賣→技能）可連續執行  

通過後再切正式 DB，並保留回滾方案。

---

## 五、驗證檢查表

- [ ] JP DB 表結構與 `jp/db/schema/mysql` 一致。
- [ ] 物品：etc_items / weapons / armors 筆數與 182 對應類型數量合理，無必填欄位為 NULL 導致伺服器報錯。
- [ ] NPC：npcs 含 182 的 NPC + 怪物，impl 正確，gfx_id、name_id 有值。
- [ ] 技能：skills 的 id、skill_number、consume_mp 等與 182 對應，伺服器載入無錯誤。
- [ ] 出生：spawn_mobs 的 npc_id 皆存在於 npcs.id，地圖與座標合理。
- [ ] 掉落：drop_items 的 npc_id、item_id 皆存在於 npcs、物品表，chance/min/max 合理。
- [ ] 伺服器啟動無 Exception，遊戲內可正常登入、看見 NPC/怪物、使用物品與技能。

---

## 六、導入資料庫的實作方式（可自動化）

- **方式 1（推薦）**：撰寫 **Python 腳本**  
  - 讀取 182 的 SQL 或連線 182 暫存 DB，用 pandas 或 sqlalchemy 做欄位對照與轉換，輸出 JP 格式的 INSERT 或 CSV。  
  - 再以 `mysql -u user -p l1jdb < jp_inserts.sql` 或 JP 的 CSV 匯入腳本寫入 DB。可一鍵執行。

- **方式 2**：**純 SQL**  
  - 先將 182 的資料匯入到暫存表（例如 `items_182`、`monster_182`、`npc_182`、`skill_list_182`），再撰寫 INSERT INTO jp 表 SELECT … FROM 暫存表 的轉換 SQL（需處理欄位對應與預設值）。  
  - 適合熟悉 SQL 的環境，可納入批次腳本。

- **方式 3**：**CSV 中繼**  
  - ETL 產出與 `jp/db/csv/tw/` 同格式的 CSV，再用現成或自寫的「CSV → MySQL」工具（如 LOAD DATA INFILE 或腳本）寫入 JP DB。

您提到「可以讀寫資料庫」：若提供 DB 連線方式（或允許在專案內加入 ETL 腳本），可進一步產出**可執行的一鍵導入腳本**（例如 Python + config 指定 182 來源與 JP 目標 DB）。

---

## 七、TODO 清單（與你方案對齊，審批後執行）

| # | 項目 | 說明 |
|---|------|------|
| 1 | 凍結目前 JP DB，做完整備份快照 | 不動正式 DB 前先備份。 |
| 2 | 建立 tw_182merge 與 3 份 mapping 表 | map_items_182_to_jp.csv、map_npcs_182_to_jp.csv、map_skills_182_to_jp.csv（含 confidence / method / note）。 |
| 3 | 寫轉換腳本（182 SQL/CSV → JP CSV） | 讀 182，依映射表產出 tw_182merge 的 CSV。 |
| 4 | 先導入 3 主表 | etc_items、npcs、skills（id 一律為 JP id）。 |
| 5 | 導入依賴表並做 referential check | drop_items、spawn_mobs、spawn_npcs、shops、mob_skills 等；檢查所有引用 ID 可解析。 |
| 6 | 匯入 l1jdb_182merge，啟動 JP server 驗證 | 無 loader 錯誤、無大量 null 查找失敗。 |
| 7 | 客戶端情境測試與問題清單修正 | 打怪→掉寶→買賣→技能等核心循環。 |
| 8 | 通過後再切正式 DB（保留回滾方案） | 切換前確認 staging 全綠。 |

---

## 八、風險與注意

- **硬編碼 ID**：JP 在 `L1ItemId.java`（如 40010 藥水）、`L1SkillId.java`（如 HEAL=1）、`C_UseItem.java` 等處有大量硬編碼；**因此必須採「安全映射」**，不得用 182 id 覆蓋 JP id。
- **182 與 JP 的 type/impl 枚舉不同**：需手動對照並在 ETL／映射表中配置（如 182 type "Guard" → JP impl "L1Guard"），避免漏對應導致 NPC 無法載入。
- **技能 impl**：JP 的 `skills.impl` 若為非空，映射時需對應到正確的 executor 類別名，否則技能可能無法施放。
- **語系**：182 資料若為簡體/韓文，可選擇轉繁或保留；name_id 多為 $ 開頭代碼，客戶端需有對應字串檔。
- **「高還原方案」**：若改為相容 182 ID，需動 L1ItemId、L1SkillId、C_UseItem 及所有依 ID 分支的邏輯，風險高、工時大，**不建議**除非有明確需求。

以上為完整整合方案與 TODO，請審批後再進行實作與導入。

---

## 附錄：JP 伺服器讀取來源與硬編碼（程式對照）

- **物品**：`jp/src/jp/l1j/server/datatables/ItemTable.java` — `SELECT * FROM etc_items` / `weapons` / `armors`。**硬編碼**：`jp/src/jp/l1j/server/model/item/L1ItemId.java`（如 40010 藥水、140010 祝福藥水）、`C_UseItem.java` 內對 item id 的判斷。
- **NPC**：`jp/src/jp/l1j/server/datatables/NpcTable.java` — `SELECT * FROM npcs`，欄位名與 `jp/db/schema/mysql/npcs.sql` 一致。
- **技能**：`jp/src/jp/l1j/server/datatables/SkillTable.java` — `SELECT * FROM skills`，欄位由 `L1Skill.fromResultSet(rs)` 對應（見 `jp/src/jp/l1j/server/templates/L1Skill.java`）。**硬編碼**：`jp/src/jp/l1j/server/model/skill/L1SkillId.java`（如 HEAL=1, LIGHT=2…）。
- **CSV**：`jp/db/csv/tw/*.csv` 與 schema 對應；若 CSV 標題與 schema 不符（例如 `max_change_count` 應為 `max_charge_count`），匯入前需修正或由 ETL 產出正確欄位名。
