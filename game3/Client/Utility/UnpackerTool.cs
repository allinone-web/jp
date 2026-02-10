using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// 解包工具：將 PackerTool 產生的 .pak 還原為 PNG 目錄 + sprite_offsets.txt
// 掛在 Godot 場景，運行後解壓。目錄和檔名請在下方 Export 自行修改。
public partial class UnpackerTool : Node
{
	// ==========================================
	// 配置區域：請在編輯器或代碼裡修改
	// ==========================================

	/// <summary>要解包的 .pak 文件路徑（絕對路徑）</summary>
	[Export]
	public string PakFilePath = "/Users/airtan/Documents/GitHub/game2/Assets/sprites-182.pak";

	/// <summary>解壓輸出的根目錄（會在此目錄下寫入 PNG 與 sprite_offsets.txt）</summary>
	[Export]
	public string OutputDirectory = "/Users/airtan/Documents/game-charlie/spr-bmp/png182";

	/// <summary>輸出的偏移檔檔名（放在 OutputDirectory 下）</summary>
	[Export]
	public string OffsetsFileName = "sprite_offsets-182-update.txt";

	// ==========================================

	public override void _Ready()
	{
		GD.Print("========================================");
		GD.Print("啟動解包工具...");
		GD.Print($"PAK 文件: {PakFilePath}");
		GD.Print($"輸出目錄: {OutputDirectory}");
		GD.Print("========================================");
		CallDeferred("StartUnpacking");
	}

	private void StartUnpacking()
	{
		try
		{
			if (!System.IO.File.Exists(PakFilePath))
			{
				GD.PrintErr($"❌ 錯誤：找不到 PAK 文件: {PakFilePath}");
				return;
			}

			Unpack(PakFilePath, OutputDirectory, OffsetsFileName);
		}
		catch (Exception e)
		{
			GD.PrintErr($"❌ 解包過程發生錯誤: {e.Message}");
			GD.PrintErr(e.StackTrace);
		}
	}

	// 與 PackerTool 對應的索引條目結構（僅用於讀取）
	struct IndexEntry
	{
		public int GfxId;
		public int ActionId;
		public int FrameIdx;
		public long Offset;
		public int Length;
		public short Dx;
		public short Dy;
	}

	private void Unpack(string pakPath, string outDir, string offsetsFileName)
	{
		const string expectedMagic = "SPAK";

		using (var fs = new FileStream(pakPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
		using (var reader = new BinaryReader(fs))
		{
			// 1. 讀頭
			byte[] magic = reader.ReadBytes(4);
			string magicStr = System.Text.Encoding.ASCII.GetString(magic);
			if (magicStr != expectedMagic)
			{
				GD.PrintErr($"❌ 錯誤：無效的 PAK 格式，magic 應為 SPAK，實際為: {magicStr}");
				return;
			}

			int version = reader.ReadInt32();
			int count = reader.ReadInt32();
			GD.Print($"PAK 版本: {version}, 條目數: {count}");

			// 2. 讀索引區（每條 28 字節：GfxId, ActionId, FrameIdx, Offset, Length, Dx, Dy）
			var entries = new List<IndexEntry>();
			for (int i = 0; i < count; i++)
			{
				entries.Add(new IndexEntry
				{
					GfxId = reader.ReadInt32(),
					ActionId = reader.ReadInt32(),
					FrameIdx = reader.ReadInt32(),
					Offset = reader.ReadInt64(),
					Length = reader.ReadInt32(),
					Dx = reader.ReadInt16(),
					Dy = reader.ReadInt16()
				});
			}

			// 3. 建立輸出目錄
			if (!System.IO.Directory.Exists(outDir))
				System.IO.Directory.CreateDirectory(outDir);

			// 4. 寫出每個 PNG 與組裝 sprite_offsets 內容
			var txtLines = new List<string>();
			string currentKey = null;

			for (int i = 0; i < entries.Count; i++)
			{
				IndexEntry e = entries[i];
				string key = $"{e.GfxId}-{e.ActionId}";
				if (key != currentKey)
				{
					txtLines.Add($"#{key}");
					currentKey = key;
				}

				// 檔名與 PackerTool 一致：gfxId-actionId-frameIdx.png
				string pngName = $"{e.GfxId}-{e.ActionId}-{e.FrameIdx:D3}.png";
				string pngPath = Path.Combine(outDir, pngName);

				fs.Seek(e.Offset, SeekOrigin.Begin);
				byte[] pngBytes = reader.ReadBytes(e.Length);
				System.IO.File.WriteAllBytes(pngPath, pngBytes);

				txtLines.Add($"FRAME {e.FrameIdx} dx={e.Dx} dy={e.Dy} bmp={pngName}");

				if ((i + 1) % 500 == 0)
					GD.Print($"已解出: {i + 1} / {count}");
			}

			// 5. 寫出 sprite_offsets.txt（UTF-8，與常見 txt 一致）
			string txtPath = Path.Combine(outDir, offsetsFileName);
			System.IO.File.WriteAllLines(txtPath, txtLines, Encoding.UTF8);

			GD.Print("========================================");
			GD.Print("✅ 解包成功");
			GD.Print($"PNG 與偏移檔已寫入: {outDir}");
			GD.Print($"條目數: {count}");
			GD.Print("========================================");
		}
	}
}
