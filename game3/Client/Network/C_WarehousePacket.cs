using System;
using Godot;

namespace Client.Network
{
    /// <summary>
    /// 仓库操作封包
    /// 对应 C_OPCODE_SHOP = 40
    /// </summary>
    public static class C_WarehousePacket
    {
        // 【JP協議對齊】對齊 jp C_Shop.java (倉庫操作使用相同的封包)
        // 注意：jp 版本中，倉庫操作也使用 C_OPCODE_SHOP = 16，type 為 2(存) 或 3(取)
        private const int OPCODE = 16; // C_OPCODE_SHOP (jp)

        // 对应 C_Shop.java 中的 switch(type)
        public const int TYPE_DEPOSIT = 2;  // 存入 (WareHousePut)
        public const int TYPE_RETRIEVE = 3; // 取出 (WareHouseGet)

        /// <summary>
        /// 构造存取请求包
        /// </summary>
        public static byte[] Make(int npcObjectId, int itemObjectId, int count, int type)
        {
            if (count <= 0) return null;

            PacketWriter writer = new PacketWriter();
            
            // 1. Opcode: 40
            writer.WriteByte(OPCODE);
            
            // 2. NPC 实例 ID: readD()
            writer.WriteInt(npcObjectId);
            
            // 3. 操作类型: readC() (2为存, 3为取)
            writer.WriteByte((byte)type);

            // 4. 操作项数 (1.38协议标准通常为1项) - 【協議修復】服務器使用 readH() 讀取無符號 short，客戶端應使用 WriteUShort
            writer.WriteUShort(1); 

            // 5. 物品实例 ID: readD()
            writer.WriteInt(itemObjectId);

            // 6. 操作数量: readD()
            writer.WriteInt(count);

            return writer.GetBytes();
        }
    }
}