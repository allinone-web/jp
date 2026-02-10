# 魔法系統全面檢查報告（僅討論、不修改）

對照您提出的七點原則，對目前客戶端魔法邏輯做逐項檢查與落差說明。**不包含任何程式修改**，僅供後續設計與根因修復參考。

---

## 一、您提出的七點原則摘要

1. **魔法只要是 fly（Action0=fly），就必須當作飛行魔法。**
2. **光箭 167 與燃燒的火球 171 同為「飛行＋連貫」魔法，應共用同一套邏輯。**
3. **差異僅在範圍：** 光箭＝單體；燃燒的火球＝群體（被攻擊目標身邊 3x3 格內所有怪物都應播放動畫）。
4. **連貫魔法必須連貫：** 第一段播完一定要播第二段，圖像層效果不可因任何意外中斷。
5. **魔法 gfx 可從本地 SkillListEntry 取得**，故日光術(2) 即便伺服器沒傳 gfx 也能正常播放。
6. **魔法 gfx 以本地為準：** 按下魔法按鈕後，客戶端只管播放、必須播動畫，再結算。
7. **效果必須保證：** 不因報錯疊加臃腫邏輯，要找到故障根源並消除，而非反覆疊加修錯。

---

## 二、原則對照與現狀落差

### 原則 1：Action0=fly 即為飛行魔法

| 項目 | 現狀 | 與原則的關係 |
|------|------|--------------|
| 判定方式 | 僅 `castGfx == 167` 視為飛行魔法（硬編碼）。171 雖在 list.spr 為 `0.fly` 仍當「非飛行」處理。 | **違反原則 1**：應以 list.spr 的 Action0 是否為 "fly" 為準，167 與 171 都應視為飛行魔法。 |
| 根因 | 為避免 171 不播（伺服器只發 Op57、不發 Op35），改為只讓 167 當飛行，171 改回「先播落點」。 | 若改回「fly 即飛行」，171 的「第一段飛行＋落點連貫」需由同一套飛行－連貫邏輯處理，而不是用「不當飛行」規避。 |

---

### 原則 2：飛行－連貫應為一套邏輯

| 項目 | 現狀 | 與原則的關係 |
|------|------|--------------|
| 光箭 167 | UseMagic 不播 → 等 Op35 → OnRangeAttackReceived 建立「飛行 SkillEffect」＋chainCallback，播完觸發 219。 | 有飛行、有連貫，但僅 167 走這條路。 |
| 燃燒的火球 171 | UseMagic 當「非飛行」→ 在目標處 SpawnEffect(171)＋chainCallback；伺服器 Op57 多目標時 TryConsumeSelfMagicCast 只消費一次，其餘目標仍會 SpawnEffect(171)。 | **違反原則 2**：171 沒有「飛行段」，只有「落點段＋連貫」，與 167 的「飛行＋落點連貫」是兩套邏輯。 |
| 理想 | 167 / 171 共用：**本地一律先播「飛行段」**（起點→終點），飛行段播完在**落點**觸發 109.effect 第二段；伺服器只負責結算與（若需要）補發視覺，不應取代客戶端「必須播」的責任。 | 需統一為：**凡 Action0=fly，客戶端自己播飛行＋連貫**；Op35/Op57 僅用於去重或補漏，不作為「是否播放」的條件。 |

---

### 原則 3：單體 vs 群體（3x3）的差異

| 項目 | 現狀 | 與原則的關係 |
|------|------|--------------|
| 伺服器 | 光箭：Op35，單目標 (cha, o, ...)。燃燒的火球：Op57，`List<L1Object> list`，主目標＋`getObjectList()` 內在 `getSkill().getRange()` 的對象；封包為一組 (x,y)＋多個 (objectId, dmg)。170/171 時 (x,y) 寫的是 **cha.getX/Y()（施法者）**。 | 171 為 AOE，多目標；座標上伺服器只給一個 (x,y)。 |
| 客戶端 Op57 | 依 targetCount 迴圈，每個 target 各發一次 ObjectMagicAttacked(attackerId, targetId, gfxId, damage, **x, y**)。x,y 全目標共用（171 時為施法者格）。 | **落點**：OnMagicVisualsReceived 若 targetId 在 _entities 則用 **targetEnt.Position**，故每個目標會在不同位置播。 |
| 3x3 動畫 | 目前每個 Op57 目標都會進 OnMagicVisualsReceived，若未被子彈 TryConsumeSelfMagicCast 吃掉，就會 SpawnEffect 一次。所以「多目標各自一顆」在 Op57 路徑上已存在。 | 與原則 3「被攻擊目標身邊 3x3 所有怪物都應播放動畫」**可對齊**，前提是：171 的「第一段（飛行／爆炸）」與「第二段 218」在每個目標上的播放邏輯一致且不因 TryConsumeSelfMagicCast 或漏播而少播。 |

---

### 原則 4：連貫不可中斷

| 項目 | 現狀 | 與原則的關係 |
|------|------|--------------|
| 觸發條件 | SkillEffect.OnAnimationFinished → 讀 list.spr EffectChain → 若有 nextGfxId 且 `_chainCallback != null` 才呼叫。 | **風險**：若 chainCallback 為 null（例如某條建立 SkillEffect 的路徑沒傳），連貫直接不觸發，違反「必須連貫」。 |
| 飛行段（Op35） | OnRangeAttackReceived 已傳 chainCallback 與 followTarget（useFollowForPosition: false），167 播完會觸發 219。 | 符合「第一段播完必播第二段」的目標。 |
| 落點段（UseMagic 或 Op57） | SpawnEffect 有傳 OnChainEffectTriggered，故 171 在「先播落點」或 Op57 時若有 chainCallback，218 會播。 | 同上，但 171 若改為「飛行魔法」且不再在 UseMagic 播落點，要確保 Op57 來的每個目標的 171 都有 chainCallback，且不受 TryConsumeSelfMagicCast 影響而漏播（見下）。 |
| 潛在中斷點 | (1) 建立 SkillEffect 時未傳 chainCallback；(2) def 或 EffectChain 為空；(3) 異常/early return 導致 OnAnimationFinished 未執行；(4) TryConsumeSelfMagicCast 讓己方 Op57 整包不播，若該包是「唯一」視覺來源則連貫也消失。 | 原則 4 要求：**圖像層**第一段播完→第二段必播；不應依賴「是否為己方」「是否重複」等邏輯中斷連貫。建議：連貫觸發只依「動畫播畢＋EffectChain 有下一段」，與 Op57 去重邏輯分離。 |

---

### 原則 5 & 6：gfx 以本地為準，按鈕按下即播再結算

| 項目 | 現狀 | 與原則的關係 |
|------|------|--------------|
| gfx 來源 | UseMagic 用 SkillListData.Get(skillId) 取 castGfx，來自本地 CSV。 | **符合原則 5、6**：日光術(2) 等可完全依本地 gfx 播放，不依賴伺服器有無傳 gfx。 |
| 先播放後結算 | UseMagic 在送 C_MagicPacket 前會（對「非飛行」）SpawnEffect。 | **符合原則 6**：「必須播放」在送封包前執行。 |
| 飛行魔法例外 | 目前 167 在 UseMagic 不播，等 Op35。若 Op35 遲到或沒發，167 就沒有「客戶端保證播」。 | **與原則 6 衝突**：原則要求「按下按鈕客戶端只管播放、必須播」。故 **飛行魔法也應在客戶端先播**（起點→終點），再送封包；Op35 若到則可視為重複而只做結算／去重，而非「不播就等 Op35」。 |

---

### 原則 7：找根因、不疊加修錯

| 項目 | 現狀 | 與原則的關係 |
|------|------|--------------|
| 飛行判定 | 從「Action0==fly」改為「castGfx==167」是為了避免 171 不播。 | **屬於疊加修錯**：用「只讓 167 當飛行」規避 171 的播放與連貫問題，而非統一「fly＝飛行＋同一套飛行－連貫邏輯」。 |
| TryConsumeSelfMagicCast | 己方先播後，Op57 再來就跳過 SpawnEffect，避免雙圖。 | 去重本身合理；但若與「連貫必播」綁在一起，可能導致 AOE 時只對第一個目標去重、其餘目標仍播，或反過來漏播，需與原則 4 一起釐清。 |
| 建議方向 | 根因應是：**所有 Action0=fly 的魔法，客戶端都應「自己播飛行＋自己播連貫」**；伺服器 Op35/Op57 只負責「結算與可選的視覺補發」，客戶端不依賴「有無收到封包」才決定播不播。 | 單一邏輯：按下魔法 → 依本地 castGfx 與 list.spr 判定是否飛行 → 是則客戶端播「飛行段＋落點連貫」，否則播「落點段＋連貫」→ 再送封包。 |

---

## 三、伺服器協議簡要（供對齊用）

- **光箭 (167)**：EnergyBolt.toMagic → `S_ObjectAttackMagic(operator, o, ...)` → **Op35**，單目標，封包含 (sx,sy)(tx,ty)。
- **燃燒的火球 (171)**：Lightning.toMagic → `S_ObjectAttackMagic(operator, list, ..., false, castGfx, o.getX(), o.getY())` → **Op57**，AOE；S_ObjectAttackMagic 內對 gfx 170/171 寫入 **cha.getX(), cha.getY()**，故 Op57 的 (x,y) 為施法者格。客戶端對每個 targetId 用 targetEnt.Position 當落點即可得到 3x3 各目標位置。

---

## 四、總結：原則 vs 現狀一覽

| 原則 | 現狀符合？ | 主要落差 |
|------|------------|----------|
| 1. fly＝飛行魔法 | 否 | 僅 167 當飛行，171 未依 list.spr 的 fly 判定。 |
| 2. 飛行－連貫一套邏輯 | 否 | 167 走 Op35 飛行＋連貫；171 走「落點＋連貫」，沒有飛行段。 |
| 3. 單體 vs 3x3 | 可對齊 | Op57 多目標＋endPos=targetEnt.Position 可做到每目標一動畫；需確認 171 改為飛行後 3x3 每格都播。 |
| 4. 連貫不可中斷 | 部分 | 有 chainCallback 時可連貫；若建立時未傳或去重導致不建立，會中斷。 |
| 5. gfx 本地 SkillListEntry | 是 | 已用 SkillListData 取 castGfx。 |
| 6. 本地為準、按鈕即播再結算 | 部分 | 非飛行已先播；飛行目前依賴 Op35，未做到「按鈕即播」。 |
| 7. 找根因、不疊加 | 否 | 用「只認 167 為飛行」規避 171，屬疊加修錯。 |

---

## 五、建議的根因與單一邏輯方向（仍不實作，僅討論）

1. **統一飛行判定**  
   以 **list.spr 的 Action0 是否為 "fly"** 為唯一依據（ListSprLoader.Get(castGfx) + def.Actions[0].Name），167 與 171 皆為飛行魔法。

2. **飛行魔法一律客戶端先播**  
   - 按下魔法 → 取本地 castGfx → 若為 fly，**客戶端自己**在起點建立飛行 SkillEffect，Tween 到目標（或主目標），並傳 chainCallback＋followTarget（落點／主目標），播完觸發 109.effect。  
   - 不依賴「有無 Op35」才播；Op35 若到可視為重複，僅做傷害／去重，不取代「必須播」的責任。

3. **單體 vs AOE 僅差在「落點段」的數量**  
   - 單體（167）：一個飛行段 → 一個落點 → 一次 219。  
   - AOE（171）：一個飛行段飛向主目標（或中心）→ 落點段／連貫 218 應在 **每個受擊目標** 上各播一次。即：飛行段可只播一條；Op57 來時對 **每個 targetId** 在 targetEnt.Position 播 171 或 218（且帶 chainCallback），保證 3x3 每隻都有動畫。

4. **連貫與去重分離**  
   - 連貫：純粹「動畫播畢＋EffectChain 有下一段」→ 必呼叫 chainCallback，不因「是否己方」「是否已播過」而關閉。  
   - 去重：僅用於「同一 gfx 同一來源（己方先播＋伺服器再來）」時避免**重複建立**同一視覺，不應導致「不建立」而讓連貫無從觸發。

5. **日光術(2) 等**  
   維持以 SkillListData 取 castGfx，伺服器有無傳 gfx 都不影響客戶端播放，符合原則 5、6。

以上為對您七點原則的全面檢查與討論，**未進行任何程式修改**；後續若實作，應以「單一飛行－連貫邏輯＋本地必播」為方向，並消除「僅 167 為飛行」的例外與疊加修錯。
