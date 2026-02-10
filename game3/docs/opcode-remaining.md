# 尚未完成或僅部分實作的 Opcode 對照（jp 為準）

依 jp `Opcodes.java` 與 game2 `PacketHandler.cs` / `Client/Network/*.cs` 比對。

---

## 一、S_（伺服器 → 客戶端）— 未處理或僅消耗位元組

| Opcode | jp 常數 | 說明 | 客戶端現狀 |
|--------|---------|------|------------|
| 1 | S_OPCODE_MAIL | 郵件封包 | 未處理 |
| 4 | S_OPCODE_TELEPORT | 傳送（NPC 傳送反手） | 已解析並發信號 TeleportReceived |
| 12 | S_OPCODE_BLESSOFEVA | 效果圖示（水底呼吸） | 未處理 |
| 50 | S_OPCODE_EMBLEM | 角色盟徽 | 未處理 |
| 53 | S_OPCODE_LIGHT | 物件亮度 | 未處理 |
| 56 | S_OPCODE_BOARDREAD | 佈告欄單則閱讀 | 未處理 |
| 59 | S_OPCODE_BLUEMESSAGE | 藍色訊息（String-h.tbl） | 未處理 |
| 64 | S_OPCODE_BOARD | 佈告欄訊息列表 | 未處理 |
| 66 | S_OPCODE_CASTLEMASTER | 角色皇冠（城主） | 可能與 71 重疊，需對齊 |
| 68 | S_OPCODE_SKILLMAKE | 魔法製作（材料製作） | 未處理 |
| 72 | S_OPCODE_TAXRATE | 稅收設定封包 | 未處理 |
| 90 | S_OPCODE_REDMESSAGE | 畫面紅字（Account ? has just logged in） | 未處理 |
| 135 | S_OPCODE_TELEPORTLOCK | 傳送鎖定 / 人物回朔檢測 | 未處理 |
| 177 | S_OPCODE_SELECTTARGET | 選擇一個目標 | 未處理 |
| 190 | S_OPCODE_PRIVATESHOPLIST | 個人商店（購買列表） | 未處理 |
| 203 | S_OPCODE_DEPOSIT | 存入資金（城堡寶庫） | 未處理 |
| 222 | S_OPCODE_SKILLBUY | 魔法購買（金幣）列表 | 未處理 |
| 224 | S_OPCODE_DRAWAL | 領出城堡寶庫金幣 | 未處理 |
| 253 | S_OPCODE_INPUTAMOUNT | 選取數量（金幣/物品）彈窗 | 已解析，開 AmountInputWindow 並以 C_109 回傳 |
| 29 | S_OPCODE_CLAN | 血盟數據更新 | 已消耗位元組避免粘包 |
| 31 | S_OPCODE_LIQUOR | 海底波紋 | 未處理 |
| 43 | S_OPCODE_IDENTIFYDESC | 鑑定描述（物品資訊） | 已解析並發信號 IdentifyDescReceived，刷新視窗 |
| 81 | S_OPCODE_CHANGENAME | 改變物件名稱 | 已解析並發信號 ObjectNameChanged，更新實體顯示名 |

**已解析但 UI/邏輯未接完：**

- **144** S_ITEMCOLOR：已發信號 `ItemColorReceived`，GameWorld.Inventory 更新 item.Bless，ItemTooltip 依 Bless 顯示名稱顏色。
- **165** S_PARALYSIS：僅消耗位元組，可接麻痺/睡魔等狀態或特效。
- **208** S_SELECTLIST：已發信號 `SelectListReceived`，需 UI 顯示損壞武器/寵物列表並發 C_238。
- **216** S_OWNCHARSTATUS2：已發信號 `OwnCharStatus2Received`，需更新角色面板六圍/負重。

---

## 二、C_（客戶端 → 伺服器）— 尚無封包類或發送路徑

| Opcode | jp 常數 | 說明 | 客戶端現狀 |
|--------|---------|------|------------|
| 44 | C_OPCODE_USEITEM | 使用物品 | 已實作：GameWorld.Inventory.UseItem/UsePolymorphScroll/UseIdentifyScroll 發送 opcode 44，對齊 jp C_UseItem |
| 65 | C_OPCODE_CHANGEHEADING | 改變角色面向 | 無獨立封包（可能合併在移動包） |
| 106 | C_OPCODE_FIX_WEAPON_LIST | 要求維修物品清單 | 無；收到 S_208 後需發此包請求列表 |
| 115 | C_OPCODE_USESKILL | 使用技能 | 已實作：C_MagicPacket opcode 115 |
| 209 | C_OPCODE_DELETEINVENTORYITEM | 刪除物品 | 已實作：C_DeleteInventoryItemPacket opcode 209 |
| 238 | C_OPCODE_SELECTLIST | 選項結果（維修/取出寵物等） | 無；選 S_208 列表後需發此包 |

其餘 C_（如 53 CommonClick、71 Restart、190 Chat、218 ReturnToLogin、122 Whisper、182 KeepAlive、54 Drop、41 SendLocation、101 Exclude、104 Quit、134 Bookmark、223 BookmarkDelete、109 Amount、61 Attr 等）客戶端已有對應發送或封包類。

---

## 三、建議優先順序（依遊玩必要度）

1. **S_253 (INPUTAMOUNT) + C_109 (AMOUNT)**  
   數量輸入彈窗與回傳，影響倉庫/商店/維修等數量操作。

2. **S_208 (SELECTLIST) + C_106 (FIX_WEAPON_LIST) + C_238 (SELECTLIST)**  
   損壞武器/寵物列表與維修/取出流程。

3. **C_44 (USEITEM)**  
   使用物品（若目前未經其他 opcode 實作）。

4. **S_222 (SKILLBUY)**  
   技能商店購買列表（若與現有技能視窗不同）。

5. 其餘為血盟、佈告欄、郵件、個人商店、城堡/稅收等，可依需求再補。

---

*生成自 jp Opcodes.java 與 game2 PacketHandler / Client/Network 比對。*
