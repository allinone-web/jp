using Godot;

namespace Client.UI
{
    public partial class WarehouseItemRow : HBoxContainer
    {
        private Label _itemName;
        private Label _countLabel;
        private LineEdit _countInput;
        private ColorRect _bgRect;
        
        public int ItemId { get; set; } // ObjectId (存儲) 或 Uid (取出)
        public int MaxCount { get; set; }
        public string ItemName { get; set; }
        public bool IsSelected { get; private set; }
        
        [Signal] public delegate void CountChangedEventHandler(int itemId, int count);
        [Signal] public delegate void ItemSelectedEventHandler(int itemId);

        public override void _Ready()
        {
            _itemName = GetNode<Label>("ItemName");
            _countLabel = GetNode<Label>("CountLabel");
            _countInput = GetNode<LineEdit>("CountInput");
            
            // 創建背景矩形用於顯示選中狀態
            _bgRect = new ColorRect();
            _bgRect.MouseFilter = MouseFilterEnum.Ignore;
            _bgRect.Color = new Color(0, 0, 0, 0);
            AddChild(_bgRect);
            MoveChild(_bgRect, 0);
            _bgRect.SetAnchorsPreset(LayoutPreset.FullRect);
            
            // 讓整行可點擊
            MouseFilter = MouseFilterEnum.Stop;
            GuiInput += OnGuiInput;
            
            if (_countInput != null)
            {
                _countInput.TextChanged += OnCountInputChanged;
                _countInput.GuiInput += (ev) => { }; // 阻止事件冒泡
            }
        }
        
        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                OnRowSelected();
            }
        }

        public void Setup(string name, int itemId, int maxCount)
        {
            ItemName = name;
            ItemId = itemId;
            MaxCount = maxCount;
            
            if (_itemName != null) _itemName.Text = $"{name} ({maxCount})";
            if (_countInput != null) _countInput.Text = maxCount.ToString();
            
            UpdateSelectionStyle();
        }

        private void OnRowSelected()
        {
            // 【多選模式】切換選中狀態
            IsSelected = !IsSelected;
            EmitSignal(SignalName.ItemSelected, ItemId);
            UpdateSelectionStyle();
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            UpdateSelectionStyle();
        }

        private void UpdateSelectionStyle()
        {
            if (_bgRect != null)
            {
                _bgRect.Color = IsSelected ? new Color(0.2f, 0.4f, 0.8f, 0.3f) : new Color(0, 0, 0, 0);
            }
        }

        private void OnCountInputChanged(string newText)
        {
            if (int.TryParse(newText, out int count))
            {
                // 限制數量範圍
                if (count > MaxCount)
                {
                    count = MaxCount;
                    if (_countInput != null) _countInput.Text = count.ToString();
                }
                else if (count < 1)
                {
                    count = 1;
                    if (_countInput != null) _countInput.Text = count.ToString();
                }
                
                EmitSignal(SignalName.CountChanged, ItemId, count);
            }
        }

        public int GetCount()
        {
            if (_countInput != null && int.TryParse(_countInput.Text, out int count))
            {
                return count;
            }
            return 1;
        }
    }
}
