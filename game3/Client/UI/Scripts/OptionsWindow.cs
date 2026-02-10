using Godot;
using System;
using System.Collections.Generic;
using Client;
using Client.Game;
using Client.Data;
using Client.Utility;
using System.Text.RegularExpressions;

namespace Client.UI
{
	public partial class OptionsWindow : GameWindow
	{
		private RichTextLabel _contentLabel;
		private VBoxContainer _audioPanel;

		/// <summary>記憶座標列表頁快取，用於 teleport_bookmark:i / delete_bookmark:i 索引。</summary>
		private List<(string name, int mapId, int id, int x, int y)> _bookmarksCache = new List<(string, int, int, int, int)>();

		// ⚠️ 调试开关：True = 强制读取英文版 (-e) 用于调试
		private bool _forceEnglishDebug = false;

		public override void _Ready()
		{
			base._Ready();

			// 1. 获取场景中预设的节点 (对应你的截图结构)
			// 结构: ContentContainer -> ScrollContainer -> VBoxContainer -> ContentLabel
			var vbox = FindChild("VBoxContainer", true, false) as VBoxContainer;
			_contentLabel = FindChild("ContentLabel", true, false) as RichTextLabel;

			if (_contentLabel == null)
			{
				GD.PrintErr("[OptionsWindow] ❌ 严重错误: 场景中找不到 'ContentLabel'！请检查节点名称。");
				return;
			}

			// 2. 配置文本框属性 (参考 TalkWindow)
			_contentLabel.BbcodeEnabled = true;
			_contentLabel.FitContent = true;
			_contentLabel.ScrollActive = false; // 让外层的 ScrollContainer 负责滚动
			_contentLabel.MouseFilter = MouseFilterEnum.Pass;

			// 绑定点击事件
			if (!_contentLabel.IsConnected("meta_clicked", new Callable(this, nameof(OnMetaClicked))))
			{
				_contentLabel.MetaClicked += OnMetaClicked;
			}

			// 3. 音訊面板（音樂／音效開關 + 音量滑桿），插入在 ContentLabel 之前
			SetupAudioPanel(vbox);

			// 4. Restart/Quit 改為選單項（option 內 action:restart / action:quit），不再用底部按鈕

			// 5. 监听可见性变化，每次打开时刷新内容
			this.VisibilityChanged += OnVisibilityChanged;
			if (this.Visible) OnVisibilityChanged();
		}

		private void OnVisibilityChanged()
		{
			if (this.Visible)
			{
				// 默认加载入口文件 "option"
				LoadAndShowHtml("option", null);
			}
		}

		private void SetupAudioPanel(VBoxContainer vbox)
		{
			if (vbox == null || _audioPanel != null) return;
			_audioPanel = new VBoxContainer();
			_audioPanel.AddThemeConstantOverride("separation", 12);

			// 背景音樂：開關 + 音量
			var musicLabel = new Label { Text = "Music (BGM)" };
			musicLabel.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			_audioPanel.AddChild(musicLabel);
			var musicRow = new HBoxContainer();
			musicRow.AddThemeConstantOverride("separation", 12);
			var musicCheck = new CheckButton { ButtonPressed = ClientConfig.MusicEnabled, Text = "ON/OFF" };
			musicCheck.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			musicCheck.Toggled += (bool on) => { ClientConfig.MusicEnabled = on; ClientConfig.Save(); Boot.Instance?.ApplyAudioSettings(); };
			musicRow.AddChild(musicCheck);
			var musicSlider = new HSlider { MinValue = -40, MaxValue = 0, Step = 1, Value = ClientConfig.MusicVolumeDb, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			var musicVal = new Label { Text = $"{ClientConfig.MusicVolumeDb:F0} dB", CustomMinimumSize = new Vector2(50, 0) };
			musicVal.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			musicSlider.ValueChanged += (double v) => { ClientConfig.MusicVolumeDb = (float)v; ClientConfig.Save(); Boot.Instance?.ApplyAudioSettings(); musicVal.Text = $"{v:F0} dB"; };
			musicRow.AddChild(musicSlider);
			musicRow.AddChild(musicVal);
			_audioPanel.AddChild(musicRow);

			// 音效：開關 + 音量
			var sfxLabel = new Label { Text = "Sound Effects (SFX)" };
			sfxLabel.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			_audioPanel.AddChild(sfxLabel);
			var sfxRow = new HBoxContainer();
			sfxRow.AddThemeConstantOverride("separation", 12);
			var sfxCheck = new CheckButton { ButtonPressed = ClientConfig.SoundEnabled, Text = "ON/OFF" };
			sfxCheck.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			sfxCheck.Toggled += (bool on) => { ClientConfig.SoundEnabled = on; ClientConfig.Save(); Boot.Instance?.ApplyAudioSettings(); };
			sfxRow.AddChild(sfxCheck);
			var sfxSlider = new HSlider { MinValue = -40, MaxValue = 0, Step = 1, Value = ClientConfig.SFXVolumeDb, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			var sfxVal = new Label { Text = $"{ClientConfig.SFXVolumeDb:F0} dB", CustomMinimumSize = new Vector2(50, 0) };
			sfxVal.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			sfxSlider.ValueChanged += (double v) => { ClientConfig.SFXVolumeDb = (float)v; ClientConfig.Save(); Boot.Instance?.ApplyAudioSettings(); sfxVal.Text = $"{v:F0} dB"; };
			sfxRow.AddChild(sfxSlider);
			sfxRow.AddChild(sfxVal);
			_audioPanel.AddChild(sfxRow);

			// 怪物頭頂血條開關（依 Opcode 104 顯示血量比例，默認開啟）
			var monsterHpLabel = new Label { Text = "怪物血條 (Monster HP Bar)" };
			monsterHpLabel.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			_audioPanel.AddChild(monsterHpLabel);
			var monsterHpCheck = new CheckButton { ButtonPressed = ClientConfig.ShowMonsterHealthBar, Text = "ON/OFF" };
			monsterHpCheck.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			monsterHpCheck.Toggled += (bool on) =>
			{
				ClientConfig.ShowMonsterHealthBar = on;
				ClientConfig.Save();
				var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
				if (world != null)
				{
					world.SendClientOption(3, on ? 1 : 0); // type=3 怪物血條，同步伺服器 hpBar 以收到 104
					world.RefreshMonsterHealthBars();
				}
			};
			_audioPanel.AddChild(monsterHpCheck);

			// 黑夜系統（日夜遮罩層）開關，關閉後徹底銷毀圖層以釋放性能
			var dayNightLabel = new Label { Text = "黑夜系統 (Day/Night Overlay)" };
			dayNightLabel.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			_audioPanel.AddChild(dayNightLabel);
			// 【修復】確保從配置正確讀取初始狀態
			var dayNightCheck = new CheckButton { ButtonPressed = ClientConfig.DayNightOverlayEnabled, Text = "ON/OFF" };
			dayNightCheck.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			dayNightCheck.Toggled += (bool on) =>
			{
				ClientConfig.DayNightOverlayEnabled = on;
				ClientConfig.Save(); // 【修復】確保狀態保存到設置文件
				GetNodeOrNull<GameWorld>("/root/Boot/World")?.SetDayNightOverlayEnabled(on);
			};
			_audioPanel.AddChild(dayNightCheck);
			
			// 【修復】監聽窗口可見性變化，每次打開時刷新按鈕狀態以確保與配置同步
			this.VisibilityChanged += () =>
			{
				if (this.Visible && dayNightCheck != null)
				{
					dayNightCheck.ButtonPressed = ClientConfig.DayNightOverlayEnabled;
				}
			};

			// 法師 Z 鍵魔法攻擊開關：開啟時 Z 鍵使用魔法（默認 skill=4 光箭，可被快捷鍵 1-8 替換）；關閉時 Z 鍵使用普通攻擊（支持近戰、弓箭，但不支持魔法）
			var mageZMagicLabel = new Label { Text = "法師 Z 鍵魔法攻擊 (Mage Z Magic Attack)" };
			mageZMagicLabel.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			_audioPanel.AddChild(mageZMagicLabel);
			var mageZMagicCheck = new CheckButton { ButtonPressed = ClientConfig.MageZMagicAttackEnabled, Text = "ON/OFF" };
			mageZMagicCheck.AddThemeFontSizeOverride("font_size", 12); // 與 HTML 內容字體大小一致
			mageZMagicCheck.Toggled += (bool on) =>
			{
				ClientConfig.MageZMagicAttackEnabled = on;
				ClientConfig.Save();
			};
			_audioPanel.AddChild(mageZMagicCheck);

			vbox.AddChild(_audioPanel);
			vbox.MoveChild(_audioPanel, 0);
		}


		private void LoadAndShowHtml(string htmlId, string[] args)
		{
			if (htmlId == "bookmark")
			{
				ShowBookmarkList();
				return;
			}
			if (htmlId == "gm")
			{
				ShowGmCommands();
				return;
			}

			string baseDir = "res://Assets/text/";
			var candidates = new List<string>();

			// --- 文件查找逻辑 ---
			if (_forceEnglishDebug)
			{
				candidates.Add($"{baseDir}{htmlId}-e.html");
				candidates.Add($"{baseDir}{htmlId}.html");
				candidates.Add($"{baseDir}{htmlId}.tbl");
			}
			else
			{
				// 多語言：ClientConfig.Language 控制載入 Assets/text 的語言版本（與 TalkWindow 一致）
				// 例如 cn/cn2→option-c.html、en→option-e.html、kr→option-k.html、j→option-j.html
				string lang = ClientConfig.Language;
				// 語言代碼映射：cn -> c, cn2 -> c, kr -> k, en -> e, j -> j
				string langSuffix = lang switch
				{
					"cn" => "c",
					"cn2" => "c",
					"kr" => "k",
					"en" => "e",
					"c" => "c", // 向後兼容舊的語言代碼
					_ => lang
				};
				candidates.Add($"{baseDir}{htmlId}-{langSuffix}.html");
				if (langSuffix != "e") candidates.Add($"{baseDir}{htmlId}-e.html");
				candidates.Add($"{baseDir}{htmlId}.html");
				candidates.Add($"{baseDir}{htmlId}.tbl");
			}

			string finalPath = "";
			foreach (var p in candidates)
			{
				if (FileAccess.FileExists(p))
				{
					finalPath = p;
					break;
				}
			}

			if (string.IsNullOrEmpty(finalPath))
			{
				_contentLabel.Text = $"[color=red]File Not Found: {htmlId}[/color]";
				return;
			}

			// 【修復】使用 UTF-8 編碼讀取文件（Assets/text 中的 HTML 文件已轉換為 UTF-8）
			string content = ReadFileAsUTF8(finalPath);
			if (string.IsNullOrEmpty(content))
			{
				_contentLabel.Text = $"[color=red]無法讀取文件: {finalPath}[/color]";
				return;
			}
			
			string bbcode = ParseHtmlToBBCode(content);
			// 僅 GM 可見：將 %GM_LINK% 替換為「GM 命令」連結或空字串
			bbcode = bbcode.Replace("%GM_LINK%", GetGmLinkPlaceholder());
			_contentLabel.Text = bbcode;
		}

		/// <summary>僅當角色為 GM（AccessLevel &gt; 0）時回傳「GM 命令」連結，否則回傳空字串。</summary>
		private string GetGmLinkPlaceholder()
		{
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			if (boot?.MyCharInfo == null) return "";
			if (boot.MyCharInfo.AccessLevel <= 0) return "";
			return "[url=\"link:gm\"]GM 命令[/url]\n";
		}

		private void ShowGmCommands()
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("[color=white][b]GM 命令一覽[/b][/color]\n\n");
			sb.Append("[url=\"link:option\"]返回選項[/url]\n\n");
			sb.Append("[color=gray]以下為伺服器常用 GM 指令與用法，實際以伺服器設定為準。[/color]\n\n");
			sb.Append("[b]傳送 / 移動[/b]\n");
			sb.Append("• .move [地圖ID] [X] [Y] — 傳送到指定座標\n");
			sb.Append("• .call [角色名] — 將目標召喚到身邊\n");
			sb.Append("• .send [角色名] — 傳送到目標身邊\n\n");
			sb.Append("[b]角色 / 道具[/b]\n");
			sb.Append("• .item [道具ID] [數量] — 取得道具\n");
			sb.Append("• .level [等級] — 設定等級\n");
			sb.Append("• .heal — 補滿 HP/MP\n\n");
			sb.Append("[b]魔法[/b]\n");
			sb.Append("• .learn50 — 習得 50 個一般魔法（自身）\n");
			sb.Append("• .addskill — 依職業習得全部魔法\n\n");
			sb.Append("[b]其他[/b]\n");
			sb.Append("• .inv — 開啟/關閉無敵\n");
			sb.Append("• .poly [變身ID] — 變身\n");
			sb.Append("• .addskill [技能ID] — 學習單一技能（依職業）\n");
			_contentLabel.Text = sb.ToString();
		}

		private void ShowBookmarkList()
		{
			var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
			if (world == null)
			{
				_contentLabel.Text = "[color=red]無法取得世界狀態[/color]";
				return;
			}
			_bookmarksCache = world.GetBookmarks();
			var sb = new System.Text.StringBuilder();
			sb.Append("[color=white][b]記憶座標[/b][/color]\n\n");
			sb.Append("[url=\"action:add_bookmark\"]增加書籤[/url]（請在下方 HUD 輸入框輸入名稱後送出）\n");
			sb.Append("[url=\"link:option\"]返回選項[/url]\n\n");
			if (_bookmarksCache.Count == 0)
				sb.Append("[color=gray]尚無記憶座標。點「增加書籤」後在 HUD 輸入框輸入名稱並送出即可記錄當前位置。[/color]");
			else
			{
				for (int i = 0; i < _bookmarksCache.Count; i++)
				{
					var b = _bookmarksCache[i];
					sb.Append($"[url=\"action:teleport_bookmark:{i}\"]{b.name} (地圖{b.mapId}, {b.x}, {b.y})[/url] [url=\"action:delete_bookmark:{i}\"]刪除[/url]\n");
				}
			}
			_contentLabel.Text = sb.ToString();
		}

		// ==========================================================
		//  HTML -> BBCode 解析器 (移植自 TalkWindow + 增强)
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

			// 6. 变量解析
			html = Regex.Replace(html, @"<var\s+src=""?#(\d+)""?>", MatchVar, RegexOptions.IgnoreCase);

			// 7. 語言設置變量替換（更新為4種語言：英語、韓文、中文、日文）
			string currentLang = ClientConfig.Language;
			html = html.Replace("%LANG_EN%", currentLang == "en" ? "[color=green]English[/color]" : "[url=\"action:language_en\"]English[/url]");
			html = html.Replace("%LANG_KR%", currentLang == "kr" ? "[color=green]한국어[/color]" : "[url=\"action:language_kr\"]한국어[/url]");
			html = html.Replace("%LANG_CN%", currentLang == "c" || currentLang == "cn" || currentLang == "cn2" ? "[color=green]中文[/color]" : "[url=\"action:language_c\"]中文[/url]");
			html = html.Replace("%LANG_J%", currentLang == "j" ? "[color=green]日本語[/color]" : "[url=\"action:language_j\"]日本語[/url]");

			html = html.Replace("&nbsp;", " ");

			return html;
		}

		private void OnMetaClicked(Variant meta) 
		{
			string data = meta.AsString();
			GD.Print($"[Options] Clicked: {data}");
			
			if (data.StartsWith("link:")) 
			{
				LoadAndShowHtml(data.Substring(5), null);
			}
			else if (data.StartsWith("action:")) 
			{
				HandleClientAction(data.Substring(7));
			}
			else 
			{
				// 兼容旧格式
				if (IsClientAction(data)) HandleClientAction(data);
				else LoadAndShowHtml(data, null);
			}
		}

		private bool IsClientAction(string cmd)
		{
			return cmd == "musicon" || cmd == "soundon" || cmd == "autosave" || cmd == "restart" || cmd == "quit" ||
			       cmd == "returntologin" || cmd == "bookmark" || cmd == "add_bookmark" ||
			       cmd.StartsWith("teleport_bookmark:") || cmd.StartsWith("delete_bookmark:") ||
			       cmd == "monsterhp" || cmd == "daynight" || cmd == "magezmagic" ||
			       cmd == "language_en" || cmd == "language_c" || cmd == "language_kr" || cmd == "language_j" ||
			       cmd == "gm_commands" || cmd.StartsWith("link:gm");
		}

		private void HandleClientAction(string action) 
		{
			GD.Print($"[Options] Action: {action}");
			if (action == "musicon") {
				 ClientConfig.MusicEnabled = !ClientConfig.MusicEnabled;
				 ClientConfig.Save();
				 Boot.Instance?.ApplyAudioSettings();
				 ReloadCurrentPage();
			}
			else if (action == "soundon") {
				 ClientConfig.SoundEnabled = !ClientConfig.SoundEnabled;
				 ClientConfig.Save();
				 Boot.Instance?.ApplyAudioSettings();
				 ReloadCurrentPage();
			}
			else if (action == "monsterhp")
			{
				ClientConfig.ShowMonsterHealthBar = !ClientConfig.ShowMonsterHealthBar;
				ClientConfig.Save();
				var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
				if (world != null)
				{
					world.SendClientOption(3, ClientConfig.ShowMonsterHealthBar ? 1 : 0); // type=3 怪物血條，同步伺服器 hpBar 以收到 104
					world.RefreshMonsterHealthBars();
				}
				ReloadCurrentPage();
			}
			else if (action == "daynight")
			{
				ClientConfig.DayNightOverlayEnabled = !ClientConfig.DayNightOverlayEnabled;
				ClientConfig.Save();
				GetNodeOrNull<GameWorld>("/root/Boot/World")?.SetDayNightOverlayEnabled(ClientConfig.DayNightOverlayEnabled);
				ReloadCurrentPage();
			}
			else if (action == "magezmagic")
			{
				ClientConfig.MageZMagicAttackEnabled = !ClientConfig.MageZMagicAttackEnabled;
				ClientConfig.Save();
				ReloadCurrentPage();
			}
			else if (action == "language_en" || action == "language_c" || action == "language_kr" || action == "language_j")
			{
				string newLang = action.Substring(9); // 從 "language_" 後提取語言代碼
				if (ClientConfig.Language != newLang)
				{
					ClientConfig.Language = newLang;
					ClientConfig.Save();
					// 提示用戶需要重登遊戲以生效新語言
					ShowLanguageChangeNotification();
					ReloadCurrentPage();
				}
			}
			else if (action == "restart")
			{
				Close();
				GetNodeOrNull<GameWorld>("/root/Boot/World")?.SendRestartRequest();
			}
			else if (action == "returntologin")
			{
				Close();
				GetNodeOrNull<GameWorld>("/root/Boot/World")?.SendReturnToLoginRequest();
			}
			else if (action == "bookmark")
			{
				LoadAndShowHtml("bookmark", null);
			}
			else if (action == "add_bookmark")
			{
				var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
				if (world != null)
				{
					world.RequestBookmarkNameInput();
					Close();
				}
			}
			else if (action.StartsWith("teleport_bookmark:"))
			{
				string idxStr = action.Substring("teleport_bookmark:".Length);
				if (int.TryParse(idxStr, out int idx) && idx >= 0 && idx < _bookmarksCache.Count)
				{
					var b = _bookmarksCache[idx];
					var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
					if (world != null)
					{
						world.SendTeleportToBookmark(b.name, b.mapId, b.x, b.y);
						Close();
					}
				}
			}
			else if (action.StartsWith("delete_bookmark:"))
			{
				string idxStr = action.Substring("delete_bookmark:".Length);
				if (int.TryParse(idxStr, out int idx) && idx >= 0 && idx < _bookmarksCache.Count)
				{
					string name = _bookmarksCache[idx].name;
					GetNodeOrNull<GameWorld>("/root/Boot/World")?.SendDeleteBookmark(name);
					ShowBookmarkList();
				}
			}
			else if (action == "quit")
			{
				Close();
				GetNodeOrNull<GameWorld>("/root/Boot/World")?.SendQuitRequest();
			}
		}

		private void ShowLanguageChangeNotification()
		{
			// 顯示提示訊息
			var hud = GetNodeOrNull<Client.UI.HUD>("/root/Boot/World/Hud");
			if (hud != null)
			{
				hud.AddSystemMessage("[color=yellow]語言設置已更改，請重新登錄遊戲以生效。[/color]");
			}
			GD.Print("[OptionsWindow] 語言設置已更改，需要重登遊戲以生效。");
		}

		private void ReloadCurrentPage() 
		{
			LoadAndShowHtml("option", null); 
		}

		private string MatchVar(Match m) 
		{ 
			if (int.TryParse(m.Groups[1].Value, out int id))
			{
				bool state = false;
				if (id == 1000) state = ClientConfig.MusicEnabled;
				if (id == 1001) state = ClientConfig.SoundEnabled;
				// 使用新的變量 ID，避免與現有選項衝突
				if (id == 1020) state = ClientConfig.ShowMonsterHealthBar;
				if (id == 1021) state = ClientConfig.DayNightOverlayEnabled;
				if (id == 1022) state = ClientConfig.MageZMagicAttackEnabled;
				return state ? "[color=green]ON[/color]" : "[color=gray]OFF[/color]";
			}
			return ""; 
		}

		/// <summary>獲取當前語言顯示名稱</summary>
		private string GetLanguageDisplayName(string langCode)
		{
			switch (langCode)
			{
				case "en": return "English";
				case "c":
				case "cn":
				case "cn2": return "中文"; // 中文統一使用 -c 後綴
				case "kr": return "한국어";
				case "j": return "日本語";
				default: return langCode;
			}
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
				GD.PrintErr($"[OptionsWindow] ReadFileAsUTF8 錯誤: {path}, {e.Message}");
				return "";
			}
		}

	}
}
