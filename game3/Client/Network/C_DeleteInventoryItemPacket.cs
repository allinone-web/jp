using Client.Network;

namespace Client.Network
{
	/// <summary>
	/// 丟棄/刪除背包物品（拖到地面）。對齊伺服器 C_DeleteInventoryItem.java opcode 209，readD() = itemObjectId。
	/// </summary>
	public static class C_DeleteInventoryItemPacket
	{
		public const byte OPCODE = 209;

		public static byte[] Make(int itemObjectId)
		{
			var writer = new PacketWriter();
			writer.WriteByte(OPCODE);
			writer.WriteInt(itemObjectId);
			return writer.GetBytes();
		}
	}
}
