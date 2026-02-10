// ============================================================================
// [FILE] Sprite138PakLoader.cs
// 從單一 .pak（或舊 .idx+.pak）讀取角色/怪物/魔法 PNG，介面與 PakLoader 相容。
// 檔名規則：gfxId-actionId-frameIdx.png；dx/dy 由 PngInfoLoader 提供。
// sprite_offsets 從 pak 內讀取，條目名固定為 sprite_offsets-138_update.txt。
// ============================================================================

using Godot;
using System;
using System.Text;

namespace Client.Utility
{
	public class Sprite138PakLoader
	{
		/// <summary>pak 內偏移檔條目名，與打包工具一致，帶副檔名避免出錯。</summary>
		public const string Sprite138OffsetsFileName = "sprite_offsets-138_update.txt";

		private PakArchiveReader _reader;
		private readonly object _lock = new object();

		/// <summary>載入 res://Assets/sprites-138-new2.pak（或 .idx+.pak），並從 pak 內載入 sprite_offsets。</summary>
		public void Load(string baseResOrPath)
		{
			string root;
			string name;
			if (baseResOrPath.StartsWith("res://"))
			{
				string s = baseResOrPath.Replace("\\", "/").TrimEnd('/');
				if (s.EndsWith(".pak")) s = System.IO.Path.GetFileNameWithoutExtension(s) ?? s;
				int last = s.LastIndexOf('/');
				root = last > 0 ? s.Substring(0, last + 1) : "res://Assets/";
				name = last >= 0 ? s.Substring(last + 1) : System.IO.Path.GetFileNameWithoutExtension(baseResOrPath) ?? "sprites-138-new2";
			}
			else
			{
				string basePath = ProjectSettings.GlobalizePath(baseResOrPath);
				string dir = System.IO.Path.GetDirectoryName(basePath);
				name = System.IO.Path.GetFileNameWithoutExtension(basePath);
				root = string.IsNullOrEmpty(dir) ? "res://Assets/" : dir.Replace("\\", "/").TrimEnd('/') + "/";
				if (!root.StartsWith("res://")) root = "res://Assets/";
			}
			if (string.IsNullOrEmpty(name)) name = "sprites-138-new2";

			_reader = new PakArchiveReader();
			_reader.Load(root, name);
			if (_reader.IsLoaded)
			{
				byte[] txtBytes = _reader.GetFile(Sprite138OffsetsFileName);
				if (txtBytes != null && txtBytes.Length > 0)
				{
					PngInfoLoader.LoadFromString(Encoding.UTF8.GetString(txtBytes));
					int groups = PngInfoLoader.GetLoadedGroupCount();
					GD.Print($"[Sprite138PakLoader] 偏移檔已從 pak 載入: {Sprite138OffsetsFileName} → {groups} 組動作");
					if (groups == 0)
						GD.PrintErr("[Sprite138PakLoader] ⚠️ 偏移檔內容為空或格式不符，角色/陰影對齊可能偏移。");
				}
				else
				{
					GD.PrintErr($"[Sprite138PakLoader] ❌ pak 內找不到偏移檔: '{Sprite138OffsetsFileName}'，角色/陰影對齊將錯誤。請用 PakPackTool「png138 打包」重新產生 .pak（會含 sprite_offsets-138_update.txt）。");
					TryLoadOffsetsFromFileFallback();
				}
			}
			GD.Print($"[Sprite138PakLoader] 載入: {name} IsLoaded={_reader?.IsLoaded}");
		}

		/// <summary>pak 內無偏移檔時，嘗試從專案內檔案載入（相容舊流程或未重打包）。</summary>
		private void TryLoadOffsetsFromFileFallback()
		{
			string[] paths = { "res://Assets/sprite_offsets-138_update.txt", "res://Assets/png138/sprite_offsets-138_update.txt" };
			foreach (string path in paths)
			{
				if (Godot.FileAccess.FileExists(path))
				{
					PngInfoLoader.Load(path);
					GD.Print($"[Sprite138PakLoader] 已從檔案載入偏移（回退）: {path} → {PngInfoLoader.GetLoadedGroupCount()} 組");
					return;
				}
			}
		}

		public Texture2D GetTexture(int gfx, int act, int frame, out int dx, out int dy)
		{
			var img = GetImage(gfx, act, frame, out dx, out dy);
			if (img == null) return null;
			return ImageTexture.CreateFromImage(img);
		}

		public Image GetImage(int gfx, int act, int frame, out int dx, out int dy)
		{
			dx = 0;
			dy = 0;
			if (_reader == null || !_reader.IsLoaded) return null;

			string key = $"{gfx}-{act}-{frame:D3}.png";
			byte[] imgBytes;
			lock (_lock)
			{
				imgBytes = _reader.GetFile(key);
			}
			if (imgBytes == null || imgBytes.Length == 0) return null;

			if (PngInfoLoader.TryGetFrame(gfx, act, frame, out var info))
			{
				dx = info.Dx;
				dy = info.Dy;
			}

			var img = new Image();
			if (img.LoadPngFromBuffer(imgBytes) != Error.Ok) return null;
			// [iOS/OpenGL ES] 強制轉為 Rgba8，避免 PNG 解碼出其他格式時在 GL ES 上不顯示
			if (img.GetFormat() != Image.Format.Rgba8)
			{
				try { img.Convert(Image.Format.Rgba8); } catch { /* 轉換失敗仍回傳原圖 */ }
			}
			return img;
		}
	}
}
