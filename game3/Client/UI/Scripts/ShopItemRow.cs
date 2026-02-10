using Godot;

namespace Client.UI
{
    public partial class ShopItemRow : HBoxContainer
    {
        private Label _itemName;
        private Label _priceLabel;
        private Label _priceValue;
        private Label _countLabel;
        private LineEdit _countInput;
        private ColorRect _bgRect;
        
        public int OrderId { get; set; }
        public int Price { get; set; }
        public bool IsSelected { get; private set; }
        
        [Signal] public delegate void ItemSelectedEventHandler(int orderId);
        [Signal] public delegate void CountChangedEventHandler(int orderId, int count);

        public override void _Ready()
        {
            _itemName = GetNode<Label>("ItemName");
            _priceLabel = GetNode<Label>("PriceLabel");
            _priceValue = GetNode<Label>("PriceValue");
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

        public void Setup(string name, int orderId, int price)
        {
            OrderId = orderId;
            Price = price;
            
            if (_itemName != null) _itemName.Text = name;
            if (_priceValue != null) _priceValue.Text = price.ToString();
            if (_countInput != null) _countInput.Text = "1";
            
            UpdateSelectionStyle();
        }

        private void OnRowSelected()
        {
            // 【多選模式】切換選中狀態
            IsSelected = !IsSelected;
            EmitSignal(SignalName.ItemSelected, OrderId);
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
                if (count < 1)
                {
                    count = 1;
                    if (_countInput != null) _countInput.Text = count.ToString();
                }
                else if (count > 1000) // 限制最大數量
                {
                    count = 1000;
                    if (_countInput != null) _countInput.Text = count.ToString();
                }
                
                EmitSignal(SignalName.CountChanged, OrderId, count);
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
