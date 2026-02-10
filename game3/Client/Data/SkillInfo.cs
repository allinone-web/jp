using Godot;

namespace Client.Data
{
    public class SkillInfo
    {
        public int SkillId;   // 唯一ID (1~200+)
        public string Name;   // 技能名称
        public int Level;     // 魔法等级 (1~10, 或精灵魔法等级)
        public int Index;     // 在该等级中的位置 (0~7)
        public int MpCost;    // MP消耗
        public int HpCost;    // HP消耗
        public int IconId;    // 图标ID
        public bool IsActive; // 是否已学习
        
        // 辅助：计算 Level 和 Index
        // 服务器逻辑：lv = (readC() + 1), no = readC()
        // 所以 SkillId 通常 = (Level-1)*8 + Index + 1
    }
}