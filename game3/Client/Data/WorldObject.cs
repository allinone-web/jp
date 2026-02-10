using Godot;

namespace Client.Data
{
    /// <summary>
    /// 地图上的物体（玩家、怪物、NPC）
    /// 对应服务器 S_ObjectAdd (Opcode 11) 的解析结果
    /// </summary>
public partial class WorldObject : GodotObject
    {
        public int X;
        public int Y;
        public int ObjectId;  // 唯一ID
        public int GfxId;     // 外观/变身ID
        public int GfxMode;   // 动作 (攻击, 行走等)
        public int Heading;   // 面向 (0-7)
        public int Light;     // 光照范围
        public int CurrentHp;
        public int Speed;     // 速度 (0:正常, 1:加速, 2:减速)
        public int Exp;       // 经验值或数量
        // 必须是 int，绝对不能是 short！
        // 如果是 short，66536 会溢出变成 0，导致 NPC 变成怪物。
        public int Lawful;
        public string Name;
        public string Title;
        public int Status;    // 状态毒/麻痹等
        public int ClanId;
        public string ClanName;
        public string OwnerName; // 宠物主人
        public int HpRatio;   // 血条百分比

        public override string ToString()
        {
            return $"[Obj {ObjectId}] {Name} at ({X},{Y}) Gfx:{GfxId}";
        }
    }
}