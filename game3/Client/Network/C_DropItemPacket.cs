namespace Client.Network
{
	/// <summary>
	/// 丟棄物品封包。對齊 jp C_DropItem.java opcode 54。
	/// 結構: writeC(54), writeH(x), writeH(y), writeD(objectId), writeD(count)
	/// </summary>
	public static class C_DropItemPacket
	{
		public const byte OPCODE = 54;

		public static byte[] Make(int mapX, int mapY, int itemObjectId, int count)
		{
			var w = new PacketWriter();
			w.WriteByte(OPCODE);
			w.WriteUShort((ushort)mapX);
			w.WriteUShort((ushort)mapY);
			w.WriteInt(itemObjectId);
			w.WriteInt(count);
			return w.GetBytes();
		}
	}
}
