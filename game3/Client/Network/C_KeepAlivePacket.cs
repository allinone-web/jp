namespace Client.Network
{
	/// <summary>
	/// 保活封包。對齊 jp C_KeepAlive.java opcode 182。
	/// 結構: writeC(182) 僅 opcode，無額外資料。
	/// </summary>
	public static class C_KeepAlivePacket
	{
		public const byte OPCODE = 182;

		public static byte[] Make()
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			return w.GetBytes();
		}
	}
}
