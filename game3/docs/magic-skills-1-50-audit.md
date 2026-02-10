# PC 魔法技能 1–50 逐項稽核（伺服器 vs 客戶端）

對齊專案約定：`/server` 為絕對真相來源。本文逐項對照 **skill_id 1–50** 的伺服器處理邏輯、`id` 參數語意、skill_list type，與客戶端實作是否一致。

---

## 1. 稽核表（skill_id → 伺服器 Handler → id 語意 → type）

| skill_id | 伺服器 Handler | toMagic(id) 之 id 語意 | 伺服器 type | 客戶端 type | 客戶端特殊處理 | 備註 |
|----------|----------------|-------------------------|-------------|-------------|----------------|------|
| 1 | LesserHeal | getObject(id) 目標角色 | buff | buff | — | ✓ |
| 2 | Light | **未使用 id**（僅對自己） | none | none | — | ✓ |
| 3 | Shield | **未使用 id**（僅對自己） | none | none | — | ✓ |
| 4 | EnergyBolt | getObject(id) 目標 | attack | attack | — | ✓ |
| 5 | Teleport | readH()→targetX, readD()→**id=地點 id**；id>0 指定傳送、否則隨機 | none | none | 封包僅寫 targetX+id（6 位元組） | ✓ |
| 6 | CurePoison | getObject(id) 目標 | buff | buff | — | ✓ |
| 7 | ChillTouch | getObject(id) 目標 | attack | attack | — | ✓ |
| 8 | CursePoison | getObject(id) 目標 | buff | buff | — | ✓ |
| 9 | EnchantWeapon | **getItemInvId(id)** 武器 InvID | item | item | targetId=GetEquippedWeaponObjectId()，跳過實體驗證 | ✓ |
| 10 | Detection | **未使用 id**（範圍內破隱） | none | none | — | ✓ |
| 11 | Lightning | getObject(id) 主目標 | attack | attack | — | ✓ |
| 12 | TurnUndead | getObject(id) 目標（不死系） | buff | buff | — | ✓ |
| 13 | Heal | getObject(id) 目標 | buff | buff | — | ✓ |
| 14 | CurseBlind | getObject(id) 目標 | buff | buff | — | ✓ |
| 15 | BlessedArmor | **getItemInvId(id)** 盔甲 InvID | item | item | targetId=GetEquippedArmorObjectId()，跳過實體驗證 | ✓ |
| 16 | Lightning | getObject(id) 主目標 | attack | attack | — | ✓ |
| 17 | PhysicalEnchantDex | getObject(id) 目標 | buff | buff | — | ✓ |
| 18 | WeaponBreak | getObject(id) 目標 | buff | buff | — | ✓ |
| 19 | ChillTouch | getObject(id) 目標 | attack | attack | — | ✓ |
| 20 | Slow | getObject(id) 目標 | buff | buff | — | ✓ |
| 21 | CurseParalyze | getObject(id) 目標 | buff | buff | — | ✓ |
| 22 | EnergyBolt | getObject(id) 目標 | attack | attack | — | ✓ |
| 23 | GreaterHeal | getObject(id) 目標 | buff | buff | — | ✓ |
| 24 | TameMonster | getObject(id) 目標怪物 | buff | buff | — | ✓ |
| 25 | RemoveCurse | getObject(id) 目標 | buff | buff | — | ✓ |
| 26 | CreateZombie | getObject(id) **死亡怪物** | buff | buff | — | ✓ |
| 27 | PhysicalEnchantStr | getObject(id) 目標 | buff | buff | — | ✓ |
| 28 | Haste | getObject(id) 目標 | buff | buff | — | ✓ |
| 29 | CancelMagic | getObject(id) 目標 | buff | buff | — | ✓ |
| 30 | EnergyBolt | getObject(id) 目標 | attack | attack | — | ✓ |
| 31 | HealPledge | **未使用 id**（範圍內血盟/召喚） | none | none | — | ✓ |
| 32 | Freeze | getObject(id) 目標 | attack | attack | — | ✓ |
| 33 | SummonMonster | **未使用 id**（召喚物固定） | none | none | — | ✓ |
| 34 | **Magic（default）** | toMagic(id) 空實作，id 未使用 | none | none | — | ✓ |
| 35 | Tornado | **未使用 id**（範圍內 list） | none | none | — | ✓ |
| 36 | FullHeal | getObject(id) 目標 | buff | buff | — | ✓ |
| 37 | Firewall | **未使用 id**（僅對自己） | buff | buff | — | ✓ |
| 38 | Tornado | **未使用 id** | none | none | — | ✓ |
| 39 | Invisibility | **未使用 id**（僅對自己） | none | none | — | ✓ |
| 40 | Resurrection | getObject(id) **死亡目標** | buff | buff | — | ✓ |
| 41 | **Magic（default）** | toMagic(id) 空實作，id 未使用 | attack | attack | — | ✓ |
| 42 | **Magic（default）** | toMagic(id) 空實作，id 未使用 | buff | buff | — | ✓ |
| 43 | Polymorph | getObject(id) 目標角色 | buff | buff | — | ✓ |
| 44 | ImmuneToHarm | getObject(id) 目標 | buff | buff | — | ✓ |
| 45 | MassTeleport | id>0 指定地點 id，否則隨機；同 C_Magic readH+readD | none | none | 封包僅寫 targetX+id | ✓ |
| 46 | CreateMagicalWeapon | **getItemInvId(id)** 武器 InvID | item | item | targetId=GetEquippedWeaponObjectId()，跳過實體驗證 | ✓ |
| 47 | Lightning | getObject(id) 主目標 | attack | attack | — | ✓ |
| 48 | **Magic（default）** | toMagic(id) 空實作，id 未使用 | buff | buff | — | ✓ |
| 49 | **Magic（default）** | toMagic(id) 空實作，id 未使用 | buff | buff | — | ✓ |
| 50 | EnergyBolt | getObject(id) 目標 | attack | attack | — | ✓ |

---

## 2. SkillTable 無專用 case 之技能（34, 41, 42, 48）

伺服器 **SkillTable.java** 對 skill_id **34、41、42、48、49** 無專用 case，走 **default: m = new Magic(pc, s)**。base **Magic.toMagic(int id)** 為空實作，故 id 不被使用。客戶端依 type（none/buff/attack）送 targetId 即可，不影響結果。（**29** 為 CancelMagic，有專用 case。）

---

## 3. 特殊封包與邏輯（已對齊）

- **C_Magic 封包**  
  - 一般：`lv, no, id`（7 位元組）。  
  - 技能 5、45：`lv, no, readH()→targetX, readD()→id`（9 位元組）；客戶端**僅**寫 targetX + targetId，不寫 targetY。
- **id 為 InvID 的技能**  
  - 9（EnchantWeapon）、15（BlessedArmor）、46（CreateMagicalWeapon）：客戶端傳裝備武器/盔甲 ObjectId（= InvID），且跳過「魔法目標實體」驗證。
- **id 未使用**  
  - 2, 3, 10, 31, 33, 35, 37, 38, 39：伺服器不讀 id，客戶端 type none/buff 送 _myPlayer.ObjectId 或選中目標皆可，不影響結果。

---

## 4. 結論

- **type 對齊**：客戶端 `skill_list.csv` 之 type（none/buff/attack/item）與伺服器 `skill_list` 表一致，無需修改。  
- **id 語意**：getObject(id) 者皆為「目標 ObjectId」；getItemInvId(id) 者為 9/15/46，客戶端已改為傳裝備 InvID。  
- **傳送 5/45**：封包已改為 6 位元組（僅 targetX + id），與 C_Magic.java 一致。  
- **未發現其餘技能有封包或目標選取錯誤**；若日後實作「指定傳送」UI，僅需對 5/45 傳入 targetX（及地點 id）即可。
