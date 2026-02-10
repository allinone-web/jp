using Client.Network;

namespace Client.Network
{
	/// <summary>
	/// 【JP協議對齊】刪除角色。對齊 jp C_DeleteChar.java
	/// Opcode: 10 (C_OPCODE_DELETECHAR)
	/// 結構: writeC(10), writeS(name)
	/// </summary>
	public static class C_CharacterDeletePacket
	{
		public const byte OPCODE = 10; // 【JP協議對齊】C_OPCODE_DELETECHAR (jp)

		public static byte[] Make(string charName)
		{
			var writer = new PacketWriter();
			writer.WriteByte(OPCODE);
			writer.WriteString(charName ?? "");
			return writer.GetBytes();
		}
	}
}
