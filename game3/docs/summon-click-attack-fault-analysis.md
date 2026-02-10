# 召喚寵物點擊觸發自動攻擊 — 故障分析報告

**現象**：召喚後的寵物，用鼠標點擊寵物時，預期應開啟召喚指令視窗（moncom），實際卻對寵物發起自動攻擊。

**結論**：點擊時用來判斷「是否為己方召喚」的 `_mySummonObjectIds` 未包含該寵物的 ObjectId，因此走到「怪物」分支並建立 Attack 任務。根因是**己方召喚僅在收到 Opcode 79 時才加入 `_mySummonObjectIds`，而召喚物生成時未必有 Opcode 79，或存在時序差**。

---

## 1. 點擊與自動攻擊流程（當前邏輯）

### 1.1 鼠標點擊分發（`GameWorld.Input.cs` → `HandleInput`）

順序如下：

1. **強制攻擊**：Shift 或 (開關 + 雙擊) → `PerformAttackOnce`，return。
2. **點到實體** (`clickedEntity != null`)：
   - **若** `_mySummonObjectIds != null && _mySummonObjectIds.Contains(clickedEntity.ObjectId)`  
     → `OpenSummonTalkWindow(clickedEntity.ObjectId)`，return。✓ 正確行為
   - **否則若** `AlignmentHelper.IsMonster(clickedEntity.Lawful)`  
     → `EnqueueTask(AutoTaskType.Attack, clickedEntity)`，即**自動攻擊**。
   - **否則若** `AlignmentHelper.IsNpc(clickedEntity.Lawful)`  
     → TalkNpc 任務。
   - **否則**  
     → PickUp 任務。

因此：**只有當「點到的實體 ObjectId 在 `_mySummonObjectIds` 裡」時，才會開召喚視窗；否則會依 Lawful 被當成怪物/NPC/其他，召喚物多半 Lawful≤0，就會被當成怪物而觸發自動攻擊。**

---

## 2. `_mySummonObjectIds` 的填入與清除

### 2.1 何時「加入」

- **唯一寫入點**：`GameWorld.Npc.cs` → `OnPetStatusChanged(int petObjId, int status)`  
  - 僅當 `petObjId > 0` 時執行 `_mySummonObjectIds.Add(petObjId)`。
- **觸發來源**：`PacketHandler` 在解析到 **Opcode 79 (S_OPCODE_SUMMON_OWN_CHANGE)** 時呼叫 `ParseSummonStatus`，且封包內 **isMine == true** 時才 `EmitSignal(PetStatusChanged, petObjId, status)`。
- 亦即：**只有當客戶端收到「寵物狀態變更」封包且標記為「自己的寵物」時，該 petObjId 才會被加入 `_mySummonObjectIds`。**

### 2.2 何時「移除」

- **唯一移除點**：`GameWorld.Entities.cs` → `OnObjectDeleted(int objectId)`  
  - 開頭執行 `_mySummonObjectIds?.Remove(objectId)`。
- 因此：**只要該 ObjectId 被刪除過一次，就會從 `_mySummonObjectIds` 移除；若之後同一 ObjectId 再次生成（例如重生），若沒有再次收到 Opcode 79，就不會再被加回。**

---

## 3. 根因分析

### 3.1 召喚物生成時未必有 Opcode 79

- 召喚物出現在畫面上是由 **S_ObjectAdd (Opcode 11)** 產生：`ParseObjectAdd` → `ObjectSpawned` → `OnObjectSpawned` → 建立實體並加入 `_entities`。
- 伺服器 **S_ObjectAdd** 對 SummonInstance 會寫入 **own**（主人名，即 `o.getOwnName()`），客戶端解析為 `WorldObject.OwnerName`，但**客戶端目前沒有**：
  - 在 `OnObjectSpawned` 時根據 `OwnerName`（或任何「歸屬」資訊）把該 ObjectId 加入 `_mySummonObjectIds`，也沒有
  - 在 `GameEntity` 上保存 `OwnerName` 供點擊時使用。
- 因此「是否為己方召喚」**完全依賴** 是否曾收到 **Opcode 79** 且 isMine 為 true。
- 若伺服器在「召喚物首次生成」時**不發** Opcode 79，或只在「之後變更狀態」時才發，則該召喚物從生成到第一次收到 79 之前，**永遠不會** 被加入 `_mySummonObjectIds`，點擊就會被當成一般怪物 → 自動攻擊。

### 3.2 時序差（競態）

- 若伺服器會發 Opcode 79，但順序是：先發 **S_ObjectAdd**（實體先出現），後發 **Opcode 79**（才標記為己方寵物），則中間會有一段時間該寵物已在 `_entities` 但**尚未**在 `_mySummonObjectIds`。
- 用戶若在這段時間內點擊寵物，同樣會走到 `IsMonster` 分支而觸發自動攻擊。

### 3.3 刪除後重生（與你提供的日誌一致）

- 日誌中有：`[RX] ObjectDeleted objId=5908874`，之後 `[RX] Spawn Obj: name $318 (obj ID:5908874)`。
- 流程是：
  1. 刪除時：`OnObjectDeleted(5908874)` → `_mySummonObjectIds.Remove(5908874)`，該 id 從「己方召喚」集合中移除。
  2. 同一 ObjectId 再次生成時：只會再走一次 `OnObjectSpawned`，**不會**自動再加回 `_mySummonObjectIds`（除非再次收到 Opcode 79 且 isMine）。
- 因此：**刪除後重生的召喚物，若伺服器沒有再次發 Opcode 79，點擊時一定不在 `_mySummonObjectIds`，就會被當成怪物而自動攻擊。**

### 3.4 召喚物的 Lawful 導致被當成怪物

- `AlignmentHelper.IsMonster(lawful)`：先排除 NPC（lawful==1000 等），再判斷 `lawful <= 0` 即視為怪物。
- 召喚物多半為 **Lawful = 0**（或類似中立/怪物值），因此只要**沒有**先被 `_mySummonObjectIds` 攔截，就會被當成怪物並建立 Attack 任務。

---

## 4. 故障鏈總結

| 步驟 | 說明 |
|------|------|
| 1 | 召喚物生成或重生後，要麼從未收到 Opcode 79，要麼 79 晚於用戶點擊。 |
| 2 | 因此該寵物的 ObjectId **不在** `_mySummonObjectIds`。 |
| 3 | 點擊時 `_mySummonObjectIds.Contains(clickedEntity.ObjectId)` 為 false。 |
| 4 | 進入 `AlignmentHelper.IsMonster(clickedEntity.Lawful)`，召喚物 Lawful≤0 → true。 |
| 5 | 執行 `EnqueueTask(AutoTaskType.Attack, clickedEntity)` → **對寵物發起自動攻擊**。 |

---

## 5. 建議修復方向（僅供確認，尚未改碼）

以下任一路徑都可消除「點召喚寵物卻變成自動攻擊」的現象，可擇一或組合使用：

### A. 在生成時就依「歸屬」標記己方召喚（推薦）【已實作】

- 在 **OnObjectSpawned** 中，若 `WorldObject` 的 **OwnerName** 非空且等於**己方角色名**（`IsOwnerNameMine(data.OwnerName)`，與 `Boot.Instance.CurrentCharName` 寬鬆比對），則將 `data.ObjectId` **加入** `_mySummonObjectIds`。
- 這樣不依賴 Opcode 79 是否在生成時發送，只要 S_ObjectAdd 帶了正確的主人名，點擊時就會被識別為己方召喚而開 moncom。
- 實作位置：`Client/Game/GameWorld.Entities.cs` → OnObjectSpawned（新實體加入 _entities 後）、輔助方法 `IsOwnerNameMine(string ownerName)`。

### B. 伺服器保證在召喚物生成時發 Opcode 79

- 若伺服器在 SummonInstance 第一次加入世界、或每次 S_ObjectAdd 送給主人時，**同時（或先於/緊接）** 對該客戶端發送 Opcode 79 且 isMine=true，則客戶端會在實體出現後很快把該 ObjectId 加入 `_mySummonObjectIds`，減少時序差。
- 刪除後重生：若同一 ObjectId 再次 S_ObjectAdd，伺服器同樣需要再發一次 Opcode 79（isMine=true），客戶端才會再次加入 `_mySummonObjectIds`。

### C. 點擊時用 OwnerName 當後備判斷（需在實體上保留 OwnerName）

- 在 **GameEntity.Init** 時把 `data.OwnerName` 存到實體（例如 `GameEntity.OwnerName`）。
- 在 **HandleInput** 中，在「召喚集合」判斷之後、**IsMonster 之前**，增加：若 `clickedEntity.OwnerName` 非空且等於己方角色名，則視為己方召喚 → `OpenSummonTalkWindow(clickedEntity.ObjectId)` 並 return。
- 效果：即使該 ObjectId 從未進過 `_mySummonObjectIds`（例如從未收過 79），只要 S_ObjectAdd 有帶主人名，點擊仍會開 moncom 而不會攻擊。

---

## 6. 相關程式位置（便於對照與日後修改）

| 項目 | 檔案與位置 |
|------|------------|
| 點擊分發、召喚檢查、IsMonster 分支 | `Client/Game/GameWorld.Input.cs` → `HandleInput`（約 161–185 行） |
| 召喚集合唯一寫入 | `Client/Game/GameWorld.Npc.cs` → `OnPetStatusChanged` |
| 召喚集合移除 | `Client/Game/GameWorld.Entities.cs` → `OnObjectDeleted` |
| Opcode 79 解析與 PetStatusChanged | `Client/Network/PacketHandler.cs` → case 79、`ParseSummonStatus` |
| 實體生成、可在此處依 OwnerName 加入 _mySummonObjectIds | `Client/Game/GameWorld.Entities.cs` → `OnObjectSpawned` |
| 實體初始化、可在此處保存 OwnerName | `Client/Game/GameEntity.cs` → `Init(WorldObject data, ...)` |
| S_ObjectAdd 的 own 欄位 | 伺服器 `server/network/server/S_ObjectAdd.java`（writeS(own)）；客戶端 `PacketHandler.ParseObjectAdd` → `obj.OwnerName` |
| 怪物判定 | `Client/Utility/AlignmentHelper.cs` → `IsMonster(lawful)` |

---

**報告結束。請先確認上述分析與修復方向後，再進行代碼修改。**
