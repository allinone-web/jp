using Godot;
using System;

namespace Client.Data
{
	// 【核心保留】继承 GodotObject 以支持 Signal 传递
	public partial class CharacterInfo : GodotObject
	{
		// --- 原有属性 ---
		public string Name { get; set; } = "Unknown";
		public string ClanName { get; set; }
		public int Type { get; set; }  // 职业
		public int Sex { get; set; }
		public int Lawful { get; set; }
		public int Level { get; set; } = 1;
		public int Exp { get; set; } = 0;

		// --- 【新增】UI 面板专用属性 (解决 CS1061 报错) ---
		public int CurrentHP { get; set; } = 100;
		public int MaxHP { get; set; } = 100;
		public int CurrentMP { get; set; } = 50;
		public int MaxMP { get; set; } = 50;
		public int Ac { get; set; } = 0; // 防御力

		// 兼容旧代码的字段映射 (如果旧代码用了 Hp/Mp)
		public int Hp { get => CurrentHP; set => CurrentHP = value; }
		public int Mp { get => CurrentMP; set => CurrentMP = value; }

		// --- 基础六围 ---
		public int Str { get; set; }
		public int Dex { get; set; }
		public int Con { get; set; }
		public int Wis { get; set; }
		public int Cha { get; set; }
		public int Int { get; set; }

		/// <summary>管理員等級（0=一般玩家，>0 為 GM）。登入時由 S_CharPacks 讀取，用於選項選單是否顯示 GM 命令連結。</summary>
		public int AccessLevel { get; set; }

		public override string ToString()
		{
			return $"{Name} (Lv.{Level} Class:{Type})";
		}
	}
}