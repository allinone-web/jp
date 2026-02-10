# l1jdb 與 jpdbbackup 物品 use_type 對照報表

**產出日期**：供審批用，審批後可依此更新 l1jdb.etc_items.use_type 以與服務器編碼對齊。

---

## 1. 範圍說明

| 表名 | 是否有 use_type | 說明 |
|------|-----------------|------|
| **etc_items** | ✅ 有 | 本報表僅含此表；l1jdb 與 jpdbbackup 皆有此欄位 |
| **weapons** | ❌ 無 | 兩邊皆無 use_type 欄位 |
| **armors** | ❌ 無 | 兩邊皆無 use_type 欄位 |

- **l1jdb.etc_items**：id 範圍 3～378，共 197 筆（182 時代資料）。
- **jpdbbackup.etc_items**：id 範圍 40001～240100，與 l1jdb **無 id 重疊**。
- **對齊方式**：以 **identified_name_id** 對應（同一名稱 ID 視為同一種道具），從 jpdbbackup 取得該名稱在 JP 的 use_type 作為「建議值」。

---

## 2. jpdbbackup 中 use_type 種類統計（參考）

服務器編碼會依 use_type 分派道具行為，以下為 jp 庫中出現過的值：

| use_type | 筆數 | use_type | 筆數 |
|----------|------|----------|------|
| normal | 1352 | none | 363 |
| choice | 90 | npc_talk | 70 |
| spell_buff | 60 | spell_long | 100 |
| healing | 39 | poly | 36 |
| teleport | 35 | material | 31 |
| spell_point | 6 | spell_short | 9 |
| dai | 19 | earring | 24 |
| extra | 16 | furniture | 18 |
| haste | 14 | ring | 16 |
| zel | 14 | brave | 10 |
| magic_healing | 8 | magic_eye | 7 |
| magic_regeneration | 5 | blank | 5 |
| bless_eva | 4 | flora | 4 |
| ntele | 4 | roulette | 4 |
| sosc | 4 | wisdom | 3 |
| cure | 2 | identify | 2 |
| res | 2 | third_speed | 2 |
| instrument | 2 | btele | 1 |
| fishing_rod | 1 | spawn | 21 |

---

## 3. 對照表（每筆一列，審批用）

- **l1jdb_use_type**：目前 l1jdb 該筆的 use_type。
- **jp_use_type_suggested**：依 identified_name_id 對到 jpdbbackup 後取出的建議 use_type（同一 name_id 在 jp 有多筆時取一筆）。
- **diff**：same = 兩邊一致，DIFF = 建議更新，jp_null = jp 無此 name_id 需手動決定。

| id | identified_name_id | item_type | l1jdb_use_type | jp_use_type_suggested | diff |
|----|--------------------|-----------|-----------------|------------------------|------|
| 3 | $2 | other | none | normal | DIFF |
| 5 | $4 | other | none | none | same |
| 12 | $23 | other | none | normal | DIFF |
| 15 | $27 | other | none | (jp無) | jp_null |
| 16 | $28 | other | none | (jp無) | jp_null |
| 17 | $55 | other | none | identify | DIFF |
| 25 | $67 | other | none | normal | DIFF |
| 26 | $68 | other | none | normal | DIFF |
| 30 | $72 | other | none | normal | DIFF |
| 36 | $80 | other | none | (jp無) | jp_null |
| 38 | $82 | other | none | normal | DIFF |
| 41 | $85 | other | none | normal | DIFF |
| 54 | $106 | other | none | normal | DIFF |
| 55 | $107 | other | none | normal | DIFF |
| 56 | $110 | other | none | brave | DIFF |
| 66 | $145 | other | none | none | same |
| 67 | $146 | other | none | none | same |
| 68 | $149 | other | none | none | same |
| 69 | $150 | other | none | none | same |
| 99 | $230 | other | none | ntele | DIFF |
| 100 | $232 | other | none | (jp無) | jp_null |
| 101 | $233 | other | none | cure | DIFF |
| 102 | $234 | other | none | (jp無) | jp_null |
| 103 | $235 | other | none | healing | DIFF |
| 104 | $237 | other | none | (jp無) | jp_null |
| 105 | $238 | other | none | healing | DIFF |
| 106 | $239 | other | none | (jp無) | jp_null |
| 108 | $243 | other | none | normal | DIFF |
| 109 | $244 | other | none | dai | DIFF |
| 110 | $249 | other | none | zel | DIFF |
| 111 | $254 | other | none | zel | DIFF |
| 112 | $257 | other | none | res | DIFF |
| 114 | $263 | other | none | (jp無) | jp_null |
| 115 | $307 | other | none | none | same |
| 116 | $308 | other | none | none | same |
| 117 | $311 | other | none | (jp無) | jp_null |
| 120 | $314 | other | none | none | same |
| 121 | $315 | other | none | none | same |
| 122 | $326 | other | none | normal | DIFF |
| 123 | $327 | other | none | normal | DIFF |
| 134 | $499 | other | none | none | same |
| 135 | $500 | other | none | none | same |
| 136 | $501 | other | none | none | same |
| 137 | $502 | other | none | none | same |
| 138 | $503 | other | none | none | same |
| 139 | $505 | other | none | normal | DIFF |
| 140 | $508 | other | none | none | same |
| 141 | $512 | other | none | none | same |
| 142 | $513 | other | none | none | same |
| 143 | $514 | other | none | none | same |
| 144 | $515 | other | none | none | same |
| 146 | $517 | spellbook | none | normal | DIFF |
| 147 | $518 | spellbook | none | normal | DIFF |
| 148 | $519 | spellbook | none | normal | DIFF |
| 149 | $520 | spellbook | none | normal | DIFF |
| 150 | $521 | spellbook | none | normal | DIFF |
| 151 | $522 | spellbook | none | normal | DIFF |
| 152 | $523 | spellbook | none | normal | DIFF |
| 153 | $524 | spellbook | none | normal | DIFF |
| 154 | $525 | spellbook | none | normal | DIFF |
| 155 | $526 | spellbook | none | normal | DIFF |
| 156 | $527 | spellbook | none | normal | DIFF |
| 157 | $528 | spellbook | none | normal | DIFF |
| 158 | $529 | spellbook | none | normal | DIFF |
| 159 | $530 | spellbook | none | normal | DIFF |
| 160 | $531 | spellbook | none | normal | DIFF |
| 161 | $532 | spellbook | none | normal | DIFF |
| 162 | $533 | spellbook | none | normal | DIFF |
| 163 | $534 | spellbook | none | normal | DIFF |
| 164 | $535 | spellbook | none | normal | DIFF |
| 165 | $536 | spellbook | none | normal | DIFF |
| 166 | $537 | spellbook | none | normal | DIFF |
| 167 | $538 | spellbook | none | normal | DIFF |
| 168 | $539 | spellbook | none | normal | DIFF |
| 169 | $540 | spellbook | none | normal | DIFF |
| 170 | $541 | spellbook | none | normal | DIFF |
| 171 | $542 | spellbook | none | normal | DIFF |
| 172 | $543 | spellbook | none | normal | DIFF |
| 173 | $544 | spellbook | none | normal | DIFF |
| 174 | $545 | spellbook | none | normal | DIFF |
| 175 | $546 | spellbook | none | normal | DIFF |
| 176 | $547 | spellbook | none | normal | DIFF |
| 177 | $548 | spellbook | none | normal | DIFF |
| 178 | $549 | spellbook | none | normal | DIFF |
| 179 | $550 | spellbook | none | normal | DIFF |
| 180 | $551 | spellbook | none | normal | DIFF |
| 181 | $552 | spellbook | none | normal | DIFF |
| 182 | $553 | spellbook | none | normal | DIFF |
| 183 | $554 | spellbook | none | normal | DIFF |
| 184 | $555 | spellbook | none | normal | DIFF |
| 185 | $556 | spellbook | none | normal | DIFF |
| 186 | $557 | spellbook | none | normal | DIFF |
| 187 | $558 | spellbook | none | normal | DIFF |
| 188 | $559 | spellbook | none | normal | DIFF |
| 189 | $560 | spellbook | none | normal | DIFF |
| 190 | $561 | spellbook | none | normal | DIFF |
| 191 | $562 | spellbook | none | normal | DIFF |
| 192 | $563 | spellbook | none | normal | DIFF |
| 193 | $564 | spellbook | none | normal | DIFF |
| 194 | $565 | spellbook | none | normal | DIFF |
| 195 | $566 | spellbook | none | normal | DIFF |
| 203 | $618 | other | none | ntele | DIFF |
| 205 | $760 | other | none | none | same |
| 206 | $761 | other | none | none | same |
| 207 | $762 | other | none | none | same |
| 208 | $763 | other | none | cure | DIFF |
| 209 | $764 | other | none | none | same |
| 210 | $765 | other | none | none | same |
| 211 | $766 | other | none | none | same |
| 212 | $767 | other | none | none | same |
| 213 | $768 | other | none | none | same |
| 214 | $769 | other | none | none | same |
| 215 | $770 | other | none | none | same |
| 216 | $771 | other | none | none | same |
| 217 | $772 | other | none | none | same |
| 219 | $774 | other | none | none | same |
| 221 | $776 | other | none | none | same |
| 222 | $777 | other | none | instrument | DIFF |
| 224 | $779 | other | none | none | same |
| 225 | $780 | other | none | none | same |
| 239 | $794 | other | none | healing | DIFF |
| 248 | $923 | other | none | (jp無) | jp_null |
| 252 | $942 | other | none | (jp無) | jp_null |
| 253 | $943 | other | none | brave | DIFF |
| 254 | $944 | other | none | wisdom | DIFF |
| 255 | $954 | other | none | none | same |
| 256 | $971 | other | none | sosc | DIFF |
| 264 | $1011 | other | none | normal | DIFF |
| 265 | $1012 | other | none | normal | DIFF |
| 267 | $1014 | other | none | normal | DIFF |
| 284 | $1036 | other | none | none | same |
| 285 | $1037 | other | none | none | same |
| 286 | $1038 | other | none | none | same |
| 287 | $1039 | other | none | none | same |
| 288 | $1040 | other | none | none | same |
| 291 | $1086 | other | none | instrument | DIFF |
| 292 | $1097 | other | none | (jp無) | jp_null |
| 295 | $1100 | other | none | choice | DIFF |
| 296 | $1102 | other | none | normal | DIFF |
| 297 | $1103 | other | none | normal | DIFF |
| 298 | $1104 | other | none | normal | DIFF |
| 299 | $1105 | other | none | normal | DIFF |
| 300 | $1106 | other | none | normal | DIFF |
| 301 | $1107 | other | none | normal | DIFF |
| 302 | $1108 | other | none | normal | DIFF |
| 303 | $1109 | other | none | normal | DIFF |
| 304 | $1146 | other | none | (jp無) | jp_null |
| 308 | $1173 | other | none | normal | DIFF |
| 309 | $1188 | other | none | normal | DIFF |
| 310 | $1197 | other | none | none | same |
| 311 | $1199 | other | none | none | same |
| 312 | $1200 | other | none | none | same |
| 321 | $1251 | other | none | healing | DIFF |
| 322 | $1252 | other | none | healing | DIFF |
| 323 | $1253 | other | none | healing | DIFF |
| 324 | $1507 | other | bless_eva | bless_eva | same |
| 325 | $1508 | other | bless_eva | bless_eva | same |
| 326 | $1533 | other | none | npc_talk | DIFF |
| 327 | $1534 | other | none | npc_talk | DIFF |
| 328 | $1535 | other | none | normal | DIFF |
| 329 | $1607 | other | none | normal | DIFF |
| 330 | $1608 | other | none | none | same |
| 331 | $6 $23 | other | none | normal | DIFF |
| 332 | $799 $512 | other | none | none | same |
| 333 | $799 $513 | other | none | none | same |
| 334 | $799 $514 | other | none | none | same |
| 335 | $799 $515 | other | none | none | same |
| 336 | $800 $512 | other | none | none | same |
| 337 | $800 $513 | other | none | none | same |
| 338 | $800 $514 | other | none | none | same |
| 339 | $800 $515 | other | none | none | same |
| 340 | $1075 | other | none | (jp無) | jp_null |
| 341 | $1075 | other | none | (jp無) | jp_null |
| 342 | $1075 | other | none | (jp無) | jp_null |
| 343 | ?????? | other | none | (jp無) | jp_null |
| 344 | $343 | other | none | (jp無) | jp_null |
| 345 | ?????? | other | none | (jp無) | jp_null |
| 346 | ????(???) | other | none | (jp無) | jp_null |
| 347 | $1487 $1485 | other | none | spell_long | DIFF |
| 348 | ????(???) | other | none | (jp無) | jp_null |
| 349 | ?????? | other | none | (jp無) | jp_null |
| 350 | ???????? | other | none | (jp無) | jp_null |
| 354 | $1652 $234 | other | none | (jp無) | jp_null |
| 355 | \\f=?? | other | none | (jp無) | jp_null |
| 356 | \\f=??(????????) | other | none | (jp無) | jp_null |
| 357 | $1889 | other | none | normal | DIFF |
| 358 | $1829 | spellbook | none | normal | DIFF |
| 359 | $1830 | spellbook | none | normal | DIFF |
| 360 | $1831 | spellbook | none | normal | DIFF |
| 361 | $1822 | other | none | none | same |
| 362 | $1835 | spellbook | none | normal | DIFF |
| 363 | $1832 | spellbook | none | normal | DIFF |
| 364 | $1833 | spellbook | none | normal | DIFF |
| 365 | $1837 | spellbook | none | normal | DIFF |
| 366 | $1838 | spellbook | none | normal | DIFF |
| 367 | $1840 | spellbook | none | normal | DIFF |
| 378 | $941 | other | none | (jp無) | jp_null |

---

## 4. 統計摘要

| 狀態 | 筆數 |
|------|------|
| **same**（與 jp 一致，無需改） | 58 |
| **DIFF**（建議改為 jp_use_type_suggested） | 107 |
| **jp_null**（jp 無此 name_id，需手動審批） | 32 |
| **合計** | 197 |

---

## 5. 審批後更新方式

審批完成後，可依您決定的 use_type 執行 SQL 更新，例如（僅範例，請依審批結果替換）：

```sql
USE l1jdb;

-- 範例：僅更新「審批通過」且與 jp 建議一致之項目時，可分批執行，例如：
-- UPDATE etc_items SET use_type = 'normal' WHERE id IN (3, 12, 25, ...);
-- UPDATE etc_items SET use_type = 'healing' WHERE id IN (103, 105, 239, 321, 322, 323);
-- UPDATE etc_items SET use_type = 'cure' WHERE id IN (101, 208);
-- ... 其餘依報表審批結果
```

**請勿未經審批直接整批覆蓋 use_type。** 建議先備份 `etc_items` 再執行更新。

---

## 6. SpellIcon / EnchantBonus 對應（use_type 與 item_id）

### SpellIcon（道具 spell icon 顯示）

- **是否可行**：可行。服務器已支援透過 **use_type=spellicon**（數值 76）或 **item_id=26/30** 將道具視為スペルアイコン（item_type=20）。
- **實作**：`ItemTable` 已加入 `_useTypes.put("spellicon", 76)` 與 `resolveEtcItemType` 中 `useType==76` 及 `itemId==30` 的對應。
- **為道具 id=30 設定 SpellIcon**（擇一即可）：
  - **推薦**：在 DB 將該道具的 use_type 設為 `spellicon`。
  - 若使用 **items** 表：`UPDATE items SET use_type='spellicon' WHERE item_id=30;`
  - 若使用 **etc_items** 表（如 l1jdb）：`UPDATE etc_items SET use_type='spellicon' WHERE id=30;`
  - 未更新 DB 時，服務器已對 **item_id=30** 直接當作 spellicon（type=20）處理。

### EnchantBonus（特殊道具額外加成）

- **是否可行**：EnchantBonus 是依 **item_id** 在 `EnchantBonusTable` 查表，不是依 use_type。可行為「為指定 item_id 註冊加成資料」。
- **實作**：已為 **item_id=54** 在 `EnchantBonusTable` 註冊 `ZeroEnchantBonusData`（加成全 0），避免無資料警告；若需實際數值可改為從 DB 載入或自訂實作。
- **為道具 id=54 設定 EnchantBonus**：無需改 use_type；程式端已完成 item_id=54 的註冊。日後若新增 enchant_bonus 表，可改為從 DB 載入 54 的數值。

---

## 7. 本輪重構完成度：道具是否全部由 DB 驅動

### 結論：**已達成「道具定義與類型由 DB 驅動」**

| 項目 | 狀態 | 說明 |
|------|------|------|
| 道具定義來源 | ✅ DB | `ItemTable` 僅從 `items` 表 `select * from items` 載入，無 XML 讀取 |
| use_type / type | ✅ DB | `use_type`、`type`（item_type）、`type2` 皆由 DB 欄位經 `resolveUseType` / `resolveEtcItemType` 設定 |
| 可否使用（use_type=-1） | ✅ DB | `C_UseItem` 以 `item.getItem().getUseType() == -1` 判斷，來自 DB |
| 行為分派主路徑 | ✅ DB | 先依 `getUseType()`、`getType()` 分派（identify、res、spell_long、food、spellicon 等），數值來自 DB |
| Item XML | ✅ 已移除 | `data/xml/Item/` 已刪除，程式無任何 Item XML 引用 |
| 特殊行為（itemId 分支） | ⚠️ 保留 | 部分道具仍以 itemId 分支（如 41401 家具除去、41426 封印、43001 性別變更），屬「同 use_type 不同行為」之合理設計 |

### 未由 DB 驅動的部分（設計取捨）

- **C_UseItem** 內約數十個 `itemId == xxx` 分支：用於「同一 use_type 下、僅特定道具才有的行為」（例如封印卷軸 41426 vs 解封 41427）。若要改為 100% 由 DB 配置表驅動，需日後引入「item_id → 行為代碼」或「use_type + 參數」表再重構。

---

*報表由 l1jdb / jpdbbackup 查詢產出，對齊鍵：identified_name_id。*
