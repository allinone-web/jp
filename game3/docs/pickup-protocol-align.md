# 拾取封包與伺服器對齊說明

## 伺服器（Source of Truth）

### C_ItemPickup.java 讀序
- `super.read(lc, data)` → `_off = 1`（略過 opcode 1 位元組）
- `this.x = readH();`     // 2 位元組 Little Endian
- `this.y = readH();`     // 2 位元組
- `this.inv_id = readD();`// 4 位元組（地面物品的世界 ObjectId）
- `this.count = readD();` // 4 位元組（拾取數量）

### 處理邏輯
- `L1Object temp = pc.getObject(this.inv_id);`
- 若 `temp != null` 且 `!pc.isLock()` 才呼叫 `temp.pickup(pc, x, y, count)`
- 無論是否拾取成功，都會 `pc.SendPacket(new S_CharacterStat(pc))`

### ItemInstance.pickup(cha, x, y, count) 條件
1. `cha.getDistance(x, y, cha.getMap(), 1)` 為 true（玩家與 (x,y) 距離 ≤1 格，且同地圖）
2. `!cha.isInvis() && !cha.isDead()`
3. `count > 0 && count <= 2147483647 && getCount() >= count`
4. 背包未滿（180）、重量可負擔等

若條件 3 不滿足（例如送出的 count 大於地面物品實際數量），伺服器會執行 `toDelete(); toReset();`，不會 insert，物品可能被刪除或重置。

## 客戶端對齊

- **Opcode**: 11（C_OPCODE_ITEMPICKUP）
- **寫序**: opcode(1) + x(2) + y(2) + objectId(4) + count(4)，均 Little Endian
- **座標**: 必須為地面物品的 MapX, MapY（與 S_ObjectAdd 一致）
- **objectId**: 地面物品的 ObjectId（pc.getObject(inv_id) 用此 ID 查找）
- **count**: 1～min(ItemCount, 2147483647)；送出大於實際數量會導致伺服器不 insert 並 toDelete/toReset

## 若仍無進背包時的除錯

1. **客戶端日誌**  
   發送時會輸出 `[Pickup][TX] objectId=... x=... y=... count=...`，請確認與伺服器端收到的值一致。

2. **伺服器建議加 log（C_ItemPickup.read）**  
   - 若 `temp == null`：代表 `pc.getObject(inv_id)` 找不到該 ID（可能不在 objectList、ID 錯誤或時序問題）。
   - 若 `pc.isLock()`：玩家被鎖定（交易、對話等），不會執行 pickup。

3. **伺服器建議加 log（ItemInstance.pickup）**  
   - 若未進入第一個 if：`getDistance(x,y,map,1)` 為 false（距離或地圖不符）。
   - 若進入 else（toDelete/toReset）：count 條件不滿足（例如 count > getCount()）。

4. **伺服器距離檢查改為物品座標**  
   `ItemInstance.pickup` 原以客戶端傳入的 (x,y) 做 `cha.getDistance(x,y,map,1)`，若客戶端與伺服器玩家位置不同步會恆為 false 而不 insert。已改為 `cha.getDistance(getX(), getY(), cha.getMap(), 1)`，以物品實際座標做距離檢查。

5. **全部拾取**  
   客戶端送 `pickCount = target.ItemCount`（S_ObjectAdd 的 Exp），若 `ItemCount <= 0` 或超出 int 則送 1。伺服器要求 `getCount() >= count && count > 0`，送出大於地面實際數量會 toDelete 不 insert。
