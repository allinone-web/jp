# 召喚怪物控制 (Summon Control)

## 封包

- **Opcode**: 39 (C_NpcTalkAction)
- **格式**: `writeC(39)`, `writeD(obj_id)` 召喚物 ObjectId, `writeS(text1)` 指令字串, `writeS(text2)` 空字串
- **客戶端**: `GameWorld.SendSummonCommand(summonObjectId, cmd)` 或 `C_NpcPacket.MakeAction(summonObjectId, cmd)`

## 伺服器指令 (SummonSystem.Commander)

| text1 (小寫) | 說明 |
|--------------|------|
| aggressive   | 主動攻擊模式 |
| defensive    | 防禦模式 |
| stay         | 停留 |
| extend       | 延長召喚時間 |
| alert        | 警戒 |
| dismiss      | 解散召喚物 |
| attackchr    | (僅 Pet 項圈寵) 攻擊指定角色，會觸發 S_ObjectPet |
| getitem      | (僅 Pet) 撿取物品 |
| changename   | (僅 Pet) 改名，會觸發 S_ServerMessageYesNo(325) |

## 使用方式

1. 召喚成功後，召喚物會出現在世界中，其 `ObjectId` 可由 Spawn 封包或點選取得。
2. 控制：呼叫 `GameWorld.SendSummonCommand(召喚物ObjectId, "aggressive")` 等。
3. UI：可做「點選召喚物後出現指令按鈕」或快捷鍵對應上述指令；目前客戶端僅提供發包 API，未內建指令 UI。
