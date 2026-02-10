// 對齊 server/skills.sql：客戶端本地緩存 skill_id -> reuse_delay
// 用於 UI 冷卻計時，與服務器資料庫一致
using Godot;
using System;
using System.Collections.Generic;

namespace Client.Data
{
	public static class SkillDbData
	{
		private static Dictionary<int, int> _reuseDelays;
		private static Dictionary<int, int> _actionIds;
		private static Dictionary<int, int> _ranged;
		private static bool _loaded;

		private static readonly string[] CsvPaths = new[]
		{
			"res://Client/Data/skills.csv",
			"res://Data/skills.csv"
		};

		public static void EnsureLoaded()
		{
			if (_loaded) return;
			_loaded = true;
			_reuseDelays = new Dictionary<int, int>();
			_actionIds = new Dictionary<int, int>();
			_ranged = new Dictionary<int, int>();

			string pathUsed = null;
			using (var fa = TryOpenCsv(out pathUsed))
			{
				if (fa == null)
				{
					GD.PrintErr("[SkillDbData] 未找到 skills.csv，已嘗試: " + string.Join(", ", CsvPaths));
					return;
				}

				string header = fa.GetLine();
				if (string.IsNullOrEmpty(header) || !header.StartsWith("id", StringComparison.OrdinalIgnoreCase))
				{
					GD.PrintErr("[SkillDbData] CSV 首行應為 skills.sql 對應欄位");
					return;
				}
				var headerParts = header.Split(',');
				int idxId = IndexOfHeader(headerParts, "id");
				int idxReuse = IndexOfHeader(headerParts, "reuse_delay");
				int idxAction = IndexOfHeader(headerParts, "action_id");
				int idxRanged = IndexOfHeader(headerParts, "ranged");

				while (fa.GetPosition() < fa.GetLength())
				{
					string line = fa.GetLine();
					if (string.IsNullOrWhiteSpace(line)) continue;
					var parts = line.Split(',');
					if (parts.Length < 9) continue;
					if (idxId < 0 || idxReuse < 0) continue;
					if (idxId >= parts.Length || idxReuse >= parts.Length) continue;
					if (!int.TryParse(parts[idxId].Trim(), out int skillId)) continue;
					if (!int.TryParse(parts[idxReuse].Trim(), out int reuseDelay)) reuseDelay = 0;
					_reuseDelays[skillId] = reuseDelay;
					if (idxAction >= 0 && idxAction < parts.Length)
					{
						if (!int.TryParse(parts[idxAction].Trim(), out int actionId)) actionId = 0;
						_actionIds[skillId] = actionId;
					}
					if (idxRanged >= 0 && idxRanged < parts.Length)
					{
						if (!int.TryParse(parts[idxRanged].Trim(), out int ranged)) ranged = 0;
						_ranged[skillId] = ranged;
					}
				}
			}

			GD.Print($"[SkillDbData] 已載入 {_reuseDelays.Count} 條技能冷卻 (path: {pathUsed})");
		}

		private static FileAccess TryOpenCsv(out string pathUsed)
		{
			pathUsed = null;
			foreach (string path in CsvPaths)
			{
				var fa = FileAccess.Open(path, FileAccess.ModeFlags.Read);
				if (fa != null)
				{
					pathUsed = path;
					return fa;
				}
			}
			return null;
		}

		public static int GetReuseDelay(int skillId)
		{
			EnsureLoaded();
			if (_reuseDelays != null && _reuseDelays.TryGetValue(skillId, out int v))
				return v;
			return 0;
		}

		public static int GetActionId(int skillId)
		{
			EnsureLoaded();
			if (_actionIds != null && _actionIds.TryGetValue(skillId, out int v))
				return v;
			return 0;
		}

		public static int GetRanged(int skillId)
		{
			EnsureLoaded();
			if (_ranged != null && _ranged.TryGetValue(skillId, out int v))
				return v;
			return 0;
		}

		private static int IndexOfHeader(string[] headers, string name)
		{
			for (int i = 0; i < headers.Length; i++)
			{
				if (string.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
					return i;
			}
			return -1;
		}
	}
}
