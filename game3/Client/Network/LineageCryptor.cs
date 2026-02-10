using System;

namespace Client.Network
{
	/// <summary>
	/// 【绝对核心】 Lineage 私有加密算法
	/// 来源：服务器 LineagePacketDecoder.java / LineagePacketEncoder.java
	/// 机制：基于流量累积量 (TotalSize) 的滚动 XOR
	/// </summary>
	public class LineageCryptor
	{
		// 服务器发来的总字节数（用于解密接收到的包）
		public long ReadTotalSize { get; private set; } = 0;

		// 客户端发送的总字节数（用于加密发送的包）
		public long WriteTotalSize { get; private set; } = 0;

		public LineageCryptor()
		{
			// 初始状态归零
			ReadTotalSize = 0;
			WriteTotalSize = 0;
		}

		/// <summary>
		/// 解密：对应 Server 的 decrypt()
		/// </summary>
		/// <param name="data">不含头部2字节长度的加密体</param>
		public void Decrypt(byte[] data)
		{
			if (data == null || data.Length == 0) return;

			int size = data.Length;
			byte[] sizeTemp = GetByte(ReadTotalSize); // 获取种子的4字节
			byte[] temp = (byte[])data.Clone(); // 此时 temp 是密文

			int idx = sizeTemp[0] & 0xFF;

			for (int i = 0; i < size; i++)
			{
				if (i > 0 && i % 8 == 0)
				{
					for (int j = 0; j < i; j++)
					{
						// Java: data[i] ^= data[j]; (data[j]已解密)
						data[i] = (byte)(data[i] ^ data[j]);
					}

					if (i % 16 == 0)
					{
						for (int k = 0; k < sizeTemp.Length; k++)
						{
							data[i] = (byte)(data[i] ^ sizeTemp[k]);
						}
					}

					for (int j = 1; j < 4; j++)
					{
						data[i] = (byte)(data[i] ^ sizeTemp[j]);
					}

					for (int j = 1; j < 4; j++)
					{
						if (i + j < size)
						{
							data[i + j] = (byte)(data[i + j] ^ sizeTemp[j]);
						}
					}
				}
				else
				{
					data[i] = (byte)(data[i] ^ idx);

					if (i == 0)
					{
						for (int j = 1; j < 4; j++)
						{
							if (i + j < size)
							{
								data[i + j] = (byte)(data[i + j] ^ sizeTemp[j]);
							}
						}
					}
				}

				// 下一轮的 key 是当前字节的密文
				idx = temp[i] & 0xFF;
			}

			// 【关键】更新累积量，必须加上本次包长
			ReadTotalSize += size;
		}

		/// <summary>
		/// 加密：对应 Server 的 encrypt()
		/// </summary>
		public void Encrypt(byte[] data)
		{
			if (data == null || data.Length == 0) return;

			int size = data.Length;
			byte[] sizeTemp = GetByte(WriteTotalSize);
			byte[] temp = (byte[])data.Clone(); // 此时 temp 是明文

			int idx = sizeTemp[0] & 0xFF;

			for (int i = 0; i < size; i++)
			{
				if (i > 0 && i % 8 == 0)
				{
					for (int j = 0; j < i; j++)
					{
						// Java: data[i] ^= temp[j]; (使用明文混淆)
						data[i] = (byte)(data[i] ^ temp[j]);
					}

					if (i % 16 == 0)
					{
						for (int k = 0; k < sizeTemp.Length; k++)
						{
							data[i] = (byte)(data[i] ^ sizeTemp[k]);
						}
					}

					for (int j = 1; j < 4; j++)
					{
						data[i] = (byte)(data[i] ^ sizeTemp[j]);
					}

					for (int j = 1; j < 4; j++)
					{
						if (i + j < size)
						{
							data[i + j] = (byte)(data[i + j] ^ sizeTemp[j]);
						}
					}
				}
				else
				{
					data[i] = (byte)(data[i] ^ idx);

					if (i == 0)
					{
						for (int j = 1; j < 4; j++)
						{
							if (i + j < size)
							{
								data[i + j] = (byte)(data[i + j] ^ sizeTemp[j]);
							}
						}
					}
				}

				// 下一轮的 key 是当前字节加密后的密文
				idx = data[i] & 0xFF;
			}

			// 更新累积量
			WriteTotalSize += size;
		}

		// 对应 Java: getByte(long size) - 强制 Little Endian 提取低4位
		private byte[] GetByte(long size)
		{
			byte[] data = new byte[4];
			data[0] = (byte)(size & 0xFF);
			data[1] = (byte)((size >> 8) & 0xFF);
			data[2] = (byte)((size >> 16) & 0xFF);
			data[3] = (byte)((size >> 24) & 0xFF);
			return data;
		}
	}
}
