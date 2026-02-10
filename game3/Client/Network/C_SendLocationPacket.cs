namespace Client.Network
{
	/// <summary>
	/// 傳送位置。對齊 jp C_SendLocation.java opcode 41。
	/// 結構: writeC(41), writeC(type), [依 type 不同]
	/// type 0x0b 傳送/記憶座標: writeS(name), writeH(mapId), writeH(x), writeH(y), writeC(msgId)
	/// </summary>
	public static class C_SendLocationPacket
	{
		public const byte OPCODE = 41;

		/// <summary>type 0x0b：傳送到指定座標（如記憶座標點擊傳送）。name 為書籤名稱或空。</summary>
		public static byte[] MakeTeleportToBookmark(string name, int mapId, int x, int y, byte msgId = 0)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteByte(0x0b);
			w.WriteString(name ?? "");
			w.WriteUShort((ushort)mapId);
			w.WriteUShort((ushort)x);
			w.WriteUShort((ushort)y);
			w.WriteByte(msgId);
			return w.GetBytes();
		}
	}
}
