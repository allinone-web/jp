using Godot;
using System;
using System.Collections.Generic;

namespace Client.Utility
{
	/// <summary>
	/// 統一資源解析結果，供 AssetManager 等使用（IMG 單圖/多幀）。
	/// 從 Assetspr 剝離。
	/// </summary>
	public class L1ResourceResult
	{
		public List<Image> Frames { get; set; } = new List<Image>();
		public int Width { get; set; }
		public int Height { get; set; }
		public string FileType { get; set; } = "Unknown";

		public uint PackId { get; set; }
		public uint Header { get; set; }
		public List<byte[]> Blocks { get; set; } = new List<byte[]>();

		public SprFrameMeta[] FrameMetas { get; set; } = Array.Empty<SprFrameMeta>();

		public bool IsValid => Frames != null && Frames.Count > 0;

		public void AddFrame(Image img)
		{
			if (img != null) Frames.Add(img);
		}
	}

	public struct SprFrameMeta
	{
		public int Width;
		public int Height;
		public int AnchorX;
		public int AnchorY;
		public int RectLeft;
		public int RectTop;
		public int RectRight;
		public int RectBottom;
	}

	public class SprResult : L1ResourceResult
	{
	}
}
