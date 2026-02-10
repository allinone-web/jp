# 掉落物拾取、Z 鍵、滑鼠邏輯 — 衝突與漏洞分析

以下為**純分析**，不包含修改。請確認後再進行修復。

---

## 1. 預期行為（您描述的「正確」邏輯）

1. **滑鼠**：可**點中**目標（鎖定）或**點空地**移動，點其他目標則**取消**前一個、改鎖定新目標。
2. **Z 鍵**：控制**自動鎖定選擇、追擊、自動攻擊**（對怪物）。
3. **若已選中金幣（地面物品）再按 Z**：應**自動走過去 → 到達距離 → 自動拾取**（或等同「點擊拾取」的結果）。

---

## 2. 目前實作摘要

### 2.1 滑鼠左鍵（HandleInput）

- **GetClickedEntity(mousePos)**：從 `_entities` 找距離 < 40 且 **HpRatio > 0** 的最近實體（排除自己）。
- **點到實體時**：
  - 召喚/寵物 → 開 TalkWindow，return。
  - **IsMonster** → 提示「目标锁定: {RealName}」，**EnqueueTask(Attack)**，`_isAutoAttacking = true`。
  - **IsNpc** → **EnqueueTask(TalkNpc)**，`_isAutoAttacking = true`。
  - **其餘（含地面物品）** → **EnqueueTask(PickUp)**，`_isAutoAttacking = true`。
- **點空地** → StopAutoActions()，**StartWalking(格座標)**。

因此：**點金幣**會進入「其餘」分支，正確排入 **PickUp** 任務；不會顯示「目标锁定」（該訊息僅怪物）。您說的「鎖定金幣」應是指「已選中金幣（當前任務為 PickUp）」的狀態。

### 2.2 任務與更新迴圈（UpdateCombatLogic）

- **GetCurrentTask()**：從 `_taskQueue` 取一筆，成為 `_currentTask`（Attack / PickUp / TalkNpc）。
- **ExecutePickupTask()**：若與目標距離 > 1 則 **StartWalking** 走向物品；距離 ≤ 1 則發送拾取封包、**FinishCurrentTask()**。

因此：**只選金幣、不按 Z** 時，理論上會自動走向金幣並在距離 ≤ 1 時拾取。若「沒有走過去」，可能是被其他邏輯覆蓋（見下）。

### 2.3 Z 鍵（ScanForAutoTarget）

- **行為**：**一律**在範圍內找「可攻擊目標」（HpRatio>0、ShouldShowHealthBar()、非己方召喚），取最佳一隻，然後：
  - 若有找到 → **EnqueueTask(Attack, bestTarget, Forced)** → **清空佇列、設為攻擊任務**，`_isAutoPickup = false`。
  - 若沒找到 → 僅當 **當前任務是 Attack** 時 **FinishCurrentTask()**；若當前是 **PickUp** 則**不處理**（不清除 PickUp）。

因此：

- **已選金幣（當前任務 = PickUp）時按 Z**：
  - 若**範圍內有怪物** → Z 會**強制覆蓋**成攻擊任務，PickUp 被清掉 → **不會走向金幣、不會拾取**，與「Z = 執行當前拾取」的預期不符。
  - 若**範圍內無怪物** → PickUp 仍保留，UpdateCombatLogic 會繼續執行 **ExecutePickupTask**，會走向金幣並拾取。

### 2.4 魔法目標（UseMagic）

- **attack 型技能**：目標來源為  
  `GetCurrentAttackTarget() ?? GetCurrentTaskTarget()`  
  - GetCurrentAttackTarget()：僅當 **當前任務為 Attack** 時回傳該目標，否則 null。
  - GetCurrentTaskTarget()：**當前任務的 Target**（不論 Attack / PickUp / TalkNpc）。
- 因此當**當前任務是 PickUp（金幣）**時：
  - GetCurrentAttackTarget() = null
  - GetCurrentTaskTarget() = **金幣實體**
  - 會把 **targetId = 金幣 ObjectId** 傳給 UseMagic。
- 之後 **IsValidMagicTarget(金幣, forAttack: true)**：金幣 **ShouldShowHealthBar() == false**（地面物品），判定為**無效** → 顯示「**請先點選目標或按 Z 選怪**」並 return。

因此：**已選金幣後再放攻擊法術**，會出現「請點選目標或按 Z 選怪」；邏輯上等於「誤把拾取目標當成法術目標 → 驗證失敗 → 提示選怪」。

---

## 3. 衝突與漏洞整理

| 編號 | 現象 | 原因 | 預期 |
|------|------|------|------|
| **A** | 已選金幣後按 Z，沒有走向金幣拾取，反而去打怪 | Z 一律執行 **ScanForAutoTarget()**，若有怪物則 **Forced** 覆蓋任務，PickUp 被清掉 | 已選金幣時，Z 應「執行當前拾取」（走向金幣並拾取），不應被 Z 改成攻擊怪 |
| **B** | 已選金幣後放攻擊法術，出現「請先點選目標或按 Z 選怪」 | attack 型技能用 GetCurrentTaskTarget() 取得目標，當前任務為 PickUp 時拿到金幣；金幣不是有效魔法目標 → 驗證失敗並提示 | 攻擊法術不應把「拾取目標」當成法術目標；應視為無攻擊目標並提示，或只認「攻擊任務目標」 |

---

## 4. 建議修復方向（供您確認後再動程式）

- **不刪除、不改動**任何「不可以刪除」的註解與對應邏輯；僅在既有架構下做最小改動。

### 4.1 漏洞 A：Z 與拾取任務並存

- **建議**：在 **ScanForAutoTarget()** 開頭增加條件 —  
  若 **當前任務不為 null 且 Type == PickUp**，則 **直接 return**，不掃怪、不覆蓋任務。  
- 效果：已選金幣（PickUp）時按 Z，不覆蓋任務，UpdateCombatLogic 繼續執行 **ExecutePickupTask**，會走向金幣並在距離 ≤ 1 時拾取。  
- 不動：Z 在「當前無任務」或「當前為 Attack/TalkNpc」時行為不變（仍為掃怪/設攻擊任務或清除攻擊任務）。

### 4.2 漏洞 B：攻擊法術誤用拾取目標

- **建議**：在 **UseMagic** 中，**attack 型**（及「其餘」需攻擊目標）取得目標時：  
  僅在 **GetCurrentTaskTarget() 不為 null 且為有效魔法目標**（例如 **IsValidMagicTarget(taskTarget, true)**）時，才把 **GetCurrentTaskTarget()** 當成 targetId 來源；否則 attack 型**只認 GetCurrentAttackTarget()**。  
- 效果：當前任務為 PickUp（金幣）時，不會把金幣當成法術目標，不會觸發「請先點選目標或按 Z 選怪」的誤導；若沒有攻擊目標則直接提示選怪。  
- 不動：buff 型仍可用 GetCurrentTaskTarget() ?? GetCurrentAttackTarget() 等既有邏輯（治癒、buff 對玩家/怪物）。

---

## 5. 其他已確認、無需改動的點

- **GetClickedEntity** 排除 **HpRatio <= 0**：地面物品預設 HpRatio=100，不會被排除，點金幣可正確得到 clickedEntity，進入 PickUp 分支。
- **ExecutePickupTask**：距離 > 1 會 StartWalking，≤ 1 會發送拾取封包並 FinishCurrentTask()，邏輯正確。
- **不可刪除的註解與邏輯**：ScanForAutoTarget 內「排除死亡／排除地面物品／排除己方召喚」等註解與對應程式皆保留，僅在**前緣**增加「若當前為 PickUp 則 return」。

---

請您確認：  
1) 上述行為與預期是否一致；  
2) **4.1 / 4.2** 的修復方向是否同意；  
3) 若同意，再進行實際程式修改（最小改動、不刪既有不可刪註解與程式）。
