using System;
using System.Collections.Generic;

namespace Client.Network
{
	/// <summary>
	/// 【JP協議對齊】創建角色封包。對齊 jp C_CreateChar.java
	/// Opcode: 253 (C_OPCODE_NEWCHAR)
	/// 結構: writeC(253), writeS(name), writeC(type), writeC(sex), writeC(str), writeC(dex), writeC(con), writeC(wis), writeC(cha), writeC(int)
	/// </summary>
	public class C_CreateCharPacket
	{
		// 服务器定义的初始属性值 (C_CharacterCreate.java 第 20-27 行)
		// 顺序: [0]王族, [1]骑士, [2]妖精, [3]法师
		public static readonly int[] ORIGINAL_STR = { 13, 16, 11, 8 };
		public static readonly int[] ORIGINAL_DEX = { 10, 12, 12, 7 };
		public static readonly int[] ORIGINAL_CON = { 10, 14, 12, 12 };
		public static readonly int[] ORIGINAL_WIS = { 11, 9, 12, 12 };
		public static readonly int[] ORIGINAL_CHA = { 13, 12, 9, 8 };
		public static readonly int[] ORIGINAL_INT = { 10, 8, 12, 12 };

		// 职业定义
		public enum ClassType { Royal = 0, Knight = 1, Elf = 2, Wizard = 3 }
		public enum SexType { Male = 0, Female = 1 }

		/// <summary>
		/// 构建创建角色封包
		/// 注意：属性点必须符合服务器校验规则，否则会返回 S_LoginFail(21)
		/// </summary>
		public static byte[] Make(string name, ClassType type, SexType sex, int str, int dex, int con, int wis, int cha, int intel)
		{
			PacketWriter writer = new PacketWriter();

			// 【JP協議對齊】Opcode 253 (C_OPCODE_NEWCHAR)
			writer.WriteByte(253);

			// 2. Name (readS)
			writer.WriteString(name);

			// 3. Type (readC)
			writer.WriteByte((int)type);

			// 4. Sex (readC)
			writer.WriteByte((int)sex);

			// 5. Stats (readC * 6)
			writer.WriteByte(str);
			writer.WriteByte(dex);
			writer.WriteByte(con);
			writer.WriteByte(wis);
			writer.WriteByte(cha);
			writer.WriteByte(intel);

			return writer.GetBytes();
		}

		/// <summary>
		/// 辅助方法：获取某职业的默认初始属性（防止发错数据被踢）
		/// </summary>
		public static (int s, int d, int c, int w, int ch, int i) GetBaseStats(ClassType type)
		{
			int idx = (int)type;
			return (ORIGINAL_STR[idx], ORIGINAL_DEX[idx], ORIGINAL_CON[idx], 
					ORIGINAL_WIS[idx], ORIGINAL_CHA[idx], ORIGINAL_INT[idx]);
		}
	}
}
