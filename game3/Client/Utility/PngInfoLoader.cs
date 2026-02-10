// Client/Utility/PngInfoLoader.cs
// 這個類負責讀取 sprite_offsets-138_update.txt，並將數據緩存到內存中，
// 供 CustomCharacterProvider 查詢

using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Client.Utility
{
	// 存储单帧的偏移信息
	public struct PngFrameInfo
	{
		public int Dx;
		public int Dy;
		public string PngName; // e.g., "0-0-000.png"
	}

	public static class PngInfoLoader
	{
		// Key: "GfxId_FileActionId" -> Value: Dictionary<FrameIndex, Info>
		// 例如: "0_0" -> { 0: {dx=12...}, 1: {dx=15...} }
		private static Dictionary<string, Dictionary<int, PngFrameInfo>> _cache = new();

		/// <summary>從 pak 內讀出的字串載入偏移（與 Load(path) 解析格式相同）。</summary>
		public static void LoadFromString(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				GD.PrintErr("[PngInfo] ❌ 偏移內容為空");
				return;
			}
			ParseContent(content);
		}

		public static void Load(string path)
		{
			if (!Godot.FileAccess.FileExists(path))
			{
				GD.PrintErr($"[PngInfo] ❌ 找不到偏移配置文件: {path}");
				return;
			}
			string content = Godot.FileAccess.GetFileAsString(path);
			ParseContent(content);
		}

		private static void ParseContent(string content)
		{
			_cache.Clear();
			using (StringReader reader = new StringReader(content))
			{
				string line;
				string currentKey = null; // 格式 "GfxId_FileActionId"

				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (string.IsNullOrWhiteSpace(line)) continue;

					// 1. 解析 Header: #0-0  => GfxId=0, FileActionId=0
					if (line.StartsWith("#"))
					{
						// 移除 #, split '-'
						string[] parts = line.Substring(1).Split('-');
						if (parts.Length >= 2)
						{
							// 你的格式是 #Gfx-Action
							// 存为 key "0_0"
							currentKey = $"{parts[0]}_{parts[1]}";
							if (!_cache.ContainsKey(currentKey))
								_cache[currentKey] = new Dictionary<int, PngFrameInfo>();
						}
						continue;
					}

					// 2. 解析 Frame: FRAME 0 dx=12 dy=-34 bmp=0-0-000.png ...
					if (line.StartsWith("FRAME") && currentKey != null)
					{
						ParseFrameLine(currentKey, line);
					}
				}
			}
			GD.Print($"[PngInfo] 載入完畢，緩存了 {_cache.Count} 組動作偏移。");
		}

		private static void ParseFrameLine(string key, string line)
		{
			try
			{
				// 简单正则提取
				// FRAME 0 dx=12 dy=-34 bmp=0-0-000.png
				var matchIndex = Regex.Match(line, @"FRAME\s+(\d+)");
				var matchDx = Regex.Match(line, @"dx=(-?\d+)");
				var matchDy = Regex.Match(line, @"dy=(-?\d+)");
				var matchBmp = Regex.Match(line, @"bmp=([^\s]+)"); // 你的例子是 png 后缀在 bmp= 后面

				if (matchIndex.Success && matchDx.Success && matchDy.Success && matchBmp.Success)
				{
					int frameIdx = int.Parse(matchIndex.Groups[1].Value);
					int dx = int.Parse(matchDx.Groups[1].Value);
					int dy = int.Parse(matchDy.Groups[1].Value);
					string pngName = matchBmp.Groups[1].Value;

					_cache[key][frameIdx] = new PngFrameInfo
					{
						Dx = dx,
						Dy = dy,
						PngName = pngName
					};
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[PngInfo] 解析行错误: {line} -> {ex.Message}");
			}
		}

		/// <summary>已載入的動作組數量（用於診斷偏移是否成功載入）。</summary>
		public static int GetLoadedGroupCount() => _cache.Count;

		public static bool TryGetFrame(int gfxId, int fileActionId, int frameIdx, out PngFrameInfo info)
		{
			string key = $"{gfxId}_{fileActionId}";
			if (_cache.TryGetValue(key, out var frames))
			{
				if (frames.TryGetValue(frameIdx, out var result))
				{
					info = result;
					return true;
				}
			}
			info = default;
			return false;
		}
	}
}
