using Godot;
using System;

namespace Client.UI
{
	/// <summary>
	/// 所有游戏浮动窗口的基类。
	/// 负责：背景加载、关闭按钮事件、鼠标拖拽移动、层级聚焦。
	/// </summary>
	public partial class GameWindow : Control
	{
		// --- UI 组件引用 (约定优于配置) ---
		// 子类场景中必须有名为 "CloseBtn" 的按钮，和 "TitleBar" (可选) 用于拖拽
		protected Button _closeBtn;
		protected Control _dragArea; // 通常是整个窗口或标题栏

		// --- 状态 ---
		private bool _isDragging = false;
		private Vector2 _dragOffset;

		// --- 事件 ---
		// 当窗口关闭时触发，通知 UIManager
		public event Action<WindowID> OnWindowClosed;

		// 窗口 ID (由 UIManager 分配)
		public WindowID ID { get; set; }

		public override void _Ready()
		{
			// 1. 自动查找通用组件
			_closeBtn = FindChild("CloseBtn", true, false) as Button;
			
			// 如果有专门的标题栏用于拖拽，命名为 "TitleBar"；否则整个窗口背景都可以拖拽
			_dragArea = FindChild("TitleBar", true, false) as Control;
			if (_dragArea == null) _dragArea = this; // 默认整个窗口可拖拽

			// 2. 绑定关闭事件
			if (_closeBtn != null)
			{
				_closeBtn.Pressed += Close;
			}
			else
			{
				// 只是警告，防止某些窗口不需要关闭按钮
				// GD.Print($"[UI] Window {Name} missing 'CloseBtn'.");
			}

			// 3. 绑定输入事件 (用于拖拽和点击聚焦)
			this.GuiInput += OnGuiInput;
			
			// 初始化时默认置顶
			MoveToFront();
		}

		// --- 核心逻辑：拖拽与聚焦 ---
		private void OnGuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mouse)
			{
				if (mouse.ButtonIndex == MouseButton.Left)
				{
					if (mouse.Pressed)
					{
						// 点击窗口时，通知管理器把自己提到最上层
						UIManager.Instance?.BringToFront(this);

						// 开始拖拽
						_isDragging = true;
						_dragOffset = GetGlobalMousePosition() - GlobalPosition;
					}
					else
					{
						// 释放拖拽
						_isDragging = false;
					}
				}
			}
			else if (@event is InputEventMouseMotion motion && _isDragging)
			{
				// 执行移动
				GlobalPosition = GetGlobalMousePosition() - _dragOffset;
			}
		}

		// --- 生命周期方法 (供子类重写) ---

		/// <summary>
		/// 打开窗口时调用。子类在此刷新数据。
		/// </summary>
		/// <param name="context">上下文数据 (如NPC ID)</param>
		public virtual void OnOpen(WindowContext context = null)
		{
			this.Visible = true;
			this.MoveToFront();
			// 子类重写此方法来 update UI
			GD.Print($"[UI] Open Window: {ID}");
		}

		/// <summary>
		/// 关闭窗口时调用。
		/// </summary>
		public virtual void Close()
		{
			this.Visible = false;
			OnWindowClosed?.Invoke(ID);
			// GD.Print($"[UI] Close Window: {ID}");
		}

		/// <summary>
		/// 切换开关状态
		/// </summary>
		public void Toggle()
		{
			if (this.Visible) Close();
			else OnOpen();
		}
	}
}
