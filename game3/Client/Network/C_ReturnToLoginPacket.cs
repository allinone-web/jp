namespace Client.Network
{
	/// <summary>
	/// 返回登入畫面封包。對齊 jp C_ReturnToLogin.java opcode 218。
	/// 結構: writeC(218) 僅 opcode；伺服器會執行登出。
	/// </summary>
	public static class C_ReturnToLoginPacket
	{
		public const byte OPCODE = 218;

		public static byte[] Make()
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			return w.GetBytes();
		}
	}
}
