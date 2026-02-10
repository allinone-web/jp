// ============================================================================
// [FILE] Client/UI/Scripts/CombatStatsWindow.cs
// [職責] 戰鬥統計窗口：顯示 DPS、總傷害、命中率、暴擊率等
// ============================================================================

using Godot;
using Client.Game;
using Client;

namespace Client.UI
{
	/// <summary>
	/// 戰鬥統計窗口
	/// </summary>
	public partial class CombatStatsWindow : GameWindow
	{
		// UI 組件引用
		private Label _dpsLabel;
		private Label _totalDamageLabel;
		private Label _hitRateLabel;
		private Label _criticalRateLabel;
		private Label _attacksLabel;
		private Label _hitsLabel;
		private Label _missesLabel;
		private Label _criticalsLabel;
		private Label _blocksLabel;
		private Label _dodgesLabel;
		private Label _damageTakenLabel;
		private Label _healingLabel;
		private Button _resetButton;
		
		private GameWorld _gameWorld;
		private CombatStats _stats;

		public override void _Ready()
		{
			base._Ready();
			ID = WindowID.CombatStats;
			
			// 獲取 GameWorld 引用（使用 Boot.Instance 確保在 HUD 場景修改後仍能正常工作）
			var boot = GetNodeOrNull<Boot>("/root/Boot");
			if (boot != null)
			{
				_gameWorld = boot.GetNodeOrNull<GameWorld>("World");
				if (_gameWorld != null)
				{
					_stats = _gameWorld.CombatStats;
				}
			}
			
			// 自動查找 UI 組件（使用 FindChild）
			_dpsLabel = FindChild("DPSLabel", true, false) as Label;
			_totalDamageLabel = FindChild("TotalDamageLabel", true, false) as Label;
			_hitRateLabel = FindChild("HitRateLabel", true, false) as Label;
			_criticalRateLabel = FindChild("CriticalRateLabel", true, false) as Label;
			_attacksLabel = FindChild("AttacksLabel", true, false) as Label;
			_hitsLabel = FindChild("HitsLabel", true, false) as Label;
			_missesLabel = FindChild("MissesLabel", true, false) as Label;
			_criticalsLabel = FindChild("CriticalsLabel", true, false) as Label;
			_blocksLabel = FindChild("BlocksLabel", true, false) as Label;
			_dodgesLabel = FindChild("DodgesLabel", true, false) as Label;
			_damageTakenLabel = FindChild("DamageTakenLabel", true, false) as Label;
			_healingLabel = FindChild("HealingLabel", true, false) as Label;
			_resetButton = FindChild("ResetButton", true, false) as Button;
			
			if (_resetButton != null)
			{
				_resetButton.Pressed += OnResetButtonPressed;
			}
			
			// 初始更新
			UpdateStats();
		}

		public override void _Process(double delta)
		{
			base._Process(delta);
			
			// 實時更新統計數據（每秒更新一次，避免過於頻繁）
			if (Visible && _stats != null)
			{
				UpdateStats();
			}
		}

		/// <summary>
		/// 更新統計數據顯示
		/// </summary>
		private void UpdateStats()
		{
			if (_stats == null) return;

			// DPS
			if (_dpsLabel != null)
				_dpsLabel.Text = $"DPS: {_stats.DPS:F1}";

			// 總傷害
			if (_totalDamageLabel != null)
				_totalDamageLabel.Text = $"總傷害: {_stats.TotalDamageDealt:N0}";

			// 命中率
			if (_hitRateLabel != null)
				_hitRateLabel.Text = $"命中率: {_stats.HitRate:F1}%";

			// 暴擊率
			if (_criticalRateLabel != null)
				_criticalRateLabel.Text = $"暴擊率: {_stats.CriticalRate:F1}%";

			// 攻擊次數
			if (_attacksLabel != null)
				_attacksLabel.Text = $"攻擊次數: {_stats.TotalAttacks}";

			// 命中
			if (_hitsLabel != null)
				_hitsLabel.Text = $"命中: {_stats.TotalHits}";

			// 未命中
			if (_missesLabel != null)
				_missesLabel.Text = $"未命中: {_stats.TotalMisses}";

			// 暴擊
			if (_criticalsLabel != null)
				_criticalsLabel.Text = $"暴擊: {_stats.TotalCriticals}";

			// 格擋
			if (_blocksLabel != null)
				_blocksLabel.Text = $"格擋: {_stats.TotalBlocks}";

			// 閃避
			if (_dodgesLabel != null)
				_dodgesLabel.Text = $"閃避: {_stats.TotalDodges}";

			// 承受傷害
			if (_damageTakenLabel != null)
				_damageTakenLabel.Text = $"承受傷害: {_stats.TotalDamageTaken:N0}";

			// 治療
			if (_healingLabel != null)
				_healingLabel.Text = $"治療: {_stats.TotalHealing:N0}";
		}

		/// <summary>
		/// 重置統計按鈕點擊事件
		/// </summary>
		private void OnResetButtonPressed()
		{
			if (_stats != null)
			{
				_stats.Reset();
				UpdateStats();
				GD.Print("[CombatStats] 統計數據已重置");
			}
		}
	}
}
