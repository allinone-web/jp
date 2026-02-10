using System;
using Client.Network;

namespace Client.Network
{
    /// <summary>
    /// 拾取封包：對齊伺服器 C_ItemPickup.java。讀序：super.read 後 readH()=x, readH()=y, readD()=inv_id, readD()=count（Little Endian）。
    /// </summary>
    public static class C_ItemPickupPacket
    {
        public static byte[] Make(int objectId, int x, int y, int count = 0)
        {
            var writer = new PacketWriter();
            writer.WriteByte(188); // jp C_OPCODE_PICKUPITEM
            writer.WriteUShort((ushort)(x & 0xFFFF));
            writer.WriteUShort((ushort)(y & 0xFFFF));
            writer.WriteInt(objectId);
            int sendCount = count <= 0 ? 1 : (count > int.MaxValue ? int.MaxValue : count);
            writer.WriteInt(sendCount);
            return writer.GetBytes();
        }
    }
}