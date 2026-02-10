using System;

namespace Client.Network
{
	/// <summary>
	/// 【JP協議對齊】對應服務器：jp.l1j.server.packets.client.C_LoginToServer.java
	/// Opcode: 131 (C_OPCODE_LOGINTOSERVER)
	/// 作用：請求進入遊戲世界
	/// 結構: writeS(charName)
	/// </summary>
	public class C_EnterWorldPacket
	{
		public static byte[] Make(string charName)
		{
			PacketWriter writer = new PacketWriter();

			// 1. Opcode 131 (jp C_OPCODE_LOGINTOSERVER)
			writer.WriteByte(131);

			// 2. 角色名稱 (Server 讀取 readS)
			writer.WriteString(charName);

			return writer.GetBytes();
		}
	}
}
