using System;

namespace Client.Network
{
	/// <summary>
	/// 对应服务器：net.network.client.C_StatDice.java
	/// Opcode: 67
	/// 结构: WriteC(职业ID)
	/// </summary>
	public class C_StatDicePacket
	{
		/// <summary>
		/// 发送初始化属性请求
		/// </summary>
		/// <param name="classType">职业ID：0=王族, 1=骑士, 2=妖精, 3=法师</param>
		public static byte[] Make(byte classType)
		{
			PacketWriter writer = new PacketWriter();

			// 1. Opcode 67
			writer.WriteByte(67);

			// 2. Class Type (对应 server: readC())
			// 服务器根据这个ID来初始化 random stat 容器-决定职业classType的
			writer.WriteByte(classType);

			return writer.GetBytes();
		}
	}
}
