namespace Client.Network
{
	/// <summary>
	/// 刪除記憶座標。對齊 jp C_DeleteBookmark.java opcode 223。
	/// 結構: writeC(223), writeS(bookmarkName)
	/// </summary>
	public static class C_BookmarkDeletePacket
	{
		public const byte OPCODE = 223;

		public static byte[] Make(string bookmarkName)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteString(bookmarkName ?? "");
			return w.GetBytes();
		}
	}
}
