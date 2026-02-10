using Godot;
using System;

namespace Tool
{
	/// <summary>
	/// 打包/解包工具場景用腳本：從 UI 觸發 Pack/Unpack，供登入素材 img182 與角色/怪物/魔法圖 png138 維護。
	/// 置於專案根目錄 Tool/，與客戶端代碼分離。
	/// </summary>
	public partial class PakPackToolScene : Control
	{
		Label _statusLabel;

		// 預設：登入素材 img182（輸入/輸出路徑不含 GitHub，與來源同目錄）
		const string DefaultSourceFolder = "/Users/airtan/Documents/game-charlie/spr-bmp/img182";
		const string DefaultOutputBase = "res://Assets/Img182";
		const string DefaultPakPath = "res://Assets/Img182.pak";
		const string DefaultUnpackFolder = "/Users/airtan/Documents/game-charlie/spr-bmp/img182out";

		// png138：角色/怪物/魔法圖
		const string Png138PngDir = "/Users/airtan/Documents/game-charlie/spr-bmp/png138";
		const string Png138TxtPath = "/Users/airtan/Documents/game-charlie/spr-bmp/png138/sprite_offsets-138_update.txt";
		const string Png138OutputBase = "res://Assets/sprites-138-new2";
		const string Png138PakPath = "res://Assets/sprites-138-new2.pak";
		const string Png138UnpackFolder = "/Users/airtan/Documents/game-charlie/spr-bmp/png138out";

		public override void _Ready()
		{
			string[] args = OS.GetCmdlineArgs();
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--pack-once")
				{
					PakPackTool.Pack(DefaultSourceFolder, DefaultOutputBase, encryptIdx: true, ".img");
					GetTree().Quit();
					return;
				}
			}

			var vbox = new VBoxContainer();
			vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
			vbox.OffsetRight = 520;
			vbox.OffsetBottom = 420;
			AddChild(vbox);

			var title = new Label { Text = "L1 素材 Pak 打包/解包 (IDX 加密)" };
			vbox.AddChild(title);

			// img182
			var btnPack = new Button { Text = "打包: img182 → Assets/Img182.pak（單一 .pak 加密）" };
			btnPack.Pressed += OnPackImg182;
			vbox.AddChild(btnPack);

			var btnUnpack = new Button { Text = "解包: Img182.pak → img182_unpacked" };
			btnUnpack.Pressed += OnUnpackImg182;
			vbox.AddChild(btnUnpack);

			// png138
			var btnPng138Pack = new Button { Text = "png138 打包 → Assets/sprites-138-new2.pak" };
			btnPng138Pack.Pressed += OnPackPng138;
			vbox.AddChild(btnPng138Pack);

			var btnPng138Unpack = new Button { Text = "解包: sprites-138-new2.pak → png138out" };
			btnPng138Unpack.Pressed += OnUnpackPng138;
			vbox.AddChild(btnPng138Unpack);

			var hint = new Label { Text = "路徑可在 Tool/PakPackToolScene.cs 修改" };
			vbox.AddChild(hint);

			_statusLabel = new Label();
			_statusLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
			_statusLabel.OffsetRight = 500;
			_statusLabel.OffsetBottom = 200;
			_statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_statusLabel.Text = "操作結果與錯誤會顯示於此。";
			vbox.AddChild(_statusLabel);
		}

		void UpdateStatus()
		{
			if (_statusLabel != null)
				_statusLabel.Text = string.IsNullOrEmpty(PakPackTool.LastMessage) ? "（無訊息）" : PakPackTool.LastMessage;
		}

		void OnPackImg182()
		{
			PakPackTool.Pack(DefaultSourceFolder, DefaultOutputBase, encryptIdx: true, ".img");
			UpdateStatus();
		}

		void OnUnpackImg182()
		{
			PakPackTool.Unpack(DefaultPakPath, DefaultUnpackFolder);
			UpdateStatus();
		}

		void OnPackPng138()
		{
			PakPackTool.PackPng138(Png138TxtPath, Png138PngDir, Png138OutputBase, "sprite_offsets-138_update.txt", encryptIdx: true);
			UpdateStatus();
		}

		void OnUnpackPng138()
		{
			PakPackTool.UnpackPng138(Png138PakPath, Png138UnpackFolder);
			UpdateStatus();
		}
	}
}
