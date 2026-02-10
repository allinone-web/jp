using System;

namespace Client.Data
{
    /// <summary>
    /// 仓库物品实体类
    /// 严格对应服务器 S_WarehouseItemList.java (Opcode 49) 的数据对齐结构
    /// </summary>
    public class WarehouseItem
    {
        /// <summary>
        /// 物品在数据库中的唯一标识 (uid)
        /// 对应服务器：rs.getInt("uid") -> writeD
        /// </summary>
        public int Uid { get; set; }

        /// <summary>
        /// 物品类型
        /// 对应服务器：rs.getInt("type") -> writeC
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// 物品的图形 ID (GFX ID)
        /// 对应服务器：rs.getInt("gfxid") -> writeH
        /// </summary>
        public int GfxId { get; set; }

        /// <summary>
        /// 祝福状态 (0: 祝福, 1: 正常, 2: 诅咒)
        /// 对应服务器：rs.getInt("bless") -> writeC
        /// </summary>
        public int Bless { get; set; }

        /// <summary>
        /// 堆叠数量
        /// 对应服务器：rs.getInt("count") -> writeD
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// 是否已经过鉴定
        /// 对应服务器：rs.getInt("definite") == 1 -> writeC
        /// </summary>
        public bool IsIdentified { get; set; }

        /// <summary>
        /// 物品名称（服务器发送的原始字符串）
        /// 对应服务器：rs.getString("name") -> writeS
        /// </summary>
        public string Name { get; set; }
    }
}