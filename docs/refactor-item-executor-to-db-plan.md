# 道具邏輯由 XML Executor 改為 DB 驅動 — 修改方案

## 一、現狀與問題

- **直接原因**：`data/xml/Item/*.xml` 已被刪除，但程式仍透過 `L1BlessOfEva.get(itemId)` 等呼叫觸發 `loadXml()`，導致 `No such file or directory`。
- **根本原因**：道具使用、強化加成、技能結束還原等邏輯依賴多個「XML executor」類（`L1BlessOfEva`、`L1HealingPotion`、`L1SpellIcon`、`L1TreasureBox`、`L1EnchantBonus`、`L1EnchantProtectScroll`、`L1ExtraPotion`、`L1FloraPotion` 等），這些類多數會從 XML 載入資料。

**linserver182**：為 `net.*` 套件、與 jp 不同 codebase，無法直接複製檔案；但目標一致：**道具行為完全由 DB（use_type + etc_items 等）驅動，不依賴 XML**。

---

## 二、涉及檔案與依賴關係

| 檔案 | 依賴的 XML Executor | 說明 |
|------|---------------------|------|
| **C_UseItem.java** | 約 25 個 executor（L1BlessOfEva、L1HealingPotion、L1SpellIcon、L1TreasureBox、各 Potion、L1PolyScroll、L1Material 等） | 依 use_type 分支呼叫 `Xxx.get(itemId).use(pc, item)` |
| **L1ItemInstance.java** | L1EnchantBonus | 約 30 處：getAc/getStr/getCon/... 依強化等級加能力 |
| **L1EnchantScroll.java** | L1EnchantBonus、L1EnchantProtectScroll | 強化成功時加能力、保護捲失敗時降級 |
| **L1SkillTimer.java** | L1ExtraPotion、L1FloraPotion | 技能結束時用 getEffect() 還原能力（固定 itemId） |
| **C_LoginToServer.java** | L1ExtraPotion、L1FloraPotion | 登入恢復 buff 時用 getEffect() 加能力（固定 itemId） |
| **C_NpcAction.java** | 僅誤 import executor.L1BeginnerItem | 實際使用 `templates.L1BeginnerItem`（DB），只改 import 即可 |

**GameServer.java**：目前已無 XML 載入邏輯；若仍報錯，多半是第一次使用道具時 `L1BlessOfEva.get()` 觸發靜態 load()。

---

## 三、修改策略（徹底 DB 驅動、功能不消失）

### 原則

1. **不再依賴任何會讀取 XML 的 executor**：要麼移除呼叫，要麼改為從 DB/ItemTable 取資料。
2. **效果盡量用現有 DB 欄位**：`etc_items` 已有 `add_hp`、`add_mp`、`add_hpr`、`add_mpr`、`add_mr`、`add_str`、`add_dex` 等，可覆蓋多數藥水與祝福類效果。
3. **無法用 etc_items 表達的**（例如 TreasureBox 內容、SpellIcon 目標、EnchantBonus 每級數值）：  
   - 若有對應 DB 表則改為從 DB 讀；  
   - 若無則暫時用「安全預設」或固定邏輯（例如保護捲固定降 1），避免崩潰，功能可後續補表再擴充。

---

## 四、具體修改清單

### 4.1 C_UseItem.java

- **移除**：所有 `model.item.executor` 的 import（L1BlessOfEva、L1HealingPotion、…、L1Material 等）。
- **use_type 分支改為 DB 驅動**（依 `item.getItem()` 的 `L1EtcItem`）：
  - **59 (healing)**：用 `getAddHp()`，對 pc 加 HP、removeItem。
  - **60 (cure)**：沿用既有 cure 邏輯或從 L1EtcItem 取效果。
  - **61 (haste)**：用 L1SkillUse 或既有 haste 邏輯（若 DB 有對應 skill_id/effect_id）。
  - **62 (brave)**、**63 (third_speed)**：同上，依 effect_id/skill 或現有封包邏輯。
  - **64 (magic_eye)**：沿用既有魔眼邏輯或從 DB 讀。
  - **65 (magic_healing)**：用 `getAddMp()`。
  - **66 (bless_eva)**：用 `getAddMr()` 或水抗等（依現有設計）；若 etc_items 無水抗欄則暫用 add_mr。
  - **67 (magic_regeneration)**、**68 (wisdom)**：用 add_mpr / add_sp 等。
  - **69 (flora)**、**78 (extra)**：用 L1EtcItem 的 add_str/add_dex/…/add_hp/add_mp/add_hpr/add_mpr 等，必要時 setSkillEffect 記時間。
  - **30 (spell_buff)**、**72 (roulette)**、**74 (spawn)**、**73 (teleport)** 等：若無對應 DB 表，可暫時「無法使用」或保留最小邏輯（例如傳送用 loc_x/loc_y/map_id），不呼叫 XML executor。
- **保留**：`L1EnchantScroll.getInstance().use(pc, item, target)`（use_type 26/27），但該類內部改為不依賴 L1EnchantBonus/L1EnchantProtectScroll 的 XML，見下。
- **L1EnchantProtectScroll**（use_type 39 等保護捲）：改為從 DB 或固定規則（例如降 1 級）取得「失敗降級數」，不再呼叫 `L1EnchantProtectScroll.get(...)`。

### 4.2 L1ItemInstance.java

- **L1EnchantBonus**：改為由「DB 驅動」取得強化加成。
  - **做法 A**：新增/使用 `enchant_bonus` 表（item_id, enchant_level, ac, str, …），在 ItemTable 或新 EnchantBonusTable 載入；`getAc()`/`getStr()` 等改為查表，無則 +0。
  - **做法 B（最小改動）**：暫時不做強化加成，`L1EnchantBonus.get(getItemId())` 改為一律視為 null（或新建不讀 XML 的 EnchantBonusTable 從 DB 讀，無資料則回傳 0）。  
  如此可先移除對 XML 的依賴，再依需求補 DB 表與讀取邏輯。

### 4.3 L1EnchantScroll.java

- **L1EnchantBonus**：與 L1ItemInstance 一致，改為 DB 或「無則 0」。
- **L1EnchantProtectScroll**：改為從 DB 或固定值取得失敗降級數（例如 1），不再呼叫 `L1EnchantProtectScroll.get(...)`。

### 4.4 L1SkillTimer.java

- **L1ExtraPotion / L1FloraPotion**：不再使用 executor 的 `getEffect()`。
- 改為依 **skillId**（與固定 itemId 對應）用 **ItemTable.getTemplate(itemId)** 取得 `L1EtcItem`，以 `getAddHp()`、`getAddStr()`、… 等做「負值」還原（與登入時加的能力對稱）。  
  即：登入時用 DB 的 add_* 加、結束時用同一組數值減。

### 4.5 C_LoginToServer.java

- **L1ExtraPotion / L1FloraPotion**：改為用 **ItemTable.getTemplate(itemId)** 取 `L1EtcItem`，用 `getAddHp()`、`getAddStr()`、… 等套用能力與再生，並 setSkillEffect；與 L1SkillTimer 還原邏輯對稱。

### 4.6 C_NpcAction.java

- 刪除 `import jp.l1j.server.model.item.executor.L1BeginnerItem`（若存在）。
- 確認僅使用 `jp.l1j.server.templates.L1BeginnerItem`（DB），無需其他改動。

### 4.7 GameServer.java

- 確認 **沒有任何** 呼叫 `L1BlessOfEva.load()`、`L1HealingPotion.load()` 等；若仍有殘留，一律移除。

### 4.8 不再使用的 executor 類（可選）

- 以下類若改為完全 DB 驅動後不再被引用，可考慮刪除或標記廢棄，以利後續維護：  
  L1BlessOfEva、L1HealingPotion、L1CurePotion、L1GreenPotion、L1BravePotion、L1ThirdSpeedPotion、L1MagicEye、L1MagicPotion、L1BluePotion、L1WisdomPotion、L1FloraPotion、L1ExtraPotion、L1PolyPotion、L1SpellIcon、L1SpellItem、L1TreasureBox、L1ShowMessage、L1Roulette、L1TeleportAmulet、L1SpawnWand、L1Furniture、L1Material、L1MaterialChoice、L1PolyScroll、L1PolyWand、L1Elixir、L1FireCracker、L1SpeedUpClock、L1UnknownMaliceWeapon、L1EnchantProtectScroll、L1EnchantBonus（若改為 EnchantBonusTable 則可保留名稱改為從 DB 讀）。  
  **建議**：先完成上述呼叫端改動並通過編譯/測試後，再批次刪除或重構這些類，避免一次改動過大。

---

## 五、DB 與表結構（若需要擴充）

- **etc_items**：已具備 `add_hp`、`add_mp`、`add_hpr`、`add_mpr`、`add_mr`、`add_str`、`add_dex`、`add_con`、`add_int`、`add_wis`、`add_sp`、`add_hit`、`add_dmg` 等，足以驅動多數藥水與祝福類道具。
- **enchant_bonus**（可選）：若保留防具/武器「每強化等級加成」，可新增表 (item_id, enchant_level, ac, str, dex, con, int, wis, cha, hp, mp, hpr, mpr, mr, …)，由 ItemTable 或 EnchantBonusTable 載入；L1ItemInstance / L1EnchantScroll 改為查此表。
- **enchant_protect**（可選）：(protect_item_id, target_item_id 或 use_type, down_level)；無則保護捲失敗固定降 1。
- **treasure_box / spell_icon 等**：若未來要還原寶箱、法術圖示等，再補對應表與讀取邏輯。

---

## 六、建議執行順序

1. **C_NpcAction**：修正 L1BeginnerItem import（約 1 處）。
2. **C_UseItem**：  
   - 先處理 **use_type 59、60、65、66、67、68、69、78**（純藥水/祝福類），改為只用 L1EtcItem 的 add_*；  
   - 再處理其餘 use_type（30、72、73、74、75、77 等），改為 DB 或暫時「無法使用」。
3. **L1ItemInstance**：L1EnchantBonus 改為 DB 或 null/0。
4. **L1EnchantScroll**：L1EnchantBonus / L1EnchantProtectScroll 改為 DB 或固定值。
5. **L1SkillTimer**：Extra/Flora 還原改為用 ItemTable + L1EtcItem 的 add_* 負值。
6. **C_LoginToServer**：Extra/Flora 登入恢復改為 ItemTable + L1EtcItem。
7. 確認 **GameServer** 無任何 executor load。
8. 編譯、啟動、測試常用道具與登入/buff 結束。
9. （可選）刪除或重構不再使用的 executor 類。

---

## 七、風險與注意

- **數值一致**：登入恢復與技能結束還原必須用同一套數值（皆來自 DB），否則會出現能力漂移。
- **use_type 與 effect_id**：ItemTable 中 `use_type` 的解析與 `effect_id` 的對應需與現有封包/客戶端一致，改動時避免改動協議。
- **保留變數命名**：依專案規範，不重構 `use_type`、`readD`、`writeC` 等與封包對齊的命名。

若同意此方案，可依上述順序在 jp 目錄內逐步修改並測試；需要我先從「C_NpcAction + use_type 59/66 的 C_UseItem + L1ItemInstance 的 L1EnchantBonus」這幾塊開始具體貼出修改片段，可指定檔案與 use_type。
