using Godot;
using System;
using System.Collections.Generic;
using Client.Game;
using Client.Data; 
using Client.Utility; 
using System.Text.RegularExpressions;

namespace Client.UI
{
	public partial class TalkWindow : GameWindow
	{
		private RichTextLabel _contentLabel;
		private int _currentNpcId;
		private int _currentSummonObjectId;
		/// <summary>S_MessageYN(155) Yes/No 模式：點擊是/否後發送 C_Attr(61)。-1 表示非 Yes/No 對話。</summary>
		private int _yesnoType = -1;
		
		// --- 调试控件 ---
		private VBoxContainer _debugContainer;
		private LineEdit _debugInput;
		private Button _debugBtn;

		public override void _Ready()
		{
			base._Ready();

			// 1. 寻找 ContentLabel
			_contentLabel = FindChild("ContentLabel", true, false) as RichTextLabel;

			if (_contentLabel == null)
			{
				GD.PrintErr("[TalkWindow] ❌ 找不到 'ContentLabel'，请检查场景结构！");
				return;
			}

			// 2. 配置 ContentLabel
			_contentLabel.BbcodeEnabled = true; 
			_contentLabel.FitContent = true;    
			_contentLabel.ScrollActive = false; 
			_contentLabel.MouseFilter = MouseFilterEnum.Pass; 
			
			if (!_contentLabel.IsConnected("meta_clicked", new Callable(this, nameof(OnMetaClicked))))
			{
				_contentLabel.MetaClicked += OnMetaClicked;
			}

			// 3. 创建调试栏
			SetupDebugBar();
		}

		private void SetupDebugBar()
		{
			_debugContainer = new VBoxContainer();
			_debugContainer.Name = "DebugBar";
			_debugContainer.Visible = false; 
			_debugContainer.CustomMinimumSize = new Vector2(0, 60);

			Node labelParent = _contentLabel.GetParent();
			if (labelParent is VBoxContainer vbox)
			{
				vbox.AddChild(_debugContainer);
			}
			else
			{
				labelParent.AddChild(_debugContainer);
				if (labelParent is Control parentCtrl)
				{
					_debugContainer.SetAnchorsPreset(LayoutPreset.BottomWide);
					_debugContainer.Position = new Vector2(0, parentCtrl.Size.Y - 60);
				}
			}

			_debugInput = new LineEdit();
			_debugInput.PlaceholderText = "输入动作 (如 teleport 1)";
			_debugContainer.AddChild(_debugInput);

			_debugBtn = new Button();
			_debugBtn.Text = "强制发送动作";
			_debugBtn.Pressed += OnDebugSendPressed;
			_debugContainer.AddChild(_debugBtn);
		}

		public override void OnOpen(WindowContext context)
		{
			base.OnOpen(context);

			if (_contentLabel == null) return;
			
			_contentLabel.Text = "Loading...";
			if (_debugContainer != null) _debugContainer.Visible = false;

			if (context.ExtraData == null) return;

			var data = context.ExtraData as Dictionary<string, object>;
			if (data == null) return;

			if (data.ContainsKey("npc_id")) _currentNpcId = (int)data["npc_id"];
			_currentSummonObjectId = 0;
			if (data.ContainsKey("summon_object_id")) _currentSummonObjectId = (int)data["summon_object_id"];
			_yesnoType = -1;
			if (data.ContainsKey("yesno_type")) _yesnoType = Convert.ToInt32(data["yesno_type"]);

			string htmlId = "";
			if (data.ContainsKey("html_id")) htmlId = (string)data["html_id"];
			
			string[] args = null;
			if (data.ContainsKey("args")) args = (string[])data["args"];

			LoadAndShowHtml(htmlId, args);
		}

		private void LoadAndShowHtml(string htmlId, string[] args)
		{
			if (string.IsNullOrEmpty(htmlId)) return;
			
			htmlId = htmlId.Trim();
			string lang = ClientConfig.Language; 
			string baseDir = "res://Assets/text/";
			
			// 語言代碼映射：cn -> c, cn2 -> c, kr -> k, en -> e
			string langSuffix = lang switch
			{
				"cn" => "c",
				"cn2" => "c",
				"kr" => "k",
				"en" => "e",
				"c" => "c", // 向後兼容舊的語言代碼
				_ => lang
			};
			
			// 搜索顺序：当前语言 -> -h -> -e -> 无后缀
			var langSuffixes = new List<string>();
			langSuffixes.Add($"-{langSuffix}");  
			if (langSuffix != "h") langSuffixes.Add("-h"); 
			if (langSuffix != "e") langSuffixes.Add("-e"); 
			langSuffixes.Add("");            

			var extensions = new List<string> { ".html", ".HTML", ".tbl", ".TBL" };
			var attemptedPaths = new List<string>();
			string finalPath = "";
			bool found = false;

			foreach (var suffix in langSuffixes)
			{
				foreach (var ext in extensions)
				{
					string path = $"{baseDir}{htmlId}{suffix}{ext}";
					attemptedPaths.Add(path);

					if (FileAccess.FileExists(path))
					{
						finalPath = path;
						found = true;
						break; 
					}
				}
				if (found) break; 
			}

			if (!found || string.IsNullOrEmpty(finalPath))
			{
				// 没找到文件，显示详细调试信息
				ShowDebugInfo(htmlId, attemptedPaths);
				return;
			}

			string fileContent = "";
			try 
			{
				// 【修復】使用 UTF-8 編碼讀取文件（Assets/text 中的 HTML 文件已轉換為 UTF-8）
				fileContent = ReadFileAsUTF8(finalPath);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[TalkWindow] 讀取文件失敗: {finalPath}, Error: {e.Message}");
				_contentLabel.Text = $"[color=red]Error reading file:\n{finalPath}[/color]";
				return;
			}

			// 解析 HTML
			string bbcode = ParseHtmlToBBCode(fileContent);

			// 【修復】支持 HTML 變量替換：<var src="#0"> 到 <var src="#9">
			// 同時支持舊的 %0 到 %9 格式
			if (args != null && args.Length > 0)
			{
				for (int i = 0; i < args.Length; i++)
				{
					// 替換 %0-%9 格式（舊格式）
					bbcode = bbcode.Replace($"%{i}", args[i]);
					// 替換 <var src="#0"> 到 <var src="#9"> 格式（寵物面板使用）
					// 注意：需要在 ParseHtmlToBBCode 之後替換，因為正則表達式會先處理這些標籤
					bbcode = Regex.Replace(bbcode, $@"<var\s+src=""?#{i}""?>", args[i], RegexOptions.IgnoreCase);
				}
			}

			_contentLabel.Text = bbcode;
		}

		// ✅✅✅ 【核心修复】显示详细的调试信息 ✅✅✅
		private void ShowDebugInfo(string htmlId, List<string> triedPaths)
		{
			string npcDisplayName = "Unknown";
			string rawName = "Unknown"; // 原始名字
			string location = "Unknown"; // 坐标
			
			var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
			int objIdForDisplay = _currentSummonObjectId > 0 ? _currentSummonObjectId : _currentNpcId;
			if (world != null && objIdForDisplay > 0 && world._entities.ContainsKey(objIdForDisplay))
			{
				var npc = world._entities[objIdForDisplay];
				rawName = npc.RealName; // 获取原始英文名/代码名
				location = $"{npc.MapX}, {npc.MapY}"; // 获取坐标
				
				if (DescTable.Instance != null)
					npcDisplayName = DescTable.Instance.ResolveName(rawName);
				else
					npcDisplayName = rawName;
			}

			string info = $"[b][color=yellow]System: File NOT Found ({htmlId})[/color][/b]\n";
			info += $"[color=gray]--------------------------------[/color]\n";
			info += $"NPC Name: [color=green]{npcDisplayName}[/color]\n";
			info += $"NPC Raw:  {rawName}\n";  // 显示原始名字 (如 $1415 或 treno)
			info += $"NPC ID:   {_currentNpcId}\n";
			info += $"Location: {location}\n"; // 显示坐标
			info += $"[color=gray]--------------------------------[/color]\n";
			
			// 显示尝试过的路径列表
			int count = triedPaths != null ? triedPaths.Count : 0;
			info += $"[color=gray]--- Tried Paths ({count}) ---[/color]\n";
			info += "[font_size=12]";
			if (triedPaths != null)
			{
				foreach (var p in triedPaths)
				{
					// 只显示文件名部分，避免路径太长
					string shortPath = p.Replace("res://Assets/text/", "");
					info += $"- {shortPath}\n"; 
				}
			}
			info += "[/font_size]";
			
			_contentLabel.Text = info;
			
			// 确保调试输入框显示出来
			if (_debugContainer != null) 
			{
				_debugContainer.Visible = true;
			}
			
			GD.PrintErr($"[TalkWindow] Missing: {htmlId}. Details shown in UI.");
		}

		private void OnMetaClicked(Variant meta)
		{
			string action = meta.AsString();
			GD.Print($"[TalkWindow] Clicked Action: '{action}'");
			SendAction(action);
		}

		private void OnDebugSendPressed()
		{
			if (_debugInput != null && !string.IsNullOrEmpty(_debugInput.Text))
			{
				SendAction(_debugInput.Text);
			}
		}

		private void SendAction(string action)
		{
			GD.Print($"[TalkWindow] Sending Action: {action}");
			if (string.IsNullOrEmpty(action)) return;
			var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
			if (world == null) return;
			
			// 【死亡對話】本地操作（不發送給伺服器）
			if (action == "restart")
			{
				Close();
				world.SendRestartRequest();
				return;
			}
			if (action == "exit")
			{
				Close();
				world.SendQuitRequest();
				return;
			}
			// 死亡對話：返回角色列表（發送 C_218 後斷線並切換選角場景）
			if (action == "returntologin")
			{
				Close();
				world.SendReturnToLoginRequest();
				return;
			}
			if (action == "cancel")
			{
				Close();
				return;
			}
			// S_MessageYN(155) Yes/No：透過 TalkWindow + html/yesno 顯示，點擊後發送 C_Attr(61)
			if (action == "yesno_yes" || action == "yesno_no")
			{
				if (_yesnoType >= 0)
				{
					world.SendYesNoResponse(_yesnoType, action == "yesno_yes");
					Close();
				}
				return;
			}
			
			// 【修復】deposit 動作應該打開存儲模式（Type=2），而不是發送 retrieve
			// 直接打開存儲模式的倉庫窗口，顯示背包物品
			if (action == "deposit")
			{
				var context = new WindowContext 
				{ 
					NpcId = _currentNpcId, 
					Type = 2, // 2=存儲模式
					ExtraData = new Godot.Collections.Array() // 空列表，顯示背包物品
				};
				UIManager.Instance.Open(WindowID.WareHouse, context);
				return;
			}
			
			// retrieve 或 leave item 動作發送給服務器，服務器會返回倉庫物品列表（option=3）
			if (_currentSummonObjectId > 0)
				world.SendSummonCommand(_currentSummonObjectId, action);
			else
				world.SendNpcAction(action);
		}

		// ==========================================================
		//  HTML -> BBCode 智能解析器 (保持不变，因为你觉得非常好)
		// ==========================================================
		private string ParseHtmlToBBCode(string html)
		{
			if (string.IsNullOrEmpty(html)) return "";

			// ==========================================================
			// 核心修复：移除源代码里的换行符，防止和 <br> 叠加导致间距过大
			// ==========================================================
			html = html.Replace("\r", "").Replace("\n", "");

			// 1. 基础标签清理
			html = html.Replace("<body>", "").Replace("</body>", "")
					   .Replace("<BODY>", "").Replace("</BODY>", "");
			
			html = Regex.Replace(html, @"<\/img>", "", RegexOptions.IgnoreCase);
			// 将 <br> 转换为换行符
			html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

			// 2. 颜色处理
			string patternColor = @"<font\s+fg=""?([0-9a-fA-F]+)""?>";
			html = Regex.Replace(html, patternColor, "[color=#$1]", RegexOptions.IgnoreCase);
			html = Regex.Replace(html, @"</font>", "[/color]", RegexOptions.IgnoreCase);

			// 3. 对齐处理
			html = Regex.Replace(html, @"<p\s+align=""?center""?>((?:.|\n)*?)<\/p>", "[center]$1[/center]\n", RegexOptions.IgnoreCase);
			html = Regex.Replace(html, @"<p\s+align=""?right""?>((?:.|\n)*?)<\/p>", "[right]$1[/right]\n", RegexOptions.IgnoreCase);
			html = Regex.Replace(html, @"<p\s+align=""?left""?>((?:.|\n)*?)<\/p>", "[left]$1[/left]\n", RegexOptions.IgnoreCase);
			
			// P 标签处理：删掉 <p>，</p> 换行
			html = Regex.Replace(html, @"<p>", "", RegexOptions.IgnoreCase);
			html = Regex.Replace(html, @"<\/p>", "\n", RegexOptions.IgnoreCase);

			// 4. 图片按钮解析
			html = Regex.Replace(html, 
				@"<img\s+src=""?([^""]+)""?\s+(?:action|link)=""?([^""]+)""?[^>]*>", 
				"[url=\"$2\"] [ 选项: $1 ] [/url]", RegexOptions.IgnoreCase);
			
			// 5. 链接解析
			string patternLinkQuoted = @"<a\s+(?:action|link)=""([^""]+)""[^>]*>"; 
			html = Regex.Replace(html, patternLinkQuoted, "[url=\"$1\"]", RegexOptions.IgnoreCase); 
			string patternLinkNoQuote = @"<a\s+(?:action|link)=([^>\s]+)[^>]*>";
			html = Regex.Replace(html, patternLinkNoQuote, "[url=\"$1\"]", RegexOptions.IgnoreCase);

			html = Regex.Replace(html, @"</a>", "[/url]", RegexOptions.IgnoreCase);

			// 6. 变量解析（保留標籤，稍後在 args 替換時處理）
			// 【修復】不再在這裡替換變量，而是在 args 替換時處理
			// html = Regex.Replace(html, @"<var\s+src=""?#(\d+)""?>", MatchVar, RegexOptions.IgnoreCase);

			html = html.Replace("&nbsp;", " ");

			return html;
		}

		// ✅✅✅ 【缺失的方法】补全了 MatchVar 方法 ✅✅✅
		// 【修復】現在變量替換已在 ParseHtmlToBBCode 中處理，此方法保留用於向後兼容
		private string MatchVar(Match m) 
		{ 
			// 變量替換已在 ParseHtmlToBBCode 中通過正則表達式處理
			// 此方法保留用於向後兼容，但不會被調用
			return ""; 
		}
		
		/// <summary>使用 UTF-8 編碼讀取文件（Assets/text 中的 HTML 文件已轉換為 UTF-8）</summary>
		private string ReadFileAsUTF8(string path)
		{
			try
			{
				// 直接使用 UTF-8 讀取（文件已轉換為 UTF-8）
				return FileAccess.GetFileAsString(path);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[TalkWindow] ReadFileAsUTF8 錯誤: {path}, {e.Message}");
				return "";
			}
		}
	}
}
