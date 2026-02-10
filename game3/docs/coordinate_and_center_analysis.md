# 角色動畫中心與座標對齊 — 全面分析與修改建議

> 先全面分析、提出方案與日誌驗證建議，**暫不直接改邏輯代碼**。

---

## 一、問題 1：角色中心未固定於畫面中央（走出中心再退回）

### 1.1 PakBrowser 為何能「中心永遠在畫面中央」

- **結構**：`previewContainer` → `centerNode`（`SetAnchorsPreset(LayoutPreset.Center)`）→ `charRoot`（`Node2D`，無設 Position，預設 0,0）→ `_bodySprite`（`AnimatedSprite2D`）。
- **關鍵**：**charRoot 從不移動**；鏡頭也不跟隨。角色視覺的「原點」就是 centerNode 的錨點（畫面中央）。
- **AnimatedSprite2D**：PakBrowser 未改 `Centered`，預設為 `true`，紋理中心在節點原點，所以視覺中心一直對齊畫面中心。

### 1.2 遊戲內角色播放的差異（可能原因）

- **結構**：`GameWorld` → `GameEntity`（會移動）→ Camera 掛在 Entity 上（`_camera.Reparent(entity)`，`_camera.Position = Vector2.Zero`）。
- **邏輯**：Entity 的 `Position` 會隨 `SetMapPosition` / Tween 改變；Camera 跟隨 Entity，所以理論上「Entity 的原點 = 畫面中心」。
- **可能導致「走出中心再退回」的點**：
  1. **Entity.Position 的計算不一致**（見下節）：新生成用「格座標×CELL_SIZE」，移動用「(格 - origin + 0.5)×GRID_SIZE」，差半格（16px）且與 CurrentMapOrigin 是否一致未統一，會造成實體與鏡頭/地圖不同步，看起來像先偏離再拉回。
  2. **Sprite 的錨點與 Entity 原點不同步**：GameEntity 使用 `Centered = false` + `Offset = (dx, dy)`。若 (dx, dy) 每幀不同（走路動畫），且有一幀漏更新或順序錯，視覺腳底會短暫偏離 Entity 的 (0,0)，看起來像輕微「走出再退回」。
  3. **Tween 與視覺不同步**：Tween 改的是 `this.Position`（Entity），若同一幀內先更新 Position 再更新 Sprite 的 Offset，或反序，可能有一幀的錯位。

**建議驗證（僅加日誌）**：

- 在 `GameEntity.Movement.SetMapPosition` 結尾打 log：`(MapX, MapY)`, `targetPos`, `Position`（更新後）。
- 在 `GameEntity.Visuals.UpdateLayerOffset` 打 log：每幀 `(dx, dy)`, `finalPos`（可採樣，例如每 N 幀一次），確認是否每幀都正確套用且與 list.spr 一致。
- 在 `SetupPlayerCamera` 後打 log：`entity.Position`、`entity.GlobalPosition`、`_camera.GlobalPosition`，確認相機是否真的跟隨 Entity 原點。

---

## 二、問題 2：座標與定位不統一（攻擊時角色在 A、怪物在 B 或面向 B）

### 2.1 目前存在的三套 Position / 格→世界 公式

| 位置 | 公式 | 說明 |
|------|------|------|
| **GameWorld.Entities.OnObjectSpawned**（新實體） | `Position = (data.X * CELL_SIZE, data.Y * CELL_SIZE)` | 絕對格座標×32，**未減 CurrentMapOrigin**，**無 +0.5（格心）** |
| **GameWorld.Entities.OnObjectSpawned**（已存在實體） | 先設 `Position = data.X*CELL_SIZE`，再呼叫 `SetMapPosition(data.X, data.Y, …)` | 第一次設錯，第二次被 SetMapPosition 覆寫成正確公式 |
| **GameEntity.Movement.SetMapPosition** | `localX = (x - origin.X + 0.5f) * GRID_SIZE` | 相對 CurrentMapOrigin，**有 +0.5（格心）** |
| **GameWorld.SkillEffect.ConvertGridToWorld** | `(gx - origin.X) * 32 + 16` | 相對 CurrentMapOrigin，**+16 即半格（格心）** |

因此：

- **新生成實體**：只被設成 `(X*32, Y*32)`，從未用過 CurrentMapOrigin 與 +0.5，與 SetMapPosition / ConvertGridToWorld **不一致**。
- 當 `CurrentMapOrigin = (0,0)` 時，同一格 (x,y)：
  - 新生成：`Position = (x*32, y*32)`
  - SetMapPosition：`Position = (x*32+16, y*32+16)`
  - 差 **16 像素**（半格），會導致「實體位置」與「地圖/技能/攻擊用的世界座標」對不齊。

### 2.2 攻擊與目標相關流程

- **PerformAttackOnce(targetId, targetX, targetY)**：用 `targetX, targetY` 發包並呼叫 `PlayAttackAnimation(targetX, targetY)`。
- **PlayAttackAnimation(tx, ty)**：用 `GetHeadingTo(tx, ty)` 以**當前** `MapX, MapY` 與 `(tx, ty)` 算朝向。
- 若傳入的 `(targetX, targetY)` 是**攻擊當下**目標的 `MapX, MapY`，且目標之後又移動，則：
  - 攻擊者會朝「攻擊瞬間」的目標格面向，視覺上可能與「現在站在 B 的怪物」不一致（怪物在 B、角色朝 A）。
- **OnObjectAttacked(attackerId, targetId, …)**：只帶 `targetId`，做 `attacker.PrepareAttack(targetId, damage)`；命中關鍵幀時用 `targetId` 找實體扣血。若伺服器與客戶端對「誰在哪一格」的認知一致，邏輯上應正確；但若 **Position 公式不統一**，實體畫在錯誤像素位置，就會出現「角色在 A 打、怪物卻在 B 動/面向 B」的錯覺。

### 2.3 其他與座標相關的點

- **GameWorld.Input**：點選判定用 `entity.GlobalPosition` 與 `GetGlobalMousePosition()`，若 Entity.Position 與地圖不同步，點選會錯格。
- **GameWorld.SkillEffect**：`attackerPos` / `endPos` 用 `atkEnt.Position`、`tgtEnt.Position` 或 `ConvertGridToWorld`；若 Position 與 ConvertGridToWorld 公式不一致，魔法起點/終點會偏。
- **GameWorld.Combat.GetBestNeighborPosition**：用 `MapX, MapY`（格），未直接依 Pixel Position，邏輯正確，但若實體實際畫在錯的格（因 Position 錯），追擊/攻擊距離判斷會與視覺不符。

---

## 三、修改建議方案（原則：對齊 PakBrowser 的「中心固定」思路 + 座標單一來源）

### 3.1 座標與 Position 統一（優先）

- **唯一公式**（建議與 SetMapPosition / ConvertGridToWorld 一致）：
  - 世界像素 = `(mapX - CurrentMapOrigin.X + 0.5f) * CELL_SIZE`（同 Y）。
  - 即格心對齊像素中心（半格偏移）。
- **具體改動建議**（實施前可先加日誌驗證）：
  1. **GameWorld.Entities.OnObjectSpawned**（新實體）：
     - 不要只設 `entity.Position = (data.X * CELL_SIZE, data.Y * CELL_SIZE)`。
     - 改為：`entity.SetMapPosition(data.X, data.Y, data.Heading)`，讓 Position 與現有移動/技能完全同一公式。
  2. **GameWorld.Entities.OnObjectSpawned**（已存在實體）：
     - 刪除第一行的 `existingEntity.Position = ...`，只保留 `SetMapPosition(data.X, data.Y, data.Heading)`，避免重複且不一致的賦值。
- 這樣可消除「新實體差 16px」「存在實體被設兩次」的問題，並與 ConvertGridToWorld、技能、攻擊的格→世界對齊。

### 3.2 角色「中心固定」在畫面的對齊方式（對齊 PakBrowser 思路）

- **原則**：像 PakBrowser 一樣，讓「角色邏輯原點（腳底/錨點）」永遠對應「鏡頭中心」。
- 目前設計已是：Camera 掛在 Entity 上且 Position=0，所以 Entity 的 (0,0) 就是畫面中心。要保證的是：
  1. **Entity.Position 唯一由上述統一公式計算**（見 3.1），且新生成與移動都用同一套。
  2. **Sprite 錨點與 Entity (0,0) 一致**：維持 `Centered = false`，`Offset = (dx, dy)` 表示「紋理左上相對於錨點的偏移」，使錨點落在 Entity 的 (0,0)；確保每幀都正確呼叫 `UpdateLayerOffset`（OnMasterFrameChanged 已綁 FrameChanged），且無漏幀。
- 若 3.1 做完後仍有「走出再退回」，再針對「同一幀內 Position 與 Offset 更新順序」或「Tween 與視覺的幀同步」加日誌排查。

### 3.3 攻擊目標與面向

- **PerformAttackOnce / PlayAttackAnimation**：確保傳入的 `(targetX, targetY)` 一律是**當前**目標的 `target.MapX`, `target.MapY`（或從 target 即時讀取），不要用舊的快取格座標。
- 伺服器若在攻擊後才發送目標移動（OnObjectMoved），客戶端已用「攻擊瞬間」的目標格面向，屬正常；重點是 **Position 統一** 後，目標實體畫在正確格上，才不會出現「角色在 A、怪物在 B」的錯覺。

### 3.4 建議的日誌驗證（先不加邏輯改動）

1. **Position 一致性**  
   - 在 `OnObjectSpawned`（新實體）：log `data.X, data.Y`、目前設的 `entity.Position`。  
   - 在 `SetMapPosition`：log `(x, y)`, `CurrentMapOrigin`, `targetPos`。  
   - 在 `ConvertGridToWorld`：log `(gx, gy)`, `CurrentMapOrigin`, 回傳的 world。  
   - 比對：同一格 (gx, gy) 經 SetMapPosition 得到的 Position，與 ConvertGridToWorld(gx, gy) 是否一致。

2. **新實體是否差半格**  
   - 生成後立刻 log 該實體 `Position` 與 `SetMapPosition(data.X, data.Y, data.Heading)` 若被呼叫時會得到的 targetPos，確認是否差 16px。

3. **攻擊目標格**  
   - 在 `PerformAttackOnce` 或呼叫處 log `targetId`, `targetX`, `targetY`，以及從 `_entities[targetId]` 取出的 `MapX, MapY`，確認是否一致。

4. **鏡頭與 Entity 原點**  
   - 在 `_Process` 或每數幀一次：若為玩家，log `_myPlayer.Position`, `_camera.GlobalPosition`，確認相機是否緊跟 Entity 原點。

---

## 四、小結

| 問題 | 可能原因 | 建議 |
|------|----------|------|
| 角色走出中心再退回 | 1) Position 公式不一致（新實體少 +0.5 格）<br>2) 每幀 Offset 或更新順序導致視覺與邏輯原點短暫分離 | 先統一 Position 公式（3.1），再視需要加日誌查 Offset/順序 |
| 角色在 A 打、怪物在 B 動/面向 B | 1) 新實體/已存在實體 Position 與 SetMapPosition/ConvertGridToWorld 不一致<br>2) 攻擊面向用錯或舊的目標格 | 統一格→世界公式並讓新實體也走 SetMapPosition；確認攻擊用當前目標 MapX, MapY |

**建議執行順序**：先按「3.4 日誌驗證」加 log，跑一輪確認上述差異與半格偏移是否存在，再依「3.1 → 3.2 → 3.3」實施修改，避免一次大改難以除錯。
