# 傷害結算診斷說明

## 現象（依測試日誌）

- **近戰物理攻擊、魔法攻擊**：無傷害結算（飄字／僵硬）。
- **弓箭攻擊**：可正常結算傷害。
- 日誌中 Op35 魔法皆為：`[Magic][Packet35] ... Damage:0`，且 `HandleEntityAttackHit target=... damage=0`。

## 結論

- **客戶端**：Op35 解析順序與 server 一致（actionId, attackerId, targetId, **damage**, heading, etcId, ...），讀取之 `damage` 即封包內值。
- **日誌顯示封包內 damage 為 0**：即 **server 送出的 Op35 中 damage 欄位為 0**。客戶端依此正確顯示 MISS（damage<=0 僅飄字、不播受擊僵硬）。
- 弓箭有傷害、近戰/魔法無，代表：**同一客戶端邏輯下，只有當 server 在 Op35 中送非 0 傷害時，客戶端才會結算傷害**。

## Server 對應（Source of Truth）

- **魔法（光箭等）**：`server/world/instance/skill/function/EnergyBolt.java`  
  `SendPacket(new S_ObjectAttackMagic(..., dmg, ...))`，其中 `dmg = Damage(o, true)`。  
  若客戶端一直收到 0，請檢查 `Magic.Damage()` 與命中判定是否正確回傳傷害。
- **近戰**：`server/world/instance/PcInstance.java`  
  `SendPacket(new S_ObjectAttack(this, target, action, dmg, effectId, false, false))`。  
  請確認近戰時傳入之 `dmg` 在命中時為非 0。
- **弓箭**：同檔 `AttackBow` 內 `SendPacket(new S_ObjectAttack(..., dmg, ...))`，`dmg = DmgSystem(target, true, 1)`，目前可正常結算，表示此路徑 server 有送正確傷害。

## 客戶端已做

- Op35：發送 `ObjectMagicHit(attackerId, targetId, damage)`（magicFlag==6）或 `ObjectAttacked(..., damage)`（近戰/弓箭），並在 `HandleEntityAttackHit` 中依 `damage` 飄字與播受擊。
- Op57：`OnMagicVisualsReceived` 開頭即呼叫 `HandleEntityAttackHit(targetId, damage)`；若 server 有送 Op57 且 damage>0，客戶端會正常結算。

## 建議

1. 在 server 端對 **魔法** 與 **近戰** 送 Op35 前加日誌，確認送出的 `dmg` 值（命中時應 > 0）。
2. 若 server 送出的 dmg 正確但客戶端仍為 0，再檢查封包格式（byte 順序／長度）是否與 S_ObjectAttack / S_ObjectAttackMagic 完全一致。
