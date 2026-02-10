# 伺服器 Opcode 104 (S_ObjectHitratio) 血量百分比規則

## 封包格式 (Server → Client)

| 欄位 | 類型 | 說明 |
|------|------|------|
| opcode | 1 byte | 104 |
| objectId | 4 bytes (writeD) | 對象 ID |
| ratio | 1 byte (writeC) | 0–100 = 血量百分比；255 = 關閉/不顯示 |

- **ratio 計算**：`(int)(currentHp / totalHp * 100.0)`（見 `S_ObjectHitratio.java`）

---

## 伺服器何時發送 104

### 1. 對象進入視野 (L1Object.addObject)

- **條件**：**本 PC** 的 `hpBar == true`，且有一個**新對象 o** 加入本 PC 的已知列表。
- **動作**：對**本 PC** 發送 `S_ObjectHitratio(o, true)`。
- **效果**：怪物/NPC 進入視野時，若玩家已開啟血條，會收到該對象的 104（進場時一次）。

**程式位置**：`server/world/object/L1Object.java` 約 836–837 行：

```java
if (this.hpBar)
  SendPacket(new S_ObjectHitratio(o, true));
```

---

### 2. 攻擊命中 (PcInstance.toAttack)

- **條件**：**攻擊者** 為 PC，且 **攻擊者** `isHpBar() == true`，且對目標造成傷害後。
- **動作**：對**攻擊者** 發送 `S_ObjectHitratio(target, true)`。
- **效果**：你攻擊怪物時，若你的 `hpBar` 為 true，每次命中後會收到該目標的 104。

**程式位置**：`server/world/instance/PcInstance.java` 約 644–645 行：

```java
if (isHpBar())
  SendPacket(new S_ObjectHitratio(target, true));
```

---

### 3. HP 變動時廣播 (Character.setCurrentHp)

- **條件**：`Config.BROADCAST_HP_TO_AROUND == true`，且受傷對象為 **MonsterInstance** 或 **PcInstance**。
- **動作**：對「在該對象 getObjectList() 內、且 `isHpBar() == true` 的**每一個 PC**」發送 `S_ObjectHitratio(this, true)`。
- **效果**：怪物（或 PC）扣血時，周圍有開血條的玩家都會收到該對象的 104。

**程式位置**：`server/world/object/Character.java` 約 134–139 行：

```java
if ((Config.BROADCAST_HP_TO_AROUND) && (((this instanceof MonsterInstance)) || ((this instanceof PcInstance))))
  for (L1Object obj : getObjectList())
    if (((obj instanceof PcInstance)) && (obj.isHpBar()))
      obj.SendPacket(new S_ObjectHitratio(this, true));
```

---

### 4. 自己 HP 更新 (Character.setCurrentHp + Config.SHOW_OW_HPBAR)

- **條件**：`Config.SHOW_OW_HPBAR == true`，且對象為 **PcInstance**（自己）。
- **動作**：對**自己**發送 `S_ObjectHitratio(pc, true)`。
- **效果**：自己扣血/補血時，會收到自己的 104（用於自己頭頂血條）。

**程式位置**：`server/world/object/Character.java` 約 124–127 行。

---

### 5. GM 指令 (GmCommand.hpbar)

- **條件**：GM 執行 `.hpbar on` / `.hpbar off`。
- **動作**：`pc.setHpBar(isOn)`，並對該 PC 發送目前視野內所有 Character 的 104（或 255）。
- **效果**：GM 可強制開關該玩家的 `hpBar` 並刷新一次 104。

---

## PC 的 hpBar 如何設定

- **預設**：`L1Object.hpBar` 為 **false**。
- **目前**：僅能透過 **GM 指令** `.hpbar on` / `.hpbar off` 呼叫 `pc.setHpBar(isOn)`。
- **客戶端同步**：客戶端可發送 **Opcode 13 (C_ClientOption)**，`type=3` 表示血條開關，`onoff=1` 開、`onoff=0` 關；伺服器在 `C_ClientOption` 中對 `type==3` 呼叫 `pc.setHpBar(onoff == 1)`，即可與客戶端「怪物血條」選項同步。

---

## 客戶端對應規則

1. **收到 104**：以 `objectId` 找到實體。若 `ratio == 255`：不更新數值，且對非玩家實體呼叫 `SetHealthBarVisible(false)`；否則呼叫 `SetHpRatio(ratio)` 並依是否為玩家／`ClientConfig.ShowMonsterHealthBar` 設定血條可見性。
2. **怪物血條顯示**：依本端設定 `ClientConfig.ShowMonsterHealthBar` 決定是否顯示頭頂血條；顯示時數值以**最後一次收到的 104 的 ratio** 為準。
3. **與伺服器同步**：當使用者切換「怪物血條」開關時，發送 **C_ClientOption (opcode 13, type=3, onoff=0/1)**，讓伺服器設定 `hpBar`，之後 104 才會在「進入視野」與「攻擊命中」時發送。
4. **無 104 時的備援**：若伺服器未送 104（例如未開 hpBar 或未開 BROADCAST_HP_TO_AROUND），客戶端可依**傷害封包**用 `newRatio = max(0, HpRatio - damage)` 做本地估算，僅供顯示；一收到 104 即改為伺服器比例。
