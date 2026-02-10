using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Client.Utility;

namespace Tool
{
	/// <summary>
	/// 單一 .pak 打包/解包工具：僅輸出一個 .pak（無 .idx），支援長檔名、加密索引。
	/// 格式：[4B 索引區長度][加密索引][檔案資料]。索引內檔名變長（2B+UTF8）。
	/// </summary>
	public static class PakPackTool
	{
		/// <summary>pak 內偏移檔條目名，與讀取端一致，帶副檔名避免出錯。</summary>
		public const string Sprite138OffsetsFileName = "sprite_offsets-138_update.txt";

		public static string LastMessage { get; private set; } = "";

		/// <summary>打包為單一 .pak（無 .idx）。encryptIdx 為 true 時加密索引。</summary>
		public static bool Pack(string sourceFolderRes, string outputBaseRes, bool encryptIdx = true, string filterExtension = ".img")
		{
			LastMessage = "";
			string srcAbs = ResolvePath(sourceFolderRes);
			string outDir = Path.GetDirectoryName(ResolvePath(outputBaseRes));
			string packName = Path.GetFileName(outputBaseRes.TrimEnd('/', '\\'));
			if (string.IsNullOrEmpty(packName)) packName = "pack";

			if (!Directory.Exists(srcAbs))
			{
				LastMessage = $"❌ 來源資料夾不存在:\n{srcAbs}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}
			if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

			var files = Directory.GetFiles(srcAbs)
				.Where(f => filterExtension == null || f.EndsWith(filterExtension, StringComparison.OrdinalIgnoreCase))
				.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (files.Count == 0)
			{
				LastMessage = $"❌ 無符合檔案\n來源: {srcAbs}\n副檔名: {filterExtension ?? "全部"}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}

			var fileList = new List<(string Name, byte[] Data)>();
			foreach (string f in files)
			{
				string name = Path.GetFileName(f);
				byte[] data = File.ReadAllBytes(f);
				fileList.Add((name, data));
			}

			string pakPath = Path.Combine(outDir, packName + ".pak");
			bool ok = WriteSinglePak(pakPath, fileList, encryptIdx);
			if (ok)
			{
				LastMessage = $"✅ 打包完成（單一 .pak）\n共 {fileList.Count} 個檔案\n.pak: {pakPath}";
				GD.Print($"[PakPackTool] {LastMessage}");
			}
			return ok;
		}

		/// <summary>解包：支援單一 .pak（新格式）或 .pak 路徑。outputFolderRes 為輸出資料夾。</summary>
		public static bool Unpack(string pakPathRes, string outputFolderRes)
		{
			LastMessage = "";
			string pakAbs = ResolvePath(pakPathRes);
			if (pakAbs.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
				pakAbs = Path.ChangeExtension(pakAbs, ".pak");
			string outAbs = ResolvePath(outputFolderRes);

			if (!File.Exists(pakAbs))
			{
				LastMessage = $"❌ 找不到 .pak\n請求: {pakPathRes}\n解析後: {pakAbs}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}
			try
			{
				if (!Directory.Exists(outAbs)) Directory.CreateDirectory(outAbs);
			}
			catch (Exception ex)
			{
				LastMessage = $"❌ 無法建立輸出目錄:\n{outAbs}\n{ex.Message}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}

			// 嘗試新格式（單一 .pak）
			int written = UnpackSinglePak(pakAbs, outAbs);
			if (written >= 0)
			{
				LastMessage = $"✅ 解包完成\n來源: {pakAbs}\n輸出: {outAbs}\n已寫出: {written} 個檔案";
				GD.Print($"[PakPackTool] {LastMessage}");
				return true;
			}

			// 舊格式：需 .idx
			string idxPath = Path.ChangeExtension(pakAbs, ".idx");
			if (!File.Exists(idxPath))
			{
				LastMessage = $"❌ 無法解析 .pak（非新格式），且找不到 .idx: {idxPath}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}
			return UnpackLegacyIdxPak(idxPath, pakAbs, outAbs);
		}

		/// <summary>寫入單一 .pak：索引加密、長檔名。fileList 為 (檔名, 內容)。</summary>
		internal static bool WriteSinglePak(string pakPath, List<(string Name, byte[] Data)> fileList, bool encryptIdx)
		{
			var payload = new List<byte>();
			payload.AddRange(BitConverter.GetBytes(fileList.Count));
			int dataOffset = 0;
			foreach (var (name, data) in fileList)
			{
				byte[] nameBytes = Encoding.UTF8.GetBytes(name ?? "");
				payload.AddRange(BitConverter.GetBytes((ushort)nameBytes.Length));
				payload.AddRange(nameBytes);
				payload.AddRange(BitConverter.GetBytes(dataOffset));
				payload.AddRange(BitConverter.GetBytes(data.Length));
				dataOffset += data.Length;
			}

			byte[] indexPayload = payload.ToArray();
			byte[] indexBlob = encryptIdx ? L1PakTools.Encode(indexPayload) : Prepend4Zero(indexPayload);
			if (indexBlob == null || indexBlob.Length == 0 && encryptIdx)
			{
				LastMessage = "❌ 索引加密失敗。請確認 res://Assets/assets_maps/ 存在 Map1～Map5.bin";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}

			try
			{
				using (var fs = new FileStream(pakPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
				{
					fs.Write(BitConverter.GetBytes(indexBlob.Length), 0, 4);
					fs.Write(indexBlob, 0, indexBlob.Length);
					foreach (var (_, data) in fileList)
						fs.Write(data, 0, data.Length);
				}
			}
			catch (Exception ex)
			{
				LastMessage = $"❌ 寫入 .pak 失敗:\n{pakPath}\n{ex.Message}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}
			return true;
		}

		/// <summary>解包單一 .pak（新格式）。傳回寫出檔案數，失敗傳回 -1。</summary>
		private static int UnpackSinglePak(string pakAbs, string outAbs)
		{
			try
			{
				using (var fs = new FileStream(pakAbs, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
				{
					if (fs.Length < 4) return -1;
					byte[] lenBuf = new byte[4];
					if (fs.Read(lenBuf, 0, 4) != 4) return -1;
					int indexBlobLen = BitConverter.ToInt32(lenBuf, 0);
					if (indexBlobLen <= 0 || indexBlobLen > 10 * 1024 * 1024) return -1;

					byte[] indexBlob = new byte[indexBlobLen];
					if (fs.Read(indexBlob, 0, indexBlobLen) != indexBlobLen) return -1;

					L1PakTools.EnsureLoaded();
					byte[] payload = L1PakTools.Decode(indexBlob, 4);
					if (payload == null || payload.Length < 4) return -1;

					long dataStart = 4 + indexBlobLen;
					int pos = 0;
					int count = BitConverter.ToInt32(payload, pos);
					pos += 4;
					int writtenCount = 0;
					for (int i = 0; i < count && pos + 2 <= payload.Length; i++)
					{
						int nameLen = (int)BitConverter.ToUInt16(payload, pos);
						pos += 2;
						if (nameLen <= 0 || pos + nameLen + 8 > payload.Length) break;
						string fname = Encoding.UTF8.GetString(payload, pos, nameLen);
						pos += nameLen;
						int offset = BitConverter.ToInt32(payload, pos);
						pos += 4;
						int size = BitConverter.ToInt32(payload, pos);
						pos += 4;
						if (string.IsNullOrWhiteSpace(fname)) continue;

						string outPath = Path.Combine(outAbs, fname.Trim());
						string outDir = Path.GetDirectoryName(outPath);
						if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
							Directory.CreateDirectory(outDir);
						fs.Seek(dataStart + offset, SeekOrigin.Begin);
						byte[] data = new byte[size];
						if (fs.Read(data, 0, size) == size)
						{
							try { File.WriteAllBytes(outPath, data); writtenCount++; }
							catch (Exception ex) { GD.PrintErr($"[PakPackTool] 寫出失敗: {outPath}: {ex.Message}"); }
						}
					}
					return writtenCount;
				}
			}
			catch { return -1; }
		}

		private static bool UnpackLegacyIdxPak(string idxPath, string pakPath, string outAbs)
		{
			const int ENTRY_SIZE = 28;
			const int NAME_LEN = 20;
			byte[] rawIdx = File.ReadAllBytes(idxPath);
			byte[] idxData = rawIdx;
			int pos = 4;
			bool encrypted = rawIdx.Length > 28 && !LooksLikePlainAscii(rawIdx, 8, 20);
			if (encrypted)
			{
				L1PakTools.EnsureLoaded();
				idxData = L1PakTools.Decode(rawIdx, 4);
				pos = 0;
			}
			int written = 0;
			using (var pakStream = new FileStream(pakPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
			{
				while (pos + ENTRY_SIZE <= idxData.Length)
				{
					int offset = BitConverter.ToInt32(idxData, pos);
					string fname = Encoding.ASCII.GetString(idxData, pos + 4, NAME_LEN).Replace("\0", "").Trim();
					int size = BitConverter.ToInt32(idxData, pos + 24);
					pos += ENTRY_SIZE;
					if (string.IsNullOrEmpty(fname)) continue;
					string outPath = Path.Combine(outAbs, fname);
					try
					{
						pakStream.Seek(offset, SeekOrigin.Begin);
						byte[] data = new byte[size];
						if (pakStream.Read(data, 0, size) == size)
						{
							Directory.CreateDirectory(Path.GetDirectoryName(outPath));
							File.WriteAllBytes(outPath, data);
							written++;
						}
					}
					catch (Exception ex) { GD.PrintErr($"[PakPackTool] 解包失敗: {fname}: {ex.Message}"); }
				}
			}
			LastMessage = $"✅ 解包完成（舊格式 .idx+.pak）\n輸出: {outAbs}\n已寫出: {written} 個檔案";
			GD.Print($"[PakPackTool] {LastMessage}");
			return true;
		}

		static byte[] Prepend4Zero(byte[] payload)
		{
			var result = new byte[4 + payload.Length];
			Array.Copy(payload, 0, result, 4, payload.Length);
			return result;
		}

		/// <summary>png138 打包：僅輸出單一 .pak，偏移檔名為 sprite_offsets-138_update.txt。</summary>
		public static bool PackPng138(string txtPathAbs, string pngDirAbs, string outputBaseRes, string offsetsFileName = "sprite_offsets-138_update.txt", bool encryptIdx = true)
		{
			LastMessage = "";
			string txtAbs = ResolvePath(txtPathAbs);
			string pngAbs = ResolvePath(pngDirAbs);
			string baseAbs = ResolvePath(outputBaseRes);
			string outDir = Path.GetDirectoryName(baseAbs);
			string packName = Path.GetFileName(outputBaseRes.TrimEnd('/', '\\'));
			if (string.IsNullOrEmpty(packName)) packName = "sprites-138-new2";
			if (string.IsNullOrEmpty(outDir)) outDir = Directory.GetCurrentDirectory();

			if (!File.Exists(txtAbs))
			{
				LastMessage = $"❌ 找不到 txt 檔:\n{txtAbs}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}
			if (!Directory.Exists(pngAbs))
			{
				LastMessage = $"❌ 找不到 PNG 目錄:\n{pngAbs}";
				GD.PrintErr($"[PakPackTool] {LastMessage}");
				return false;
			}
			try { if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir); }
			catch (Exception ex) { LastMessage = $"❌ 無法建立輸出目錄: {ex.Message}"; return false; }

			var fileList = new List<(string Name, byte[] Data)>();
			string[] lines = File.ReadAllLines(txtAbs);
			string currentKey = "";

			foreach (var line in lines)
			{
				string l = line.Trim();
				if (string.IsNullOrEmpty(l)) continue;
				if (l.StartsWith("#")) { currentKey = l.Substring(1).Trim(); continue; }
				if (l.StartsWith("FRAME"))
				{
					var matchDx = Regex.Match(l, @"dx=(-?\d+)");
					var matchDy = Regex.Match(l, @"dy=(-?\d+)");
					var matchBmp = Regex.Match(l, @"bmp=([^\s]+)");
					var matchIdx = Regex.Match(l, @"FRAME\s+(\d+)");
					if (!matchBmp.Success || !matchDx.Success || !matchDy.Success || string.IsNullOrEmpty(currentKey)) continue;

					string pngName = Path.ChangeExtension(matchBmp.Groups[1].Value, ".png");
					string fullPath = Path.Combine(pngAbs, pngName);
					if (!File.Exists(fullPath)) continue;
					if (pngName.EndsWith("-a.png", StringComparison.OrdinalIgnoreCase) || pngName.EndsWith("a.png", StringComparison.OrdinalIgnoreCase)) continue;
					if (new FileInfo(fullPath).Length == 0) continue;

					var keys = currentKey.Split('-');
					if (keys.Length < 2) continue;
					if (!int.TryParse(keys[0].Trim(), out _) || !int.TryParse(keys[1].Trim(), out _) || !matchIdx.Success) continue;

					fileList.Add((pngName.ToLowerInvariant(), File.ReadAllBytes(fullPath)));
				}
			}

			byte[] txtContent = File.ReadAllBytes(txtAbs);
			fileList.Add((Sprite138OffsetsFileName, txtContent));

			string pakPath = Path.Combine(outDir, packName + ".pak");
			bool ok = WriteSinglePak(pakPath, fileList, encryptIdx);
			if (ok)
			{
				LastMessage = $"✅ png138 打包完成（單一 .pak）\n共 {fileList.Count} 條（含 {offsetsFileName}）\n.pak: {pakPath}";
				GD.Print($"[PakPackTool] {LastMessage}");
			}
			return ok;
		}

		public static bool UnpackPng138(string pakPathRes, string outputFolderRes)
		{
			return Unpack(pakPathRes, outputFolderRes);
		}

		static bool LooksLikePlainAscii(byte[] data, int start, int length)
		{
			if (start + length > data.Length) return false;
			for (int i = 0; i < length; i++)
			{
				byte b = data[start + i];
				if (b != 0 && (b < 0x20 || b > 0x7E)) return false;
			}
			return true;
		}

		static string ResolvePath(string resOrAbs)
		{
			if (resOrAbs.StartsWith("res://"))
				return ProjectSettings.GlobalizePath(resOrAbs);
			return Path.GetFullPath(resOrAbs);
		}
	}
}
