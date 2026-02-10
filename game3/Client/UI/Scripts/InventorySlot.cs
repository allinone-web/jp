using Godot;
using Client.Data;

namespace Client.UI
{
	public partial class InventorySlot : Control
	{
		[Signal] public delegate void ItemClickedEventHandler(int objectId);
		[Signal] public delegate void ItemDoubleClickedEventHandler(int objectId);

		public InventoryItem ItemData;
		
		private TextureRect _icon;
		private Label _count;
		private Button _btn;
		private ColorRect _bg;
		private Label _selectedMark; // 【修復】選中標記：顯示 "X" 文字

		private const string DEFAULT_ICON_PATH = "res://Assets/default_item.png"; 
		private static Texture2D _cachedDefaultTexture;
		private static CanvasLayer _tooltipLayer;
		private static Control _currentTooltipInstance;
		private bool _isHovering = false;
		private bool _isSelected = false;
		private long _lastClickMs = 0;

		public override void _Ready()
		{
			_icon = GetNode<TextureRect>("Icon");
			_count = GetNode<Label>("Count");
			_btn = GetNode<Button>("Btn");
			_bg = GetNodeOrNull<ColorRect>("ColorRect");

			// 【關鍵修復】確保 InventorySlot 本身可以接收拖拽事件
			MouseFilter = MouseFilterEnum.Stop;

			// 選中標記：小 v（取代大 X）
			_selectedMark = new Label();
			_selectedMark.Text = "v";
			_selectedMark.AddThemeFontSizeOverride("font_size", 14);
			_selectedMark.AddThemeColorOverride("font_color", new Color(0.2f, 0.9f, 0.3f, 1f)); // 綠色
			_selectedMark.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
			_selectedMark.AddThemeConstantOverride("outline_size", 1);
			_selectedMark.HorizontalAlignment = HorizontalAlignment.Right;
			_selectedMark.VerticalAlignment = VerticalAlignment.Bottom;
			_selectedMark.MouseFilter = MouseFilterEnum.Ignore;
			_selectedMark.Visible = false;
			_selectedMark.SetAnchorsPreset(LayoutPreset.FullRect);
			_selectedMark.OffsetLeft = -4;
			_selectedMark.OffsetTop = -4;
			_selectedMark.OffsetRight = 4;
			_selectedMark.OffsetBottom = 4;
			AddChild(_selectedMark);

			// 【关键修复】让内部组件不拦截鼠标事件，传给父节点处理拖拽
			if (_icon != null) _icon.MouseFilter = MouseFilterEnum.Ignore;
			if (_count != null) _count.MouseFilter = MouseFilterEnum.Ignore;
			if (_bg != null) _bg.MouseFilter = MouseFilterEnum.Ignore;

			if (_btn != null)
			{
				_btn.FocusMode = FocusModeEnum.None;
				// 【关键修复】改为 Pass，这样拖拽事件可以穿透到 InventorySlot
				_btn.MouseFilter = MouseFilterEnum.Pass; 
				_btn.GuiInput += OnButtonGuiInput;
				_btn.MouseEntered += OnMouseEntered;
				_btn.MouseExited += OnMouseExited;
			}
			
			SetProcess(true);

			if (_cachedDefaultTexture == null && ResourceLoader.Exists(DEFAULT_ICON_PATH))
				_cachedDefaultTexture = ResourceLoader.Load<Texture2D>(DEFAULT_ICON_PATH);
		}
		
		private void OnButtonGuiInput(InputEvent @event)
		{
			if (ItemData == null) return;
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				long now = (long)Time.GetTicksMsec();
				bool manualDouble = _lastClickMs > 0 && (now - _lastClickMs) <= 300;

				if (mb.DoubleClick || manualDouble)
				{
					// 設計說明：雙擊即時切換外觀/裝備狀態（快速裝備體驗）
					// 這是更好的交互，不要改回官方兩步驟（先卸下再裝上）。
					GD.Print($"[UI] Double Clicked Item: {ItemData.Name} (ID: {ItemData.ObjectId})");
					EmitSignal(SignalName.ItemDoubleClicked, ItemData.ObjectId);
					_lastClickMs = 0;
				}
				else
				{
					EmitSignal(SignalName.ItemClicked, ItemData.ObjectId);
					_lastClickMs = now;
				}
			}
		}

		// 【修复拖拽实现】
		public override Variant _GetDragData(Vector2 atPosition)
		{
			if (ItemData == null)
			{
				GD.Print("[InventorySlot] _GetDragData: ItemData is null");
				return default;
			}

			// 1. 创建预览
			var preview = new TextureRect();
			preview.Texture = _icon.Texture;
			preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			preview.CustomMinimumSize = new Vector2(32, 32);
			preview.Modulate = new Color(1, 1, 1, 0.7f);
			SetDragPreview(preview);

			// 2. 打包数据
			var dragData = new Godot.Collections.Dictionary();
			dragData["type"] = "item";
			dragData["id"] = ItemData.ObjectId;
			// 如果没有图标路径，使用默认路径
			string iconPath = (_icon.Texture != null && !string.IsNullOrEmpty(_icon.Texture.ResourcePath)) 
				? _icon.Texture.ResourcePath 
				: DEFAULT_ICON_PATH;
			dragData["icon_path"] = iconPath;
			
			GD.Print($"[InventorySlot] _GetDragData: Item ID={ItemData.ObjectId}, icon_path={iconPath}");
			return dragData;
		}

		public override void _Process(double delta)
		{
			if (_isHovering && IsTooltipValid())
			{
				_currentTooltipInstance.Position = GetViewport().GetMousePosition() + new Vector2(15, 15);
				if (ItemData != null)
				{
					var method = _currentTooltipInstance.GetType().GetMethod("Setup");
					if (method != null) method.Invoke(_currentTooltipInstance, new object[] { ItemData });
				}
			}
		}

		/// <summary>
		/// 背包圖標路徑：res://Assets/Items/{GfxId}.png，與 ChaWindow 裝備格一致；缺檔時使用預設圖。
		/// </summary>
		public void SetItem(InventoryItem item)
		{
			ItemData = item;
			if (_count != null) _count.Text = item.Count > 1 ? item.Count.ToString() : "";

			if (_icon != null)
			{
				string path = $"res://Assets/Items/{item.GfxId}.png";
				if (ResourceLoader.Exists(path))
				{
					_icon.Texture = ResourceLoader.Load<Texture2D>(path);
				}
				else
				{
					if (_cachedDefaultTexture != null)
						_icon.Texture = _cachedDefaultTexture;
				}
				_icon.Modulate = new Color(1, 1, 1);
				_icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			}

			if (_bg != null)
			{
				_bg.Color = item.IsEquipped ? new Color(0, 0.6f, 0, 1) : new Color(0.2f, 0.2f, 0.2f, 1);
			}
		}

		/// <summary>設置選中狀態（顯示 "X" 標記）</summary>
		public void SetSelected(bool selected)
		{
			_isSelected = selected;
			if (_selectedMark != null)
			{
				_selectedMark.Visible = selected;
			}
		}

		private void OnMouseEntered()
		{
			if (ItemData == null) return;
			_isHovering = true;
			CleanupTooltip();
			if (_tooltipLayer == null || !IsInstanceValid(_tooltipLayer)) { 
				_tooltipLayer = new CanvasLayer(); _tooltipLayer.Layer = 128; _tooltipLayer.Name = "TooltipLayer"; GetTree().Root.AddChild(_tooltipLayer); 
			}
			
			var scene = GD.Load<PackedScene>("res://Client/UI/Scenes/Components/ItemTooltip.tscn");
			if (scene != null) {
				var tooltip = scene.Instantiate() as Control;
				_tooltipLayer.AddChild(tooltip);
				_currentTooltipInstance = tooltip;
				var method = tooltip.GetType().GetMethod("Setup");
				if (method != null) method.Invoke(tooltip, new object[] { ItemData });
			}
		}

		private void OnMouseExited() { _isHovering = false; CleanupTooltip(); }
		public override void _ExitTree() { CleanupTooltip(); }
		private void CleanupTooltip() { if (_currentTooltipInstance != null && IsInstanceValid(_currentTooltipInstance)) { _currentTooltipInstance.QueueFree(); _currentTooltipInstance = null; } }
		private bool IsTooltipValid() { return _currentTooltipInstance != null && IsInstanceValid(_currentTooltipInstance); }
	}
}
