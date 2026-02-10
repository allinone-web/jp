using System;

namespace Client.Network
{
	/// <summary>
	/// 【JP協議對齊】對應服務器：jp.l1j.server.packets.client.C_Attack.java
	/// Opcode: 68 (C_OPCODE_ATTACK)
	/// 結構: WriteD(目標ID) -> WriteH(目標X) -> WriteH(目標Y)
	/// </summary>
	public class C_AttackPacket
	{
		public static byte[] Make(int targetId, int x, int y)
		{
			PacketWriter writer = new PacketWriter();
			writer.WriteByte(68); // Opcode 68 (jp C_OPCODE_ATTACK)
			writer.WriteInt(targetId);   // 目標 ID
			writer.WriteUShort(x);       // 目標 X
			writer.WriteUShort(y);       // 目標 Y
			return writer.GetBytes();
		}
	}
}
