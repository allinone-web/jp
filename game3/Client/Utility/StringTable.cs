using Godot;
using System;
using System.Collections.Generic;

// 【修复 1】改名为 Client.Utility，防止遮挡 System 命名空间
namespace Client.Utility 
{
	public partial class StringTable : Node
	{
		public static StringTable Instance { get; private set; }
		
		private Dictionary<int, string> _textMap = new Dictionary<int, string>();

		public override void _Ready()
		{
			Instance = this;
			LoadStringTable("res://Assets/string.txt"); 
		}

		private void LoadStringTable(string path)
		{
			// 【修复 2】明确使用 Godot.FileAccess
			if (!Godot.FileAccess.FileExists(path))
			{
				GD.PrintErr($"[StringTable] File not found: {path}");
				return;
			}

			// 【修复 2】明确使用 Godot.FileAccess
			using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
			while (!file.EofReached())
			{
				string line = file.GetLine().Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

				string[] parts = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
				
				if (parts.Length >= 2 && parts[1].StartsWith("$"))
				{
					string idStr = parts[1].Substring(1); 
					if (int.TryParse(idStr, out int id))
					{
						string cText = parts[parts.Length - 1];
						_textMap[id] = cText;
					}
				}
			}
			GD.Print($"[StringTable] Loaded {_textMap.Count} strings.");
		}

		public string GetText(int id)
		{
			if (_textMap.TryGetValue(id, out string text))
			{
				return text;
			}
			return $"${id}"; 
		}
		
		public string ResolveName(string rawName)
		{
			if (string.IsNullOrEmpty(rawName)) return "";
			
			if (rawName.StartsWith("$"))
			{
				string[] parts = rawName.Split(' ');
				string idPart = parts[0].Substring(1);
				
				if (int.TryParse(idPart, out int id))
				{
					string translated = GetText(id);
					if (parts.Length > 1) 
						return translated + " " + rawName.Substring(parts[0].Length + 1);
					else
						return translated;
				}
			}
			return rawName;
		}
	}
}
