# 攻擊系統重構方案 (Attack System Refactor)

結合以下故障與需求，提出一套**簡潔、可維護**的攻擊系統設計，供實作時依序套用。

---

## 一、待解決問題整理

| 情境 | 現象 | 根因（簡述） |
|------|------|--------------|
| 被連續攻擊 | 播僵硬後停止攻擊、按 Z 無效、無法移動 | IsActionBusy 長期 true → 整段戰鬥邏輯不跑；Z 因「已有任務」跳過；僵硬擋移動 |
| 攻擊 MISS 後走開 | 不再攻擊、按 Z 無法打其他怪 | Z 有任務就 skip，不重選目標；任務可能仍鎖定舊怪或無效目標 |
| 邏輯臃腫 | 多處重複判斷、狀態重疊 | _autoTarget 與 _currentTask.Target 並存；IsActionBusy、_attackInProgress、冷卻計時三重門檻 |

目標：**Z 一律可重選目標、攻擊只由「冷卻＋伺服器速率」節流、單一目標來源、減少重複判斷**。

---

## 二、設計原則

1. **Z 鍵語義**：Z =「以當前視野重新選一個最佳目標並開始攻擊」，不是「若已有目標就什麼都不做」。
2. **攻擊節流**：只由 **冷卻計時** 與 **SpeedManager** 決定能否發送攻擊封包，**不用 IsActionBusy 阻擋戰鬥迴圈**。
3. **單一目標來源**：目標只來自 `_currentTask.Target`，不再維護獨立的 `_autoTarget`。
4. **任務有效性**：每幀檢查當前任務目標是否仍有效（存在、未死亡）；無效則清掉任務，避免「鎖在空氣」上。

---

## 三、具體改動方案

### 3.1 Z 鍵：ScanForAutoTarget 改為「一律重掃並覆蓋攻擊目標」

**現狀**：已有 Attack 任務時直接 return，不掃描、不換目標。

**改為**：

- **一律執行**「範圍內找最佳目標」的掃描（與現有掃描邏輯相同）。
- 若**有找到**目標：
  - 若當前任務也是 Attack 且目標**同一個**：可選擇不重設（維持現狀）或仍呼叫 `StartAutoAttack(best)` 以重置狀態（建議：仍呼叫，確保目標實體引用更新）。
  - 若當前任務**不是** Attack，或目標**不同**：`EnqueueTask(new AutoTask(AutoTaskType.Attack, bestTarget))`，且若希望 Z 立即切換，可用 **Forced** 優先級清空佇列並置入新任務（即「Z = 強制換成這次掃到的那隻」）。
- 若**沒找到**目標：
  - **清除攻擊任務**：`FinishCurrentTask()`（或僅當當前是 Attack 時清除），讓下次按 Z 時不會再被「已有任務」擋住。
  - 不再呼叫 `PerformAttackOnce(0, ...)`（避免無目標時還進攻擊流程）。

**要點**：  
刪除「已有 Attack 任務就 return」的區塊；Z 每次都是「用這次掃描結果覆蓋/更新攻擊目標」，必要時用 Forced 清佇列，確保「按 Z 就能打眼前這隻」。

---

### 3.2 戰鬥迴圈：不再用 IsActionBusy 擋住整段 UpdateCombatLogic

**現狀**：`UpdateCombatLogic()` 開頭 `if (_myPlayer.IsActionBusy) return;`，導致被連續攻擊時整段不跑、永遠不執行攻擊。

**改為**：

- **移除** `UpdateCombatLogic()` 開頭的 `if (_myPlayer.IsActionBusy) return;`。
- 攻擊是否可執行，只由下列兩者決定：
  - **冷卻**：`HandleExecutingState` 內現有的 `_attackInProgress` + `_attackCooldownTimer`（冷卻到才設 `_attackInProgress = false`）。
  - **發包節流**：`PerformAttackOnce()` 內保留 **SpeedManager.CanPerformAction**，不通過就不發包、不播攻擊動畫。

**可選**：  
若希望「同一時間只播一個攻擊動畫」，可在 `ExecuteAttackAction` 內保留「若 IsActionBusy 則只 skip 本次、下一幀再試」，但**不要**在 UpdateCombatLogic 最外層用 IsActionBusy 擋掉整個迴圈。建議先拿掉外層阻擋，觀察是否還有「攻擊停住」再決定是否在 ExecuteAttackAction 做輕量檢查。

---

### 3.3 執行攻擊：PerformAttackOnce 僅保留 SpeedManager，不依賴 IsActionBusy

**現狀**：`PerformAttackOnce()` 開頭 `if (_myPlayer.IsActionBusy) return;`，與戰鬥迴圈雙重阻擋。

**改為**：

- **移除** `PerformAttackOnce()` 內的 `IsActionBusy` 判斷。
- **保留**：`SpeedManager.CanPerformAction`（伺服器端攻速）、`targetId == 0 && _isAutoAttacking` 的 return（無目標且自動攻擊中不發包）。
- 發包與播放攻擊動畫的條件 = 有目標 + SpeedManager 通過；不再看 IsActionBusy。

效果：被連續攻擊時，只要冷卻與 SpeedManager 允許，就能再次送出攻擊並播動畫，不會被「僵硬動畫未結束」卡死。

---

### 3.4 單一目標來源：用 _currentTask.Target 取代 _autoTarget

**現狀**：`UpdateCombatLogic` 內 `_autoTarget = task.Target`，後續 `ExecuteAttackAction`、`ExecutePickupTask` 等都用 `_autoTarget`。

**改為**：

- 不再給 `_autoTarget` 賦值；所有使用 `_autoTarget` 的地方改為使用 `_currentTask.Target`（或區域變數 `var target = _currentTask?.Target`）。
- 若專案其他處仍有讀取 `_autoTarget`（例如 UI、除錯），改為 `_currentTask?.Target` 或封裝成屬性 `CurrentAttackTarget => _currentTask?.Type == AutoTaskType.Attack ? _currentTask.Target : null`。
- 最終可刪除欄位 `_autoTarget`，避免雙重來源。

---

### 3.5 任務有效性：每幀檢查目標仍存在且未死亡

**現狀**：已有 `if (_autoTarget == null || !_entities.ContainsKey(_autoTarget.ObjectId) || (task.Type == AutoTaskType.Attack && _autoTarget.CurrentAction == GameEntity.ACT_DEATH))` 後呼叫 `FinishCurrentTask()`。

**改為**：

- 使用 `_currentTask.Target` 做同樣檢查（目標為 null、不在 _entities、或為攻擊任務且目標已死亡），不通過就 `FinishCurrentTask()` 並 return。
- 確保「怪物死亡 / 被刪除」後任務立刻清掉，不會出現「還在攻擊已刪除的 objId」或「按 Z 一直 skip 因為任務還在」。

---

### 3.6 MISS 與僵硬動畫（維持現有設計）

- **MISS（damage<=0）不播僵硬**：維持現狀，僅飄字，不呼叫 `SetAction(ACT_DAMAGE)`。
- **僵硬與移動**：是否在受擊僵硬時禁止移動（`DamageStiffnessBlocksMovement`）可另案調整；本方案不強制改動，只要求「攻擊迴圈不被 IsActionBusy 卡死」。

---

## 四、建議實作順序

1. **ScanForAutoTarget**：改為「一律掃描；有目標就 StartAutoAttack（或 Forced 入隊），無目標就清除攻擊任務」；刪除「已有 Attack 任務就 return」。
2. **UpdateCombatLogic**：移除開頭 `if (_myPlayer.IsActionBusy) return;`。
3. **PerformAttackOnce**：移除 `IsActionBusy` 判斷，保留 SpeedManager 與 targetId==0 的判斷。
4. **替換 _autoTarget**：全專案改為使用 `_currentTask?.Target`，最後刪除 `_autoTarget`。
5. **（可選）ExecuteAttackAction**：若仍希望避免「動畫疊加」，可保留對 IsActionBusy 的輕量檢查並只 skip 本幀，不影響整體迴圈。

---

## 五、預期效果

- **按 Z**：每次都會重掃並鎖定當前範圍內最佳目標（或清除目標），不再出現「按 Z 沒反應」。
- **攻擊 MISS 後走開再按 Z**：會掃到新怪並建立新任務，可正常攻擊其他怪物。
- **被連續攻擊時**：戰鬥迴圈仍會跑，冷卻與 SpeedManager 到就發送攻擊，不會因僵硬動畫而永久停攻。
- **程式**：單一目標來源、少一層 IsActionBusy 阻擋、邏輯路徑更短，後續除錯與擴充較簡單。

---

*本方案僅為設計與重構建議，實作時請依專案現有 TaskQueue、Movement、HitChain 等介面微調命名與呼叫處。*
