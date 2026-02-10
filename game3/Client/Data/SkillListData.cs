// 對齊 server/skill_list.sql：客戶端本地緩存 skill_id -> cast_gfx, range, type
// 用於「先播放魔法動畫、再等伺服器 Op57 結算傷害」
using Godot;
using System;
using System.Collections.Generic;

namespace Client.Data
{
    /// <summary>
    /// 單條技能數據（與 skill_list 表 cast_gfx / range / type 對齊）
    /// </summary>
    public class SkillListEntry
    {
        public int SkillId;
        public int CastGfx;
        public int Range;
        public string Type;

        public SkillListEntry(int skillId, int castGfx, int range, string type)
        {
            SkillId = skillId;
            CastGfx = castGfx;
            Range = range;
            Type = type ?? "";
        }
    }

    /// <summary>
    /// 從本地 CSV（對應 server/skill_list.sql）載入技能表，供魔法播放與範圍/類型判斷。
    /// </summary>
    public static class SkillListData
    {
        private static Dictionary<int, SkillListEntry> _table;
        private static bool _loaded;

        private static readonly string[] CsvPaths = new[]
        {
            "res://Client/Data/skill_list.csv",
            "res://Data/skill_list.csv"
        };

        /// <summary>
        /// 載入 CSV 到內存（首次訪問時懶加載）
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _table = new Dictionary<int, SkillListEntry>();
            _loaded = true;

            string pathUsed = null;
            using (var fa = TryOpenCsv(out pathUsed))
            {
                if (fa == null)
                {
                    GD.PrintErr("[SkillListData] 未找到 skill_list.csv，已嘗試: " + string.Join(", ", CsvPaths));
                    return;
                }
                string header = fa.GetLine();
                if (string.IsNullOrEmpty(header) || !header.StartsWith("skill_id", StringComparison.OrdinalIgnoreCase))
                {
                    GD.PrintErr("[SkillListData] CSV 首行應為 skill_id,cast_gfx,range,type");
                    return;
                }
                while (fa.GetPosition() < fa.GetLength())
                {
                    string line = fa.GetLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length < 4) continue;
                    if (!int.TryParse(parts[0].Trim(), out int skillId)) continue;
                    int.TryParse(parts[1].Trim(), out int castGfx);
                    int.TryParse(parts[2].Trim(), out int range);
                    string type = parts[3].Trim();
                    _table[skillId] = new SkillListEntry(skillId, castGfx, range, type);
                }
            }
            GD.Print($"[SkillListData] 已載入 {_table.Count} 條技能 (path: {pathUsed})");
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

        /// <summary>
        /// 取得技能數據；若未載入則先載入。無則返回 null。
        /// </summary>
        public static SkillListEntry Get(int skillId)
        {
            EnsureLoaded();
            return _table != null && _table.TryGetValue(skillId, out var e) ? e : null;
        }
    }
}
