# 魔法系統開發文檔（Magic System）

本文件整理客戶端與伺服器端魔法的**網路協定、攻擊規則、魔法分類、目標選擇、範圍與群體、魔法動畫顯示**等全部邏輯、規則與用途。對齊專案約定：`/server` 為絕對真相來源，客戶端須與伺服器協定一致。

---

## 1. 總覽

| 項目 | 說明 |
|------|------|
| **客戶端入口** | `GameWorld.UseMagic(skillId, targetId)`，由技能按鈕、雙擊技能圖標、法師 Z 鍵（光箭）等觸發。 |
| **資料來源** | 本地 `skill_list.csv`（對齊 `server/skill_list` 表）：`skill_id, cast_gfx, range, type`。 |
| **流程原則** | **先播放後結算**：客戶端依本地 cast_gfx 先播魔法動畫，再送 C_Magic；傷害與結果由伺服器 Op35/Op57 結算。 |
| **PC 魔法 ID** | 1–50（精靈魔法 129+ 施法封包尚未實作）。 |

---

## 2. 網路協定

### 2.1 客戶端 → 伺服器：施法封包（C_Magic, Opcode 20）

| 項目 | 說明 |
|------|------|
| **客戶端** | `Client/Network/C_MagicPacket.cs` → `Make(skillId, targetId, targetX, targetY)`。 |
| **伺服器** | `server/network/client/C_Magic.java`：`readC()`→lv, `readC()`→no, `readD()`→id（目標 ObjectId）。 |
| **skill_level / slot** | `levelIdx = (skillId - 1) / 5`，`slotIdx = (skillId - 1) % 5`；對齊伺服器每級 5 格、skill_level 1–10。 |
| **傳送類** | 技能 5（指定傳送）、45（集體傳送）時僅多寫 `writeShort(targetX)` 再 `writeInt(targetId)`，共 6 位元組；對齊 C_Magic.java 僅 `readH()` 一次再 `readD(id)`，伺服器不讀 targetY。 |
| **群體魔法** | 只發**一包** C_Magic(skillId, **主目標** targetId)；伺服器依主目標位置算範圍內所有目標，回傳一個 Op57（多筆 targetId+damage）。 |

### 2.2 伺服器 → 客戶端：魔法攻擊封包

#### Opcode 35（S_ObjectAttack，單體魔法／物理遠程共用）

| 項目 | 說明 |
|------|------|
| **用途** | 單體魔法（如光箭 EnergyBolt）、物理遠程（弓箭）共用；封包內含 `magicFlag=6` 表魔法。 |
| **伺服器** | `S_ObjectAttackMagic(cha, temp, action, dmg, gfx)` → writeC(35)，含施法者、目標、傷害、gfx。 |
| **客戶端** | `PacketHandler.ParseObjectAttack` → 若 magicFlag==6 發 **ObjectMagicHit** → `OnObjectMagicHit` → 立即 `HandleEntityAttackHit`；飛行段由 `ObjectRangeAttacked` → `OnRangeAttackReceived` 處理起點→終點。 |

#### Opcode 57（S_ObjectAttackMagic，多目標／地面魔法）

| 項目 | 說明 |
|------|------|
| **格式** | actionId, attackerId, x, y, heading, etcId, gfxId, type(0=單體/8=AOE), targetCount, [targetId, damage]×N；targetCount==0 時為無目標地面魔法（如日光術落點）。 |
| **客戶端解析** | `PacketHandler.ParseObjectAttackMagic` → 每筆目標 emit `ObjectMagicAttacked(attackerId, targetId, gfxId, damage, x, y)`；targetCount==0 時 emit 一次 targetId=0，客戶端用 (x,y) 播落點。 |
| **綁定** | `GameWorld.Bindings`：ObjectMagicAttacked → `OnMagicVisualsReceived`（視覺）+ `OnObjectMagicAttacked`（傷害結算）。 |

---

## 3. 魔法分類（skill_list.csv 的 type）

目標選取**唯一**依本地 **skill_list.csv** 的 **type** 欄位；與伺服器 `skill_list` 表對齊。

| type 值 | 行為 | 目標來源 | 無效目標時 |
|--------|------|----------|------------|
| **none** | 一律對自己施法 | 不檢查 targetId，不使用傳入或任務/攻擊目標；`targetId = _myPlayer.ObjectId`。 | — |
| **item** | 一律對自己施法 | 同上；即便選了別人或地面物品也只對自己。 | — |
| **buff** | 人工選中或自己 | targetId==0 時：`GetCurrentTaskTarget() ?? GetCurrentAttackTarget()`；若仍無則目標=自己。 | 若解析出的目標無效（地面物品/死亡/無血條）→ 改為對自己施法。 |
| **attack** 及其餘 | 必須有目標 | targetId==0 時：`GetCurrentAttackTarget() ?? GetCurrentTaskTarget()`；若仍無則提示「請先點選目標或按 Z 選怪」並 **return**，不發包。 | 若解析出的目標無效 → 提示並 return，不發包。 |

- **實作位置**：`Client/Game/GameWorld.Skill.cs` → UseMagic 開頭（步驟 2、2.1）。
- **注意**：壞物術等需敵方目標時，應在 skill_list.csv 將該技能 **type** 設為 **attack**，而非 buff。

### 3.1 技能 9（擬似魔法武器／神聖武器）與傳入武器 InvID

| 項目 | 說明 |
|------|------|
| **伺服器定義** | `server/world/instance/skill/function/EnchantWeapon.java`：`toMagic(int id)` 內 `ItemInstance item = this.operator.getInventory().getItemInvId(id)`；**id** 為**施法者背包內武器之 InvID**，對該武器上 Buff（增加攻擊/命中）。 |
| **skill_list** | 技能 9 的 type 為 **item**（對齊 server SkillTable case 9 → EnchantWeapon）。 |
| **客戶端傳值** | 需傳入「被施法角色之當前裝備武器」的 InvID。因技能 9 僅對自己施法（type=item），故以**自己當前裝備的武器**之 InvID 傳入：`targetId = GetEquippedWeaponObjectId()`（客戶端物品 ObjectId 即伺服器 InvID，對齊 S_InventoryList/S_InventoryAdd 之 writeD(getInvID())）。 |
| **無武器時** | 若 `GetEquippedWeaponObjectId() == 0`，提示「你沒拿武器。」並 return，不發包。 |
| **對照** | `Client/Game/GameWorld.Inventory.cs` → GetEquippedWeaponObjectId()；`Client/Game/GameWorld.Skill.cs` → UseMagic 內 skillId==9 時 targetId=weaponInvId。 |

### 3.2 技能 15（鎧甲護持 Blessed Armor）與傳入盔甲 InvID

| 項目 | 說明 |
|------|------|
| **伺服器定義** | `server/world/instance/skill/function/BlessedArmor.java`：`toMagic(int id)` 內 `ItemInstance item = this.operator.getInventory().getItemInvId(id)`；**id** 為**施法者背包內盔甲之 InvID**，且須 `item instanceof ItemArmorInstance` 且 `getType() == 16`（對齊 ItemsTable armor type=16）。 |
| **skill_list** | 技能 15 的 type 為 **item**（對齊 server SkillTable case 15 → BlessedArmor）。 |
| **客戶端傳值** | 需傳入「被施法角色之當前裝備盔甲」的 InvID：`targetId = GetEquippedArmorObjectId()`（客戶端物品 ObjectId 即伺服器 InvID）。 |
| **無盔甲時** | 若 `GetEquippedArmorObjectId() == 0`，提示「你沒穿盔甲。」並 return，不發包。 |
| **對照** | `Client/Game/GameWorld.Inventory.cs` → GetEquippedArmorObjectId()、IsArmor(type==16)；`Client/Game/GameWorld.Skill.cs` → UseMagic 內 skillId==15 時 targetId=armorInvId。 |

### 3.3 技能 46（創造魔法武器 CreateMagicalWeapon）與傳入武器 InvID

| 項目 | 說明 |
|------|------|
| **伺服器定義** | `server/world/instance/skill/function/CreateMagicalWeapon.java`：`toMagic(int id)` 內 `ItemInstance item = this.operator.getInventory().getItemInvId(id)`；**id** 為**施法者背包內武器之 InvID**，須為 ItemWeaponInstance、getEnLevel()==0、可附魔。 |
| **skill_list** | 技能 46 的 type 為 **item**（對齊 server SkillTable case 46 → CreateMagicalWeapon）。 |
| **客戶端傳值** | 以「當前裝備的武器」之 InvID 傳入：`targetId = GetEquippedWeaponObjectId()`。 |
| **無武器時** | 若 `GetEquippedWeaponObjectId() == 0`，提示「你沒拿武器。」並 return，不發包。 |
| **對照** | `Client/Game/GameWorld.Skill.cs` → UseMagic 內 skillId==46 時 targetId=weaponInvId；跳過魔法目標實體驗證。 |

---

## 4. 目標選擇與區分

### 4.1 目標來源

| 來源 | 說明 | 取得方式 |
|------|------|----------|
| **當前攻擊目標** | Z 鍵或點擊怪物後建立的 Attack 任務目標。 | `GetCurrentAttackTarget()` = `_currentTask?.Target`（僅當 Type==Attack）。 |
| **當前任務目標** | 任意任務（Attack / PickUp / TalkNpc）的目標；點擊地面物品會產生 PickUp 任務，目標即該物品。 | `GetCurrentTaskTarget()` = `_currentTask?.Target`。 |

- **實作位置**：`Client/Game/GameWorld.Combat.cs` → GetCurrentAttackTarget / GetCurrentTaskTarget。

### 4.2 魔法目標驗證（IsValidMagicTarget）

僅**有效魔法目標**才可作為落點／跟隨對象；避免魔法在地面物品頭上播放。

| 條件 | 說明 |
|------|------|
| 不為 null / 不為自己 | 自己僅在 buff/none/item 時允許（forAttack==false）。 |
| **有血條** | `entity.ShouldShowHealthBar()` == true → list.spr **102.type(5) 或 102.type(10)**；地面物品（type 9 等）為 false。 |
| **未死亡** | `entity.HpRatio > 0`。 |
| **attack 時排除己方寵物** | `!_mySummonObjectIds.Contains(entity.ObjectId)`。 |

- **實作位置**：`Client/Game/GameWorld.Skill.cs` → IsValidMagicTarget(e, forAttack)。
- **時機**：UseMagic 在依 type 取得 targetId 後，若 targetId>0 且對應實體無效，則 attack 系提示並 return，buff 改為對自己施法。

### 4.3 攻擊目標選擇（Z 鍵／ScanForAutoTarget）

按 Z 時掃描的「可攻擊目標」須排除下列對象，**僅選擇 102.type(5)/(10) 有血量**之怪物/NPC/玩家：

| 排除項 | 說明 |
|--------|------|
| 自己 | `entity == _myPlayer` → continue。 |
| 死亡 | `entity.HpRatio <= 0` → continue。 |
| 地面物品等 | `!entity.ShouldShowHealthBar()` → continue。 |
| 己方召喚/寵物 | `_mySummonObjectIds.Contains(entity.ObjectId)` → continue。 |
| 距離 | 格距 > 15 不選。 |

- **實作位置**：`Client/Game/GameWorld.Combat.cs` → ScanForAutoTarget；註解「不可以刪除修改」「排除地面物品等：僅選擇 102.type（5）/（10）有血量…」須保留。

---

## 5. 範圍與群體魔法

### 5.1 資料來源

| 欄位 | 來源 | 用途 |
|------|------|------|
| **range** | SkillListData.Get(skillId).Range（skill_list.csv） | 群體魔法「主目標周圍 N 格」；range>=2 視為群體。 |
| **cast_gfx** | SkillListData.Get(skillId).CastGfx | 特效 GfxId、飛行/方向判定。 |

### 5.2 群體類型

| 類型 | 判定 | 範圍內目標 |
|------|------|------------|
| **單向群體** | list.spr 該 cast_gfx 的 Action0 **DirectionFlag==1**（如極光雷電 170）。 | 以施法者為原點，僅「與主目標同方向、距離<=range」的實體（一條路上的怪物）。 |
| **全方向群體** | Action0 DirectionFlag!=1（如燃燒的火球 171）。 | 主目標周圍 **range** 格內全部實體。 |

- **判定**：`ListSprLoader.IsAction0Directional(castGfx)`。
- **實作位置**：`Client/Game/GameWorld.Skill.cs` → UseMagic 內「群體魔法」區塊；單向用 heading 篩選，全方向用 GetGridDistance<=aoeRange。

### 5.3 攻擊範圍 vs 攻擊距離

| 概念 | 含義 | 取得方式 | 用途 |
|------|------|----------|------|
| **攻擊範圍** | 群體魔法「主目標周圍 N 格」。 | SkillListData.Get(skillId)?.Range | AOE 判定哪些實體頭上播動畫；伺服器 AOE 計算。 |
| **攻擊距離** | 角色「能從多遠格數發動攻擊/魔法」。 | list.spr 102.type(8/9) 或 GetAttackRangeFallback() | 戰鬥狀態機「是否在射程內」、尋路與追擊。 |

---

## 6. 魔法動畫顯示

### 6.1 流程概覽

1. **UseMagic**：施法動作 SetAction(ACT_SPELL_DIR)；依 cast_gfx 與是否飛行／群體，在**落點或起點→終點**播放。
2. **發包**：C_MagicPacket.Make(skillId, targetId, targetX, targetY) 送伺服器；技能 5/45 時 targetX 寫入封包。
3. **伺服器**：Op35（單體魔法/遠程）或 Op57（多目標/地面）；客戶端收到後**僅結算傷害**，視覺由「先播放」或 Op57 落點/連貫段補齊，且己方去重。

### 6.2 非飛行魔法（落點播放）

- **UseMagic**：RecordSelfMagicCast(castGfx)；若群體則對 targetsInRange 每個 SpawnEffect(castGfx, t.GlobalPosition, th, t)；否則 SpawnEffect(castGfx, endPos, heading, followTarget)。
- **Op57**：若己方剛施放且 TryConsumeSelfMagicCast 為 true → 不重播主段，僅在每個目標播 109.effect 連貫段（若有）；否則在他方或無己方記錄時在落點 SpawnEffect(gfxId, endPos, heading, targetEnt)。

### 6.3 飛行魔法（起點→終點 + 連貫）

- **判定**：`ListSprLoader.IsAction0Fly(castGfx)`（Action0 名稱含 "fly"）。
- **UseMagic**：RecordSelfMagicCast + RecordSelfFlyingCast(castGfx)；在起點建立 SkillEffect，Tween 到終點，Init(…, followTarget, OnChainEffectTriggered, **useFollowForPosition: false**)；群體時對 targetsInRange 每個播 109 連貫段。
- **Op35（ObjectRangeAttacked）**：若己方且 TryConsumeSelfFlyingCast(gfxId) 為 true → 跳過重複播放；否則在起點→終點播放，播畢觸發 109.effect。
- **Op57**：己方已播主段則不重播主段，僅在各目標播連貫段；他方正常 SpawnEffect 主段。

### 6.4 連貫播放（109.effect）

- **list.spr**：`109.effect(a b)` 表示該 Gfx 播畢後，在**相同/跟隨目標位置**播放下一個 GfxId **b**。
- **客戶端**：SkillEffect 在 OnAnimationFinished 檢查 EffectChain；若有 nextGfxId 則呼叫 chainCallback(nextGfxId, position, heading, followTarget)，由 GameWorld.OnChainEffectTriggered → SpawnEffect 播下一段。
- **用途**：如 171（燃燒的火球）→ 218；光箭 167 → 219 在目標處播放。

### 6.5 去重與結算

| 機制 | 說明 |
|------|------|
| **RecordSelfMagicCast / TryConsumeSelfMagicCast** | 己方在 UseMagic 播過主段時記錄 (gfxId, 時間)；Op57 收到時若為己方且在時間窗內則跳過重複 SpawnEffect，僅結算傷害。 |
| **RecordSelfFlyingCast / TryConsumeSelfFlyingCast** | 己方飛行段同上，Op35 收到時跳過重複起點→終點。 |
| **傷害結算** | Op57 每筆 targetId+damage 在 **OnObjectMagicAttacked** 中呼叫 HandleEntityAttackHit(targetId, damage)（targetId>0）；不經 keyframe。 |

- **實作位置**：`Client/Game/GameWorld.SkillEffect.cs`（RecordSelfMagicCast, TryConsumeSelfMagicCast, RecordSelfFlyingCast, TryConsumeSelfFlyingCast）；`GameWorld.Combat.cs` → OnObjectMagicAttacked；`GameWorld.SkillEffect.cs`（partial）→ OnMagicVisualsReceived, OnRangeAttackReceived。

### 6.6 方向與落點

- **有方向魔法（DirectionFlag==1）**：AOE 時依**每個目標**單獨計算「施法者→該目標」的 8 方向 heading；與 list.spr 檔序一致。
- **落點優先級**（Op57）：目標實體 Position > 網格 (targetX,targetY) > 攻擊者位置。
- **無目標地面魔法**（targetCount==0）：用封包 (x,y) 在該格播特效（如日光術）。

---

## 7. 攻擊規則（魔法 vs 物理）

| 項目 | 魔法 | 物理 |
|------|------|------|
| **冷卻** | SpeedManager.CanPerformAction(ActionType.Magic, gfxId, ACT_SPELL_DIR)；依 SprDataTable 施法間隔。 | SpeedManager.CanPerformAction(ActionType.Attack, gfxId, actionId)；依攻擊間隔。 |
| **傷害權威** | 僅封包：Op35（單體魔法）或 Op57（每目標一筆）；不依 actionId。 | 僅封包：ObjectAttacked / ObjectRangeAttacked → 關鍵幀或 ObjectMagicHit 時 HandleEntityAttackHit。 |
| **法師 Z 鍵** | 法師（class=3）按 Z = 自動魔法攻擊（skill 4 光箭 Gfx167）；其他職業 Z = 物理攻擊。 |

---

## 8. 檔案與資料對照

| 功能 | 客戶端檔案 | 伺服器/資料 |
|------|------------|-------------|
| 施法入口與目標解析 | GameWorld.Skill.cs（UseMagic, IsValidMagicTarget） | skill_list type |
| 攻擊/任務目標 | GameWorld.Combat.cs（GetCurrentAttackTarget, GetCurrentTaskTarget, ScanForAutoTarget） | — |
| 施法封包 | C_MagicPacket.cs | C_Magic.java |
| 魔法封包解析 | PacketHandler.ParseObjectAttackMagic | S_ObjectAttackMagic.java |
| 魔法視覺與傷害 | GameWorld.SkillEffect.cs（OnMagicVisualsReceived, OnRangeAttackReceived）, GameWorld.Combat.cs（OnObjectMagicAttacked） | S_ObjectAttackMagic(57), S_ObjectAttack(35) |
| 特效實例 | SkillEffect.cs（Init, TryLoadAndPlay, OnAnimationFinished, 109 連貫） | list.spr 104.attr(8), 109.effect |
| 技能表 | SkillListData.cs, skill_list.csv | skill_list 表 |
| 飛行/方向判定 | ListSprLoader.IsAction0Fly, IsAction0Directional | list.spr Action0 name, DirectionFlag |

---

## 9. 用途摘要

- **type none/item**：傳送、日光術、初級治癒等僅對自己，避免誤選目標。
- **type buff**：治癒、解毒、神聖武器等可對己或友方；無目標時 fallback 自己。
- **type attack**：光箭、燃燒的火球、極道落雷等必須有敵方目標；無效目標（物品/死亡/己方寵物）不發包並提示。
- **魔法目標驗證**：確保特效僅在怪物/NPC/玩家（102.type 5/10、有血、未死、非己方寵物）上播放，不在地面物品上播放。
- **先播放後結算**：本地先播動畫再送封包，伺服器回傳只做傷害與同步，己方不重複播主段，避免雙重特效。
