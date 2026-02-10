using Godot;
using Client.Game;
using System.Linq;

namespace Client.UI
{
	/// <summary>
	/// 寵物面板窗口：顯示寵物血量、藍量、等級等信息
	/// 對齊服務器 S_ObjectPet (Opcode 42, "anicom" 或 "moncom")
	/// 與法師召喚物面板相同
	/// </summary>
	public partial class PetPanelWindow : GameWindow
	{
		private Label _nameLabel;
		private Label _levelLabel;
		private ProgressBar _hpBar;
		private Label _hpLabel;
		private ProgressBar _mpBar;
		private Label _mpLabel;
		private Label _statusLabel;
		private Label _foodLabel; // 寵物專用：食物狀態
		private Label _expLabel;  // 寵物專用：經驗百分比
		private Label _lawfulLabel; // 寵物專用：正義值

		public override void _Ready()
		{
			base._Ready();
			
			// 自動查找 UI 組件
			_nameLabel = FindChild("NameLabel", true, false) as Label;
			_levelLabel = FindChild("LevelLabel", true, false) as Label;
			_hpBar = FindChild("HPBar", true, false) as ProgressBar;
			_hpLabel = FindChild("HPLabel", true, false) as Label;
			_mpBar = FindChild("MPBar", true, false) as ProgressBar;
			_mpLabel = FindChild("MPLabel", true, false) as Label;
			_statusLabel = FindChild("StatusLabel", true, false) as Label;
			_foodLabel = FindChild("FoodLabel", true, false) as Label;
			_expLabel = FindChild("ExpLabel", true, false) as Label;
			_lawfulLabel = FindChild("LawfulLabel", true, false) as Label;
		}

		public override void OnOpen(WindowContext context = null)
		{
			base.OnOpen(context);
			if (context == null || context.ExtraData == null) return;
			
			// 解析寵物數據
			if (context.ExtraData is Godot.Collections.Dictionary petData)
			{
				UpdatePetData(petData);
			}
		}

		/// <summary>更新寵物數據顯示</summary>
		private void UpdatePetData(Godot.Collections.Dictionary petData)
		{
			string panelType = petData.ContainsKey("panelType") ? (string)petData["panelType"] : "anicom";
			bool isPet = (panelType == "anicom");
			
			// 基本信息
			if (_nameLabel != null)
				_nameLabel.Text = petData.ContainsKey("name") ? (string)petData["name"] : "Unknown";
			
			if (_levelLabel != null)
				_levelLabel.Text = $"Lv. {(petData.ContainsKey("level") ? (string)petData["level"] : "0")}";
			
			// 狀態
			if (_statusLabel != null)
			{
				string status = petData.ContainsKey("status") ? (string)petData["status"] : "";
				_statusLabel.Text = status;
			}
			
			// HP
			int currentHp = 0, totalHp = 0;
			if (petData.ContainsKey("currentHp") && int.TryParse((string)petData["currentHp"], out currentHp) &&
			    petData.ContainsKey("totalHp") && int.TryParse((string)petData["totalHp"], out totalHp))
			{
				if (_hpBar != null)
				{
					_hpBar.MaxValue = totalHp;
					_hpBar.Value = currentHp;
				}
				if (_hpLabel != null)
					_hpLabel.Text = $"{currentHp} / {totalHp}";
			}
			
			// MP
			int currentMp = 0, totalMp = 0;
			if (petData.ContainsKey("currentMp") && int.TryParse((string)petData["currentMp"], out currentMp) &&
			    petData.ContainsKey("totalMp") && int.TryParse((string)petData["totalMp"], out totalMp))
			{
				if (_mpBar != null)
				{
					_mpBar.MaxValue = totalMp;
					_mpBar.Value = currentMp;
				}
				if (_mpLabel != null)
					_mpLabel.Text = $"{currentMp} / {totalMp}";
			}
			
			// 寵物專用字段
			if (isPet)
			{
				if (_foodLabel != null)
					_foodLabel.Text = petData.ContainsKey("foodStatus") ? (string)petData["foodStatus"] : "";
				
				if (_expLabel != null)
					_expLabel.Text = $"Exp: {(petData.ContainsKey("expPercentage") ? (string)petData["expPercentage"] : "0")}%";
				
				if (_lawfulLabel != null)
					_lawfulLabel.Text = $"Lawful: {(petData.ContainsKey("lawful") ? (string)petData["lawful"] : "0")}";
			}
			else
			{
				// 召喚物：隱藏寵物專用字段
				if (_foodLabel != null) _foodLabel.Visible = false;
				if (_expLabel != null) _expLabel.Visible = false;
				if (_lawfulLabel != null) _lawfulLabel.Visible = false;
			}
		}

		/// <summary>
		/// 供外部調用，更新寵物狀態（例如攻擊/跟隨）
		/// </summary>
		/// <param name="status">狀態值（0=休息, 1=攻擊, 2=跟隨, 3=防禦, 4=停留, 5=警戒）</param>
		public void UpdatePetStatus(int status)
		{
			// TODO: 根據 status 更新寵物面板上的圖標或文字，表示其當前行為模式
			GD.Print($"[PetPanel] 更新寵物狀態為: {status}");
		}
	}
}
