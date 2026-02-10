			// 2. 资源与背景加载 (保持原逻辑，依赖 AssetManager)
			// 必须在确保先执行 _loginButton = ...GetNodeOrNull...，
			// 然后再执行 SetupVisuals()。
			// 这样 SetupVisuals 运行时，按钮变量才有值，图片才能被赋值上去

using Godot;
using Client; // 引用 Boot
using System;
using Skins.CustomFantasy;
namespace UI
{
	public partial class Login : Control
	{
		[Export] private LineEdit _accountEdit = null!;
		[Export] private LineEdit _passwordEdit = null!;
		[Export] private TextureButton _loginButton = null!;
		[Export] private TextureButton _exitButton = null!; 

		public override void _Ready()
		{
			GD.Print(">>> [UI] Login Scene _Ready... (New Architecture)");



			// 1. 获取控件引用
			var vbox = GetNodeOrNull<Control>("VBoxContainer");
			if (vbox != null)
			{
				var btnContainer = vbox.GetNodeOrNull<Control>("BtnContainer");
				if (btnContainer != null)
				{
					_accountEdit = btnContainer.GetNodeOrNull<LineEdit>("AccountEdit");
					_passwordEdit = btnContainer.GetNodeOrNull<LineEdit>("PasswordEdit");
					_loginButton = btnContainer.GetNodeOrNull<TextureButton>("LoginButton");
					_exitButton = btnContainer.GetNodeOrNull<TextureButton>("ExitButton");
				}
			}


			// 2. 资源与背景加载 (保持原逻辑，依赖 AssetManager)
			// 必须在确保先执行 _loginButton = ...GetNodeOrNull...，
			// 然后再执行 SetupVisuals()。
			// 这样 SetupVisuals 运行时，按钮变量才有值，图片才能被赋值上去
			SetupVisuals();

			// 3. 按钮事件绑定
			if (_loginButton != null)
			{
				// 清理旧连接
				if (_loginButton.IsConnected(BaseButton.SignalName.Pressed, Callable.From(OnLoginPressed)))
					_loginButton.Disconnect(BaseButton.SignalName.Pressed, Callable.From(OnLoginPressed));
				_loginButton.Pressed += OnLoginPressed;
			}

			if (_exitButton != null)
			{
				_exitButton.Pressed += () => GetTree().Quit();
			}

			// ================================================================
			// 4. [新架构] 网络事件绑定 (通过 Boot)
			// ================================================================
			if (Boot.Instance != null)
			{
				// 先断开防止重复订阅
				Boot.Instance.LoginSuccess -= OnLoginSuccess;
				Boot.Instance.LoginFailed -= OnLoginFailed;

				// 订阅
				Boot.Instance.LoginSuccess += OnLoginSuccess;
				Boot.Instance.LoginFailed += OnLoginFailed;
			}

			// 5. 播放 BGM (适配新 Boot)
			try 
			{
				if (Boot.Instance != null)
					Boot.Instance.PlayBgm(0); // music0.mp3 对应的 ID，假设为 0，需确认 ID 映射
				else
					GD.PrintErr("[UI] Boot Instance is null!");
			}
			catch (Exception e) { GD.PrintErr(e.Message); }
		}

		public override void _ExitTree()
		{
			// 清理事件
			if (Boot.Instance != null)
			{
				Boot.Instance.LoginSuccess -= OnLoginSuccess;
				Boot.Instance.LoginFailed -= OnLoginFailed;
			}
		}

		private void SetupVisuals()
		{
			// 保持原有的 AssetManager 逻辑
			var bg = GetNodeOrNull<TextureRect>("Background"); 
			if (bg != null) 
			{
				bg.Texture = AssetManager.Instance.GetUITexture("310.img");
				bg.MouseFilter = MouseFilterEnum.Ignore;
			}



			// 2. [恢复] 动态处理输入框背景 (59.img)
			// 先获取 VBoxContainer
			var vbox = GetNodeOrNull<Control>("VBoxContainer");
			if (vbox != null)
			{
				// 尝试查找是否已经手动添加了 InputBg
				var inputBg = vbox.GetNodeOrNull<TextureRect>("InputBg");
				
				if (inputBg == null)
				{
					// 如果没找到，代码动态创建一个，确保界面美观
					var newBg = new TextureRect();
					newBg.Name = "InputBg";
					newBg.Texture = AssetManager.Instance.GetUITexture("59.img");
					newBg.ShowBehindParent = true;
					//newBg.SetAnchorsPreset(LayoutPreset.FullRect);
					//newBg.MouseFilter = MouseFilterEnum.Ignore;
					vbox.AddChild(newBg);
					vbox.MoveChild(newBg, 0); // 移到最底层
				}
			else
			{
				inputBg.Texture = AssetManager.Instance.GetUITexture("59.img");
				}
			}


			
			// 按钮图片
			if (_loginButton != null) SetupTextureButton(_loginButton, "53.img", "54.img");
			if (_exitButton != null) SetupTextureButton(_exitButton, "63.img", "64.img");
		}

		private void SetupTextureButton(TextureButton btn, string normalImg, string hoverImg)
		{
			if (btn == null) return;
			btn.TextureNormal = AssetManager.Instance.GetUITexture(normalImg);
			var active = AssetManager.Instance.GetUITexture(hoverImg);
			btn.TexturePressed = active;
			btn.TextureHover = active;
		}

		public void OnLoginPressed()
		{
			if (_accountEdit == null || _passwordEdit == null) return;

			string acc = _accountEdit.Text.Trim();
			string pwd = _passwordEdit.Text.Trim();
			
			if (string.IsNullOrEmpty(acc)) return;

			if (_loginButton != null) _loginButton.Disabled = true;
			
			GD.Print($">>> [UI] Calling Boot.Action_Login: {acc}");
			
			// [新架构] 调用 Boot
			if (Boot.Instance != null)
				Boot.Instance.Action_Login(acc, pwd);
		}

		// [新架构] 成功回调
		private void OnLoginSuccess() 
		{ 
			GD.Print(">>> [UI] Login Success! Transitioning to CharacterSelect...");
			// 跳转到选角场景
			if (Boot.Instance != null)
				Boot.Instance.ToCharacterSelectScene();
		}
		
		// [新架构] 失败回调
		private void OnLoginFailed(string msg) 
		{ 
			GD.Print($">>> [UI] Login Failed: {msg}"); 
			if (_loginButton != null) _loginButton.Disabled = false; 
			
			// 可以在这里加个简单的弹窗或者 Label 显示 msg
			_passwordEdit.Text = "";
			_accountEdit.PlaceholderText = msg;
		}
	}
}
