namespace Client.Network
{
	/// <summary>
	/// 離開遊戲。對齊 jp C_OPCODE_QUITGAME = 104。伺服器收到後執行登出。
	/// 結構: writeC(104) 僅 opcode（或無額外資料）。
	/// </summary>
	public static class C_QuitGamePacket
	{
		public const byte OPCODE = 104;

		public static byte[] Make()
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			return w.GetBytes();
		}
	}
}
