# 故障記錄 (Fault Records)

本文件記錄由日誌分析得出的故障原因，供後續一併修復時參考。**暫不修改代碼**，僅記錄根因與相關程式位置。

---

## 故障：被攻擊播放僵硬動畫後，角色停止攻擊、按 Z 無法攻擊、無法移動

**現象：**
- 被怪物連續攻擊時，會正確播放受擊僵硬動畫（SetAction(DAMAGE)+Stutter）。
- 之後角色不再發動攻擊（自動攻擊停止）。
- 按 Z 鍵無反應（無法攻擊）。
- 無法移動。

**日誌特徵：**
- 大量 `[HitChain] Target 10002 SetAction(DAMAGE)+Stutter done`（玩家被多次設為 DAMAGE）。
- `[Combat-Diag] PerformAttackOnce entered targetId=6474984 IsActionBusy=False _attackInProgress=True`：曾有一次攻擊送出並播動畫；之後再次進入時出現 `[SpeedManager] Attack too fast, packet blocked`。
- 按 Z 時：`[Combat-Diag] Z / action_attack pressed -> ScanForAutoTarget` → `[Combat-Diag] ScanForAutoTarget: already have Attack task, skip`（每次都因「已有攻擊任務」而直接跳過）。

---

### 根因分析

#### 1. 按 Z 無效（無法攻擊／無法換目標）

**原因：**  
`ScanForAutoTarget()` 在「已有攻擊任務」時會直接 return，不允許重新選目標或強制攻擊。

**程式位置：**  
`Client/Game/GameWorld.Combat.cs` — `ScanForAutoTarget()` 開頭：

```csharp
if (_currentTask != null && _entities.ContainsKey(_currentTask.Target.ObjectId))
{
    if (_currentTask.Type == AutoTaskType.Attack) { ... return; }  // 已有攻擊任務就跳過
}
```

**結果：**  
只要 `_currentTask` 仍是 Attack（例如目標 6474984），按 Z 只會觸發 ScanForAutoTarget，然後立刻「already have Attack task, skip」，不會取消任務、不會重新掃描、也不會執行攻擊。使用者無法用 Z 打斷當前目標或重新鎖定。

---

#### 2. 自動攻擊停止（角色不再出刀）

**原因：**  
戰鬥更新在「玩家 IsActionBusy」時整段跳過，因此永遠不會執行到「執行攻擊」的邏輯。

**程式位置：**  
`Client/Game/GameWorld.Combat.cs` — `UpdateCombatLogic()` 開頭（約 154 行）：

```csharp
if (_myPlayer.IsActionBusy) return;  // 玩家忙碌則整段戰鬥邏輯不跑
```

- `IsActionBusy` 僅在 **動畫播完** 時由 `OnUnifiedAnimationFinished()` 設為 `false`（`GameEntity.Action.cs`）。
- 被連續攻擊時，每次命中都會 `SetAction(ACT_DAMAGE)`，DAMAGE 為單次動畫並會設 `_isActionBusy = true`。
- 若在僵硬動畫播完前又中下一擊，會再次 `SetAction(DAMAGE)`，再度把 `_isActionBusy` 設為 true，動畫可能被重啟或覆蓋。
- 因此多數幀都會是 `IsActionBusy == true`，`UpdateCombatLogic` 一進來就 return，不會執行 `ExecuteAttackTask` → `ExecuteAttackAction` → `PerformAttackOnce`，所以不會再送出攻擊封包、也不會播攻擊動畫。

**結果：**  
「被連續攻擊 + 僵硬動畫」導致 IsActionBusy 長期為 true，戰鬥迴圈從不執行攻擊，角色看起來就「停止攻擊」。

---

#### 3. 無法移動

**原因：**  
移動邏輯在「受擊僵硬」期間會直接 return，禁止移動。

**程式位置：**  
`Client/Game/GameWorld.Movement.cs`：

- `UpdateMovementLogic`（約 25–26 行）：  
  `if (GameEntity.DamageStiffnessBlocksMovement && _myPlayer.IsInDamageStiffness) return;`
- `StartWalking`（約 63 行）：  
  `if (GameEntity.DamageStiffnessBlocksMovement && _myPlayer != null && _myPlayer.IsInDamageStiffness) return;`

`IsInDamageStiffness` = `_currentRawAction == ACT_DAMAGE`（`GameEntity.cs`）。

**結果：**  
只要當前動作是 ACT_DAMAGE（僵硬），就無法移動。被連續攻擊時會反覆被設成 DAMAGE，或 DAMAGE 狀態持續過久，導致長時間無法移動。

---

### 因果鏈總結

| 現象 | 直接原因 | 相關程式 |
|------|----------|----------|
| 按 Z 無效 | 已有 Attack 任務時 ScanForAutoTarget 直接 return，不重選目標、不取消任務 | `GameWorld.Combat.cs` ScanForAutoTarget |
| 自動攻擊停止 | UpdateCombatLogic 因 IsActionBusy 整段 return，從不執行 ExecuteAttackAction | `GameWorld.Combat.cs` UpdateCombatLogic |
| IsActionBusy 長期 true | 被連續攻擊 → 反覆 SetAction(DAMAGE) → 僵硬動畫重啟/覆蓋，動畫結束訊號無法穩定解鎖 | `GameEntity.Action.cs` SetAction / OnUnifiedAnimationFinished |
| 無法移動 | 受擊僵硬期間禁止移動（DamageStiffnessBlocksMovement + IsInDamageStiffness） | `GameWorld.Movement.cs` UpdateMovementLogic, StartWalking |

**惡性循環：**  
有攻擊任務 → 被怪物連續打 → 反覆 DAMAGE → IsActionBusy 常為 true → 戰鬥邏輯不跑 → 不攻擊；同時僵硬 → 不能移動；按 Z 又因「已有任務」被忽略 → 無法取消或換目標，形成卡死。

---

### 後續修復方向（僅記錄，暫不實作）

1. **Z 鍵行為：**  
   當已有 Attack 任務時，可考慮：允許 Z 取消當前任務並重新掃描；或 Z 作為「強制攻擊當前目標一次」而不被「already have task」擋住。需與既有 TaskQueue 設計一致。

2. **被連續攻擊時的 IsActionBusy：**  
   - 選項 A：DAMAGE 動畫在「被新一次 DAMAGE 覆蓋」時，仍保證在適當時機解除 _isActionBusy（例如重設時先解鎖再上鎖，或縮短/不重啟動畫）。  
   - 選項 B：連續受擊時對 DAMAGE 的 SetAction 做節流或合併，避免動畫與 busy 狀態不斷被重設，讓 OnUnifiedAnimationFinished 有機會執行。

3. **移動與僵硬：**  
   若希望「被圍毆時仍可移動」，可考慮調整 DamageStiffnessBlocksMovement 或受擊時的移動阻斷條件（例如僅在單次僵硬短時間內禁止移動）；目前先保留現狀，與上述兩點一併評估後再改。

---

*記錄時間：依使用者提供之日誌分析。待後續測試與最終故障確認後，再一併修改代碼。*
