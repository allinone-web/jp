using Godot;
using System;

namespace Client.Utility
{
	/// <summary>
	/// L1 .img 格式解碼：原始位元組 (ARGB1555/RLE) → Godot Image。
	/// 從 Assetspr 剝離，供登入/選角/創角等 UI 素材使用。
	/// </summary>
	public static class ImgDecoder
	{
		public enum ColorFormat { ARGB1555, RGB565, BGR565, RGB555 }
		public enum MaskMode { Black, None, Green, FirstPixel }

		/// <summary>
		/// 核心解碼入口 (支援 Raw 與 RLE)
		/// </summary>
		public static Image Decode(byte[] data, ColorFormat fmt = ColorFormat.ARGB1555, MaskMode mask = MaskMode.Black)
		{
			if (data == null || data.Length < 4) return null;

			int w = BitConverter.ToUInt16(data, 0);
			int h = BitConverter.ToUInt16(data, 2);

			if (w <= 0 || h <= 0 || w > 4096) return null;

			bool isRle = data.Length < (4 + w * h * 2);

			return isRle ? DecodeRle(data, w, h, fmt) : DecodeRaw(data, w, h, fmt, mask);
		}

		private static Image DecodeRaw(byte[] data, int w, int h, ColorFormat fmt, MaskMode maskMode)
		{
			byte[] pixels = new byte[w * h * 4];
			int ptr = 4;
			ushort? maskColor = (maskMode == MaskMode.Black) ? (ushort)0x0000 : null;

			for (int i = 0; i < (w * h) && ptr + 1 < data.Length; i++)
			{
				ushort c = BitConverter.ToUInt16(data, ptr);
				ptr += 2;
				WriteToBuffer(pixels, i * 4, c, fmt, maskColor);
			}
			return Image.CreateFromData(w, h, false, Image.Format.Rgba8, pixels);
		}

		private static Image DecodeRle(byte[] data, int w, int h, ColorFormat fmt)
		{
			byte[] pixels = new byte[w * h * 4];
			int ptr = 4;

			for (int y = 0; y < h; y++)
			{
				int x = 0;
				while (x < w && ptr < data.Length)
				{
					int skip = data[ptr++];
					x += skip;
					if (x >= w || ptr >= data.Length) break;

					int run = data[ptr++];
					for (int p = 0; p < run; p++)
					{
						if (ptr + 1 >= data.Length) break;
						ushort c = BitConverter.ToUInt16(data, ptr);
						ptr += 2;
						if (x < w) WriteToBuffer(pixels, (y * w + x) * 4, c, fmt, 0x0000);
						x++;
					}
				}
			}
			return Image.CreateFromData(w, h, false, Image.Format.Rgba8, pixels);
		}

		private static void WriteToBuffer(byte[] buf, int idx, ushort c, ColorFormat fmt, ushort? maskColor)
		{
			if (maskColor.HasValue && c == maskColor.Value)
			{
				buf[idx] = 0; buf[idx + 1] = 0; buf[idx + 2] = 0; buf[idx + 3] = 0;
				return;
			}

			byte r = 0, g = 0, b = 0;
			if (fmt == ColorFormat.ARGB1555)
			{
				r = (byte)((c >> 10) & 0x1F);
				g = (byte)((c >> 5) & 0x1F);
				b = (byte)(c & 0x1F);
			}
			buf[idx] = (byte)((r << 3) | (r >> 2));
			buf[idx + 1] = (byte)((g << 3) | (g >> 2));
			buf[idx + 2] = (byte)((b << 3) | (b >> 2));
			buf[idx + 3] = 255;
		}
	}
}
