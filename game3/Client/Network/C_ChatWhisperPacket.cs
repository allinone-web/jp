namespace Client.Network
{
	/// <summary>
	/// 密語封包。對齊 jp C_ChatWhisper.java opcode 122。
	/// 結構: writeC(122), writeS(targetName), writeS(text)
	/// </summary>
	public static class C_ChatWhisperPacket
	{
		public const byte OPCODE = 122;

		public static byte[] Make(string targetName, string text)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteString(targetName ?? "");
			w.WriteString(text ?? "");
			return w.GetBytes();
		}
	}
}
