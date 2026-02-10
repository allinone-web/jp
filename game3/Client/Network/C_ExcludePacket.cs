namespace Client.Network
{
	/// <summary>
	/// 拒絕名單（遮斷/解除）。對齊 jp C_Exclude.java opcode 101。
	/// 結構: writeC(101), writeS(name)
	/// </summary>
	public static class C_ExcludePacket
	{
		public const byte OPCODE = 101;

		public static byte[] Make(string name)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteString(name ?? "");
			return w.GetBytes();
		}
	}
}
