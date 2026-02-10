using Godot;
using System.Collections.Generic;
using Client.Game;
using Client.Utility;

namespace Client.UI
{
	public partial class SkillWindow : GameWindow
	{
		private TabContainer _tabs;
		private Dictionary<int, SkillSlot> _slots = new Dictionary<int, SkillSlot>();

		public override void _Ready()
		{
			base._Ready();
			var container = FindChild("ContentContainer", true, false);
			if (container == null) container = this;
			_tabs = container.FindChild("TabContainer", true, false) as TabContainer;
			if (_tabs != null)
				InitSkillSlots();
			else
				GD.PrintErr("[SkillWindow] ❌ 找不到 TabContainer！");
		}

		// 角色最多 50 個可學習魔法圖標，每排 4 個，圖標 40x40
		private void InitSkillSlots()
		{
			CreateTab("全部魔法", 1, 50);
		}

		private void CreateTab(string title, int startId, int endId)
		{
			var grid = new GridContainer();
			grid.Name = title;
			grid.Columns = 5;
			_tabs.AddChild(grid);

			for (int i = startId; i <= endId; i++)
			{
				string name = DescTable.Instance != null ? DescTable.Instance.GetSkillName(i) : $"魔法 #{i}";
				var slot = new SkillSlot();
				slot.Name = $"Skill_{i}";
				slot.Setup(i, i.ToString(), name, $"技能 ID: {i}");
				grid.AddChild(slot);
				_slots[i] = slot;
			}
		}

		// --- 核心：刷新数据 ---
		// 由 GameWorld.RefreshWindows() 调用
		public void RefreshSkills(int[] skillMasks)
		{
			if (_slots.Count == 0 || skillMasks == null) return;

			// 遍历所有已生成的图标，检查是否点亮
			foreach (var kvp in _slots)
			{
				int skillId = kvp.Key;
				SkillSlot slot = kvp.Value;

				bool learned = CheckLearned(skillId, skillMasks);
				slot.SetLearned(learned);
			}
		}

		/// <summary>檢查是否已學（與 GameWorld.HasLearnedSkill 一致）。僅適用 PC 魔法 1–50：levelIdx=(skillId-1)/5, bitIdx=(skillId-1)%5。</summary>
		private bool CheckLearned(int skillId, int[] masks)
		{
			if (skillId < 1 || skillId > 50) return false;
			int levelIdx = (skillId - 1) / 5;
			int bitIdx = (skillId - 1) % 5;
			if (levelIdx >= 0 && levelIdx < masks.Length)
				return (masks[levelIdx] & (1 << bitIdx)) != 0;
			return false;
		}
	}
}
