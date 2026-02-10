# 魔法動畫播放邏輯鏈 — 全面檢查結論（僅匯報，未修改代碼）

## 一、動畫播放邏輯鏈條（從封包到特效）

| 階段 | 檔案 | 方法/位置 | 說明 |
|------|------|-----------|------|
| 1. 收包 | PacketHandler.cs | `switch(opcode)` case **57** → `ParseObjectAttackMagic(reader)` | 僅當**第一字節 = 57**時進入；若伺服器未發 Op57，此處永遠不執行。 |
| 2. 解析 | PacketHandler.cs | `ParseObjectAttackMagic` | 讀取順序：actionId(1), attackerId(4), x(2), y(2), heading(1), etcId(4), gfxId(2), type(1), padding(2), targetCount(2)，再依 targetCount 讀 [targetId(4), damage(1)]。與伺服器 `S_ObjectAttackMagic`（List 建構）寫入順序**一致**。 |
| 3. 發信號 | PacketHandler.cs | `EmitSignal(ObjectMagicAttacked, attackerId, targetId, gfxId, damage, x, y)` | 每個目標發一次；若 targetCount==0 再發一次 targetId=0。 |
| 4. 綁定 | GameWorld.Bindings.cs | `ObjectMagicAttacked += (atk,tgt,gfx,dmg,x,y) => OnMagicVisualsReceived(...)` | **唯一**連接到 **GameWorld.SkillEffect.cs** 的 `OnMagicVisualsReceived`。 |
| 5. 視覺處理 | GameWorld.SkillEffect.cs | `OnMagicVisualsReceived(attackerId, targetId, gfxId, damage, targetX, targetY)` | 計算 endPos（目標實體 > 網格 targetX/targetY > 攻擊者）、heading（有方向時依施法者→目標），若 `endPos != Zero && gfxId > 0` 則呼叫 `SpawnEffect(gfxId, endPos, heading, targetEnt)`。 |
| 6. 生成特效 | GameWorld.SkillEffect.cs | `SpawnEffect(gfxId, position, heading, followTarget)` | 建立 SkillEffect、設 GlobalPosition、`effect.Init(gfxId, heading, ...)`。若 `_skinBridge == null` 則 ABORT 並 return。 |
| 7. 初始化 | SkillEffect.cs | `Init(gfxId, heading, ...)` | 快取 gfxId、heading，呼叫 `TryLoadAndPlay()`。 |
| 8. 載入並播 | SkillEffect.cs | `TryLoadAndPlay()` | `GetBodyFrames(_gfxId, 0, _cacheHeading)` → CustomCharacterProvider.BuildLayer。若 frames 為 null 則進入輪詢（SetProcess(true)），逾時後仍無則不播。取到 frames 後設動畫名（heading 或 "0"）、Play、依 duration 設 Timer 銷毀或 109 連環。 |

**結論（鏈條）**：從 Op57 解析 → 信號 → OnMagicVisualsReceived → SpawnEffect → SkillEffect.Init → TryLoadAndPlay → GetBodyFrames/BuildLayer，邏輯鏈完整；封包讀取順序與伺服器寫入一致。

---

## 二、為何「魔法沒有播放動畫」— 可能原因（依鏈條排查）

### 1. 客戶端根本沒收到 Op57（最可能）

- **伺服器**：`Lightning.java` 僅在 `getObject(id) != null` 時呼叫 `SendPacket(new S_ObjectAttackMagic(...))`。若伺服器端 `operator.getObject(targetId)` 為 null（目標不在其 object 列表、id 不一致、或未通過 HpMpCheck/ConsumeCount），則**不會發送 Op57**。
- **客戶端**：若未收到 Op57，則不會出現 `[Magic][Packet57] 收到 Op57 開始解析`，後續 OnMagicVisualsReceived、SpawnEffect、SkillEffect 都不會執行。
- **如何確認**：日誌中若有 `[Magic] Casting Skill 16 on Target xxx` 但**沒有** `[Magic][Packet57] 收到 Op57 開始解析`，即可判定為**伺服器未發 Op57**（以伺服器為準時，屬伺服器條件未滿足，非客戶端 bug）。

### 2. 解析或信號後中斷

- 若出現 `[Magic][Packet57] 收到 Op57` 但出現 `[Packet] Magic Parse Error`：封包結構或長度與預期不符（或讀取越界）。
- 若出現 `[Magic][Op57] Received Attacker:...` 但接著出現 `[Magic][Op57] SKIP SpawnEffect ... endPos:{0,0}`：表示 endPos 為 Zero（目標與網格座標皆未取得），不會建立特效。

### 3. SpawnEffect 未執行或中止

- `_skinBridge == null` 時會 `GD.PrintErr(...ABORT _skinBridge=null...)` 並 return，不會建立 SkillEffect。

### 4. 資源載入失敗（無圖可播）

- list.spr 無 Gfx 171 或無 Action 0/3 → BuildLayer 回傳 null → TryLoadAndPlay 輪詢，可能出現 `[Magic][SkillEffect.TryLoad] 幀為空 進入輪詢 ...`。
- PAK 缺少對應圖檔（如 171-0-xxx.png ~ 171-7-xxx.png）→ GetTexture 為 null → BuildLayer 產出空 SpriteFrames 或無有效動畫名 → 可能 `[Magic][SkillEffect.TryLoad] 無有效動畫名 ...` 或動畫為空。
- **list.spr 現狀**：已確認 #171 有 `0.fly(1 5,...)`、`104.attr(8)`、`109.effect(5 218)`，客戶端邏輯會取 Action 0、Attr 8，方向與連環皆支援。

### 5. 重複/死碼導致混淆（不影響 Op57 特效鏈，但影響行為理解）

- **GameWorld.Combat.cs** 內有 `OnObjectMagicAttacked(int attackerId, int targetId, int gfxId, int damage, int x, int y)`，內含：施法者 SetAction(ACT_SPELL_DIR)、PrepareAttack(targetId, damage)、以及根據 gfxId 呼叫 SpawnEffect。
- **該方法未與任何信號綁定**。Bindings 僅將 `ObjectMagicAttacked` 連到 **GameWorld.SkillEffect.OnMagicVisualsReceived**，因此 **Combat.OnObjectMagicAttacked 從未被呼叫**（死碼）。
- **影響**：  
  - Op57 的**特效生成**只會由 OnMagicVisualsReceived → SpawnEffect 完成，這部分正確。  
  - 施法者的 **SetAction(ACT_SPELL_DIR)** 與目標的 **PrepareAttack(targetId, damage)**（受擊反應）目前僅存在於未使用的 Combat.OnObjectMagicAttacked，故**收到 Op57 時不會執行**；若 UI 上「施法動作」或「目標受擊反應」缺失，可能與此有關。  
  - 先前若在 Combat.OnObjectMagicAttacked 加診斷日誌，那些日誌**永遠不會出現**，因為該方法從未被呼叫。

---

## 三、封包結構對照（伺服器 vs 客戶端）

**伺服器** `S_ObjectAttackMagic(cha, list, action, false, gfx, x, y)`（gfx 170/171 時 x,y = cha.getX/Y）：

- writeC(57), writeC(action), writeD(cha.getObjectId()), writeH(x), writeH(y), writeC(cha.getHeading()), writeD(etc), writeH(gfx), writeC(8), writeH(0), writeH(list.size()), for each: writeD(o.getObjectId()), writeC(o.getDmg()).

**客戶端** ParseObjectAttackMagic：

- ReadByte(actionId), ReadInt(attackerId), ReadUShort(x), ReadUShort(y), ReadByte(heading), ReadInt(etcId), ReadUShort(gfxId), ReadByte(type), ReadUShort(), ReadUShort(targetCount), for each: ReadInt(targetId), ReadByte(damage).

**結論**：順序與長度一致，無結構性錯誤。

---

## 四、Skill 17（通暢氣脈術）與「不為單一魔法寫特例」

- Skill 17 在 skill_list 為 Buff、Gfx 750；若伺服器對 Buff 不發 Op57，則客戶端依現有邏輯**不會**自動播放該 Gfx。
- 若曾為 Skill 17 在客戶端做「一按就強制 SpawnEffect(750)」的特例，會違反「全面開發魔法、不為單一魔法疊加特例」的原則；建議**撤銷**該特例，改為與其他魔法同一套邏輯（依封包驅動：有 Op57/Op83/Op55 才播對應 Gfx）。
- 若希望 Buff 類魔法也有動畫，應由**同一套**魔法框架支援（例如伺服器對該 Buff 發送 Op57 或 Op83/Op55，客戶端統一在 OnMagicVisualsReceived / OnEffectAtLocation / OnObjectEffectReceived 中處理），而非對 skillId==17 單獨分支。

---

## 五、總結與建議（僅結論，不實作）

1. **動畫不播的主因**：在「以伺服器為準、不改伺服器」的前提下，最可能是**伺服器未發 Op57**（Skill 16 時 `getObject(id)==null`）。用日誌對照：有 `[Magic] Casting Skill 16` 且無 `[Magic][Packet57] 收到 Op57` → 可確認。
2. **邏輯鏈**：Op57 → ParseObjectAttackMagic → ObjectMagicAttacked 信號 → **OnMagicVisualsReceived（SkillEffect.cs）** → SpawnEffect → SkillEffect.Init → TryLoadAndPlay；鏈條正確，封包對齊。
3. **重複/死碼**：Combat.OnObjectMagicAttacked 未被綁定，為死碼；施法動作與目標受擊反應若依賴此方法則目前不會執行。若要統一魔法行為（含施法/受擊），應在**實際被呼叫的** OnMagicVisualsReceived 中補齊，或收斂至單一處理入口，避免兩套邏輯。
4. **不為單一魔法特例**：建議移除對 Skill 17 的強制播放，改為全面依封包驅動；若需 Buff 動畫，用同一套魔法/特效入口處理。
5. **日誌**：保留並依需要加強「是否收到 Op57」「endPos 是否 Zero」「SpawnEffect 是否被呼叫」即可精準定位問題，無須為單一技能疊加複雜分支。

（本報告僅為檢查結論，未對代碼做任何修改。）
