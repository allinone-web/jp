using System;
using System.IO;
using System.Text;

namespace Client.Network
{
	public class PacketReader : IDisposable
	{
		private MemoryStream _stream;
		private BinaryReader _reader;
		private Encoding _encoding;

		public PacketReader(byte[] data)
		{
			// 尝试获取 GBK 编码，如果失败回退到默认
			try 
			{
				_encoding = Encoding.GetEncoding("GBK");
			}
			catch
			{
				_encoding = Encoding.Default; 
			}
			
			_stream = new MemoryStream(data);
			_reader = new BinaryReader(_stream);
		}

		public int Remaining => (int)(_stream.Length - _stream.Position);

		public byte ReadByte()
		{
			if (Remaining < 1) return 0;
			return _reader.ReadByte();
		}

		public byte[] ReadBytes(int count)
		{
			if (Remaining < count) count = Remaining;
			return _reader.ReadBytes(count);
		}

		// 对应 Java writeH (无符号处理)
		public ushort ReadUShort()
		{
			if (Remaining < 2) return 0;
			return _reader.ReadUInt16(); 
		}

		// 对应 Java writeH (有符号处理，如 Lawful)
		public short ReadShort()
		{
			if (Remaining < 2) return 0;
			return _reader.ReadInt16();
		}

		// 对应 Java writeD
		public int ReadInt()
		{
			if (Remaining < 4) return 0;
			return _reader.ReadInt32(); 
		}
		
		public double ReadDouble()
		{
			if (Remaining < 8) return 0;
			return _reader.ReadDouble();
		}

		// 对应 Java writeS
		public string ReadString()
		{
			try
			{
				var bytes = new System.Collections.Generic.List<byte>();
				while (Remaining > 0)
				{
					byte b = _reader.ReadByte();
					if (b == 0) break; 
					bytes.Add(b);
				}
				return _encoding.GetString(bytes.ToArray());
			}
			catch 
			{ 
				return string.Empty; 
			}
		}

		public void Dispose()
		{
			_reader?.Close();
			_stream?.Close();
		}
	}
}
