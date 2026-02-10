# JP 服務器協議對齊文檔

## 概述

本文檔記錄了將 `game2` 客戶端從 `linserver182` 協議遷移到 `jp` (lineage-jp) 服務器協議的完整修改記錄。

**開發原則**：一切以服務器為準，客戶端必須完全對齊服務器協議，不修改服務器代碼。

---

## 一、登錄流程封包對齊

### 1.1 握手封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_SERVERVERSION` | 0 / 222 | **151** | 服務器版本握手 |
| `C_OPCODE_CLIENTVERSION` | 222 | **127** | 客戶端版本驗證 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - 更新 `case 151` 處理
- `Client/Boot.cs` - 更新握手完成判斷邏輯

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_ServerVersion.java`）：
```
writeC(151)          // Opcode
writeC(0x00)         // Padding
writeC(0x01)         // 服務器編號
writeD(SERVER_VERSION)   // 服務器版本
writeD(CACHE_VERSION)    // 緩存版本
writeD(AUTH_VERSION)    // 認證版本
writeD(NPC_VERSION)      // NPC 版本
writeD(0x0)              // 服務器啟動時間
writeC(0x00)            // 未知
writeC(0x00)            // 未知
writeC(CLIENT_LANGUAGE) // 客戶端語言
writeD(SERVER_TYPE)     // 服務器類型
writeD(UPTIME)          // 運行時間
writeH(0x01)            // 標誌
```

### 1.2 登錄封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_LOGINPACKET` | 1 | **57** | 登錄請求 |
| `S_OPCODE_LOGINRESULT` | 2 | **51** | 登錄結果 |

**修改文件**：
- `Client/Network/C_LoginPacket.cs` - Opcode: 1 → 57
- `Client/Network/PacketHandler.cs` - `case 51` 處理登錄結果

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_LoginResult.java`）：
```
writeC(51)           // Opcode
writeD(0)            // Padding 1
writeD(0)            // Padding 2
writeD(0)            // Padding 3
```

### 1.3 角色列表封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_CHARAMOUNT` | 3 | **126** | 角色數量 |
| `S_OPCODE_CHARLIST` | 4 | **184** | 角色列表 |
| `S_OPCODE_LOGINTOGAME` | - | **131** | 進入遊戲確認 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - 更新所有角色相關封包處理

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_CharAmount.java`）：
```
writeC(126)          // Opcode
writeC(value)        // 當前角色數
writeC(maxAmount)    // 最大角色數
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_CharPacks.java`）：
```
writeC(184)          // Opcode
writeS(name)         // 角色名
writeS(title)        // 稱號
writeD(objId)        // 對象 ID
writeC(type)         // 職業類型
writeC(sex)          // 性別
writeC(lawful)       // 正義值
writeH(heading)      // 朝向
writeD(x)            // X 坐標
writeD(y)            // Y 坐標
writeS(mapname)      // 地圖名
writeD(exp)          // 經驗值
writeH(level)       // 等級
writeH(str)          // 力量
writeH(dex)          // 敏捷
writeH(con)          // 體質
writeH(wis)          // 智慧
writeH(cha)          // 魅力
writeH(intel)        // 智力
writeH(hp)           // 當前 HP
writeH(mp)           // 當前 MP
writeH(ac)           // 防禦力
writeH(mr)           // 魔法防禦
writeD(birthday)     // 生日（新增）
writeC(accessLevel)  // 訪問等級（新增）
writeD(code)         // 代碼（新增）
```

### 1.4 進入遊戲封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_LOGINTOSERVER` | 5 | **131** | 進入遊戲請求 |
| `S_OPCODE_CHARPACK` | 11 | **3** | 角色對象封包 |
| `S_OPCODE_OWNCHARSTATUS` | 12 | **145** | 角色狀態 |

**修改文件**：
- `Client/Network/C_EnterWorldPacket.cs` - Opcode: 5 → 131
- `Client/Network/PacketHandler.cs` - 更新角色對象和狀態封包處理

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_OwnCharPack.java`）：
- 字段順序和數量與 linserver182 不同，需完全對齊服務器實現

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_OwnCharStatus.java`）：
```
writeC(145)          // Opcode
writeD(pc.getId())   // 角色 ID（新增）
writeH(currentHp)    // 當前 HP
writeH(maxHp)        // 最大 HP
writeH(currentMp)    // 當前 MP
writeH(maxMp)        // 最大 MP
writeH(ac)           // 防禦力
writeH(mr)           // 魔法防禦
writeH(str)          // 力量
writeH(dex)          // 敏捷
writeH(con)          // 體質
writeH(wis)          // 智慧
writeH(cha)          // 魅力
writeH(intel)        // 智力
writeD(exp)          // 經驗值
writeH(level)        // 等級
writeD(monsterKill)  // 擊殺數（新增）
```

---

## 二、世界對象封包對齊

### 2.1 移動封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_MOVECHAR` | 10 | **95** | 移動請求 |
| `S_OPCODE_MOVEOBJECT` | 18 | **122** | 移動響應 |

**修改文件**：
- `Client/Network/C_MoveCharPacket.cs` - Opcode: 10 → 95
- `Client/Network/PacketHandler.cs` - `case 122` 處理移動

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_MoveCharPacket.java`）：
```
writeC(122)          // Opcode
writeD(objId)        // 對象 ID
writeH(x)            // X 坐標
writeH(y)            // Y 坐標
writeC(heading)      // 朝向
```

### 2.2 攻擊封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_ATTACK` | 23 | **68** | 攻擊請求 |
| `S_OPCODE_ATTACKPACKET` | 35 | **142** | 攻擊響應 |

**修改文件**：
- `Client/Network/C_AttackPacket.cs` - Opcode: 23 → 68
- `Client/Network/PacketHandler.cs` - `case 142` 處理攻擊

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_AttackPacket.java`）：
```
writeC(142)          // Opcode
writeD(attackerId)    // 攻擊者 ID
writeD(targetId)     // 目標 ID
writeC(damage)       // 傷害值
writeC(actionId)     // 動作 ID
```

### 2.3 魔法封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_USESKILL` | 20 | **115** | 使用技能 |
| `S_OPCODE_SKILLSOUNDGFX` | 57 | **232** | 魔法攻擊 |

**修改文件**：
- `Client/Network/C_MagicPacket.cs` - Opcode: 20 → 115
- `Client/Network/PacketHandler.cs` - `case 232` 處理魔法攻擊

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_UseAttackSkill.java` 和 `S_RangeSkill.java`）：
```
writeC(232)          // Opcode
writeD(casterId)     // 施法者 ID
writeD(targetId)     // 目標 ID
writeH(skillId)      // 技能 ID
writeC(damage)       // 傷害值
writeC(actionId)     // 動作 ID
```

### 2.4 物品封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_PICKUPITEM` | 11 | **188** | 拾取物品 |
| `S_OPCODE_ADDITEM` | 22 | **63** | 添加物品 |
| `C_OPCODE_USEITEM` | 28 | **44** | 使用物品 |
| `S_OPCODE_INVLIST` | 65 | **180** | 背包列表 |

**修改文件**：
- `Client/Network/C_ItemPickupPacket.cs` - Opcode: 11 → 188
- `Client/Network/PacketHandler.cs` - `case 63` 處理添加物品
- `Client/Game/GameWorld.Inventory.cs` - `UseItem` 方法：Opcode 28 → 44
- `Client/Network/PacketHandler.cs` - `case 180` 處理背包列表

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_AddItem.java`）：
```
writeC(63)           // Opcode
writeD(itemId)        // 物品 ID
writeH(count)         // 數量
writeS(name)          // 物品名
writeC(status)        // 狀態
writeC(identified)    // 是否鑑定
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_InvList.java`）：
```
writeC(180)           // Opcode
writeH(count)         // 物品數量
[for each item:]
  writeD(itemId)      // 物品 ID
  writeC(magicCatalystType)  // 魔法催化劑類型
  writeC(useType)     // 使用類型
  writeH(chargeCount) // 充電次數
  writeH(gfxId)       // 圖形 ID
  writeC(status)       // 狀態
  writeD(count)       // 數量
  writeC(identified)  // 是否鑑定
  writeS(name)        // 物品名
  [status bytes...]   // 狀態字節
```

### 2.5 狀態更新封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_HPUPDATE` | 13 | **42** | HP 更新 |
| `S_OPCODE_MPUPDATE` | 77 | **73** | MP 更新 |
| `S_OPCODE_DELETEOBJECT` | 21 | **185** | 刪除對象 |
| `S_OPCODE_CHANGEHEADING` | 28 | **199** | 改變朝向 |
| `S_OPCODE_OBJECTMODE` | 29 | **113** | 對象模式 |
| `S_OPCODE_GAMETIME` | 33 | **194** | 遊戲時間 |
| `S_OPCODE_MAPID` | 40 | **150** | 地圖 ID |

**修改文件**：
- `Client/Network/PacketHandler.cs` - 更新所有狀態相關封包處理

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_HpUpdate.java`）：
```
writeC(42)           // Opcode
writeH(currentHp)    // 當前 HP
writeH(maxHp)        // 最大 HP
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_MpUpdate.java`）：
```
writeC(73)           // Opcode
writeH(currentMp)    // 當前 MP
writeH(maxMp)        // 最大 MP
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_RemoveObject.java`）：
```
writeC(185)          // Opcode
writeD(objId)       // 對象 ID
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_ChangeHeading.java`）：
```
writeC(199)          // Opcode
writeD(objId)        // 對象 ID
writeC(heading)      // 朝向
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_CharVisualUpdate.java`）：
```
writeC(113)          // Opcode
writeD(objId)        // 對象 ID
writeC(currentWeapon) // 當前武器
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_GameTime.java`）：
```
writeC(194)          // Opcode
writeD(worldTimeSeconds) // 世界時間（秒）
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_MapID.java`）：
```
writeC(150)          // Opcode
writeH(mapId)        // 地圖 ID
writeC(mapAttr)      // 地圖屬性（例如是否水下）
```

---

## 三、UI 系統封包對齊

### 3.1 聊天封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_NORMALCHAT` | 19 | **76** | 普通聊天 |
| `S_OPCODE_GLOBALCHAT` | 15 | **10** | 全局聊天 |
| `S_OPCODE_SERVERMSG` | 16 | **14** | 服務器消息 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - 更新所有聊天相關封包處理

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_ChatPacket.java`）：
```
writeC(76)           // Opcode
writeC(type)          // 聊天類型
writeD(objId)        // 對象 ID
writeS(name: chat)   // 名稱和聊天內容
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_SystemMessage.java`）：
```
writeC(10)           // Opcode
writeC(type)         // 消息類型
writeS(msg)          // 消息內容
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_ServerMessage.java`）：
```
writeC(14)           // Opcode
writeH(type)         // 消息類型
writeC(args.length)  // 參數數量
writeS(arg1)         // 參數 1
...
```

### 3.2 NPC 對話封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_NPCTALK` | 41 | **58** | NPC 對話請求 |
| `C_OPCODE_NPCACTION` | 39 | **59** | NPC 動作請求 |
| `S_OPCODE_SHOWHTML` | 42 | **119** | 顯示 HTML |

**修改文件**：
- `Client/Network/C_NpcPacket.cs` - `MakeTalk`: Opcode 41 → 58, `MakeAction`: Opcode 39 → 59
- `Client/Network/PacketHandler.cs` - `case 119` 處理 HTML 顯示

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_NpcTalkReturn.java`）：
```
writeC(119)          // Opcode
writeD(objId)        // NPC ID
writeS(htmlId)       // HTML ID
writeH(0x01)         // 標誌
writeH(data.length)  // 數據長度
writeS(datum)        // 數據
...
```

### 3.3 商店封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_SHOP` | 40 | **16** | 商店操作 |

**修改文件**：
- `Client/Network/C_ShopPacket.cs` - Opcode: 40 → 16
- `Client/Network/C_WarehousePacket.cs` - Opcode: 40 → 16（倉庫使用相同 opcode）

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/client/C_Shop.java`）：
```
writeC(16)           // Opcode
writeC(type)         // 操作類型（0: 購買, 1: 出售, 2: 倉庫存入, 3: 倉庫取出）
writeD(itemId)       // 物品 ID
writeD(count)        // 數量
```

### 3.4 倉庫封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_WAREHOUSE` | 49 | **250** | 倉庫列表 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - `case 250` 處理倉庫列表

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_ShowRetrieveList.java`）：
```
writeC(250)          // Opcode
writeD(npcId)        // NPC ID
writeH(count)        // 物品數量
writeC(type)         // 類型
[for each item:]
  writeD(item.id)    // 物品 ID
  writeD(item.count) // 數量
  writeS(item.name)  // 物品名
  ...
```

---

## 四、技能系統封包對齊

### 4.1 技能列表封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_ADDSKILL` | 30 | **48** | 添加技能 |
| `S_OPCODE_DELSKILL` | 31 | **18** | 刪除技能 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - `case 48` 和 `case 18` 處理技能

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_AddSkill.java`）：
```
writeC(48)           // Opcode
writeC(header)       // 頭部
[28 bytes levels]    // 技能等級數組
writeC(0) * 5       // Padding
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_DelSkill.java`）：
```
writeC(18)           // Opcode
writeC(header)       // 頭部
[28 bytes levels]    // 技能等級數組
writeD(0)            // Padding 1
writeD(0)            // Padding 2
```

### 4.2 技能購買封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_SKILLBUY` | 73 | **173** | 請求技能列表 |
| `C_OPCODE_SKILLBUYOK` | 74 | **207** | 購買技能確認 |
| `S_OPCODE_SKILLBUY` | 78 | **222** | 技能購買列表 |

**修改文件**：
- `Client/Network/C_SkillBuyPacket.cs` - `MakeRequestList`: Opcode 73 → 173, `MakeBuyOK`: Opcode 74 → 207
- `Client/Network/PacketHandler.cs` - `case 222` 處理技能購買列表

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_SkillBuy.java`）：
```
writeC(222)          // Opcode
writeD(100)          // 固定值
writeH(inCount)      // 技能數量
[for each skill:]
  writeD(k)          // 技能 ID
  writeD(price)      // 價格
  ...
```

### 4.3 技能效果封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_SKILLHASTE` | 41 | **149** | 加速技能 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - `case 149` 處理加速技能

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_SkillHaste.java`）：
```
writeC(149)          // Opcode
writeD(objId)        // 對象 ID
writeC(type)         // 類型
writeH(time)         // 持續時間
```

---

## 五、Buff 系統封包對齊

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_INVIS` | 52 | **57** | 隱身術 |
| `S_OPCODE_SKILLBRAVE` | 98 | **200** | 勇敢藥水 |
| `S_OPCODE_CURSEBLIND` | 10 | **238** | 失明詛咒 |
| `S_OPCODE_POISON` | 50 | **93** | 中毒狀態 |
| `S_OPCODE_SKILLICONSHIELD` | 109 | **69** | 防禦盾牌 |
| `S_OPCODE_STRUP` | 107 | **120** | 力量提升 |
| `S_OPCODE_DEXUP` | 108 | **28** | 敏捷提升 |
| `S_OPCODE_OWNCHARATTRDEF` | - | **15** | 角色屬性防禦（新增） |

**修改文件**：
- `Client/Network/PacketHandler.cs` - 更新所有 Buff 相關封包處理

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Invis.java`）：
```
writeC(57)           // Opcode
writeD(objId)        // 對象 ID
writeH(ck)           // 是否隱身（1: 是, 0: 否）
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_SkillBrave.java`）：
```
writeC(200)          // Opcode
writeD(objId)        // 對象 ID
writeC(type)         // 類型
writeH(time)         // 持續時間
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_CurseBlind.java`）：
```
writeC(238)          // Opcode
writeH(type)         // 失明類型
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Poison.java`）：
```
writeC(93)           // Opcode
writeD(objId)        // 對象 ID
writeC(type1)        // 類型 1（1: 綠毒, 0: 無）
writeC(type2)        // 類型 2（1: 灰毒, 0: 無）
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_SkillIconShield.java`）：
```
writeC(69)           // Opcode
writeH(time)         // 持續時間
writeC(type)         // 類型
writeD(0)            // Padding
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Strup.java`）：
```
writeC(120)          // Opcode
writeH(time)         // 持續時間
writeC(str)          // 力量值
writeC(weight)       // 重量
writeC(type)         // 類型
writeD(0)            // Padding
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Dexup.java`）：
```
writeC(28)           // Opcode
writeH(time)         // 持續時間
writeC(dex)          // 敏捷值
writeC(type)         // 類型
writeD(0)            // Padding
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_OwnCharAttrDef.java`）：
```
writeC(15)           // Opcode
writeC(ac)           // 防禦力
writeC(fire)         // 火屬性防禦
writeC(water)        // 水屬性防禦
writeC(wind)         // 風屬性防禦
writeC(earth)        // 土屬性防禦
```

---

## 六、角色系統封包對齊

### 6.1 角色創建封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_NEWCHAR` | 112 | **253** | 創建角色 |

**修改文件**：
- `Client/Network/C_CreateCharPacket.cs` - Opcode: 112 → 253

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/client/C_CreateChar.java`）：
```
writeC(253)          // Opcode
writeS(name)          // 角色名
writeC(type)          // 職業類型
writeC(sex)           // 性別
writeC(str)           // 力量
writeC(dex)           // 敏捷
writeC(con)           // 體質
writeC(wis)           // 智慧
writeC(cha)           // 魅力
writeC(intel)         // 智力
```

### 6.2 角色刪除封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `C_OPCODE_DELETECHAR` | 7 | **10** | 刪除角色 |

**修改文件**：
- `Client/Network/C_CharacterDeletePacket.cs` - Opcode: 7 → 10

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/client/C_DeleteChar.java`）：
```
writeC(10)           // Opcode
writeS(charName)      // 角色名
```

---

## 七、其他系統封包對齊

### 7.1 交易系統封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_TRADE` | - | **77** | 交易窗口 |
| `S_OPCODE_TRADEADDITEM` | - | **86** | 交易添加物品 |
| `S_OPCODE_TRADESTATUS` | - | **239** | 交易狀態 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - 添加交易相關封包處理

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Trade.java`）：
```
writeC(77)           // Opcode
writeS(name)          // 交易對象名稱
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_TradeAddItem.java`）：
```
writeC(86)           // Opcode
writeC(type)         // 類型（0: 上段, 1: 下段）
writeH(gfxId)        // 圖形 ID
writeS(name)         // 物品名
writeC(status)      // 狀態
writeC(0)           // Padding
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_TradeStatus.java`）：
```
writeC(239)          // Opcode
writeC(type)         // 類型（0: 完成, 1: 取消）
```

### 7.2 血盟系統封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_CLAN` | - | **29** | 血盟更新 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - `case 29` 處理血盟更新

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Clan.java`）：
```
writeC(29)           // Opcode
writeC(type)         // 類型
writeS(clanName)     // 血盟名稱
...
```

### 7.3 音效和特效封包

| 封包 | linserver182 | jp | 說明 |
|------|-------------|-----|------|
| `S_OPCODE_SOUND` | 74 | **84** | 播放音效 |
| `S_OPCODE_POLY` | 39 | **164** | 變身效果 |

**修改文件**：
- `Client/Network/PacketHandler.cs` - 更新音效和變身封包處理

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Sound.java`）：
```
writeC(84)           // Opcode
writeC(0)            // 重複標誌
writeH(sound)        // 音效 ID
```

**封包結構**（對齊 `jp/src/jp/l1j/server/packets/server/S_Poly.java`）：
```
writeC(164)          // Opcode
writeD(objId)        // 對象 ID
writeH(polyId)       // 變身 ID
```

---

## 八、加密/解密邏輯對齊

### 8.1 加密算法變更

**linserver182** 使用基於流量累積量 (TotalSize) 的滾動 XOR 加密算法。

**jp** 使用基於 key 的加密算法（`Cipher.java`），key 在握手時由服務器發送。

### 8.2 握手流程

**jp 服務器握手流程**：
1. 服務器發送 `S_OPCODE_INITPACKET (161)`，包含：
   - Opcode: 161
   - 4 字節隨機 key（Little Endian）
   - 11 字節 FIRST_PACKET 數據
2. 客戶端提取 key 並初始化 `JpCipher`
3. 之後所有封包使用 `JpCipher` 加密/解密

**修改文件**：
- `Client/Network/JpCipher.cs` - 新建，對齊 `jp/src/jp/l1j/server/utils/Cipher.java`
- `Client/Network/GodotTcpSession.cs` - 更新加密/解密邏輯

### 8.3 JpCipher 實現

**對齊 `jp/src/jp/l1j/server/utils/Cipher.java`**：

```csharp
public class JpCipher
{
    // 靜態常量
    private const int _1 = 0x9c30d539;
    private const int _2 = 0x930fd7e2;
    private const int _3 = 0x7c72e993;
    private const int _4 = 0x287effc3;
    
    // 編碼/解碼鑰匙（各 8 字節）
    private readonly byte[] eb = new byte[8];
    private readonly byte[] db = new byte[8];
    private readonly byte[] tb = new byte[4];
    
    // 初始化：使用服務器發送的 key
    public JpCipher(int key) { ... }
    
    // 加密/解密方法
    public void Encrypt(byte[] data) { ... }
    public void Decrypt(byte[] data) { ... }
}
```

### 8.4 握手包處理

**GodotTcpSession.cs 中的處理邏輯**：
```csharp
// 檢測握手包 S_OPCODE_INITPACKET (161)
if (bodyLen > 0 && body[0] == 161 && !_cipherInitialized)
{
    // 提取加密 key (4 bytes) - Little Endian
    int key = (body[1] & 0xFF) | ((body[2] & 0xFF) << 8) 
            | ((body[3] & 0xFF) << 16) | ((body[4] & 0xFF) << 24);
    _cipher = new JpCipher(key);
    _cipherInitialized = true;
    // 跳過握手包，不發送給 PacketHandler
    continue;
}

// 解密（僅在 Cipher 初始化後）
if (_cipher != null && _cipherInitialized)
{
    _cipher.Decrypt(body);
}
```

---

## 九、修改文件清單

### 9.1 核心網絡文件

1. **Client/Network/PacketHandler.cs**
   - 更新所有服務器封包的 opcode 和解析邏輯
   - 添加新封包處理（交易、血盟等）

2. **Client/Network/GodotTcpSession.cs**
   - 更新加密/解密邏輯：`LineageCryptor` → `JpCipher`
   - 添加握手包處理邏輯

3. **Client/Network/JpCipher.cs**（新建）
   - 實現 jp 服務器的加密算法

### 9.2 客戶端封包文件

1. **Client/Network/C_LoginPacket.cs** - Opcode: 1 → 57
2. **Client/Network/C_EnterWorldPacket.cs** - Opcode: 5 → 131
3. **Client/Network/C_MoveCharPacket.cs** - Opcode: 10 → 95
4. **Client/Network/C_AttackPacket.cs** - Opcode: 23 → 68
5. **Client/Network/C_MagicPacket.cs** - Opcode: 20 → 115
6. **Client/Network/C_ItemPickupPacket.cs** - Opcode: 11 → 188
7. **Client/Network/C_NpcPacket.cs** - Opcode: 41 → 58, 39 → 59
8. **Client/Network/C_ShopPacket.cs** - Opcode: 40 → 16
9. **Client/Network/C_WarehousePacket.cs** - Opcode: 40 → 16
10. **Client/Network/C_SkillBuyPacket.cs** - Opcode: 73 → 173, 74 → 207
11. **Client/Network/C_CharacterDeletePacket.cs** - Opcode: 7 → 10
12. **Client/Network/C_CreateCharPacket.cs** - Opcode: 112 → 253

### 9.3 遊戲邏輯文件

1. **Client/Game/GameWorld.Inventory.cs**
   - `UseItem` 方法：Opcode 28 → 44

2. **Client/Boot.cs**
   - 更新握手完成判斷邏輯

---

## 十、測試檢查清單

### 10.1 登錄流程測試
- [ ] 握手封包正確接收和處理
- [ ] 登錄請求成功發送
- [ ] 登錄結果正確解析
- [ ] 角色列表正確顯示
- [ ] 進入遊戲成功

### 10.2 基本操作測試
- [ ] 角色移動正常
- [ ] 攻擊功能正常
- [ ] 使用技能正常
- [ ] 拾取物品正常
- [ ] 使用物品正常

### 10.3 UI 系統測試
- [ ] 聊天功能正常
- [ ] NPC 對話正常
- [ ] 商店功能正常
- [ ] 倉庫功能正常

### 10.4 技能系統測試
- [ ] 技能列表正確顯示
- [ ] 學習技能正常
- [ ] 使用技能正常
- [ ] 技能購買功能正常

### 10.5 Buff 系統測試
- [ ] 所有 Buff 效果正確顯示
- [ ] Buff 狀態正確更新

### 10.6 加密/解密測試
- [ ] 握手包正確處理
- [ ] 封包加密/解密正常
- [ ] 無封包解析錯誤

---

## 十一、注意事項

### 11.1 協議差異
- **封包長度**：jp 服務器使用 2 字節長度頭（Little Endian）
- **字符串編碼**：使用服務器指定的字符編碼
- **字節序**：注意 Little Endian 和 Big Endian 的差異

### 11.2 加密注意事項
- 握手包（S_OPCODE_INITPACKET）**不加密**，直接處理
- 之後所有封包都使用 `JpCipher` 加密/解密
- 確保 key 提取正確（Little Endian）

### 11.3 開發原則
- **一切以服務器為準**：客戶端必須完全對齊服務器協議
- **不修改服務器**：所有修改都在客戶端完成
- **測試優先**：必須測試通過才能確認修改正確

---

## 十二、參考文檔

### 12.1 服務器源碼位置
- **jp 服務器**：`/Users/airtan/Documents/GitHub/jp/src/jp/l1j/server/`
- **Opcodes 定義**：`jp/src/jp/l1j/server/codes/Opcodes.java`
- **封包實現**：`jp/src/jp/l1j/server/packets/server/` 和 `client/`
- **加密實現**：`jp/src/jp/l1j/server/utils/Cipher.java`

### 12.2 客戶端源碼位置
- **網絡處理**：`game2/Client/Network/`
- **封包處理**：`game2/Client/Network/PacketHandler.cs`
- **封包構建**：`game2/Client/Network/C_*.cs`

---

## 更新記錄

- **2024-XX-XX**：完成登錄流程封包對齊
- **2024-XX-XX**：完成世界對象封包對齊
- **2024-XX-XX**：完成 UI 系統封包對齊
- **2024-XX-XX**：完成技能系統封包對齊
- **2024-XX-XX**：完成 Buff 系統封包對齊
- **2024-XX-XX**：完成角色系統封包對齊
- **2024-XX-XX**：完成加密/解密邏輯對齊
- **2024-XX-XX**：完成其他系統封包對齊

---

**文檔版本**：1.0  
**最後更新**：2024-XX-XX  
**維護者**：開發團隊
