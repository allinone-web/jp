using System;
using Client.Network;

namespace Client.Network
{
	/// <summary>
	/// 給物品封包：對齊伺服器 C_GiveItem.java (Opcode 17)
	/// 用於喂食怪物（抓寵物）或給寵物物品
	/// 讀序：readD()=t_obj, readD()=etc, readD()=inv_id, readD()=count
	/// </summary>
	public static class C_GiveItemPacket
	{
		/// <summary>
		/// 創建給物品封包
		/// </summary>
		/// <param name="targetObjectId">目標對象ID（怪物或寵物）</param>
		/// <param name="itemObjectId">物品ObjectId（背包中的物品ID）</param>
		/// <param name="count">數量</param>
		public static byte[] Make(int targetObjectId, int itemObjectId, int count = 1)
		{
			var writer = new PacketWriter();
			writer.WriteByte(244); // C_OPCODE_GIVEITEM
			writer.WriteInt(targetObjectId); // t_obj
			writer.WriteInt(0); // etc (填充，服務器不使用)
			writer.WriteInt(itemObjectId); // inv_id
			writer.WriteInt(count); // count
			return writer.GetBytes();
		}
	}
}
