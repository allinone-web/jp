using System;
using System.Text;
using System.IO;

namespace Client.Network
{
	public class PacketWriter
	{
		private readonly MemoryStream _stream;
		private readonly BinaryWriter _writer;
		private readonly Encoding _encoding;

	public PacketWriter()
	{
		_stream = new MemoryStream();
		// 【JP協議對齊】對齊 jp Config.CLIENT_LANGUAGE_CODE
		// 【修改為 UTF-8】服務器和客戶端都使用 UTF-8 編碼，更國際通用
		// 服務器配置：ClientLanguage = 0 (對應 LANGUAGE_CODE_ARRAY[0] = "UTF8")
		_encoding = Encoding.UTF8; // UTF-8 編碼
		_writer = new BinaryWriter(_stream);
	}

		// 对应 Java writeC
		public void WriteByte(int value)
		{
			_writer.Write((byte)value);
		}

		// 对应 Java writeH
		public void WriteUShort(int value)
		{
			_writer.Write((ushort)value);
		}

	

		// 对应 Java writeS
		public void WriteString(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				_writer.Write((byte)0);
				return;
			}
			// 【JP協議對齊】對齊 jp ServerBasePacket.writeS()
			// 服務器使用 CLIENT_LANGUAGE_CODE 來讀取字符串
			// 對於純 ASCII 字符串，SJIS 和 ASCII 編碼結果相同
			// 但為了確保兼容性，使用配置的編碼
			byte[] bytes = _encoding.GetBytes(value);
			_writer.Write(bytes);
			_writer.Write((byte)0); // Null Terminator
		}

		public byte[] GetBytes()
		{
			_writer.Flush();
			byte[] data = _stream.ToArray();
			
			// 【JP協議對齊】對齊 jp ServerBasePacket.getBytes()
			// 服務器會自動填充到 4 字節的倍數，客戶端也需要這樣做
			// 這樣可以確保加密/解密後封包結構正確
			int padding = data.Length % 4;
			if (padding != 0)
			{
				// 需要填充到 4 字節的倍數
				int targetLength = data.Length + (4 - padding);
				byte[] padded = new byte[targetLength];
				Array.Copy(data, 0, padded, 0, data.Length);
				// 剩餘字節填充為 0
				for (int i = data.Length; i < targetLength; i++)
				{
					padded[i] = 0;
				}
				return padded;
			}
			
			return data;
		}

		// 对应服务器 writeH (Little Endian)
		public void WriteShort(int value)
		{
		    WriteByte((byte)(value & 0xFF));
		    WriteByte((byte)((value >> 8) & 0xFF));
		}


		// 对应 Java writeD
		public void WriteInt(int value)
		{
		 	_writer.Write((int)value);
		}

		// 对应服务器 writeD (Little Endian)
		// 【极其重要】你的 UseItem 发送的是物品 ID，如果这个不对，服务器就找不到物品！
		// public void WriteInt(int value)
		// {
		//     WriteByte((byte)(value & 0xFF));
		//     WriteByte((byte)((value >> 8) & 0xFF));
		//     WriteByte((byte)((value >> 16) & 0xFF));
		//     WriteByte((byte)((value >> 24) & 0xFF));
		// }

	}
}
