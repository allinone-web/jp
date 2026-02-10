using Godot;

namespace Client.UI
{
	/// <summary>
	/// 魔法圖標的懸停/點擊提示框，顯示魔法名稱與簡介。效果對齊 ItemTooltip / InventorySlot。
	/// </summary>
	public partial class SkillTooltip : Control
	{
		private Label _nameLabel;
		private RichTextLabel _infoLabel;

		public override void _Ready()
		{
			_nameLabel = GetNodeOrNull<Label>("PanelContainer/VBoxContainer/NameLabel");
			_infoLabel = GetNodeOrNull<RichTextLabel>("PanelContainer/VBoxContainer/InfoLabel");
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public void Setup(int skillId, string skillName, string description)
		{
			if (_nameLabel != null) _nameLabel.Text = string.IsNullOrEmpty(skillName) ? $"魔法 #{skillId}" : skillName;
			if (_infoLabel != null) _infoLabel.Text = string.IsNullOrEmpty(description) ? $"ID: {skillId}" : description;
		}
	}
}
