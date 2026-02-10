using System;

namespace Client.Network
{
	/// <summary>
	/// 【JP協議對齊】對應服務器：jp.l1j.server.packets.client.C_AuthLogin.java
	/// Opcode: 57 (C_OPCODE_LOGINPACKET)
	/// 結構: WriteS(帳號) -> WriteS(密碼)
	/// </summary>
	public class C_LoginPacket
	{
		public static byte[] Make(string user, string pass)
		{
			PacketWriter writer = new PacketWriter();

			// 1. 寫入 Opcode (57) - jp C_OPCODE_LOGINPACKET
			writer.WriteByte(57);

			// 2. 寫入帳號 (對應 server: readS())
			writer.WriteString(user);

			// 3. 寫入密碼 (對應 server: readS())
			writer.WriteString(pass);

			// 返回構建好的字節數組
			return writer.GetBytes();
		}
	}
}
