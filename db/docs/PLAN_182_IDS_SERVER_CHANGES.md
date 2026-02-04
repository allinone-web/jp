# 採用 182 ID 體系整合方案（僅方案・待審批）

## 一、目標與原則

- **目標**：遊戲內 **裝備、NPC、魔法、地圖傳送、NPC 座標、NPC 物品掉落** 等 **六大類及所有關聯資料**，**全部採用 182 資料庫的資料與 ID 命名方式**（例如物品 1～500、技能 1～50、NPC/怪物 1～600+），不再做「182→JP」的 ID 映射。
- **保留**：  
  - **JP 伺服器**的新功能與程式邏輯。  
  - **JP 的資料庫結構**（表名、欄位名、型別、主外鍵設計），僅將「內容與 ID」換成 182 的資料與 ID。
- **代價**：JP 程式內所有依賴「物品 ID / 技能 ID / NPC ID」的硬編碼與範圍判斷，必須**逐項對照 182 的 ID** 做修改，否則功能會錯亂或失效。

本文件僅描述**方案、對比與修改清單**，不實作程式碼，供審批後再執行。

---

## 二、資料層：採用 182 的範圍與命名

### 2.1 資料庫結構與資料來源

| 項目 | 結構（Schema） | 資料與 ID 來源 |
|------|----------------|----------------|
| 表結構、欄位名、型別 | **JP**（`jp/db/schema/mysql/`） | — |
| 物品主資料（id、name、gfx、weight…） | — | **182** `items` → 轉入 JP 的 `etc_items` / `weapons` / `armors`（**保留 182 的 item_id 作為 id**） |
| NPC/怪物主資料（id、name、gfx、hp…） | — | **182** `npc` + `monster` → 合併轉入 JP 的 `npcs`（**保留 182 的 npcid / monster uid 作為 npcs.id**） |
| 技能主資料（id、name、skill_number…） | — | **182** `skill_list` → 轉入 JP 的 `skills`（**保留 182 的 skill_id 作為 id**） |
| 地圖傳送 | — | **182** `npc_teleport` → 對應到 JP 的傳送表（若 JP 為 `return_locations` / `restart_locations` 或專用表，需欄位對照後用 182 的 npc_id、座標、地圖、aden） |
| NPC 座標（出生點） | — | **182** `npc_spawnlist`、`monster_spawnlist` → 轉入 JP 的 `spawn_npcs`、`spawn_mobs`（**npc_id 使用 182 的 id**） |
| NPC 物品掉落 | — | **182** `monster_item_drop` → 轉入 JP 的 `drop_items`（**npc_id / item_id 皆為 182 的 id**） |
| 其他關聯 | — | **182** 的商店、怪物技能、鑰匙、旅館等 → 對應 JP 的 `shops`、`mob_skills`、`inn_keys`、`inns` 等，**所有外鍵 ID 一律使用 182 的 id** |

### 2.2 182 與 JP 的 ID 範圍對照（摘要）

| 類型 | 182 範圍／範例 | JP 現狀（硬編碼前） |
|------|----------------|----------------------|
| **物品** | item_id 1～數百（5=金幣、3=灯、25=蠟燭、17=鑑定卷軸、15/16=魔杖…） | 40001+（etc）、20000+（防具）、40010=治癒藥水、40308=阿丁等 |
| **技能** | skill_id 1～50（PC 魔法）、1000+（NPC 技能） | L1SkillId 1～220、500～502、1000～1037、3000～3052 等 |
| **NPC/怪物** | npcid 1～600+、monster uid 1～183+ | npcs.id 常為 45001+、5 位數 |

因此：**若全面改用 182 的 ID**，伺服器內所有寫死「JP 用大數字」的地方都必須改成「182 用的小數字或對應 182 id」。

---

## 三、伺服器需修改的範圍（總覽）

1. **物品 ID 硬編碼**  
   - `L1ItemId.java` 常數、`C_UseItem.java` 內所有 `itemId == xxx`、`itemId >= xxx && itemId <= yyy`、以及其餘引用到「具體物品 ID」的檔案。  
   - 需逐一對照：**JP 原 ID 對應的「語意」（例如治癒藥水、阿丁、傳送卷軸）** → **182 的 item_id**，並改為 182 的 ID 或改為依 DB/設定檔讀取。

2. **技能 ID 硬編碼**  
   - `L1SkillId.java` 及所有 `skillId == L1SkillId.xxx` 或數字判斷。  
   - 182 的 skill_id 1～50 與 JP 的 1～50 語意大致對應（初級治癒術=1、日光術=2…），但 JP 另有 87+ 騎士、97+ 黑妖、113+ 王族、129+ 妖精、181+ 龍騎、201+ 幻術等；若 182 沒有對應技能，需決定保留 JP 常數但改為 182 的 id，或移除/改邏輯。

3. **NPC ID 硬編碼**  
   - 少數檔案直接寫死 npc_id（例如 C_GiveItem、C_UseItem 內 70850、46291 等）。  
   - 改為 182 對應的 NPC/怪物 id，或改為由 DB/設定讀取。

4. **依賴「物品／技能／NPC」的模組**  
   - 掉落（DropTable）、商店、倉庫、傳送、旅館、寵物、魔法娃娃、技能書、強化卷軸等，凡用到「具體 id」的地方，都要對照 182 做修改或改為資料驅動。

5. **祝福/詛咒物品的 id 計算**  
   - JP 常用「原 id + 100000 / + 200000」表示祝福/詛咒；182 若無此規則，需訂出 182 的祝福/詛咒 id 規則，並在程式內統一改為該規則或改為由 DB 判斷。

以下分「物品」「技能」「NPC」「地圖傳送」「出生與掉落」「其他」列出**具體需修改的檔案與項目**（僅清單，不改代碼）。

---

## 四、物品：資料差異與伺服器修改清單

### 4.1 關鍵物品 ID 對照（182 vs JP 語意）

| 語意／用途 | JP 現 ID（例） | 182 item_id（例） | 說明 |
|------------|----------------|-------------------|------|
| 金幣／阿丁 | 40308 | 5 | 全服唯一貨幣，改後需全局替換 |
| 治癒藥水（紅水） | 40010 | 需查 182 表（可能無直接對應名） | 182 有「胡萝卜」26、食物等，需定對應或新增 |
| 祝福/詛咒紅水 | 140010 / 240010 | 182 若無則訂規則 | 可能需改祝福/詛咒計算邏輯 |
| 燈／燈油 | 40001～40003、40002 | 3（灯）、需查燈油 | C_UseItem 燈油→燈 邏輯 |
| 蠟燭 | 40005 | 25 | |
| 鑑定卷軸 | 7(identify) | 17 | |
| 魔杖類 | 40006～40009、140006/140008 | 15（枫木）、16（松木）等 | S_DropItem 等魔杖 isId 判斷 |
| 傳送卷軸 | 40100、140100 | 需查 182 | 指定傳送、祝福傳送 |
| 集體傳送 | 40086 | 需查 182 | |
| 防具/武器強化卷 | 40074/140074/240074、40087/140087/240087 | 需查 182 | |
| 歸還卷軸 | 40079、40095 | 需查 182 | |
| 解咒卷軸 | 40097、40119 等 | 需查 182 | |
| 寵物項圈／笛 | 40314、40315、40316 | 需查 182 pet 相關 | |
| 旅館鑰匙 | 40312 | 36（钥匙）或 182 專用 | S_DropItem、S_IdentifyDesc、InnKeyTable |
| 騎馬用頭盔 | 20383 | 需查 182 防具 | S_DropItem |
| 變身卷軸 | 40088、40096、140088 | 需查 182 | C_NpcAction |
| 溶解劑、聖水、封印卷等 | 41245、41315、41426～41428… | 需逐項查 182 | C_UseItem 內大量分支 |
| 精霊の石 | 40515 | 需查 182 | DeleteItemController |
| 釣魚相關 | 41294 | 需查 182 | FishingTimeController |
| 拍賣／倉庫／存款取款 | L1ItemId.ADENA 多處 | 全部改為 5（182 金幣） | DropTable、C_NpcAction、C_Drawal、C_Deposit、C_SkillBuyOK、C_CreateClan、C_Result、AuctionTimeController 等 |

以上「需查 182」項目，需產出**完整 182 物品表關鍵欄位清單**（item_id, name, nameid, type），並標註「對應 JP 的哪個功能」，再決定是改為 182 的 id 或保留由 DB 讀取。

### 4.2 需修改的檔案清單（物品相關）

| 序號 | 檔案路徑 | 修改內容概要 |
|------|----------|--------------|
| 1 | `jp/l1j/server/model/item/L1ItemId.java` | 所有常數改為 182 的 item_id（ADENA=5、藥水/卷軸/燈等對應 182）；若 182 無則改為常數對應 182 或改為從設定/DB 讀取 |
| 2 | `jp/l1j/server/packets/client/C_UseItem.java` | 所有 `itemId ==`、`itemId >= xxx && itemId <= yyy`、以及 40003/40002、40008、40070、40079、40124、40314/40315/40316/40317、40493、40858、41245、41315、41316、41354、41401、41426～41428、42100、43001、49168、50501、50539、50547、50560～63、50585～86、45023～45024、40226～40232、40164～40166、41147～41148、40264～40280、42106、49102～49136、49117～49136 等範圍或單點判斷，改為 182 對應 id 或改為依 use_type/DB |
| 3 | `jp/l1j/server/model/L1ItemDelay.java` | 20077、20062、120077 改為 182 對應防具 id 或改為由 DB 判斷 |
| 4 | `jp/l1j/server/packets/server/S_DropItem.java` | 40312、20383、40006/40007/40008/40009/140006/140008 改為 182 的鑰匙、騎馬盔、魔杖 id |
| 5 | `jp/l1j/server/packets/server/S_IdentifyDesc.java` | 40312、20383 同上 |
| 6 | `jp/l1j/server/packets/server/S_PetList.java` | 40314、40316 改為 182 寵物項圈 id |
| 7 | `jp/l1j/server/datatables/DropTable.java` | L1ItemId.ADENA 改為 182 金幣 id（5）；其餘若有用到具體 item id 則一併改 |
| 8 | `jp/l1j/server/controller/timer/DeleteItemController.java` | 40515 改為 182 精霊の石 id |
| 9 | `jp/l1j/server/controller/timer/FishingTimeController.java` | 41294 改為 182 釣魚道具 id |
| 10 | `jp/l1j/server/controller/timer/AuctionTimeController.java` | L1ItemId.ADENA 改為 5 |
| 11 | `jp/l1j/server/packets/client/C_NpcAction.java` | 所有 L1ItemId.ADENA、40088/40096/140088、以及 1000/700000/100000/10000/100 等消費阿丁的邏輯，改為 182 金幣 id；其他具體 item id 依 182 表修改 |
| 12 | `jp/l1j/server/packets/client/C_SkillBuyOK.java` | L1ItemId.ADENA 改為 5 |
| 13 | `jp/l1j/server/packets/client/C_CreateClan.java` | ADENA 改為 5 |
| 14 | `jp/l1j/server/packets/client/C_Drawal.java` | L1ItemId.ADENA 改為 5 |
| 15 | `jp/l1j/server/packets/client/C_Deposit.java` | L1ItemId.ADENA 改為 5 |
| 16 | `jp/l1j/server/packets/client/C_Result.java` | L1ItemId.ADENA、40314/40316、49016～49025 等改為 182 對應 id |
| 17 | `jp/l1j/server/packets/client/C_GiveItem.java` | 40054、petType.getTameItemId()/getTransformItemId() 改為 182 對應（或保留由 pet_types 表讀取）；46291 改為 182 對應 NPC id |
| 18 | 其他所有引用 `L1ItemId.xxx` 或具體數字物品 id 的 .java | 依 grep 結果逐檔改為 182 id 或改為資料驅動 |

**說明**：  
- 「改為 182 對應 id」＝先完成「182 物品表關鍵欄位清單」與「JP 功能對照表」，再將常數或魔數改為該 id。  
- 若希望後續易維護，可將「金幣 id、鑑定卷 id、傳送卷 id」等改為 Config 或 DB 設定，程式只讀取一次；但本方案以「對應 182」為主，仍須列出每個需改的點。

---

## 五、技能：資料差異與伺服器修改清單

### 5.1 技能 ID 對照要點

- **182**：`skill_list` 的 skill_id 1～50 為 PC 一般魔法（1=初級治癒術、2=日光術…），與 **JP L1SkillId 1～50** 語意大致一致；50 之後 182 有 NPC 技能（1000+）等。
- **JP**：L1SkillId 除 1～80 一般魔法外，尚有 87～91 騎士、97～112 黑妖、113～120 王族、129～176 妖精、181～195 龍騎、201～220 幻術；以及 STATUS_*、COOKING_*、GMSTATUS_*、部分 BOSS 技能等。
- **若全面採用 182**：  
  - 182 有的 skill_id（1～50 等）直接使用。  
  - JP 獨有技能（騎士/黑妖/王族/妖精/龍騎/幻術/料理/狀態）：若 182 沒有，需決定是（A）在 182 表新增並沿用 JP 的數字、或（B）改 L1SkillId 常數為 182 的 id 並接受 182 沒有則該技能不存在。

### 5.2 需修改的檔案清單（技能相關）

| 序號 | 檔案路徑 | 修改內容概要 |
|------|----------|--------------|
| 1 | `jp/l1j/server/model/skill/L1SkillId.java` | 所有常數改為與 182 skill_list 的 skill_id 一致；182 沒有的技能，常數改為 182 若有的 id 或註記「182 無」並由後續設計決定是否移除/改邏輯 |
| 2 | 所有 `import static ... L1SkillId.*` 或 `skillId == L1SkillId.xxx`、`skillId == 數字` 的檔案 | 改為 182 的 skill id；需全文搜尋 L1SkillId、getSkillId()、skill_id 比對 |

技能使用處分散在：技能施放、技能書、BUFF、寵物/召喚、魔法娃娃、NPC 技能等，建議用 grep 產出「skill id 引用清單」再逐項對 182 表修正。

---

## 六、NPC：資料差異與伺服器修改清單

### 6.1 NPC ID 對照要點

- **182**：`npc` 表 npcid（1～600+）、`monster` 表 uid（1～183+）；傳送表 `npc_teleport` 使用 npc_id（如 135、136、373）。
- **JP**：npcs 表 id 多為 5 位數（45001 起等）；少數程式寫死 npc_id（如 70850、46291）。
- **若全面採用 182**：spawn、shops、drop、inns、mob_skills、npc_chats、傳送表 等全部改為 182 的 npcid/uid；程式內寫死的 npc_id 改為 182 對應 id。

### 6.2 需修改的檔案清單（NPC 相關）

| 序號 | 檔案路徑 | 修改內容概要 |
|------|----------|--------------|
| 1 | `jp/l1j/server/packets/client/C_UseItem.java`（或 C_NpcAction） | 70850（パン）改為 182 對應怪物/NPC id |
| 2 | `jp/l1j/server/packets/client/C_GiveItem.java` | 46291 改為 182 對應 NPC id |
| 3 | 其他若有直接寫死 npc_id 的 .java | 依 grep 結果改為 182 的 npc/monster id |

其餘 NPC 使用處多為「從 DB 讀 npc_id」（spawn_npcs、spawn_mobs、shops、drop_items、inns、npc_chats、npc_teleport 等），只要 DB 改為 182 的 id 即可，無需改程式；唯需確保 **npcs.impl** 與 182 的 type 對應正確（Guard→L1Guard、Shop→L1Shop、monster→L1Monster 等）。

---

## 七、地圖傳送

- **182**：`npc_teleport`（action, npc_id, tele_num, check_lv_min/max, check_map, x, y, map, aden）。
- **JP**：可能有 `return_locations`、`restart_locations` 或專用傳送表；需對照 schema 後，將 182 的 npc_id、座標、map、aden 匯入 JP 表，**npc_id 使用 182 的 id**。
- **伺服器**：若程式有依 npc_id 查傳送目的地的邏輯，只要 DB 為 182 的 npc_id 即一致；若有寫死 npc_id，則改為 182 對應 id（目前未在已搜尋檔案中發現傳送專用硬編碼，可再 grep 確認）。

---

## 八、NPC 座標（出生點）與 NPC 物品掉落

- **資料**：  
  - 出生：182 `monster_spawnlist`、`npc_spawnlist` → JP `spawn_mobs`、`spawn_npcs`（npc_id、loc、map_id 等用 182 的 id 與座標）。  
  - 掉落：182 `monster_item_drop`（monid, itemid, count_min/max, chance）→ JP `drop_items`（npc_id, item_id, min, max, chance）；**npc_id = 182 monster uid，item_id = 182 item_id**。
- **伺服器**：DropTable、SpawnTable 等皆依 DB 的 npc_id、item_id 運作，**不需改程式**，只要確保 DB 內容為 182 的 id；唯一例外是 DropTable 內「L1ItemId.ADENA」改為 182 金幣 id（5），已列於「四、物品」清單。

---

## 九、其他關聯表與一致性

- **shops**：npc_id、item_id 改為 182 的 id。  
- **mob_skills**：npc_id 改為 182 的 monster uid。  
- **inns / inn_keys**：npc_id、key 相關 item_id 改為 182。  
- **pet_types**：npc_id、transform_npc_id、馴服/進化用 item_id 改為 182。  
- **magic_dolls**、**beginner_items**、**restart_locations**、**return_locations** 等：凡有 item_id/npc_id/skill_id 的欄位，一律改為 182 的 id。  

以上皆為**資料匯入與一致性檢查**，不需改程式（除物品/技能/NPC 的硬編碼已列於第四～六節）。

---

## 十、執行順序建議（審批通過後）

1. **產出 182 完整對照表**  
   - 物品：182 items 全表（item_id, name, nameid, type）＋與 JP 功能對照（金幣、藥水、卷軸、魔杖、鑰匙、寵物、騎馬盔等）。  
   - 技能：182 skill_list（skill_id, name, skill_level, skill_no）＋與 L1SkillId 常數對照。  
   - NPC/怪物：182 npc + monster（id, name, name_id, gfx）＋與 JP impl 對應。

2. **依本文件第四～六節清單**，逐檔修改：  
   - 先改 L1ItemId、L1SkillId 常數與 C_UseItem 主分支。  
   - 再改 DropTable、C_NpcAction、C_Drawal、C_Deposit、C_Result、C_GiveItem、S_DropItem、S_IdentifyDesc、S_PetList、各 Timer 與 C_* 內具體 id。

3. **資料庫**：用 JP schema 建表，再將 182 資料轉入（保留 182 的 id），並完成傳送、出生、掉落、商店、mob_skills、inns、pet_types 等關聯表。

4. **一致性檢查**：所有引用 item_id/npc_id/skill_id 的 DB 欄位與程式常數，皆能在 182 對照表中找到且一致。

5. **啟動與遊戲內驗證**：伺服器啟動無錯、登入、打怪、掉寶、買賣、技能、傳送、寵物、旅館等核心循環可連續執行。

---

## 十一、風險與注意

- **祝福/詛咒**：182 若無 +100000/+200000 規則，需在 182 表或程式中訂出新規則並統一。  
- **182 缺少的 JP 物品/技能**：需決定是捨棄、或保留並在 182 表「擴充」對應 id，程式則用 182 的 id。  
- **客戶端**：若客戶端有寫死物品/技能/NPC 的圖示或名稱對應，也需對應 182 的 id 與 182 的 nameid/名稱。  

以上為「採用 182 ID 體系」的完整方案與修改清單，**僅方案、不改代碼**，供審批後再依序執行。

---

## 附錄 A：L1ItemId 常數一覽（JP 現值 → 需改為 182 對應）

| 常數名 | JP 現值 | 語意 | 182 對應 item_id（需查表確認） |
|--------|---------|------|--------------------------------|
| ADENA | 40308 | 金幣 | 5 |
| POTION_OF_HEALING | 40010 | 紅水 | 待查 182 |
| B_POTION_OF_HEALING | 140010 | 祝福紅水 | 待訂規則 |
| C_POTION_OF_HEALING | 240010 | 詛咒紅水 | 待訂規則 |
| POTION_OF_EXTRA_HEALING | 40011 | 橙水 | 待查 |
| B_POTION_OF_EXTRA_HEALING | 140011 | 祝福橙水 | 待訂 |
| POTION_OF_GREATER_HEALING | 40012 | 澄水 | 待查 |
| B_POTION_OF_GREATER_HEALING | 140012 | 祝福澄水 | 待訂 |
| POTION_OF_HASTE_SELF | 40013 | 加速水 | 待查 |
| B_POTION_OF_HASTE_SELF | 140013 | 祝福加速水 | 待訂 |
| POTION_OF_GREATER_HASTE_SELF | 40018 | 強化加速水 | 待查 |
| B_POTION_OF_GREATER_HASTE_SELF | 140018 | 祝福強化加速水 | 待訂 |
| POTION_OF_EMOTION_BRAVERY | 40014 | 勇敢水 | 待查 |
| B_POTION_OF_EMOTION_BRAVERY | 140014 | 祝福勇敢水 | 待訂 |
| POTION_OF_MANA | 40015 | 魔力回復水 | 待查 |
| B_POTION_OF_MANA | 140015 | 祝福魔力水 | 待訂 |
| POTION_OF_EMOTION_WISDOM | 40016 | 智慧水 | 待查 |
| B_POTION_OF_EMOTION_WISDOM | 140016 | 祝福智慧水 | 待訂 |
| POTION_OF_CURE_POISON | 40017 | 解毒水 | 待查 |
| CONDENSED_POTION_* | 40019～40021 | 濃縮體力水 | 待查 |
| POTION_OF_BLINDNESS | 40025 | 布萊德水 | 待查 |
| SCROLL_OF_ENCHANT_ARMOR | 40074 | 防具強化卷 | 待查 |
| B/C_SCROLL_OF_ENCHANT_ARMOR | 140074/240074 | 祝福/詛咒防卷 | 待訂 |
| SCROLL_OF_ENCHANT_WEAPON | 40087 | 武器強化卷 | 待查 |
| B/C_SCROLL_OF_ENCHANT_WEAPON | 140087/240087 | 祝福/詛咒武卷 | 待訂 |
| SCROLL_OF_ENCHANT_QUEST_WEAPON | 40660 | 試練卷 | 待查 |
| SCROLL_OF_TELEPORT | 40100 | 傳送卷 | 待查 |
| B_SCROLL_OF_TELEPORT | 140100 | 祝福傳送卷 | 待訂 |
| SPELL_SCROLL_TELEPORT | 40863 | 魔法卷傳送 | 待查 |
| SCROLL_OF_MASS_TELEPORT | 40086 | 集體傳送卷 | 待查 |
| IVORY_TOWER_TELEPORT_SCROLL | 40099 | 象牙塔傳送卷 | 待查 |

（其餘 L1ItemId 常數同樣需逐項對 182 表或訂出祝福/詛咒規則。）

---

## 附錄 B：182 已知關鍵物品（items 表前段）

| 182 item_id | name | 對應 JP 功能建議 |
|-------------|------|------------------|
| 1 | 长剑 | 武器 |
| 2 | 斧 | 武器 |
| 3 | 灯 | 燈 |
| 4 | 双手剑 | 武器 |
| 5 | 金币 | **金幣/阿丁** |
| 15 | 枫木魔杖 | 魔杖 |
| 16 | 松木魔杖 | 魔杖 |
| 17 | 鉴定卷轴 | 鑑定 |
| 25 | 蜡烛 | 蠟燭 |
| 26 | 胡萝卜 | 食物/補品？ |
| 36 | 钥匙 | 旅館鑰匙候選 |

（完整對照需從 182 `items.sql` 全表匯出後逐項標註 JP 功能與 L1ItemId/常數對應。）
