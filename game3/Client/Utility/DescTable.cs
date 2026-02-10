using Godot;
using System;
using System.Collections.Generic;

namespace Client.Utility 
{
    // 专门解析 desc.txt (格式: desc \t $ID \t kText \t cText)
    public partial class DescTable : Node
    {
        public static DescTable Instance { get; private set; }
        
        private Dictionary<int, string> _descMap = new Dictionary<int, string>();
        private Dictionary<int, string> _skillNameMap = new Dictionary<int, string>();

        public override void _Ready()
        {
            Instance = this;
            LoadDescTable("res://Assets/desc.txt");
        }

        private void LoadDescTable(string path)
        {
            if (!Godot.FileAccess.FileExists(path))
            {
                GD.PrintErr($"[DescTable] File not found: {path}");
                return;
            }

            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            while (!file.EofReached())
            {
                string line = file.GetLine().Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var parts = line.Split(new char[] { '\t' }, StringSplitOptions.None);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim();
                if (key == "desc" && parts.Length >= 4 && parts[1].Trim().StartsWith("$"))
                {
                    string idStr = parts[1].Trim().Substring(1);
                    if (int.TryParse(idStr, out int descId))
                    {
                        _descMap[descId] = parts[parts.Length - 1].Trim();
                    }
                }
                else if (key == "skill" && parts.Length >= 3)
                {
                    if (int.TryParse(parts[1].Trim(), out int skillId))
                    {
                        _skillNameMap[skillId] = parts[2].Trim();
                    }
                }
            }
            GD.Print($"[DescTable] Loaded {_descMap.Count} desc, {_skillNameMap.Count} skills.");
        }

        public string GetName(int id)
        {
            if (_descMap.TryGetValue(id, out string text))
                return text;
            return $"${id}";
        }

        public string GetSkillName(int skillId)
        {
            if (_skillNameMap.TryGetValue(skillId, out string name) && !string.IsNullOrEmpty(name))
                return name;
            return $"魔法 #{skillId}";
        }
        
        /// <summary>解析名稱：$數字 與括號內 ($9)、($117) 都查 desc 替換。例如 "+0 $261" → "+0 變形控制戒指"，"($9)" → "(揮舞)"。</summary>
        public string ResolveName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string s = rawName;
            // 1) 替換 $數字（含 "+0 $261" 中的 $261）
            while (true)
            {
                int i = s.IndexOf('$');
                if (i < 0) break;
                string after = s.Substring(i + 1);
                int len = 0;
                while (len < after.Length && char.IsDigit(after[len])) len++;
                if (len == 0) break;
                if (!int.TryParse(after.Substring(0, len), out int id)) break;
                string translated = GetName(id);
                if (translated == $"${id}") break;
                s = s.Replace("$" + after.Substring(0, len), translated);
            }
            // 2) 替換括號內 ($9)、($117) 等
            while (true)
            {
                int open = s.IndexOf("($");
                if (open < 0) break;
                int close = s.IndexOf(')', open);
                if (close <= open + 2) break;
                string inner = s.Substring(open + 2, close - open - 2).Trim();
                int len = 0;
                while (len < inner.Length && char.IsDigit(inner[len])) len++;
                if (len == 0) break;
                if (!int.TryParse(inner.Substring(0, len), out int id)) break;
                string translated = GetName(id);
                string toReplace = "($" + inner.Substring(0, len) + ")";
                s = s.Replace(toReplace, "(" + translated + ")");
            }
            return s;
        }
    }
}