using Godot;
using System;
using System.Collections.Generic;
using Skins.CustomFantasy;
using Client.Utility;
using System.Threading.Tasks; // 引入异步库

namespace Client.Utility
{
	public partial class PakBrowser : Control
	{
		// ================================================================
		// 配置路径
		// ================================================================
		private const string LIST_SPR_PATH = "res://Assets/list.spr";
		private static readonly string[] PAK_OPTIONS = new[] { "res://Assets/sprites-138-new2.pak", "res://Assets/sprites-138.pak" };
		// ================================================================
		// 核心组件
		// ================================================================
		private CustomCharacterProvider _provider;
		private AnimatedSprite2D _bodySprite;
		private AnimatedSprite2D _shadowSprite;   // 第2層：101.shadow(參數) → GfxId
		private AnimatedSprite2D _clothesSprite; // 第3層：105.clothes(參數) → GfxId
		private AnimatedSprite2D _effectSprite;  // ]effectId 幀內特效（如 ]212 加載 gfxId=212）
		private AudioStreamPlayer _sfxPlayer;    // [ 與 < 幀音效
		private Label _infoLabel;
		private Label _loadingLabel; // 加载提示
		private ItemList _gfxList;
		private GridContainer _actionGrid;
		private TextEdit _listSprText;
		
		// 状态数据
		private int _currentGfxId = -1;
		private int _currentActionId = 0; // 默认 Walk
		private int _currentHeading = 4;  // 默认 左下
		private bool _isPlaying = true;
		private int _currentPakIndex = 0;   // 0 或 1 對應 PAK_OPTIONS
		private int _currentParamFilter = 0; // 0=all, 101,102,104,105,106,109 篩選角色列表

		// ================================================================
		// [核心修改] 使用 async void _Ready 实现异步启动
		// ================================================================
		public override async void _Ready()
		{
			GD.Print(">>> [PakBrowser] 1. 启动程序...");

			// 1. 先构建基础 UI (全屏背景 + 加载提示)
			// 这一步非常快，保证窗口瞬间有内容
			SetupBaseUI();

			// 2. 【关键】强制等待 0.1 秒，让 Godot 渲染出一帧画面
			// 这样你就不会只看到 Logo 了，而是看到深红色的背景和文字
			await ToSignal(GetTree().CreateTimer(0.1f), "timeout");

			GD.Print(">>> [PakBrowser] 2. 开始加载重型资源...");

			// 3. 执行耗时的加载任务
			bool success = InitializeSystems();

			if (!success)
			{
				_loadingLabel.Text = "❌ 启动失败！\n请检查 res://Assets/ 下是否有 list.spr 和 sprites.pak\n查看 Output 控制台获取详情。";
				_loadingLabel.Modulate = Colors.Red;
				return; // 终止
			}

			// 4. 资源加载成功，构建完整功能 UI
			_loadingLabel.QueueFree(); // 删除加载提示
			BuildMainUI();             // 构建主界面
			RefreshGfxList();          // 刷新数据

			GD.Print(">>> [PakBrowser] 3. 就绪！");
		}

		private void SetupBaseUI()
		{
			// 强制全屏
			this.SetAnchorsPreset(LayoutPreset.FullRect);
			this.Size = GetViewportRect().Size;

			// 深红色背景 (调试用，证明代码运行了)
			var bg = new ColorRect();
			bg.Color = new Color(0.3f, 0.0f, 0.0f); 
			bg.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(bg);

			// 加载文字
			_loadingLabel = new Label();
			_loadingLabel.Text = "正在读取 PAK 和 SPR 文件...\n请稍候...";
			_loadingLabel.Position = new Vector2(50, 50);
			_loadingLabel.Scale = new Vector2(2, 2);
			AddChild(_loadingLabel);
		}

		private bool InitializeSystems()
		{
			try
			{
				string listPath = ProjectSettings.GlobalizePath(LIST_SPR_PATH);
				string pakPath = ProjectSettings.GlobalizePath(PAK_OPTIONS[_currentPakIndex]);

				if (!System.IO.File.Exists(listPath))
				{
					GD.PrintErr($"[缺失] List: {listPath}");
					return false;
				}
				if (!System.IO.File.Exists(pakPath))
				{
					GD.PrintErr($"[缺失] Pak:  {pakPath}");
					return false;
				}

				ListSprLoader.Load(listPath);
				_provider = new CustomCharacterProvider(pakPath);
				return true;
			}
			catch (Exception e)
			{
				GD.PrintErr($"[崩溃] 初始化失败: {e}");
				return false;
			}
		}

		// ================================================================
		// UI 构建
		// ================================================================
		// 【修复】函数名从 BuildUI 改为 BuildMainUI，以匹配调用
		private void BuildMainUI()
		{
			// 背景板
			var bg = new Panel();
			bg.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(bg);

			// 主分割容器
			var mainSplit = new HSplitContainer();
			mainSplit.SetAnchorsPreset(LayoutPreset.FullRect);
			mainSplit.SplitOffset = 250; 
			AddChild(mainSplit);

			// --- 左侧：角色列表 ---
			var leftPanel = new VBoxContainer();
			// 左侧加点边距
			var marginContainer = new MarginContainer();
			marginContainer.AddThemeConstantOverride("margin_top", 10);
			marginContainer.AddThemeConstantOverride("margin_left", 10);
			marginContainer.AddThemeConstantOverride("margin_bottom", 10);
			marginContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			
			var leftVBox = new VBoxContainer();
			leftVBox.AddChild(new Label { Text = " 角色列表 (SprDefinition)" });
			
			_gfxList = new ItemList();
			_gfxList.SizeFlagsVertical = SizeFlags.ExpandFill;
			_gfxList.ItemSelected += OnGfxSelected;
			leftVBox.AddChild(_gfxList);
			
			marginContainer.AddChild(leftVBox);
			leftPanel.AddChild(marginContainer);
			leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill; // 确保撑开
			
			mainSplit.AddChild(leftPanel);

			// --- 右側：預覽+控制+動作 | list.spr 定義 ---
			var rightSplit = new HSplitContainer();
			rightSplit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rightSplit.SplitOffset = 600;
			mainSplit.AddChild(rightSplit);

			var rightPanel = new VBoxContainer();
			rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rightSplit.AddChild(rightPanel);

			// 1. 顶部：预览窗口
			var previewContainer = new Control();
			previewContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			previewContainer.SizeFlagsStretchRatio = 0.6f;
			previewContainer.CustomMinimumSize = new Vector2(0, 300);
			
			var previewBg = new ColorRect { Color = new Color(0.2f, 0.2f, 0.2f) };
			previewBg.SetAnchorsPreset(LayoutPreset.FullRect);
			previewContainer.AddChild(previewBg);
			
			// 取消十字線，避免動畫圖像上疊一層白色
			var centerNode = new Control();
			centerNode.SetAnchorsPreset(LayoutPreset.Center);
			previewContainer.AddChild(centerNode);
			
			var charRoot = new Node2D { Scale = new Vector2(2, 2) }; 
			centerNode.AddChild(charRoot);

			// 三層疊加順序（由下到上）：shadow(101) → body(主體) → clothes(105)，皆中心對齊
			_shadowSprite = new AnimatedSprite2D { Modulate = new Color(1, 1, 1, 0.5f) };
			_shadowSprite.Centered = true;
			_shadowSprite.Position = Vector2.Zero;
			charRoot.AddChild(_shadowSprite);

			_bodySprite = new AnimatedSprite2D();
			_bodySprite.Centered = true;
			_bodySprite.Position = Vector2.Zero;
			_bodySprite.FrameChanged += OnFrameChanged;
			_bodySprite.AnimationFinished += OnBodyAnimationFinished;
			charRoot.AddChild(_bodySprite);

			_clothesSprite = new AnimatedSprite2D();
			_clothesSprite.Centered = true;
			_clothesSprite.Position = Vector2.Zero;
			charRoot.AddChild(_clothesSprite);

			_effectSprite = new AnimatedSprite2D();
			_effectSprite.Centered = true;
			_effectSprite.Position = Vector2.Zero;
			_effectSprite.Visible = false;
			charRoot.AddChild(_effectSprite);

			_sfxPlayer = new AudioStreamPlayer();
			if (AudioServer.GetBusIndex("SFX") >= 0) _sfxPlayer.Bus = "SFX";
			AddChild(_sfxPlayer);

			// 信息标签 (放到左上角)
			_infoLabel = new Label();
			_infoLabel.Position = new Vector2(10, 10);
			previewContainer.AddChild(_infoLabel);

			rightPanel.AddChild(previewContainer);

			// 2. 控制栏
			var controlBar = new HBoxContainer();
			controlBar.Alignment = BoxContainer.AlignmentMode.Center;
			controlBar.AddChild(CreateBtn("⏮", () => { _bodySprite.Frame = 0; }));
			controlBar.AddChild(CreateBtn("⏯ 播放/暂停", TogglePlay));
			controlBar.AddChild(CreateBtn("⏭ 单帧", NextFrame));
			controlBar.AddChild(new VSeparator());
			for(int i=0; i<8; i++) {
				int h = i;
				controlBar.AddChild(CreateBtn(h.ToString(), () => ChangeHeading(h)));
			}
			rightPanel.AddChild(controlBar);

			// 3. 底部：角色動作區 (SprActionSequence)
			var actionLabel = new Label { Text = " 角色動作區 (SprActionSequence) — 選動作後播放" };
			rightPanel.AddChild(actionLabel);
			var scrollAction = new ScrollContainer();
			scrollAction.SizeFlagsVertical = SizeFlags.ExpandFill;
			scrollAction.SizeFlagsStretchRatio = 0.4f;
			
			_actionGrid = new GridContainer();
			_actionGrid.Columns = 8;
			_actionGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scrollAction.AddChild(_actionGrid);
			
			rightPanel.AddChild(scrollAction);

			// 4. 右邊第 4 區：list.spr 定義（對應角色的 list.spr 內容）
			var listSprPanel = new VBoxContainer();
			listSprPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			listSprPanel.CustomMinimumSize = new Vector2(280, 0);
			listSprPanel.AddChild(new Label { Text = " list.spr 定義 (對應角色)" });
			var listSprScroll = new ScrollContainer();
			listSprScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			_listSprText = new TextEdit();
			_listSprText.Editable = false;
			_listSprText.WrapMode = TextEdit.LineWrappingMode.Boundary;
			_listSprText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_listSprText.SizeFlagsVertical = SizeFlags.ExpandFill;
			listSprScroll.AddChild(_listSprText);
			listSprPanel.AddChild(listSprScroll);

			// 4. PAK 選擇區（內置 2 個邏輯）
			listSprPanel.AddChild(new Label { Text = " 4. PAK 選擇區" });
			var pakRow = new HBoxContainer();
			for (int i = 0; i < PAK_OPTIONS.Length; i++)
			{
				string name = System.IO.Path.GetFileName(PAK_OPTIONS[i]);
				var btn = new Button { Text = name };
				int idx = i;
				btn.Pressed += () => OnPakSelected(idx);
				if (i == _currentPakIndex) btn.Modulate = new Color(0.6f, 1f, 0.6f);
				pakRow.AddChild(btn);
			}
			listSprPanel.AddChild(pakRow);

			// 5. 特別參數選擇區（all + 101,102,104,105,106,109）關聯角色列表篩選
			listSprPanel.AddChild(new Label { Text = " 5. 特別參數選擇區（篩選角色列表）" });
			var paramRow = new HBoxContainer();
			var paramIds = new[] { 0, 101, 102, 104, 105, 106, 109 }; // 0 = all
			foreach (int pid in paramIds)
			{
				string label = pid == 0 ? "all" : pid.ToString();
				var btn = new Button { Text = label };
				int p = pid;
				btn.Pressed += () => OnParamFilterSelected(p);
				if (p == _currentParamFilter) btn.Modulate = new Color(0.6f, 1f, 0.6f);
				paramRow.AddChild(btn);
			}
			listSprPanel.AddChild(paramRow);

			rightSplit.AddChild(listSprPanel);
		}

		// ================================================================
		// 逻辑控制
		// ================================================================
		private void RefreshGfxList()
		{
			_gfxList.Clear();
			var ids = ListSprLoader.GetAllGfxIds();
			ids.Sort();
			foreach (var id in ids)
			{
				var def = ListSprLoader.Get(id);
				if (def == null) continue;
				// 特別參數篩選：0=all，否則只顯示有該元數據 ID 的 SprDefinition
				if (_currentParamFilter != 0 && !def.ParsedMetadataIds.Contains(_currentParamFilter))
					continue;
				string name = def.Name ?? "Unknown";
				_gfxList.AddItem($"#{id} - {name}");
				_gfxList.SetItemMetadata(_gfxList.ItemCount - 1, id);
			}
			GD.Print($"[PakBrowser] 角色列表 (SprDefinition) 共 {_gfxList.ItemCount} 個 (篩選={_currentParamFilter})");
		}

		private void OnPakSelected(int index)
		{
			if (index == _currentPakIndex) return;
			_currentPakIndex = index;
			string pakPath = ProjectSettings.GlobalizePath(PAK_OPTIONS[_currentPakIndex]);
			if (!System.IO.File.Exists(pakPath))
			{
				GD.PrintErr($"[PakBrowser] 找不到 PAK: {pakPath}");
				return;
			}
			_provider = new CustomCharacterProvider(pakPath);
			RefreshGfxList();
			RefreshActionButtons();
			RefreshListSprContent();
			PlayAnimation();
		}

		private void OnParamFilterSelected(int paramId)
		{
			if (paramId == _currentParamFilter) return;
			_currentParamFilter = paramId;
			RefreshGfxList();
		}

		private void OnGfxSelected(long index)
		{
			_currentGfxId = (int)_gfxList.GetItemMetadata((int)index);
			RefreshActionButtons();
			RefreshListSprContent();
			PlayAnimation();
		}

		private void RefreshListSprContent()
		{
			if (_listSprText == null) return;
			string path = ProjectSettings.GlobalizePath(LIST_SPR_PATH);
			_listSprText.Text = GetListSprBlock(path, _currentGfxId);
		}

		/// <summary>從 list.spr 讀取對應 GfxId 的定義區塊（#id 開頭到下一 # 或檔案結尾）</summary>
		private static string GetListSprBlock(string listSprPath, int gfxId)
		{
			if (gfxId < 0 || !System.IO.File.Exists(listSprPath)) return "";
			string prefix = "#" + gfxId.ToString();
			var lines = new List<string>();
			bool inBlock = false;
			foreach (string line in System.IO.File.ReadAllLines(listSprPath))
			{
				string trimmed = line.Trim();
				if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//")) continue;
				if (trimmed.StartsWith("#"))
				{
					if (trimmed.StartsWith(prefix) && (trimmed.Length == prefix.Length || trimmed[prefix.Length] == ' ' || trimmed[prefix.Length] == '\t'))
					{
						inBlock = true;
						lines.Add(line);
					}
					else
						inBlock = false;
					continue;
				}
				if (inBlock) lines.Add(line);
			}
			return string.Join("\n", lines);
		}

		private void RefreshActionButtons()
		{
			foreach (var c in _actionGrid.GetChildren()) c.QueueFree();

			var def = ListSprLoader.Get(_currentGfxId);
			if (def == null) return;

			// 根據 SprActionSequence 列出該角色所有動作 (def.Actions)
			var actionIds = new List<int>(def.Actions.Keys);
			actionIds.Sort();
			foreach (int actId in actionIds)
			{
				var seq = def.Actions[actId];
				string btnName = $"{actId}.{seq?.Name ?? ""}";
				var btn = new Button { Text = btnName };
				btn.Modulate = new Color(0.5f, 1f, 0.5f);
				int a = actId;
				btn.Pressed += () => ChangeAction(a);
				_actionGrid.AddChild(btn);
			}
		}

		private void ChangeAction(int actionId)
		{
			_currentActionId = actionId;
			PlayAnimation();
		}

		private void ChangeHeading(int h)
		{
			_currentHeading = h;
			PlayAnimation();
		}

		private void PlayAnimation()
		{
			if (_currentGfxId < 0 || _provider == null) return;

			_effectSprite.Visible = false;

			try 
			{
				var def = ListSprLoader.Get(_currentGfxId);
				var bodyFrames = _provider.GetBodyFrames(_currentGfxId, _currentActionId, _currentHeading);
				
				if (bodyFrames != null)
				{
					_bodySprite.SpriteFrames = bodyFrames;
					// 動畫名：依 SprActionSequence.DirectionFlag，無向用 "0"，有向用 heading
					var seq = ListSprLoader.GetActionSequence(def, _currentActionId);
					string animName = (seq != null && seq.DirectionFlag == 0) ? "0" : _currentHeading.ToString();
					if (bodyFrames.HasAnimation(animName))
						_bodySprite.Play(animName);
					else
					{
						var names = bodyFrames.GetAnimationNames();
						if (names != null && names.Length > 0)
							_bodySprite.Play(names[0]);
					}
					_bodySprite.SpeedScale = _isPlaying ? 1.0f : 0.0f;
				}
				else
				{
					_bodySprite.SpriteFrames = null;
				}

				// 第2層：101.shadow(參數) — 與主體同動作、同幀數，才能同步停止（1.attack、3.breath 等非循環時陰影一起停）
				if (def != null && def.ShadowId > 0)
				{
					var shadowFrames = _provider.GetBodyFrames(def.ShadowId, _currentGfxId, _currentActionId, _currentHeading);
					if (shadowFrames == null) shadowFrames = _provider.GetBodyFrames(def.ShadowId, _currentActionId, _currentHeading);
					if (shadowFrames == null) shadowFrames = _provider.GetBodyFrames(def.ShadowId, _currentGfxId, 0, _currentHeading);
					if (shadowFrames == null) shadowFrames = _provider.GetBodyFrames(def.ShadowId, 0, _currentHeading);
					if (shadowFrames != null)
					{
						_shadowSprite.SpriteFrames = shadowFrames;
						var shadowSeq = ListSprLoader.GetActionSequence(ListSprLoader.Get(def.ShadowId), _currentActionId);
						string shadowAnim = (shadowSeq != null && shadowSeq.DirectionFlag == 0) ? "0" : _currentHeading.ToString();
						if (shadowFrames.HasAnimation(shadowAnim))
							_shadowSprite.Play(shadowAnim);
						else
						{
							var names = shadowFrames.GetAnimationNames();
							if (names != null && names.Length > 0) _shadowSprite.Play(names[0]);
						}
						_shadowSprite.SpeedScale = 0f;
						_shadowSprite.Visible = true;
					}
					else _shadowSprite.Visible = false;
				}
				else _shadowSprite.Visible = false;

				// 第3層：105.clothes(參數) — 括號內有參數如 "1 242" 則取 GfxId=242 疊加
				if (def != null && def.ClothesIds != null && def.ClothesIds.Count > 0)
				{
					int clothesGfxId = def.ClothesIds[def.ClothesIds.Count - 1];
					var clothesFrames = _provider.GetBodyFrames(clothesGfxId, _currentGfxId, _currentActionId, _currentHeading);
					if (clothesFrames == null) clothesFrames = _provider.GetBodyFrames(clothesGfxId, _currentActionId, _currentHeading);
					if (clothesFrames != null)
					{
						_clothesSprite.SpriteFrames = clothesFrames;
						var seq = ListSprLoader.GetActionSequence(ListSprLoader.Get(clothesGfxId), _currentActionId);
						string animName = (seq != null && seq.DirectionFlag == 0) ? "0" : _currentHeading.ToString();
						if (clothesFrames.HasAnimation(animName))
							_clothesSprite.Play(animName);
						else
						{
							var names = clothesFrames.GetAnimationNames();
							if (names != null && names.Length > 0) _clothesSprite.Play(names[0]);
						}
						_clothesSprite.SpeedScale = 0f;
						_clothesSprite.Visible = true;
					}
					else _clothesSprite.Visible = false;
				}
				else _clothesSprite.Visible = false;

				// 三層起始幀一致：主體由 Play() 從 0 開始，陰影/服裝手動設為 0，確保 1.attack / 3.breath 等從頭同步
				_shadowSprite.Frame = 0;
				_clothesSprite.Frame = 0;

				UpdateAllLayerOffsets();
				UpdateInfoLabel();
			}
			catch(Exception e)
			{
				GD.PrintErr($"播放动画出错: {e.Message}");
			}
		}

		// ================================================================
		// 【已測試通過，不允許修改】此功能（body 主體 / shadow 陰影 / clothes 衣服三層對齊）與 GameEntity.Visuals 規則一致。
		// 依 sprite_offsets 對齊三層，規則與 batch_merge_offsets.py 一致。(dx,dy)=該幀圖像「左上角」在共同座標系。
		// 主體中心置於 (0,0)；Shadow/Clothes 相對主體中心公式。僅用首幀 (frame 0) 計算 Offset。若需修改必須獲得許可。
		// ================================================================
		/// <summary>從圖層指定幀紋理讀取 sprite_offsets (dx,dy)=左上角位置 與尺寸 (w,h)。frameIndex &lt; 0 用當前幀。</summary>
		private static bool GetLayerFrameAnchor(AnimatedSprite2D layer, int frameIndex, out int dx, out int dy, out int w, out int h)
		{
			dx = dy = w = h = 0;
			if (layer == null || layer.SpriteFrames == null) return false;
			int useFrame = frameIndex >= 0 ? frameIndex : layer.Frame;
			int count = layer.SpriteFrames.GetFrameCount(layer.Animation);
			if (count <= 0) return false;
			useFrame = useFrame % count;
			var tex = layer.SpriteFrames.GetFrameTexture(layer.Animation, useFrame);
			if (tex == null) return false;
			dx = tex.HasMeta("spr_anchor_x") ? (int)tex.GetMeta("spr_anchor_x") : 0;
			dy = tex.HasMeta("spr_anchor_y") ? (int)tex.GetMeta("spr_anchor_y") : 0;
			w = tex.GetWidth();
			h = tex.GetHeight();
			return true;
		}

		/// <summary>【已測試通過，不允許修改】batch_merge 規則：(dx,dy)=圖像左上角。主體中心 (0,0)；Shadow/Clothes 相對主體中心公式。若需修改必須獲得許可。</summary>
		private void UpdateAllLayerOffsets()
		{
			const int useFrame = 0;
			if (!GetLayerFrameAnchor(_bodySprite, useFrame, out int bodyDx, out int bodyDy, out int bodyW, out int bodyH))
			{
				_bodySprite.Offset = Vector2.Zero;
				if (_shadowSprite.Visible && _shadowSprite.SpriteFrames != null) _shadowSprite.Offset = Vector2.Zero;
				if (_clothesSprite.Visible && _clothesSprite.SpriteFrames != null) _clothesSprite.Offset = Vector2.Zero;
				return;
			}
			// 主體：batch_merge 中身體左上在 (bodyDx, bodyDy)，中心在 (bodyDx + bodyW/2, bodyDy + bodyH/2)。置中心於畫面 (0,0) → Offset=(0,0)。
			_bodySprite.Offset = Vector2.Zero;

			if (_shadowSprite.Visible && _shadowSprite.SpriteFrames != null &&
			    GetLayerFrameAnchor(_shadowSprite, useFrame, out int sDx, out int sDy, out int sW, out int sH))
			{
				// 共同座標系下陰影左上在 (sDx, sDy)。以主體中心為原點時，陰影左上應在 (sDx - bodyDx - bodyW/2, sDy - bodyDy - bodyH/2)。
				// Centered=true 時紋理左上在 (Offset - sW/2, Offset - sH/2)，令其等於上式得：
				float ox = sDx - bodyDx - bodyW / 2f + sW / 2f;
				float oy = sDy - bodyDy - bodyH / 2f + sH / 2f;
				_shadowSprite.Offset = new Vector2(ox, oy);
			}
			else if (_shadowSprite.Visible && _shadowSprite.SpriteFrames != null)
				_shadowSprite.Offset = Vector2.Zero;

			if (_clothesSprite.Visible && _clothesSprite.SpriteFrames != null &&
			    GetLayerFrameAnchor(_clothesSprite, useFrame, out int cDx, out int cDy, out int cW, out int cH))
			{
				float ox = cDx - bodyDx - bodyW / 2f + cW / 2f;
				float oy = cDy - bodyDy - bodyH / 2f + cH / 2f;
				_clothesSprite.Offset = new Vector2(ox, oy);
			}
			else if (_clothesSprite.Visible && _clothesSprite.SpriteFrames != null)
				_clothesSprite.Offset = Vector2.Zero;
		}

		private static string GetTextureFileName(AnimatedSprite2D layer, int frameIndex)
		{
			if (layer?.SpriteFrames == null) return "—";
			int useFrame = frameIndex >= 0 ? frameIndex : layer.Frame;
			int count = layer.SpriteFrames.GetFrameCount(layer.Animation);
			if (count <= 0) return "—";
			useFrame = useFrame % count;
			var tex = layer.SpriteFrames.GetFrameTexture(layer.Animation, useFrame);
			return tex != null && tex.HasMeta("spr_file_name") ? (string)tex.GetMeta("spr_file_name") : "—";
		}

		private void UpdateInfoLabel()
		{
			var def = ListSprLoader.Get(_currentGfxId);
			var seq = ListSprLoader.GetActionSequence(def, _currentActionId);
			string actName = seq?.Name ?? "-";
			int frameCount = seq?.Frames?.Count ?? 0;
			string layer2 = (def != null && def.ShadowId > 0) ? $" 101.shadow→Gfx{def.ShadowId}" : " 101.shadow:—";
			int clothesGfx = (def != null && def.ClothesIds != null && def.ClothesIds.Count > 0) ? def.ClothesIds[def.ClothesIds.Count - 1] : -1;
			string layer3 = clothesGfx >= 0 ? $" 105.clothes→Gfx{clothesGfx}" : " 105.clothes:—";

			// a) 命名規則（gfxId, refGfxId, actionId, heading → 首幀檔名）
			string bodyRule = $"Body gfxId={_currentGfxId} refGfxId={_currentGfxId} act={_currentActionId} head={_currentHeading} → 首幀 {GetTextureFileName(_bodySprite, 0)}";
			string shadowRule = "—";
			if (def != null && def.ShadowId > 0 && _shadowSprite.Visible && _shadowSprite.SpriteFrames != null)
				shadowRule = $"Shadow gfxId={def.ShadowId} refGfxId={_currentGfxId} act={_currentActionId} head={_currentHeading} → 首幀 {GetTextureFileName(_shadowSprite, 0)}";
			string clothesRule = "—";
			if (clothesGfx >= 0 && _clothesSprite.Visible && _clothesSprite.SpriteFrames != null)
				clothesRule = $"Clothes gfxId={clothesGfx} refGfxId={_currentGfxId} act={_currentActionId} head={_currentHeading} → 首幀 {GetTextureFileName(_clothesSprite, 0)}";

			// b) 當前實際播放的檔名
			string bodyCur = $"當前 Body {GetTextureFileName(_bodySprite, -1)}";
			string shadowCur = _shadowSprite.Visible ? $"當前 Shadow {GetTextureFileName(_shadowSprite, -1)}" : "";
			string clothesCur = _clothesSprite.Visible ? $"當前 Clothes {GetTextureFileName(_clothesSprite, -1)}" : "";

			_infoLabel.Text = $" Gfx: {_currentGfxId}\n Act: {_currentActionId} ({actName})\n Dir: {_currentHeading}\n 幀數: {frameCount}\n{layer2}\n{layer3}\n\n【命名規則】\n{bodyRule}\n{shadowRule}\n{clothesRule}\n\n【實際播放】\n{bodyCur}\n{shadowCur}\n{clothesCur}";
		}

		private void TogglePlay()
		{
			_isPlaying = !_isPlaying;
			if (_bodySprite.SpriteFrames != null) _bodySprite.SpeedScale = _isPlaying ? 1.0f : 0.0f;
		}

		private void NextFrame()
		{
			_isPlaying = false;
			if (_bodySprite.SpriteFrames != null)
			{
				_bodySprite.SpeedScale = 0;
				int f = _bodySprite.Frame;
				int count = _bodySprite.SpriteFrames.GetFrameCount(_bodySprite.Animation);
				if (count > 0) _bodySprite.Frame = (f + 1) % count;
			}
			if (_shadowSprite.SpriteFrames != null)
			{
				_shadowSprite.SpeedScale = 0;
				if (_shadowSprite.SpriteFrames.HasAnimation(_shadowSprite.Animation))
				{
					int f = _bodySprite.Frame;
					int count = _shadowSprite.SpriteFrames.GetFrameCount(_shadowSprite.Animation);
					if (count > 0) _shadowSprite.Frame = f % count;
				}
			}
			if (_clothesSprite.SpriteFrames != null)
			{
				_clothesSprite.SpeedScale = 0;
				if (_clothesSprite.SpriteFrames.HasAnimation(_clothesSprite.Animation))
				{
					int bodyFrame = _bodySprite.Frame;
					int count = _clothesSprite.SpriteFrames.GetFrameCount(_clothesSprite.Animation);
					if (count > 0) _clothesSprite.Frame = bodyFrame % count;
				}
			}
		}

		/// <summary>主體動畫結束時（非循環如 1.attack、3.breath）強制同步陰影/服裝到主體當前幀，三層一起停。</summary>
		private void OnBodyAnimationFinished()
		{
			SyncShadowAndClothesToBodyFrame();
		}

		private void SyncShadowAndClothesToBodyFrame()
		{
			if (_bodySprite.SpriteFrames == null) return;
			int bodyFrame = _bodySprite.Frame;

			if (_shadowSprite.Visible && _shadowSprite.SpriteFrames != null && _shadowSprite.SpriteFrames.HasAnimation(_shadowSprite.Animation))
			{
				int count = _shadowSprite.SpriteFrames.GetFrameCount(_shadowSprite.Animation);
				if (count > 0) _shadowSprite.Frame = bodyFrame % count;
			}
			if (_clothesSprite.Visible && _clothesSprite.SpriteFrames != null && _clothesSprite.SpriteFrames.HasAnimation(_clothesSprite.Animation))
			{
				int count = _clothesSprite.SpriteFrames.GetFrameCount(_clothesSprite.Animation);
				if (count > 0) _clothesSprite.Frame = bodyFrame % count;
			}
			if (_effectSprite.Visible && _effectSprite.SpriteFrames != null && _effectSprite.SpriteFrames.HasAnimation(_effectSprite.Animation))
			{
				int count = _effectSprite.SpriteFrames.GetFrameCount(_effectSprite.Animation);
				if (count > 0) _effectSprite.Frame = bodyFrame % count;
			}
		}

		private void OnFrameChanged()
		{
			if (_bodySprite.SpriteFrames == null) return;

			SyncShadowAndClothesToBodyFrame();
			UpdateInfoLabel();

			var tex = _bodySprite.SpriteFrames.GetFrameTexture(_bodySprite.Animation, _bodySprite.Frame);
			if (tex == null) return;

			// [ 與 < 幀音效：實裝播放（同一幀多音效時播第一個，避免單一播放器覆蓋）
			if (tex.HasMeta("spr_sound_ids"))
			{
				var sounds = (Godot.Collections.Array<int>)tex.GetMeta("spr_sound_ids");
				if (sounds.Count > 0)
					PlayFrameSound(sounds[0]);
			}

			// ]effectId 幀內特效：加載並顯示對應 gfxId（如 ]212 → gfxId=212）
			if (tex.HasMeta("effects"))
			{
				var effectArr = (Godot.Collections.Array<int>)tex.GetMeta("effects");
				if (effectArr.Count > 0 && _provider != null)
				{
					int effectGfxId = effectArr[0];
					var effectFrames = _provider.GetBodyFrames(effectGfxId, effectGfxId, _currentActionId, _currentHeading);
					if (effectFrames == null) effectFrames = _provider.GetBodyFrames(effectGfxId, effectGfxId, 0, _currentHeading);
					if (effectFrames != null)
					{
						_effectSprite.SpriteFrames = effectFrames;
						var effectDef = ListSprLoader.Get(effectGfxId);
						var effectSeq = ListSprLoader.GetActionSequence(effectDef, _currentActionId) ?? ListSprLoader.GetActionSequence(effectDef, 0);
						string animName = (effectSeq != null && effectSeq.DirectionFlag == 0) ? "0" : _currentHeading.ToString();
						if (effectFrames.HasAnimation(animName))
							_effectSprite.Play(animName);
						else
						{
							var names = effectFrames.GetAnimationNames();
							if (names != null && names.Length > 0) _effectSprite.Play(names[0]);
						}
						_effectSprite.SpeedScale = 0f;
						_effectSprite.Frame = _bodySprite.Frame % Math.Max(1, effectFrames.GetFrameCount(_effectSprite.Animation));
						_effectSprite.Visible = true;
					}
					else
						_effectSprite.Visible = false;
				}
				else
					_effectSprite.Visible = false;
			}
			else
				_effectSprite.Visible = false;
		}

		private void PlayFrameSound(int soundId)
		{
			string path = $"res://Assets/CustomFantasy/sound/{soundId}.wav";
			if (!FileAccess.FileExists(path)) path = $"res://Assets/CustomFantasy/sound/{soundId}.WAV";
			if (!FileAccess.FileExists(path)) { GD.Print($"[PakBrowser] 音效 ID:{soundId} 找不到: {path}"); return; }
			var stream = GD.Load<AudioStream>(path);
			if (stream == null) { GD.PrintErr($"[PakBrowser] 音效 ID:{soundId} 載入失敗"); return; }
			_sfxPlayer.Stream = stream;
			_sfxPlayer.Play();
		}

		private Button CreateBtn(string text, Action onPress)
		{
			var b = new Button { Text = text };
			b.Pressed += onPress;
			return b;
		}
	}
}
