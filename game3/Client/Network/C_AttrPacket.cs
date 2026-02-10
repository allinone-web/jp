namespace Client.Network
{
	/// <summary>
	/// 點選項目結果封包（NPC 對話/Yes-No/傳送選項等）。對齊 jp C_Attr.java opcode 61。
	/// 結構: writeC(61), writeH(attrCode) 或 writeH(479) 或 writeH(i), writeD(count), writeH(attrcode) 等，依伺服器情境不同。
	/// 最小常用：僅送 opcode + writeH(attrCode)。
	/// </summary>
	public static class C_AttrPacket
	{
		public const byte OPCODE = 61;

		/// <summary>僅送 opcode + 2 字節 attr（用於簡單 Yes/No 或選項回傳）。</summary>
		public static byte[] MakeWithAttr(ushort attrCode)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteUShort(attrCode);
			return w.GetBytes();
		}
	}
}
