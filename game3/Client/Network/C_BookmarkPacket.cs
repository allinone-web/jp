namespace Client.Network
{
	/// <summary>
	/// 增加記憶座標。對齊 jp C_AddBookmark.java opcode 134。
	/// 結構: writeC(134), writeS(name) — 當前位置以該名稱記憶。
	/// </summary>
	public static class C_BookmarkPacket
	{
		public const byte OPCODE = 134;

		public static byte[] Make(string name)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteString(name ?? "");
			return w.GetBytes();
		}
	}
}
