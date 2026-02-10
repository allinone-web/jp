# 魔法封包與技能 ID 全面審計

## 一、技能 ID 計算公式（唯一正確）

- **伺服器**：`skill_list` 為 **每級 5 格**，`skill_level` 1–10、`skill_no` 0–4（對應 id=1,2,4,8,16）。
- **對應**：`skill_id` 1–5→level1(skill_no 0–4), 6–10→level2, … 46–50→level10。
- **公式**：
  - `levelIdx = (skillId - 1) / 5`（0..9）
  - `slotIdx = (skillId - 1) % 5`（0..4）
- **封包**：客戶端送 `WriteByte(levelIdx)`, `WriteByte(slotIdx)`；伺服器得 `lv = readC()+1`, `no = readC()`。

**錯誤公式（已廢棄）**：`(skillId-1)/8` 與 `(skillId-1)%8` 會導致錯技（如 10→7）或無匹配（如 16→(2,7) 無 skill_no=7）。

---

## 二、已對齊的檔案與常數

| 檔案 | 用途 | 範圍/備註 |
|------|------|-----------|
| **Client/Network/C_MagicPacket.cs** | 施法封包 (lv, no) | 1–50；常數 MinPcSkillId=1, MaxPcSkillId=50；超出時 GD.PrintErr |
| **Client/Game/GameWorld.Skill.cs** | UseMagic / HasLearnedSkill | 1–50；同常數；UseMagic 超出直接 return |
| **Client/UI/Scripts/SkillWindow.cs** | CheckLearned | 1–50；與 HasLearnedSkill 一致 |
| **Client/Data/SkillListData.cs** | 本地 cast_gfx/range/type | 依 CSV，無 ID 上限（Get 無則 null） |

---

## 三、C_Magic 封包長度（對齊伺服器）

- **伺服器 C_Magic.java**：  
  - 一般技能：`readC()`(lv), `readC()`(no), `readD()`(id) → 1+1+4 = 6 字節 body。  
  - 瞬移 (lv=1,no=4 或 lv=9,no=4)：`readH()`, `readD()`(id) → 再 2+4，共 8 字節 body。
- **客戶端**：僅在 skillId=5 或 45 時寫入 targetX/targetY（WriteShort×2）再 WriteInt(targetId)；其餘只 WriteInt(targetId)。  
  **若對非瞬移多寫 2 個 short，會破壞下一包 opcode 對齊。**

---

## 四、未完成／未實作功能

1. **精靈魔法 (skill_id 129+)**  
   - 伺服器 S_SkillAdd 送 lv[16], lv[17], lv[18]（elf1, elf2, elf3），對應 skill_level 17–18 等，**非** (skillId-1)/5。  
   - 客戶端：HasLearnedSkill / C_MagicPacket 僅支援 1–50；精靈魔法「是否已學」與「施法 (lv,no)」尚未實作。

2. **技能購買窗口 (Op 78)**  
   - `OnSkillBuyListReceived` 僅打 log，購買 UI 註解為「暂未实现窗口，先留接口」。

3. **Buff 圖標／AC 顯示 (GameWorld.Buffs.cs)**  
   - TODO：HUD 藍色氣泡圖標、AC 更新、黑色遮罩等。

4. **Op 29 Buff 圖標倒數 (PacketHandler)**  
   - TODO：更新 UI Buff 圖標倒數。

---

## 五、漏洞修復摘要

| 項目 | 修復內容 |
|------|----------|
| (lv, no) 公式 | 全專案統一 (skillId-1)/5、(skillId-1)%5，廢棄 /8、%8 |
| C_Magic 長度 | 僅瞬移 (5, 45) 寫 targetX/targetY，其餘只寫 targetId |
| 群體魔法發包 | 只發一包 C_Magic(skillId, targetId)，不發多包 |
| 技能 ID 邊界 | UseMagic / HasLearnedSkill / CheckLearned / C_MagicPacket 限定 1–50，超出有提示或 return false |

---

## 六、回歸檢查清單

- [ ] 技能 1–50 施法後伺服器正確執行對應技能（可抽查 1, 5, 10, 16, 45）。
- [ ] 瞬移 (5, 45) 封包含 targetX/targetY；其餘技能不含，下一包 opcode 正常。
- [ ] 技能 10 無所遁形術不造成傷害、不播 252；技能 16 燃燒的火球有 Op57 傷害結算。
- [ ] 技能窗口 1–50 已學/未學與 S_SkillAdd 一致。
