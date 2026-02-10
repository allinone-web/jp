using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Data;
using Client.Network;
using Client.Utility;
// [清理] 移除了 using Client.Game; (如果不需要显式引用)

namespace Client.UI
{
	public partial class HUD : CanvasLayer
	{
		// --- 内部组件引用 ---
		private ProgressBar _hpBar;
		private Label _hpLabel;
		
		private ProgressBar _mpBar;
		private Label _mpLabel;
		
		private RichTextLabel _chatBox;
		
		private LineEdit _chatInput; // 新增输入框
		private string _defaultInputPlaceholder = "";
		private Action<string> _pendingSubmitCallback;

		/// <summary>Buff 圖標容器：屏幕頂部左上角，顯示加速/勇敢/護盾等圖標，到期自動銷毀。</summary>
		private HBoxContainer _buffIconContainer;
		/// <summary>當前顯示的 Buff 圖標：key=iconKey（haste/brave/shield/aqua 等），value=(Control 節點, 過期時間 Unix 秒)。</summary>
		private readonly Dictionary<string, (Control Node, double ExpireTime)> _buffIcons = new Dictionary<string, (Control, double)>();

		// [清理] 移除了 private GameWorld _gameWorld; 

		// --- Export 引用 (保留但不依赖) ---
		[Export] public ProgressBar HpBar;
		[Export] public ProgressBar MpBar;
		[Export] public RichTextLabel ChatBox;
		[Export] public LineEdit ChatInput;
		
		// ❌ [删除] [Export] public CharacterWindow CharacterWindow; 
		// 现在 C 面板由 UIManager 独立管理，HUD 不需要知道它的存在。

		// 聊天输入信号 (保持不变)
		[Signal] public delegate void ChatSubmittedEventHandler(string text);

		public override void _Ready()
		{
			// 1. 绑定组件 (保持不变，确保路径正确)
			_hpBar = GetNodeOrNull<ProgressBar>("Panel/HPBar");
			if (_hpBar != null)
			{
				_hpLabel = _hpBar.GetNodeOrNull<Label>("Label");
				ApplyHPBarStyle(_hpBar);
			}

			_mpBar = GetNodeOrNull<ProgressBar>("Panel/MPBar");
			if (_mpBar != null)
			{
				_mpLabel = _mpBar.GetNodeOrNull<Label>("Label");
				ApplyMPBarStyle(_mpBar);
			}

			_chatBox = GetNodeOrNull<RichTextLabel>("chat/ChatBox");
			if (_chatBox != null) _chatBox.BbcodeEnabled = true;
			_chatInput = GetNodeOrNull<LineEdit>("chat/ChatInput");

			// Buff 圖標容器：左上角，橫向排列
			_buffIconContainer = GetNodeOrNull<HBoxContainer>("BuffIcons");
			if (_buffIconContainer == null)
			{
				_buffIconContainer = new HBoxContainer();
				_buffIconContainer.Name = "BuffIcons";
				_buffIconContainer.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
				_buffIconContainer.OffsetLeft = 8;
				_buffIconContainer.OffsetTop = 8;
				_buffIconContainer.AddThemeConstantOverride("separation", 4);
				AddChild(_buffIconContainer);
			}

			// 2. 绑定聊天事件（不隱藏，正常顯示）；滑鼠移出輸入框時釋放焦點，避免鍵盤一直被攔截、無需按 ESC 退出
			if (_chatInput != null)
			{
				_chatInput.Visible = true; // 不隱藏，正常顯示
				_defaultInputPlaceholder = _chatInput.PlaceholderText ?? "";
				_chatInput.TextSubmitted += OnChatSubmitted;
				_chatInput.MouseExited += () => _chatInput.ReleaseFocus();
			}

			// 2. 【核心修复】重新绑定系统消息信号！
			// 2. 绑定系统消息 (直接连接 PacketHandler)
			// 你之前删除了这段，导致所有系统提示都无法显示
			var ph = GetNodeOrNull<PacketHandler>("/root/Boot/PacketHandler");
			if (ph != null)
			{
				// 先断开防止重复绑定
				if (ph.IsConnected(PacketHandler.SignalName.SystemMessage, new Callable(this, MethodName.AddSystemMessage)))
					ph.SystemMessage -= AddSystemMessage;
					
				ph.SystemMessage += AddSystemMessage;
				GD.Print("[HUD] ✅ Connected to PacketHandler.SystemMessage");
			}

		 
			
			// ❌ [删除] 原有的 CharacterWindow 查找逻辑
			// if (CharacterWindow == null) ...
		}

		/// <summary>運行時強制 HP 條樣式：紅色底，上面有一層高光色，極度簡潔的動態血條。</summary>
		private static void ApplyHPBarStyle(ProgressBar bar)
		{
			var bg = new StyleBoxFlat();
			bg.BgColor = new Color(1.0f, 0.2f, 0.2f, 1.0f); // 亮紅色底
			bg.BorderWidthLeft = 0;
			bg.BorderWidthTop = 0;
			bg.BorderWidthRight = 0;
			bg.BorderWidthBottom = 0;
			bar.AddThemeStyleboxOverride("background", bg);
			
			var fill = new StyleBoxFlat();
			fill.BgColor = new Color(0.4f, 0.0f, 0.0f, 1.0f); // 深紅色高光
			fill.BorderWidthLeft = 0;
			fill.BorderWidthTop = 0;
			fill.BorderWidthRight = 0;
			fill.BorderWidthBottom = 0;
			bar.AddThemeStyleboxOverride("fill", fill);
		}

		/// <summary>運行時強制 MP 條樣式：藍色底，上面有一層高光色，極度簡潔的動態藍條。</summary>
		private static void ApplyMPBarStyle(ProgressBar bar)
		{
			var bg = new StyleBoxFlat();
			bg.BgColor = new Color(0.2f, 0.5f, 1.0f, 1.0f); // 亮藍色底
			bg.BorderWidthLeft = 0;
			bg.BorderWidthTop = 0;
			bg.BorderWidthRight = 0;
			bg.BorderWidthBottom = 0;
			bar.AddThemeStyleboxOverride("background", bg);
			
			var fill = new StyleBoxFlat();
			fill.BgColor = new Color(0.0f, 0.2f, 0.4f, 1.0f); // 深藍色高光
			fill.BorderWidthLeft = 0;
			fill.BorderWidthTop = 0;
			fill.BorderWidthRight = 0;
			fill.BorderWidthBottom = 0;
			bar.AddThemeStyleboxOverride("fill", fill);
		}

		// 【修復】處理聊天輸入（不隱藏，只清空）
		private void OnChatSubmitted(string text) 
		{ 
			if (string.IsNullOrWhiteSpace(text))
			{
				if (_chatInput != null)
					_chatInput.Clear();
				return;
			}

			if (_pendingSubmitCallback != null)
			{
				var cb = _pendingSubmitCallback;
				_pendingSubmitCallback = null;
				RestoreInputPrompt();
				if (_chatInput != null) _chatInput.Clear();
				cb(text);
				return;
			}
			
			// 1. 發送信號給 GameWorld 處理網絡發送
			EmitSignal(SignalName.ChatSubmitted, text);
			
			// 2. 本地立即顯示 (Local Echo)
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			string myName = (boot != null) ? boot.CurrentCharName : "Me";
			AddChatMessage(myName, text); 
			
			// 3. 清空輸入框（不隱藏）
			if (_chatInput != null)
				_chatInput.Clear();
		}

		/// <summary>設定輸入框提示文字（用於「增加書籤」等單次輸入）。</summary>
		public void SetInputPrompt(string placeholder)
		{
			if (_chatInput != null)
				_chatInput.PlaceholderText = placeholder ?? _defaultInputPlaceholder;
		}

		/// <summary>還原輸入框為預設提示。</summary>
		public void RestoreInputPrompt()
		{
			if (_chatInput != null)
				_chatInput.PlaceholderText = _defaultInputPlaceholder;
		}

		/// <summary>聚焦到聊天輸入框並清空內容。</summary>
		public void FocusChatInput()
		{
			if (_chatInput != null)
			{
				_chatInput.Clear();
				_chatInput.GrabFocus();
			}
		}

		/// <summary>設定單次提交回調：下次輸入提交時只呼叫 callback，不發 ChatSubmitted、不顯示為聊天。用畢自動還原。</summary>
		public void SetSubmitCallback(Action<string> callback)
		{
			_pendingSubmitCallback = callback;
		}

		// --- 更新接口 ---
		public void UpdateHP(int current, int max)
		{
			if (_hpBar != null) 
			{ 
				_hpBar.MaxValue = max; 
				_hpBar.Value = current; 
				if (_hpLabel != null) _hpLabel.Text = $"{current}/{max}";
			}
		}

		public void UpdateMP(int current, int max)
		{
			if (_mpBar != null) 
			{ 
				_mpBar.MaxValue = max; 
				_mpBar.Value = current; 
				if (_mpLabel != null) _mpLabel.Text = $"{current}/{max}";
			}
		}
		
		/// <summary>將訊息中的 $數字（怪物/物品 id）解析為 DescTable 對應名稱。</summary>
		private static string ResolveMessageNames(string text)
		{
			if (string.IsNullOrEmpty(text) || DescTable.Instance == null) return text ?? "";
			return Regex.Replace(text, @"\$(\d+)", m =>
			{
				string key = m.Value;
				return DescTable.Instance.ResolveName(key);
			});
		}

		/// <summary>移除 L1 協議 \fX 字型/顏色碼；未替換的 %0、%1、%s 改為空字串，避免在 ChatBox 顯示為亂碼。</summary>
		private static string SanitizeChatMessage(string text)
		{
			if (string.IsNullOrEmpty(text)) return text;
			text = Regex.Replace(text, @"\\f.", "");
			text = Regex.Replace(text, @"%[0-9]+", "");
			text = Regex.Replace(text, @"%s", "");
			return text;
		}

		// 显示系统消息 (黄色)，怪物編號、裝備名稱等 $nnn 會解析為語言
		public void AddSystemMessage(string msg)
		{
			msg = SanitizeChatMessage(msg);
			msg = ResolveMessageNames(msg);
			if (_chatBox != null)
				_chatBox.AppendText($"[color=yellow]{msg}[/color]\n");
			else
				GD.Print($"[System] {msg}");
		}

		// 显示普通消息 (白色)，$nnn 解析為名稱
		public void AddChatMessage(string name, string msg)
		{
			msg = SanitizeChatMessage(msg);
			name = SanitizeChatMessage(name);
			msg = ResolveMessageNames(msg);
			name = ResolveMessageNames(name);
			if (_chatBox != null)
				_chatBox.AppendText($"[color=white]{name}: {msg}[/color]\n");
		}

		// ========================================================================
		// Buff 圖標：屏幕頂部左上角顯示魔法/藥水圖標，到期自動銷毀
		// 素材路徑：res://Assets/Skills2/{iconKey}.png
		// iconKey: haste, slow, brave, aqua, shield 等（與封包對應）
		// ========================================================================
		public override void _Process(double delta)
		{
			if (_buffIcons.Count == 0) return;
			double now = Time.GetUnixTimeFromSystem();
			var toRemove = new List<string>();
			foreach (var kv in _buffIcons)
			{
				if (now >= kv.Value.ExpireTime)
					toRemove.Add(kv.Key);
			}
			foreach (string key in toRemove)
				RemoveBuffIcon(key);
		}

		/// <summary>顯示一個 Buff 圖標，持續 timeSeconds 秒後自動移除。timeSeconds &lt;= 0 則僅移除已有圖標。</summary>
		public void AddBuffIcon(string iconKey, int timeSeconds)
		{
			if (_buffIconContainer == null) return;
			if (timeSeconds <= 0)
			{
				RemoveBuffIcon(iconKey);
				return;
			}
			RemoveBuffIcon(iconKey);
			string path = $"res://Assets/Skills2/{iconKey}.png";
			if (!FileAccess.FileExists(path))
			{
				GD.Print($"[HUD] Buff 圖標不存在: {path}");
				return;
			}
			var tex = GD.Load<Texture2D>(path);
			if (tex == null) return;
			var box = new Control();
			box.CustomMinimumSize = new Vector2(28, 28);
			var rect = new TextureRect();
			rect.Texture = tex;
			rect.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
			rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			box.AddChild(rect);
			var label = new Label();
			label.Text = timeSeconds > 60 ? $"{timeSeconds / 60}m" : $"{timeSeconds}s";
			label.Position = new Vector2(0, 18);
			label.AddThemeFontSizeOverride("font_size", 9);
			box.AddChild(label);
			_buffIconContainer.AddChild(box);
			double expire = Time.GetUnixTimeFromSystem() + timeSeconds;
			_buffIcons[iconKey] = (box, expire);
		}

		/// <summary>移除指定 key 的 Buff 圖標並銷毀節點。</summary>
		public void RemoveBuffIcon(string iconKey)
		{
			if (!_buffIcons.TryGetValue(iconKey, out var pair)) return;
			_buffIcons.Remove(iconKey);
			if (pair.Node != null && IsInstanceValid(pair.Node))
			{
				pair.Node.QueueFree();
			}
		}
	}
}
