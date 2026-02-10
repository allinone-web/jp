using System;

namespace Client.Network
{
    /// <summary>
    /// 【JP協議對齊】對應服務器：jp.l1j.server.packets.client.C_MoveChar.java
    /// Opcode: 95 (C_OPCODE_MOVECHAR)
    /// 結構: WriteH(X) -> WriteH(Y) -> WriteC(Heading)
    /// </summary>
    public class C_MoveCharPacket
    {
        public static byte[] Make(int x, int y, int heading)
        {
            PacketWriter writer = new PacketWriter();
            writer.WriteByte(95);       // Opcode: 95 (jp C_OPCODE_MOVECHAR)
            writer.WriteUShort(x);       // 下一步的 X 座標
            writer.WriteUShort(y);       // 下一步的 Y 座標
            writer.WriteByte(heading);   // 朝向 (0-7)
            return writer.GetBytes();
        }
    }
}