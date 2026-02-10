using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using FileAccess = Godot.FileAccess;

namespace Client.Utility
{
	/// <summary>
	/// 資源包讀取器：支援單一 .pak（新格式，長檔名+加密索引）與舊 .idx+.pak（L1 格式）向後相容。
	/// 新格式：.pak = [4B 索引區長度][加密索引][檔案資料]，索引內檔名長度不限。
	/// </summary>
	public class PakArchiveReader
	{
		private string _name;
		private Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
		private string _pakPath;
		private long _dataStart; // 檔案資料區起始位置（新格式用）

		struct Entry { public long Offset; public int Size; }

		public string PackName => _name;
		public bool IsLoaded { get; private set; }

		public List<string> GetAllFilenames() => new List<string>(_entries.Keys);

		public bool HasFile(string fileName)
		{
			if (string.IsNullOrEmpty(fileName)) return false;
			return _entries.ContainsKey(fileName.Trim().ToLowerInvariant());
		}

		/// <summary>
		/// 載入資源包。root 例如 "res://Assets/"；packName 例如 "Img182"。
		/// 優先讀取單一 .pak（新格式）；若不存在則嘗試 .idx + .pak（舊 L1 格式）。
		/// </summary>
		public void Load(string root, string packName)
		{
			IsLoaded = false;
			_entries.Clear();
			_name = packName;
			string basePath = root.TrimEnd('/') + "/" + packName;
			_pakPath = basePath + ".pak";
			_dataStart = 0;

			if (FileAccess.FileExists(_pakPath))
			{
				if (TryLoadSinglePak())
					return;
				GD.PrintErr($"[PakArchiveReader] ⚠️ 無法解析單一 .pak: {_pakPath}");
			}

			string idxPath = basePath + ".idx";
			if (FileAccess.FileExists(idxPath) && FileAccess.FileExists(_pakPath))
			{
				TryLoadLegacyIdxPak(idxPath);
				return;
			}

			GD.PrintErr($"[PakArchiveReader] ❌ 資源遺失: {packName} 路徑: {root}");
		}

		/// <summary>新格式：單一 .pak = [4B indexBlobLen][indexBlob][data]，索引加密、檔名變長。</summary>
		private bool TryLoadSinglePak()
		{
			try
			{
				using var f = FileAccess.Open(_pakPath, FileAccess.ModeFlags.Read);
				if (f == null) return false;

				if (f.GetLength() < 4) return false;
				byte[] lenBuf = f.GetBuffer(4);
				if (lenBuf == null || lenBuf.Length < 4) return false;
				int indexBlobLen = BitConverter.ToInt32(lenBuf, 0);
				if (indexBlobLen <= 0 || indexBlobLen > 10 * 1024 * 1024) return false; // 合理上限 10MB

				byte[] indexBlob = f.GetBuffer(indexBlobLen);
				if (indexBlob == null || indexBlob.Length != indexBlobLen) return false;

				L1PakTools.EnsureLoaded();
				byte[] payload = L1PakTools.Decode(indexBlob, 4);
				if (payload == null || payload.Length < 4) return false;

				_entries.Clear();
				int pos = 0;
				int count = BitConverter.ToInt32(payload, pos);
				pos += 4;
				for (int i = 0; i < count && pos + 2 <= payload.Length; i++)
				{
					int nameLen = (int)BitConverter.ToUInt16(payload, pos);
					pos += 2;
					if (nameLen <= 0 || pos + nameLen + 8 > payload.Length) break;
					string fname = Encoding.UTF8.GetString(payload, pos, nameLen).Trim().ToLowerInvariant();
					pos += nameLen;
					int offset = BitConverter.ToInt32(payload, pos);
					pos += 4;
					int size = BitConverter.ToInt32(payload, pos);
					pos += 4;
					if (!string.IsNullOrEmpty(fname))
						_entries[fname] = new Entry { Offset = offset, Size = size };
				}
				if (_entries.Count == 0)
				{
					GD.PrintErr($"[PakArchiveReader] ❌ {_name} 索引為 0。請確認已匯出 res://Assets/assets_maps/Map1.bin~Map5.bin。");
					_entries.Clear();
					return false;
				}

				_dataStart = 4 + indexBlobLen;
				IsLoaded = true;
				GD.Print($"[PakArchiveReader] 載入資源包: {_name}");
				GD.Print($"[PakArchiveReader] ✅ {_name} 索引建立成功（單一 .pak），共 {_entries.Count} 條");
				return true;
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[PakArchiveReader] 單一 .pak 解析失敗: {ex.Message}");
				_entries.Clear();
				return false;
			}
		}

		/// <summary>舊格式：.idx + .pak，L1 固定 28 位元組條目、20 字元檔名。</summary>
		private void TryLoadLegacyIdxPak(string idxPath)
		{
			GD.Print($"[PakArchiveReader] 載入資源包: {_name}");
			byte[] rawIdx = FileAccess.GetFileAsBytes(idxPath);
			const int ENTRY_SIZE = 28;
			const int NAME_LEN = 20;

			bool encrypted = rawIdx.Length > 28 && !LooksLikePlainAscii(rawIdx, 8, 20);
			byte[] idxData = rawIdx;
			int pos = 4;
			if (encrypted)
			{
				L1PakTools.EnsureLoaded();
				idxData = L1PakTools.Decode(rawIdx, 4);
				pos = 0;
			}

			_entries.Clear();
			_dataStart = 0; // 舊格式 offset 即 .pak 內絕對位置
			while (pos + ENTRY_SIZE <= idxData.Length)
			{
				int offset = BitConverter.ToInt32(idxData, pos);
				string rawName = Encoding.ASCII.GetString(idxData, pos + 4, NAME_LEN);
				string fname = rawName.Replace("\0", "").Trim().ToLowerInvariant();
				int size = BitConverter.ToInt32(idxData, pos + 24);
				if (!string.IsNullOrEmpty(fname))
					_entries[fname] = new Entry { Offset = offset, Size = size };
				pos += ENTRY_SIZE;
			}
			IsLoaded = true;
			GD.Print($"[PakArchiveReader] ✅ {_name} 索引建立成功（.idx+.pak），共 {_entries.Count} 條");
			if (encrypted && _entries.Count == 0)
				GD.PrintErr("[PakArchiveReader] ⚠️ IDX 已加密但解析後條目為 0，請確認 res://Assets/assets_maps/ 存在 Map1～Map5.bin。");
		}

		private static bool LooksLikePlainAscii(byte[] data, int start, int length)
		{
			if (start + length > data.Length) return false;
			for (int i = 0; i < length; i++)
			{
				byte b = data[start + i];
				if (b != 0 && (b < 0x20 || b > 0x7E)) return false;
			}
			return true;
		}

		public byte[] GetFile(string filename)
		{
			if (!IsLoaded) return null;

			string key = filename.Trim().ToLowerInvariant();
			if (!_entries.TryGetValue(key, out var entry)) return null;

			using var f = FileAccess.Open(_pakPath, FileAccess.ModeFlags.Read);
			if (f == null) return null;

			f.Seek((ulong)(_dataStart + entry.Offset));
			return f.GetBuffer(entry.Size);
		}
	}
}
