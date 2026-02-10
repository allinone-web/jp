# JP 協議 Opcode 未完成／未對齊清單

依據 **jp** (`Opcodes.java` + `PacketHandler.java` + `packets/server`) 與 **game2** (`PacketHandler.cs` + `C_*.cs`) 比對結果。  
**標準**：/jp 為準，客戶端需與 jp 對齊。

---

## 一、服務器→客戶端（S_）未完成或需修正

### 1. Opcode 錯誤（客戶端用錯數字，需改為 jp 值）

| 說明 | jp Opcode | 客戶端現狀 | 建議 |
|------|-----------|------------|------|
| **地面特效位置** S_EffectLocation | **112** | case **83** | 改為 `case 112`，解析結構對齊 `S_EffectLocation.java`（writeH(x), writeH(y), writeH(gfxId), writeC(0)）。83 若為他用途可保留或刪除。 |
| **商店購買列表** S_ShopBuyList（NPC 賣給玩家） | **170** (S_OPCODE_SHOWSHOPSELLLIST) | case **43** | 改為 `case 170`，解析對齊 `S_ShopBuyList.java`。 |
| **商店販賣列表** S_ShopSellList（玩家賣給 NPC） | **254** (S_OPCODE_SHOWSHOPBUYLIST) | case **44** | 改為 `case 254`，解析對齊 `S_ShopSellList.java`。 |
| **夜視/能力** S_Ability | **116** | case **38** | 改為 `case 116`，或確認 jp 是否仍發 38。 |
| **紫名** S_PinkName | **252** | case **106** | 改為 `case 252`，對齊 `S_PinkName.java`。 |

### 2. 服務器會發送、客戶端尚未處理的 S_ Opcode

| jp Opcode | 常數名 | 說明 | 客戶端 |
|-----------|--------|------|--------|
| **1** | S_OPCODE_MAIL | 郵件 | 未處理 |
| **4** | S_OPCODE_TELEPORT | 傳送（NPC 傳送等） | 可能僅部分由 MapChanged 涵蓋，需確認 |
| **11** | S_OPCODE_BOOKMARKS | 記憶座標列表 | 未處理 |
| **12** | S_OPCODE_BLESSOFEVA | 水底呼吸效果圖示 | 未處理 |
| **16** | S_OPCODE_RANGESKILLS | 範圍魔法／遠程技能（S_RangeSkill） | ✅ 已處理（解析並發 EffectAtLocation） |
| **24** | S_OPCODE_HOUSELIST | 血盟小屋列表 | 未處理 |
| **31** | S_OPCODE_LIQUOR | 海底波紋 | 未處理 |
| **35** | S_OPCODE_ATTRIBUTE | 物件屬性（門開關等） | 未處理（客戶端 35 在 PacketBox 子類型） |
| **50** | S_OPCODE_EMBLEM | 角色盟徽 | 未處理 |
| **56** | S_OPCODE_BOARDREAD | 佈告欄閱讀 | 未處理 |
| **59** | S_OPCODE_BLUEMESSAGE | 藍色訊息 | 未處理 |
| **64** | S_OPCODE_BOARD | 佈告欄列表 | 未處理 |
| **66** | S_OPCODE_CASTLEMASTER | 角色皇冠 | 未處理 |
| **72** | S_OPCODE_TAXRATE | 稅收設定 | 未處理 |
| **90** | S_OPCODE_REDMESSAGE | 紅色訊息（登入等） | 未處理 |
| **110** | S_OPCODE_TRUETARGET | 精準目標魔法動畫 | ✅ 已處理（發 SystemMessage） |
| **116** | S_OPCODE_ABILITY | 夜視等能力 | 見上，opcode 需改為 116 |
| **133** | S_OPCODE_NPCSHOUT | NPC 大喊/一般 | 未處理 |
| **135** | S_OPCODE_TELEPORTLOCK | 傳送鎖定 | 未處理 |
| **144** | S_OPCODE_ITEMCOLOR | 物品祝福/詛咒狀態 | 未處理 |
| **155** | S_OPCODE_YES_NO | Yes/No 選項 | ✅ 已處理（YesNoRequestReceived 信號，UI 可回傳 C_Attr 61） |
| **165** | S_OPCODE_PARALYSIS | 麻痺/癱瘓 | 未處理 |
| **190** | S_OPCODE_PRIVATESHOPLIST | 個人商店列表 | 未處理 |
| **192** | S_OPCODE_PLEDGE_RECOMMENDATION | 血盟推薦 | ✅ 已處理（消耗位元組避免粘包） |
| **203** | S_OPCODE_DEPOSIT | 存入資金（城堡等） | 未處理 |
| **208** | S_OPCODE_SELECTLIST | 損壞武器/取出寵物列表 | 未處理 |
| **216** | S_OPCODE_OWNCHARSTATUS2 | 角色狀態(2) | 未處理 |
| **224** | S_OPCODE_DRAWAL | 領出資金 | 未處理 |
| **253** | S_OPCODE_INPUTAMOUNT | 數量輸入（拍賣/製作等） | 未處理 |
| **255** | S_OPCODE_WHISPERCHAT | 密語 | ✅ 已處理（ObjectChat type 16） |
| **161** | S_OPCODE_INITPACKET | 初始化 | 未處理 |
| **68** | S_OPCODE_SKILLMAKE | 魔法製作（材料） | 未處理 |

### 3. 客戶端已處理、需確認結構與 jp 一致

- **33**：同時對應 S_OPCODE_CHARRESET 與 S_OPCODE_PETCTRL，需依封包內容區分。
- **40**：S_OPCODE_PACKETBOX / ACTIVESPELLS / SKILLICONGFX 共用，子類型解析須與 jp 一致。
- **43/44**：見上，應改為 170/254 並對齊商店封包結構。

---

## 二、客戶端→服務器（C_）未完成或需確認

### 1. 服務器有處理、客戶端尚未發送的 C_ Opcode（常用）

| jp Opcode | 常數名 | 說明 | 客戶端 |
|-----------|--------|------|--------|
| **41** | C_OPCODE_SENDLOCATION | 傳送位置 | 未發送 |
| **101** | C_OPCODE_EXCLUDE | 拒絕名單 | 未發送 |
| **104** | C_OPCODE_QUITGAME | 離開遊戲（可選） | 未發送 |
| **109** | C_OPCODE_AMOUNT | 數量確認 | ✅ C_AmountPacket.Make |
| **122** | C_OPCODE_CHATWHISPER | 密語 | ✅ C_ChatWhisperPacket.Make |
| **134** | C_OPCODE_BOOKMARK | 增加記憶座標 | 未發送 |
| **182** | C_OPCODE_KEEPALIVE | KeepAlive | ✅ C_KeepAlivePacket.Make |
| **218** | C_OPCODE_RETURNTOLOGIN | 返回登入畫面 | ✅ C_ReturnToLoginPacket.Make |
| **223** | C_OPCODE_BOOKMARKDELETE | 刪除記憶座標 | 未發送 |
| **54** | C_OPCODE_DROPITEM | 丟棄物品 | ✅ C_DropItemPacket.Make |
| **61** | C_OPCODE_ATTR | 點選項目結果（對話/選項） | ✅ C_AttrPacket.MakeWithAttr（Yes/No 等）；與 67 不同，見下 |

### 2. 客戶端已發送的 C_ Opcode（與 jp 對照）

| 客戶端封包 | 發送 Opcode | jp 常數 | 備註 |
|------------|-------------|---------|------|
| C_LoginPacket | 57 | C_OPCODE_LOGINPACKET | 一致 |
| C_EnterWorldPacket | 131 | C_OPCODE_LOGINTOSERVER | 一致 |
| C_CreateCharPacket | 253 | C_OPCODE_NEWCHAR | 一致 |
| C_CharacterDeletePacket | 10 | C_OPCODE_DELETECHAR | 一致 |
| C_AttackPacket | 68 | C_OPCODE_ATTACK | 一致 |
| C_ItemPickupPacket | 188 | C_OPCODE_PICKUPITEM | 一致 |
| C_MagicPacket | 115 | C_OPCODE_USESKILL | 一致 |
| C_NpcPacket (talk) | 58 | C_OPCODE_NPCTALK | 一致 |
| C_NpcPacket (action) | 37 | C_OPCODE_NPCACTION | 一致 |
| C_ShopPacket | 16 | C_OPCODE_SHOP | 一致 |
| C_MoveCharPacket | 95 | C_OPCODE_MOVECHAR | 一致 |
| C_SkillBuyPacket (OK) | 207 | C_OPCODE_SKILLBUYOK | 一致 |
| C_SkillBuyPacket (buy) | 173 | C_OPCODE_SKILLBUY | 一致 |
| C_DeleteInventoryItemPacket | 209 | C_OPCODE_DELETEINVENTORYITEM | 一致 |
| C_GiveItemPacket | 244 | C_OPCODE_GIVEITEM | 一致 |
| C_WarehousePacket | 16 | C_OPCODE_SHOP (type 2/3) | 一致 |
| C_StatDicePacket | 67 | jp PacketHandler 無 67 | 見下「C_ 67 釐清」 |
| C_AmountPacket | 109 | C_OPCODE_AMOUNT | 一致 |
| C_ChatWhisperPacket | 122 | C_OPCODE_CHATWHISPER | 一致 |
| C_KeepAlivePacket | 182 | C_OPCODE_KEEPALIVE | 一致 |
| C_ReturnToLoginPacket | 218 | C_OPCODE_RETURNTOLOGIN | 一致 |
| C_DropItemPacket | 54 | C_OPCODE_DROPITEM | 一致 |
| C_AttrPacket | 61 | C_OPCODE_ATTR | 一致（對話/Yes-No 結果，與 67 不同） |

### C_ 67 釐清（與 61 對照）

- **jp**：`C_OPCODE_ATTR = 61`，用於「點選項目結果」（NPC 對話連結、Yes/No、傳送選項等），由 `C_Attr.java` 處理。
- **jp**：`PacketHandler` 中**沒有** opcode **67** 的 case；即 jp 服務器目前不處理 67。
- **game2**：`C_StatDicePacket` 使用 **67** 發送「職業 type」以取得/初始化角色骰子屬性，與 61（對話選項）**語義不同**，故**不**將 67 改為 61。
- **結論**：**保留** `C_StatDicePacket` 為 opcode **67**。若 jp 端未來支援 67 或將創角/屬性流程改為其他 opcode，再對齊；目前若伺服器不處理 67，該封包會被忽略，不影響已驗證的創角流程（253 等）。

---

## 三、優先建議（已落實部分）

1. ~~**立即修正**~~：S_ 112、170/254、252、116 已於前次修正。
2. ~~**補齊常用 S_**~~：16、110、155、192、255 已於本次補齊。
3. ~~**補齊常用 C_**~~：109、122、182、218、54、61 已於本次補齊（封包類已新增，需由 UI/流程呼叫發送）。
4. **C_ 67**：已釐清，保留 67，不改為 61。

---

*清單依據當前 jp 與 game2 代碼比對，後續若 jp 增刪封包請再對照更新。*
