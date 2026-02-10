using System;

namespace Client.Network
{
	/// <summary>
	/// 【JP協議對齊】jp 服務器加密算法
	/// 來源：jp.l1j.server.utils.Cipher.java
	/// 機制：基於 key 的加密，key 在握手時由服務器發送
	/// 【重要】完全按照 Java 版本實現，確保加密/解密邏輯完全一致
	/// </summary>
	public class JpCipher
	{
		// 靜態常量（對齊 jp Cipher.java）
		// 注意：這些值超過 int.MaxValue，在 C# 中需要使用 unchecked 轉換為有符號 int
		private const int _1 = unchecked((int)0x9c30d539);
		private const int _2 = unchecked((int)0x930fd7e2);
		private const int _3 = unchecked((int)0x7c72e993);
		private const int _4 = unchecked((int)0x287effc3);

		// 編碼鑰匙和解碼鑰匙（各 8 字節）
		private readonly byte[] eb = new byte[8];
		private readonly byte[] db = new byte[8];
		private readonly byte[] tb = new byte[4];

		/// <summary>
		/// 初始化 Cipher，對齊 jp Cipher(int key) 構造函數
		/// </summary>
		/// <param name="key">32位加密鍵（由服務器在握手時發送）</param>
		public JpCipher(int key)
		{
			// 【JP協議對齊】完全對齊 jp Cipher.java 的初始化邏輯
			int[] keys = { key ^ _1, _2 };
			keys[0] = RotateLeft(keys[0], 0x13);
			keys[1] ^= keys[0] ^ _3;

			for (int i = 0; i < keys.Length; i++)
			{
				for (int j = 0; j < tb.Length; j++)
				{
					eb[(i * 4) + j] = db[(i * 4) + j] = (byte)(keys[i] >> (j * 8) & 0xff);
				}
			}
		}

		/// <summary>
		/// 加密：對齊 jp Cipher.encrypt()
		/// 【重要】Java 版本直接訪問 data[3], data[2], data[1]，假設 data.length >= 4
		/// </summary>
		public void Encrypt(byte[] data)
		{
			if (data == null || data.Length == 0) return;

			// 【JP協議對齊】Java 版本假設 data.length >= 4
			// 如果封包長度 < 4，需要擴展到 4 字節（但這不應該發生，因為 PacketWriter.GetBytes() 會填充）
			if (data.Length < 4)
			{
				byte[] expanded = new byte[4];
				Array.Copy(data, 0, expanded, 0, data.Length);
				for (int i = data.Length; i < 4; i++)
				{
					expanded[i] = 0;
				}
				EncryptInternal(expanded);
				// 只複製實際需要的字節回原數組（保持原始長度）
				Array.Copy(expanded, 0, data, 0, data.Length);
				return;
			}

			EncryptInternal(data);
		}

		/// <summary>
		/// 加密內部實現：完全對齊 jp Cipher.encrypt()
		/// </summary>
		private void EncryptInternal(byte[] data)
		{
			// 【JP協議對齊】保存前 4 個字節（Java: for (int i = 0; i < tb.length; i++) tb[i] = data[i];）
			for (int i = 0; i < tb.Length; i++)
			{
				tb[i] = data[i];
			}

			// 【JP協議對齊】對齊 Java: data[0] ^= eb[0];
			data[0] = (byte)(data[0] ^ eb[0]);

			// 【JP協議對齊】對齊 Java: for (int i = 1; i < data.length; i++) { data[i] ^= data[i - 1] ^ eb[i & 7]; }
			for (int i = 1; i < data.Length; i++)
			{
				data[i] = (byte)(data[i] ^ data[i - 1] ^ eb[i & 7]);
			}

			// 【JP協議對齊】對齊 Java 的最後 4 行加密操作
			data[3] = (byte)(data[3] ^ eb[2]);
			data[2] = (byte)(data[2] ^ eb[3] ^ data[3]);
			data[1] = (byte)(data[1] ^ eb[4] ^ data[2]);
			data[0] = (byte)(data[0] ^ eb[5] ^ data[1]);

			// 【JP協議對齊】對齊 Java: update(eb, tb);
			Update(eb, tb);
		}

		/// <summary>
		/// 解密：對齊 jp Cipher.decrypt()
		/// 【重要】Java 版本直接訪問 data[3], data[2], data[1]，假設 data.length >= 4
		/// </summary>
		public void Decrypt(byte[] data)
		{
			if (data == null || data.Length == 0) return;

			// 【JP協議對齊】Java 版本假設 data.length >= 4
			if (data.Length < 4)
			{
				byte[] expanded = new byte[4];
				Array.Copy(data, 0, expanded, 0, data.Length);
				for (int i = data.Length; i < 4; i++)
				{
					expanded[i] = 0;
				}
				DecryptInternal(expanded);
				// 只複製實際需要的字節回原數組（保持原始長度）
				Array.Copy(expanded, 0, data, 0, data.Length);
				return;
			}

			DecryptInternal(data);
		}

		/// <summary>
		/// 解密內部實現：完全對齊 jp Cipher.decrypt()
		/// </summary>
		private void DecryptInternal(byte[] data)
		{
			// 【JP協議對齊】對齊 Java 的前 4 行解密操作
			data[0] = (byte)(data[0] ^ db[5] ^ data[1]);
			data[1] = (byte)(data[1] ^ db[4] ^ data[2]);
			data[2] = (byte)(data[2] ^ db[3] ^ data[3]);
			data[3] = (byte)(data[3] ^ db[2]);

			// 【JP協議對齊】對齊 Java: for (int i = data.length - 1; i >= 1; i--) { data[i] ^= data[i - 1] ^ db[i & 7]; }
			for (int i = data.Length - 1; i >= 1; i--)
			{
				data[i] = (byte)(data[i] ^ data[i - 1] ^ db[i & 7]);
			}

			// 【JP協議對齊】對齊 Java: data[0] ^= db[0];
			data[0] = (byte)(data[0] ^ db[0]);

			// 【JP協議對齊】對齊 Java: update(db, data);
			Update(db, data);
		}

		/// <summary>
		/// 更新鑰匙：完全對齊 jp Cipher.update()
		/// </summary>
		private void Update(byte[] data, byte[] refData)
		{
			// 【JP協議對齊】對齊 Java: for (int i = 0; i < tb.length; i++) { data[i] ^= ref[i]; }
			for (int i = 0; i < tb.Length; i++)
			{
				data[i] ^= refData[i];
			}

			// 【JP協議對齊】對齊 Java 的 int32 計算
			// Java: int int32 = (((data[7] & 0xFF) << 24) | ((data[6] & 0xFF) << 16) | ((data[5] & 0xFF) << 8) | (data[4] & 0xFF)) + _4;
			int int32 = (((data[7] & 0xFF) << 24) | ((data[6] & 0xFF) << 16)
					| ((data[5] & 0xFF) << 8) | (data[4] & 0xFF))
					+ _4;

			// 【JP協議對齊】對齊 Java: for (int i = 0; i < tb.length; i++) { data[i + 4] = (byte) (int32 >> (i * 8) & 0xff); }
			for (int i = 0; i < tb.Length; i++)
			{
				data[i + 4] = (byte)(int32 >> (i * 8) & 0xff);
			}
		}

		/// <summary>
		/// 左旋轉（對齊 Java Integer.rotateLeft）
		/// </summary>
		private int RotateLeft(int value, int distance)
		{
			distance &= 0x1F;
			// 【JP協議對齊】Java Integer.rotateLeft 使用無符號右移 (>>>)
			uint v = (uint)value;
			return (int)((v << distance) | (v >> (32 - distance)));
		}
	}
}
