using Godot;
using Client.Data;
using Client.Utility;

namespace Client.UI
{
    public partial class ItemTooltip : Control
    {
        private RichTextLabel _nameLabel;
        private RichTextLabel _infoLabel;

        public override void _Ready()
        {
            _nameLabel = GetNode<RichTextLabel>("PanelContainer/VBoxContainer/NameLabel");
            _infoLabel = GetNode<RichTextLabel>("PanelContainer/VBoxContainer/InfoLabel");
            _nameLabel.BbcodeEnabled = false;
            _infoLabel.BbcodeEnabled = false;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        /// <summary>名稱用 DescTable 解析 $數字 與括號內 ($9)、($117)；Bless=0 金色；Ident=0 顯示未鑑定。</summary>
        public void Setup(InventoryItem item)
        {
            if (item == null) return;

            string rawName = item.Name ?? "";
            string displayName = DescTable.Instance != null ? DescTable.Instance.ResolveName(rawName) : rawName;
            _nameLabel.Text = displayName;
            string status = item.IsEquipped ? "[已裝備]" : "[未裝備]";
            if (item.Ident == 0)
                status = "[未鑑定] " + status;
            string info = $"數量: {item.Count}  類型: {item.Type}\n{status}";
            if (!string.IsNullOrEmpty(item.DetailInfo))
                info += "\n\n── 屬性 ──\n" + item.DetailInfo;
            else if (item.Ident == 1)
                info += "\n\n（無額外屬性資料）";
            _infoLabel.Text = info;

            if (item.Bless == 0)
            {
                _nameLabel.AddThemeColorOverride("default_color", Colors.Gold);
                _infoLabel.AddThemeColorOverride("default_color", Colors.Gold);
            }
            else
            {
                _nameLabel.RemoveThemeColorOverride("default_color");
                _infoLabel.RemoveThemeColorOverride("default_color");
            }
        }
    }
}
