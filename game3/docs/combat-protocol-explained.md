# 戰鬥協議通俗講解：一回合裡客戶端與伺服器在做什麼

本文用**通俗話 + 具體數字**說明：近戰、弓箭、魔法（單體／群體）時，客戶端該傳什麼、伺服器怎麼回、一回合如何完成「發招 → 結算 → 顯示」。

---

## 一、比喻：一回合戰鬥像「點餐 + 廚房回單」

- **客戶端**：你點餐（我要打誰、用什麼招）。
- **伺服器**：廚房算傷害、扣血、決定命中與否，然後**回單**（誰打了誰、多少傷害、播什麼動作）。
- **客戶端**：收到回單後，播動畫、扣血條、飄字，完成「一回合」的視覺與結算。

也就是說：**誰打中、扣多少血，全是伺服器算的**；客戶端只負責「發請求」和「照回單演」。

---

## 二、客戶端要傳什麼？（三種「點餐」方式）

伺服器用 **Opcode（第一個 byte）** 區分你是要「近戰 / 弓箭 / 魔法」。

| 攻擊類型 | Opcode | 伺服器類別 | 客戶端該傳的資料（對齊伺服器 read 順序） |
|----------|--------|------------|------------------------------------------|
| **近戰** | **23** | C_Attack.java | 目標 ObjectId(4) + 目標格 X(2) + 目標格 Y(2) |
| **弓箭** | **24** | C_AttackBow.java | 目標 ObjectId(4) + 目標格 X(2) + 目標格 Y(2) |
| **魔法** | **20** | C_Magic.java | 技能等級/編號(1+1) + 目標 ObjectId(4)；部分技能多讀 H+D |

### 1. 近戰（Opcode 23）

- **伺服器讀法**：`readD()` 目標 ID → `readH()` 目標 X → `readH()` 目標 Y。  
  然後呼叫 `pc.Attack(target, locx, locy, 1, 0)`，伺服器自己算距離、命中、傷害。
- **客戶端要傳**（例如打怪物 ID=10001，站在格 (33050, 32760)）：
  - `23`（1 byte）
  - `10001`（4 bytes，Int32）
  - `33050`（2 bytes，Short）
  - `32760`（2 bytes，Short）  
  → 共 **9 bytes**。
- **本客戶端**：`GameWorld.Combat.SendAttackPacket(targetId, targetX, targetY)` → `C_AttackPacket.Make(targetId, x, y)`，或直接 `WriteByte(23); WriteInt(tid); WriteUShort(x); WriteUShort(y)`。

### 2. 弓箭（Opcode 24）

- **伺服器讀法**：和近戰一樣，`readD()` 目標 ID、`readH()` X、`readH()` Y。  
  然後 `pc.AttackBow(obj, locx, locy, 1, 66, false)`，伺服器檢查箭矢、距離、算傷害。
- **客戶端要傳**：格式與近戰**完全相同**，只把第一個 byte 改成 **24**。  
  → 共 **9 bytes**（24 + 目標ID + X + Y）。
- **本客戶端**：`SendAttackBowPacket(targetId, targetX, targetY)`，Opcode 24，其餘同近戰。

### 3. 魔法（Opcode 20）— 單體與群體都用同一個「點餐」

- **伺服器讀法**（C_Magic.java）：
  - `lv = readC() + 1`（技能「等級/行」）
  - `no = readC()`（技能「編號/列」）
  - 部分技能（如瞬移）：`readH()` 再 `readD()` 當 id；  
    一般：`id = readD()` → **目標 ObjectId**（單體就一個怪，群體是「主目標」或 0）。
- **伺服器內部**：`pc.getSkill().toMagic(lv, no, id)` 會依技能決定是單體還是群體、誰在範圍內、每人扣多少血，然後**自己發** Op 35 或 Op 57 回給客戶端。
- **客戶端要傳**（例如技能 ID=4 光箭，打怪物 10002）：
  - 技能 ID 4 → 對應 `lv=(4-1)/5+1`, `no=(4-1)%5`，所以 levelIdx=0, slotIdx=3；`20`（1）+ levelIdx（1）+ slotIdx（1）+ targetId（4）= **7 bytes**。
  - 技能 5/45（傳送）時客戶端多寫 **targetX**（2 bytes）再 targetId（4），共 9 bytes；對齊 C_Magic.java 僅 readH() 一次再 readD(id)，不讀 targetY。
- **本客戶端**：`C_MagicPacket.Make(skillId, targetId, targetX, targetY)`；`UseMagic(skillId, targetId, targetX, targetY)` 時呼叫（targetX/targetY 可選，傳送時 targetX 寫入封包）。

**小結**：  
- 近戰／弓箭：**只傳「打誰 + 打哪一格」**（23 或 24 + targetId + x + y）。  
- 魔法：**只傳「用哪個技能 + 打誰（或 0）」**（20 + lv/no + targetId）；單體還是群體、打幾個人、每人多少傷害，**全是伺服器算完再告訴你**。

---

## 三、伺服器怎麼回？（回單的兩種「單據」）

伺服器算完傷害後，只會用兩種「回單」告訴客戶端：**Opcode 35** 或 **Opcode 57**。

### 1. Opcode 35 — 「單次打擊」單據（近戰 / 弓箭 / 單體魔法）

- **誰在用**：  
  - 近戰：`S_ObjectAttack(cha, target, action, dmg, effectId, false, false)`  
  - 弓箭：`S_ObjectAttack(cha, target, action, dmg, effectId, true, arrow)`  
  - 單體魔法：`S_ObjectAttackMagic(cha, target, action, dmg, gfx)` → 同樣寫 **writeC(35)**。
- **共通結構**（客戶端 ParseObjectAttack 對齊）：
  - `35`（1）
  - `action`（1）— **動畫動作 ID**（近戰/弓 常為 getGfxMode()+1，單體魔法為 17/18/19；變身時可能其他數字）
  - `attackerId`（4）
  - `targetId`（4）
  - `damage`（1）
  - `heading`（1）
  - 後面再一截：**若為弓箭/單體魔法**，會再寫 `writeD(ETC)`、`writeH(gfx)`、**`writeC(6)` 表示魔法**、座標等；**近戰**則 `writeD(0), writeC(0)`。
- **客戶端怎麼用**：
  - 用 **magicFlag==6** 判斷「這包 35 是單體魔法」→ 走 **ObjectMagicDamage35** 做傷害結算（飄字、扣血條）。
  - 其餘一律當「動畫 + 物理」：`ObjectAttacked` → 播 `action`、物理用 PrepareAttack/keyframe 結算。
- **數字例子**：  
  你打怪物 10002，伺服器算完傷害 15，回一包 35：  
  `35, 1, 你的ID, 10002, 15, 你的朝向, ...`  
  客戶端：播攻擊動作、對 10002 扣 15 血、飄字「15」。

### 2. Opcode 57 — 「群體魔法」單據（多目標 + 每人傷害）

- **誰在用**：群體魔法（如 Lightning、Tornado）用 `S_ObjectAttackMagic(cha, list, action, none, gfx, x, y)`，寫 **writeC(57)**。
- **結構**（客戶端 ParseObjectAttackMagic 對齊）：
  - `57`（1）+ `action`（1）+ `attackerId`（4）
  - 再來是座標、gfx、type（0 或 8）、**targetCount**（2）
  - 然後 **for (targetCount)**：`targetId`（4）+ `damage`（1）
- **客戶端**：依 targetCount 迴圈，每個 (targetId, damage) 發一次 **ObjectMagicAttacked** → 對每個目標做 HandleEntityAttackHit（扣血、飄字），並在對應位置播魔法特效。
- **數字例子**：  
  火球打 3 隻怪：targetCount=3，列表 (10001,20), (10002,15), (10003,18)。  
  客戶端收到 57 後，對 10001 扣 20、10002 扣 15、10003 扣 18，並播一次群體魔法特效。

---

## 四、一回合完整流程（以「你近戰砍一隻怪」為例）

1. **客戶端**：  
   你點怪 → 發 **Opcode 23**：`[23, 目標ID(4), 目標X(2), 目標Y(2)]`（共 9 bytes）。  
   同時本地先播「揮刀」動畫（樂觀表現）。

2. **伺服器**：  
   收到 23 → C_Attack 解析 targetId、locx、locy → 檢查距離、是否凍結等 → 算傷害（例如 12）→ 扣怪物 HP → 發 **Opcode 35**：  
   `[35, action=1, 你的ID, 怪物ID, damage=12, heading, 0, 0]`（近戰 weapon 分支）。

3. **客戶端**：  
   收到 35 → ParseObjectAttack 讀出 action、attackerId、targetId、damage、magicFlag 等 →  
   - 因為 magicFlag≠6 → 當物理攻擊：  
     - 發 **ObjectAttacked** → 播攻擊者 action、對目標做 **PrepareAttack**（或 keyframe 到時再 **HandleEntityAttackHit**）。  
   - 傷害結算：在命中關鍵幀或立即對 targetId 扣 12 血、飄字「12」。

4. **可選**：  
   伺服器若開血條同步，會再發 **Opcode 104**（ObjectHitRatio）告訴該怪物當前血量比例，客戶端更新血條%。

這樣一來，「發 23 → 收 35 → 播動作 + 結算 12 傷害」就完成**一回合**的近戰結算。

---

## 五、三種攻擊對照（一回合要做的事）

| 類型 | 客戶端發送 | 伺服器回應 | 客戶端結算方式 |
|------|------------|------------|----------------|
| **近戰** | Op 23：targetId + X + Y（9 bytes） | Op 35（物理）：action + 雙方ID + damage + …，無 writeC(6) | ObjectAttacked → SetAction + PrepareAttack → keyframe 或立即 HandleEntityAttackHit |
| **弓箭** | Op 24：targetId + X + Y（9 bytes） | Op 35（弓）：同上但後段有 ETC/effectId/座標，無 writeC(6) | ObjectAttacked + ObjectRangeAttacked（飛行彈）→ PrepareAttack → HandleEntityAttackHit |
| **單體魔法** | Op 20：lv, no, targetId（+ 可選 X,Y） | Op 35（魔法）：同上且後段 **writeC(6)** + gfx/座標 | magicFlag==6 → **ObjectMagicDamage35** → HandleEntityAttackHit；ObjectAttacked 只播動作、不做 PrepareAttack |
| **群體魔法** | Op 20：lv, no, 主目標 targetId（或 0） | Op **57**：action + attackerId + 座標/gfx + **targetCount** + [targetId, damage]×N | 對每個 (targetId, damage) 發 ObjectMagicAttacked → HandleEntityAttackHit；同一個 57 播一次群體特效 |

---

## 六、本客戶端對應關係（方便你對碼）

- **近戰**：`PerformAttackOnce` → `SendAttackPacket(23, targetId, tx, ty)`。  
  收 35（非 magicFlag）→ `OnObjectAttacked` → `PrepareAttack` + keyframe 或直接 `HandleEntityAttackHit`。
- **弓箭**：`PerformAttackOnce`（弓）→ `SendAttackBowPacket(24, targetId, tx, ty)`。  
  收 35（ObjectRangeAttacked + ObjectAttacked）→ 同上，傷害由 PrepareAttack/keyframe 結算。
- **魔法**：`UseMagic(skillId, targetId, targetX, targetY)` → `C_MagicPacket.Make(skillId, targetId, targetX, targetY)` 發 Op 20；技能 5/45 時 targetX 寫入封包。  
  - 單體魔法：伺服器回 **Op 35 且 magicFlag=6** → **ObjectMagicDamage35** → `OnObjectMagicDamage35` → `HandleEntityAttackHit`；動畫仍由 ObjectAttacked 的 action 驅動。  
  - 群體魔法：伺服器回 **Op 57** → `ParseObjectAttackMagic` 迴圈 → 多個 **ObjectMagicAttacked** → `OnObjectMagicAttacked` → 每個目標 `HandleEntityAttackHit`。

---

## 七、記住這幾點

1. **傷害與命中只信伺服器**：客戶端只發「我要打誰、用什麼」，不自己算傷害。  
2. **35 與 57 的差別**：35 = 一次打擊（近戰/弓/單體魔法），57 = 群體魔法一包多個 (targetId, damage)。  
3. **單體魔法用「封包長相」認**：看 Op 35 後面有沒有 **writeC(6)**（magicFlag==6），不是看 action 是不是 17/18/19，變身時 action 會變。  
4. **一回合**：客戶端發一個「攻擊請求」（23/24/20）→ 伺服器回一個或多個「結果」（35 或 57）→ 客戶端依結果播動畫、做傷害結算（飄字、血條、Op 104 可選），這一來一往就是一次完整回合。

以上對齊目前本專案伺服器（C_Attack / C_AttackBow / C_Magic、S_ObjectAttack、S_ObjectAttackMagic）與客戶端（PacketHandler、GameWorld.Combat）的實作。
