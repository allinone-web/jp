using Godot;
using System;
using System.Collections.Generic;
using Client;
using Core;
using Skins.CustomFantasy;

namespace UI
{
	public partial class CharacterCreate : Control
	{
		private int _remainingPoints = 0;
		private Dictionary<string, int> _currentStats = new();
		private Dictionary<string, int> _baseStats = new();
		private readonly string[] _statKeys = { "str", "dex", "con", "wis", "cha", "int" };
		
		private readonly int[][] _classBaseStats = new int[][] {
			new int[] { 13, 10, 10, 11, 13, 10 }, 
			new int[] { 16, 12, 14, 9, 12, 8 },   
			new int[] { 11, 12, 12, 12, 9, 12 },  
			new int[] { 8, 7, 12, 12, 8, 12 }     
		};
		private readonly int[] _classBonusPoints = new int[] { 8, 4, 7, 16 };

		private TextureButton[] _classBtns = new TextureButton[4];
		private TextureButton[] _sexBtns = new TextureButton[2];
		
		private int _curClassIdx = 1; 
		private int _curSexIdx = 0;   
		private AnimatedSprite2D _charPreview = null!;
		private Label _lblRemaining = null!; 
		private TextureButton _btnConfirm = null!;

		public override void _Ready()
		{
			GD.Print(">>> [UI] CharacterCreate 进入线性状态机增强版...");
			if (Boot.Instance != null)
			{
				Boot.Instance.IsInCreateCharScene = true;
				
				// --- 修复：使用唯一入口，并确保线程安全 ---
				Boot.Instance.LoginFailed += OnErrorFeedback;
				Boot.Instance.CreateCharFailed += OnErrorFeedback;
				
				// 仅监听一个成功信号，避免重复触发场景跳转
				Boot.Instance.CharacterInfoReceived += OnSuccessReceived;

				Boot.Instance.PlayBgm(0);
			  }
			
			// 1. 初始化视觉组件
			InitVisuals();
			
			// 2. 延迟刷新数据，确保 NodeTree 完全准备好
			CallDeferred(nameof(RefreshUIOnly), _curClassIdx);
		}

		public override void _ExitTree()
		{
			if (Boot.Instance != null)
			{
			Boot.Instance.IsInCreateCharScene = false;
				Boot.Instance.LoginFailed -= OnErrorFeedback;
				Boot.Instance.CreateCharFailed -= OnErrorFeedback;
				Boot.Instance.CharacterInfoReceived -= OnSuccessReceived;

			   }
		}



		// --- 修复：創角成功後跳轉角色列表。伺服器成功時會先發 S_LoginFail(2) 再發 S_CharacterAdd(opcode 5)，故收到 CharacterInfoReceived 才視為成功 ---
		private void OnSuccessReceived(Client.Data.CharacterInfo info)
		{
			GD.PrintRich($"[b][color=green]>>> [UI] 收到创角成功信号: {info?.Name}[/color][/b]");
			CallDeferred(nameof(DeferredTransition));
		}

		private void DeferredTransition()
		{
			if (Boot.Instance != null) Boot.Instance.ToCharacterSelectScene();
		}

		/// <summary>伺服器創角成功時也會發 S_LoginFail(2)，不要當成失敗處理，僅在非 Code:2 時顯示錯誤。</summary>
		private void OnErrorFeedback(string reason)
		{
			if (reason != null && reason.Contains("Code: 2")) return; // 創角成功時伺服器發的假失敗，忽略
			CallDeferred(nameof(DeferredShowError), reason);
		}

		private void DeferredShowError(string reason)
		{
			if (_btnConfirm != null) { 
				_btnConfirm.Disabled = false; 
				_btnConfirm.Modulate = Colors.White; 
			}
			// ... (保留原有的 Placeholder 逻辑)
		}

		// 点击按钮传输创建角色
		private async void OnConfirmPressed() 
		{
			var input = FindChild("NameInput", true) as LineEdit;
			string charName = input != null ? input.Text : "";
			
			if (string.IsNullOrEmpty(charName) || _remainingPoints > 0) return;

			if (_btnConfirm != null) { 
				_btnConfirm.Disabled = true; 
				_btnConfirm.Modulate = new Color(0.5f, 0.5f, 0.5f, 1); 
			}

			GD.PrintRich($"[b][color=yellow]>>> [UI] 启动 'Helper类' 创角流程...[/color][/b]");

			// 1. 发送 Op 67 (StatDice) - 激活服务器 Stat 对象
			Boot.Instance.Action_RequestNewChar(_curClassIdx);

			// 2. 延迟 0.5 秒 - 等待服务器内存就绪
			await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

			// 3. 发送 Op 12 (CreateChar)
			// 注意：这里传参顺序必须匹配 Boot 的 Action_CreateChar 签名
			// 我们的签名是: name, type, sex, str, dex, con, wis, cha, int
			Boot.Instance.Action_CreateChar(
				charName, 
				_curClassIdx, 
				_curSexIdx, 
				_currentStats["str"], 
				_currentStats["dex"], 
				_currentStats["con"], 
				_currentStats["wis"], 
				_currentStats["cha"], 
				_currentStats["int"]
			);

			// 4. 超时保护
			await ToSignal(GetTree().CreateTimer(5.0f), "timeout");
			if (IsInstanceValid(this) && _btnConfirm != null && _btnConfirm.Disabled) {
				_btnConfirm.Disabled = false;
				_btnConfirm.Modulate = Colors.White;
				GD.PrintErr("无响应：请检查名字是否重复");
			}
		}


		private void RefreshUIOnly(int type) {
			int[] baseVals = _classBaseStats[type];
			int i = 0;
			foreach(var k in _statKeys) _baseStats[k] = baseVals[i++];
			_remainingPoints = _classBonusPoints[type];
			_currentStats.Clear();
			foreach(var k in _baseStats.Keys) _currentStats[k] = _baseStats[k];
			
			UpdateUI();
			UpdatePreviewAnimation();
		}

		private void InitVisuals() {
			// 背景
			var bg = FindChild("PaperBackground", true) as TextureRect;
			if (bg != null) bg.Texture = AssetManager.Instance.GetUITexture("310.img");

			// 预览
			_charPreview = FindChild("CharPreview", true) as AnimatedSprite2D;
			
			// 剩余点数 Label
			_lblRemaining = FindChild("LabelRemaining", true) as Label;

			// 职业按钮
			string[] classNames = { "BtnRoyal", "BtnKnight", "BtnElf", "BtnMage" };
			for (int i = 0; i < 4; i++) {
				_classBtns[i] = FindChild(classNames[i], true) as TextureButton;
				SetupSelectionBtn(_classBtns[i], i, true);
			}

			// 性别按钮
			_sexBtns[0] = FindChild("BtnMale", true) as TextureButton;
			_sexBtns[1] = FindChild("BtnFemale", true) as TextureButton;
			SetupSelectionBtn(_sexBtns[0], 0, false);
			SetupSelectionBtn(_sexBtns[1], 1, false);

			// 属性调整按钮组
			SetupStatGroups();

			// 确认/返回
			_btnConfirm = FindChild("BtnConfirm", true) as TextureButton;
			if (_btnConfirm != null) {
				SetupBtn(_btnConfirm, "61.img", "62.img");
				_btnConfirm.Pressed += OnConfirmPressed;
			}

			var btnBack = FindChild("BtnBack", true) as TextureButton;
			if (btnBack != null) {
				SetupBtn(btnBack, "63.img", "64.img");
				btnBack.Pressed += () => Boot.Instance.ToCharacterSelectScene();
			}
		}

		private void SetupSelectionBtn(TextureButton btn, int idx, bool isClass) {
			if (btn == null) return;
			string n = isClass ? (idx switch { 0=>"107.img", 1=>"111.img", 2=>"109.img", _=>"113.img" }) : (idx == 0 ? "304.img" : "306.img");
			string a = isClass ? (idx switch { 0=>"108.img", 1=>"112.img", 2=>"110.img", _=>"114.img" }) : (idx == 0 ? "305.img" : "307.img");
			
			btn.SetMeta("normal", n);
			btn.SetMeta("active", a);
			btn.TextureNormal = AssetManager.Instance.GetUITexture(n);
			
			btn.Pressed += () => {
				if (isClass) { 
					_curClassIdx = idx; 
					RefreshUIOnly(idx); // 这个方法里包含了 UpdatePreviewAnimation
				} 
				else { 
					_curSexIdx = idx; 
					// [修复] 切换性别时，必须手动更新预览动画！
					UpdatePreviewAnimation(); 
				}
				UpdateSelectionVisuals();
			};
		}

		private void SetupStatGroups() {
			foreach (var key in _statKeys) {
				var hbox = FindChild($"HBox_{key}", true) as HBoxContainer;
				if (hbox != null) {
					hbox.GetNodeOrNull<BaseButton>("Buttonadd")?.Connect("pressed", Callable.From(() => OnAdjustStat(key, 1)));
					hbox.GetNodeOrNull<BaseButton>("Buttonless")?.Connect("pressed", Callable.From(() => OnAdjustStat(key, -1)));
				}
			}
		}

		private void UpdateUI() {
			foreach (var key in _statKeys) {
				var lbl = FindChild($"HBox_{key}", true)?.GetNodeOrNull<Label>("LabelValue");
				if(lbl != null) lbl.Text = _currentStats[key].ToString();
			}
			if (_lblRemaining != null) _lblRemaining.Text = $"Remaining: {_remainingPoints}";
		}

		private void UpdateSelectionVisuals() {
			for(int i=0; i<4; i++) if(_classBtns[i]!=null) SetBtnState(_classBtns[i], i==_curClassIdx);
			for(int i=0; i<2; i++) if(_sexBtns[i]!=null) SetBtnState(_sexBtns[i], i==_curSexIdx);
		}

		private void SetBtnState(TextureButton btn, bool active) {
			string key = active ? "active" : "normal";
			if(btn.HasMeta(key)) btn.TextureNormal = AssetManager.Instance.GetUITexture((string)btn.GetMeta(key));
		}

		private void SetupBtn(TextureButton btn, string normal, string hover) {
			btn.TextureNormal = AssetManager.Instance.GetUITexture(normal);
			btn.TextureHover = AssetManager.Instance.GetUITexture(hover);
			btn.TexturePressed = AssetManager.Instance.GetUITexture(hover);
		}

		private void OnAdjustStat(string key, int val) {
			if (val > 0 && _remainingPoints > 0) { _currentStats[key]++; _remainingPoints--; }
			else if (val < 0 && _currentStats[key] > _baseStats[key]) { _currentStats[key]--; _remainingPoints++; }
			UpdateUI();
		}

		private void UpdatePreviewAnimation() {
			if (_charPreview == null) return;
			SpriteFrames sf = null;
			// 调用 AssetManager 加载序列帧动画
			if (_curClassIdx == 0) sf = (_curSexIdx==0) ? AssetManager.Instance.CreateCharacterFrames(138, 151, 162) : AssetManager.Instance.CreateCharacterFrames(163, 176, 187);
			else if (_curClassIdx == 1) sf = (_curSexIdx==0) ? AssetManager.Instance.CreateCharacterFrames(213, 226, 237) : AssetManager.Instance.CreateCharacterFrames(238, 251, 264);
			else if (_curClassIdx == 2) sf = (_curSexIdx==0) ? AssetManager.Instance.CreateCharacterFrames(332, 345, 356) : AssetManager.Instance.CreateCharacterFrames(188, 201, 212);
			else if (_curClassIdx == 3) sf = (_curSexIdx==0) ? AssetManager.Instance.CreateCharacterFrames(362, 375, 386) : AssetManager.Instance.CreateCharacterFrames(621, 634, 645);
			
			if (sf != null) { 
				_charPreview.SpriteFrames = sf; 
				_charPreview.Play("walk"); 
			}
		}

		private void OnSuccessWithInfo(Client.Data.CharacterInfo info) => CallDeferred(nameof(ToSelectScene));
		private void ToSelectScene() => Boot.Instance?.ToCharacterSelectScene();
	}
}
