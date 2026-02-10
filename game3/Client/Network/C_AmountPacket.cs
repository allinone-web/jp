namespace Client.Network
{
	/// <summary>
	/// 數量確認封包。對齊 jp C_Amount.java opcode 109。
	/// 結構: writeC(109), writeD(objectId), writeD(amount), writeC(c), writeS(s)
	/// </summary>
	public static class C_AmountPacket
	{
		public const byte OPCODE = 109;

		public static byte[] Make(int objectId, int amount, byte c, string s)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteInt(objectId);
			w.WriteInt(amount);
			w.WriteByte(c);
			w.WriteString(s ?? "");
			return w.GetBytes();
		}
	}
}
