using System;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Client.Utility
{
	/// <summary>
	/// L1 IDX 加密/解密工具。解密表從 res://Assets/assets_maps/ 載入。
	/// 供素材 pak 與後續角色/裝備等資源打包加密使用。
	/// </summary>
	public static class L1PakTools
	{
		private static byte[] Map1, Map2, Map3, Map4, Map5;
		private static bool _isLoaded = false;

		public static void EnsureLoaded()
		{
			if (_isLoaded) return;
			GD.Print("[L1PakTools] 載入解密表...");

			string validPath = "res://Assets/assets_maps/";
			if (!FileAccess.FileExists(validPath + "Map1.bin"))
			{
				GD.PrintErr("[L1PakTools] ❌ 找不到 assets_maps 目錄，無法解密/加密資源。");
				return;
			}

			try
			{
				Map1 = LoadBin(validPath + "Map1.bin");
				Map2 = LoadBin(validPath + "Map2.bin");
				Map3 = LoadBin(validPath + "Map3.bin");
				Map4 = LoadBin(validPath + "Map4.bin");
				Map5 = LoadBin(validPath + "Map5.bin");
				_isLoaded = true;
				GD.Print("[L1PakTools] ✅ 解密表載入完成");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[L1PakTools] 解密表載入異常: {ex.Message}");
			}
		}

		private static byte[] LoadBin(string path)
		{
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			if (file == null) throw new System.IO.FileNotFoundException(path);
			return file.GetBuffer((long)file.GetLength());
		}

		/// <summary>解密 IDX：前 4 位元組保留，從 index=4 開始解密。</summary>
		public static byte[] Decode(byte[] src, int index)
		{
			if (!_isLoaded) EnsureLoaded();
			if (Map1 == null) return src;
			return Coder(src, index, false);
		}

		/// <summary>加密索引內容。payload 為索引位元組；傳回 4 位元組頭 + 加密內容，用於舊 .idx 或新 .pak 內索引區。</summary>
		public static byte[] Encode(byte[] payload)
		{
			if (!_isLoaded) EnsureLoaded();
			if (Map1 == null || payload == null || payload.Length == 0) return payload;
			byte[] encoded = Coder(payload, 0, true);
			byte[] result = new byte[4 + encoded.Length];
			result[0] = result[1] = result[2] = result[3] = 0;
			Array.Copy(encoded, 0, result, 4, encoded.Length);
			return result;
		}

		private static byte[] Coder(byte[] src, int index, bool IsEncode)
		{
			byte[] numArray = new byte[src.Length - index];
			if (numArray.Length >= 8)
			{
				byte[] src1 = new byte[8];
				int num = numArray.Length / 8;
				int destinationIndex = 0;
				for (; num > 0; --num)
				{
					Array.Copy(src, destinationIndex + index, src1, 0, 8);
					byte[] processed = IsEncode ? sub_403160(src1) : sub_403220(src1);
					Array.Copy(processed, 0, numArray, destinationIndex, 8);
					destinationIndex += 8;
				}
			}
			int length = numArray.Length % 8;
			if (length > 0)
			{
				int destinationIndex = numArray.Length - length;
				Array.Copy(src, destinationIndex + index, numArray, destinationIndex, length);
			}
			return numArray;
		}

		private static byte[] sub_403220(byte[] src)
		{
			byte[][] numArray = new byte[17][];
			numArray[0] = sub_4032E0(src, Map1);
			int index = 0;
			int a1 = 15;
			while (a1 >= 0)
			{
				numArray[index + 1] = sub_403340(a1, numArray[index]);
				--a1;
				++index;
			}
			return sub_4032E0(new byte[8] {
				numArray[16][4], numArray[16][5], numArray[16][6], numArray[16][7],
				numArray[16][0], numArray[16][1], numArray[16][2], numArray[16][3]
			}, Map2);
		}

		private static byte[] sub_403160(byte[] src)
		{
			byte[][] numArray = new byte[17][];
			numArray[0] = sub_4032E0(src, Map1);
			int index = 0;
			int a1 = 0;
			while (a1 <= 15)
			{
				numArray[index + 1] = sub_403340(a1, numArray[index]);
				++a1;
				++index;
			}
			return sub_4032E0(new byte[8] {
				numArray[16][4], numArray[16][5], numArray[16][6], numArray[16][7],
				numArray[16][0], numArray[16][1], numArray[16][2], numArray[16][3]
			}, Map2);
		}

		private static byte[] sub_4032E0(byte[] a1, byte[] a2)
		{
			byte[] numArray = new byte[8];
			int index1 = 0;
			int num1 = 0;
			while (num1 < 16)
			{
				byte num2 = a1[index1];
				int num3 = (int)num2 >> 4;
				int num4 = (int)num2 % 16;
				for (int index2 = 0; index2 < 8; ++index2)
				{
					int num5 = num1 * 128 + index2;
					if (num5 + (16 + num4) * 8 >= a2.Length) continue;
					numArray[index2] |= (byte)((uint)a2[num5 + num3 * 8] | (uint)a2[num5 + (16 + num4) * 8]);
				}
				num1 += 2;
				++index1;
			}
			return numArray;
		}

		private static byte[] sub_403340(int a1, byte[] a2)
		{
			byte[] a1_1 = new byte[4];
			Array.Copy(a2, 4, a1_1, 0, 4);
			byte[] numArray = sub_4033B0(a1_1, a1);
			return new byte[8] {
				a2[4], a2[5], a2[6], a2[7],
				(byte)((uint)numArray[0] ^ (uint)a2[0]),
				(byte)((uint)numArray[1] ^ (uint)a2[1]),
				(byte)((uint)numArray[2] ^ (uint)a2[2]),
				(byte)((uint)numArray[3] ^ (uint)a2[3])
			};
		}

		private static byte[] sub_4033B0(byte[] a1, int a2)
		{
			byte[] numArray = sub_403450(a1);
			int index = a2 * 6;
			return sub_4035A0(sub_403520(new byte[6] {
				(byte)((uint)numArray[0] ^ (uint)Map5[index]),
				(byte)((uint)numArray[1] ^ (uint)Map5[index + 1]),
				(byte)((uint)numArray[2] ^ (uint)Map5[index + 2]),
				(byte)((uint)numArray[3] ^ (uint)Map5[index + 3]),
				(byte)((uint)numArray[4] ^ (uint)Map5[index + 4]),
				(byte)((uint)numArray[5] ^ (uint)Map5[index + 5])
			}));
		}

		private static byte[] sub_403450(byte[] a1)
		{
			return new byte[6] {
				(byte)((int)a1[3] << 7 | ((int)a1[0] & 249 | (int)a1[0] >> 2 & 6) >> 1),
				(byte)(((int)a1[0] & 1 | (int)a1[0] << 2) << 3 | ((int)a1[1] >> 2 | (int)a1[1] & 135) >> 3),
				(byte)((int)a1[2] >> 7 | ((int)a1[1] & 31 | ((int)a1[1] & 248) << 2) << 1),
				(byte)((int)a1[1] << 7 | ((int)a1[2] & 249 | (int)a1[2] >> 2 & 6) >> 1),
				(byte)(((int)a1[2] & 1 | (int)a1[2] << 2) << 3 | ((int)a1[3] >> 2 | (int)a1[3] & 135) >> 3),
				(byte)((int)a1[0] >> 7 | ((int)a1[3] & 31 | ((int)a1[3] & 248) << 2) << 1)
			};
		}

		private static byte[] sub_403520(byte[] a1)
		{
			return new byte[4] {
				Map4[(int)a1[0] * 16 | (int)a1[1] >> 4],
				Map4[4096 + ((int)a1[2] | (int)a1[1] % 16 * 256)],
				Map4[8192 + ((int)a1[3] * 16 | (int)a1[4] >> 4)],
				Map4[12288 + ((int)a1[5] | (int)a1[4] % 16 * 256)]
			};
		}

		private static byte[] sub_4035A0(byte[] a1)
		{
			byte[] numArray = new byte[4];
			for (int index1 = 0; index1 < 4; ++index1)
			{
				int index2 = (index1 * 256 + (int)a1[index1]) * 4;
				numArray[0] |= Map3[index2];
				numArray[1] |= Map3[index2 + 1];
				numArray[2] |= Map3[index2 + 2];
				numArray[3] |= Map3[index2 + 3];
			}
			return numArray;
		}
	}
}
