using Godot;
using System;
using System.Collections.Generic;
using Client.Game;
using Client.Data;
using Client.Utility;
using System.Text.RegularExpressions;

namespace Client.UI
{
	public partial class SkinSelectWindow : GameWindow
	{
		/// <summary>變身清單 HTML 基底名；載入 SkinSelectWindow-{lang}.html（如 SkinSelectWindow-c.html）。</summary>
		private const string BaseHtmlId = "SkinSelectWindow";

		private RichTextLabel _contentLabel;
		private int? _scrollObjectId;

		public override void OnOpen(WindowContext context = null)
		{
			base.OnOpen(context);
			_scrollObjectId = null;
			if (context?.ExtraData is int id)
				_scrollObjectId = id;
		}

		public override void _Ready()
		{
			base._Ready();
			
			// 1. 获取场景中预设的节点 (对应你的截图结构)
			// 结构: ContentContainer -> ScrollContainer -> VBoxContainer -> ContentLabel
			_contentLabel = FindChild("ContentLabel", true, false) as RichTextLabel;

			if (_contentLabel == null)
			{
				GD.PrintErr("[SkinSelectWindow] ❌ 严重错误: 场景中找不到 'ContentLabel'！请检查节点名称。");
				return;
			}

			// 2. 配置文本框属性 (参考 TalkWindow)
			_contentLabel.BbcodeEnabled = true;
			_contentLabel.FitContent = true;   
			_contentLabel.ScrollActive = false; // 让外层的 ScrollContainer 负责滚动
			_contentLabel.MouseFilter = MouseFilterEnum.Pass;

			if (!_contentLabel.IsConnected("meta_clicked", new Callable(this, nameof(OnMetaClicked))))
				_contentLabel.MetaClicked += OnMetaClicked;

			this.VisibilityChanged += OnVisibilityChanged;
			if (this.Visible) OnVisibilityChanged();
		}

		private void OnVisibilityChanged()
		{
			if (this.Visible)
				LoadAndShowHtml(BaseHtmlId, null);
		}



		/// <summary>依 TalkWindow 規則搜尋：基底名 + 語言後綴(-{lang}, -h, -e, 無後綴) + 副檔名(.html/.tbl)，支援多語言。</summary>
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
			string finalPath = "";
			bool found = false;

			foreach (var suffix in langSuffixes)
			{
				foreach (var ext in extensions)
				{
					string path = $"{baseDir}{htmlId}{suffix}{ext}";
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
			_contentLabel.Text = bbcode;
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

			// 連結解析（變身清單 <a action="db"> 名稱）
			string patternLinkQuoted = @"<a\s+(?:action|link)=""([^""]+)""[^>]*>";
			html = Regex.Replace(html, patternLinkQuoted, "[url=\"$1\"]", RegexOptions.IgnoreCase);
			string patternLinkNoQuote = @"<a\s+(?:action|link)=([^>\s]+)[^>]*>";
			html = Regex.Replace(html, patternLinkNoQuote, "[url=\"$1\"]", RegexOptions.IgnoreCase);
			html = Regex.Replace(html, @"</a>", "[/url]", RegexOptions.IgnoreCase);

			html = html.Replace("&nbsp;", " ");

			return html;
		}

		private void OnMetaClicked(Variant meta)
		{
			string data = meta.AsString();
			if (!_scrollObjectId.HasValue) return;
			var gw = GetNodeOrNull<GameWorld>("/root/Boot/World");
			if (gw == null) return;
			gw.UsePolymorphScroll(_scrollObjectId.Value, data);
			Close();
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
				GD.PrintErr($"[SkinSelectWindow] ReadFileAsUTF8 錯誤: {path}, {e.Message}");
				return "";
			}
		}
	}
}
