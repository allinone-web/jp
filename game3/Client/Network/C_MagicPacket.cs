using System;
using Godot;
using Client.Utility;

namespace Client.Network
{
    /// <summary>
    /// 施法封包 (對應 Server C_Magic.java)。
    /// 僅支援 PC 魔法 skill_id 1–50；精靈魔法 (129+ ) 使用不同 skill_level 對應，尚未實作。
    /// </summary>
    public static class C_MagicPacket
    {
        /// <summary>PC 魔法 ID 有效範圍（對齊 server skill_list 1–50）。</summary>
        public const int MinPcSkillId = 1;
        public const int MaxPcSkillId = 50;

        /// <summary>
        /// 構建施法封包。群體魔法只發一包 C_Magic(skillId, targetId)，伺服器回傳一個 Op57 多筆目標+傷害。
        /// </summary>
        /// <param name="skillId">技能 ID (1–50，PC 魔法)</param>
        /// <param name="targetId">目標 ObjectId (0=自己/無目標；群體魔法為主目標)</param>
        /// <param name="targetX">目標格 X (僅技能 5 指定傳送、45 集體傳送時伺服器會 readH)</param>
        /// <param name="targetY">目標格 Y（僅供客戶端邏輯；傳送封包對齊伺服器只送 targetX + id，不送 targetY）</param>
        public static byte[] Make(int skillId, int targetId, int targetX = 0, int targetY = 0, string message = null, string charName = null)
        {
            if (skillId < MinPcSkillId || skillId > MaxPcSkillId)
            {
                GD.PrintErr($"[C_MagicPacket] skillId={skillId} 超出 PC 魔法範圍 1–50，封包 (lv,no) 可能與伺服器不符；精靈魔法尚未支援。");
            }

            var w = new PacketWriter();
            w.WriteByte(115); // jp C_OPCODE_USESKILL

            // 【JP協議對齊】對齊 jp C_UseSkill.java：每級 8 格，skill_row 和 skill_column
            // skillId = (row * 8) + column + 1
            int row = (skillId - 1) / 8;
            int column = (skillId - 1) % 8;
            w.WriteByte((byte)row);
            w.WriteByte((byte)column);

            // 【JP協議對齊】對齊 jp C_UseSkill.java 依 skillId 讀取參數
            // - TELEPORT(5), MASS_TELEPORT(69): readH(mapId) + readD(bookmarkId)
            // - FIRE_WALL(58), LIFE_STREAM(63): readH(x) + readH(y)
            // - TRUE_TARGET(113): readD(targetId) + readH(x) + readH(y) + readS(message)
            // - CALL_CLAN(116), RUN_CLAN(118): readS(charName)
            // - 其他技能: readD(targetId) + readH(x) + readH(y)
            const int SKILL_TELEPORT = 5;
            const int SKILL_MASS_TELEPORT = 69;
            const int SKILL_FIRE_WALL = 58;
            const int SKILL_LIFE_STREAM = 63;
            const int SKILL_TRUE_TARGET = 113;
            const int SKILL_CALL_CLAN = 116;
            const int SKILL_RUN_CLAN = 118;

            if (skillId == SKILL_TELEPORT || skillId == SKILL_MASS_TELEPORT)
            {
                // mapId 放在 targetX，bookmarkId 放在 targetId
                w.WriteUShort(targetX);
                w.WriteInt(targetId);
            }
            else if (skillId == SKILL_FIRE_WALL || skillId == SKILL_LIFE_STREAM)
            {
                w.WriteUShort(targetX);
                w.WriteUShort(targetY);
            }
            else if (skillId == SKILL_TRUE_TARGET)
            {
                w.WriteInt(targetId);
                w.WriteUShort(targetX);
                w.WriteUShort(targetY);
                w.WriteString(message ?? string.Empty);
            }
            else if (skillId == SKILL_CALL_CLAN || skillId == SKILL_RUN_CLAN)
            {
                w.WriteString(charName ?? string.Empty);
            }
            else
            {
                w.WriteInt(targetId);
                w.WriteUShort(targetX);
                w.WriteUShort(targetY);
            }
            return w.GetBytes();
        }
    }
}
