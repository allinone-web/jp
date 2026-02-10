// ============================================================================
// [FILE] Client/UI/Scripts/SkillCooldownDisplay.cs
// [職責] 技能冷卻顯示：在屏幕右下角顯示技能冷卻倒計時（5, 4, 3, 2, 1）
// ============================================================================

using Godot;
using System.Collections.Generic;
using Client.Game;

namespace Client.UI
{
	/// <summary>
	/// 技能冷卻顯示組件：在屏幕右下角動態顯示技能冷卻倒計時
	/// </summary>
	public partial class SkillCooldownDisplay : Control
	{
		// UI 組件
		private Label _cooldownLabel;
		
		// 當前顯示的技能ID（0表示無技能在冷卻）
		private int _currentSkillId = 0;
		
		// 最小顯示時間（毫秒），小於此值不顯示
		private const long MIN_DISPLAY_TIME_MS = 1;

		public override void _Ready()
		{
			ProcessMode = ProcessModeEnum.Always;
			SetProcess(true);
			// 創建冷卻標籤（如果場景中沒有）
			_cooldownLabel = GetNodeOrNull<Label>("CooldownLabel");
			if (_cooldownLabel == null)
			{
				_cooldownLabel = new Label();
				_cooldownLabel.Name = "CooldownLabel";
				_cooldownLabel.HorizontalAlignment = HorizontalAlignment.Center;
				_cooldownLabel.VerticalAlignment = VerticalAlignment.Center;
				_cooldownLabel.AddThemeFontSizeOverride("font_size", 48); // 大字體
				_cooldownLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0f, 1f)); // 金黃色
				_cooldownLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 1f)); // 黑色描邊
				_cooldownLabel.AddThemeConstantOverride("outline_size", 4); // 描邊大小
				_cooldownLabel.Visible = false;
				AddChild(_cooldownLabel);
			}
			
			// 設置錨點到右下角
			SetAnchorsPreset(Control.LayoutPreset.BottomRight);
			OffsetLeft = -100;
			OffsetTop = -100;
			OffsetRight = 0;
			OffsetBottom = 0;
			
			// 確保標籤填滿整個控件
			if (_cooldownLabel != null)
			{
				_cooldownLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
				_cooldownLabel.OffsetLeft = 0;
				_cooldownLabel.OffsetTop = 0;
				_cooldownLabel.OffsetRight = 0;
				_cooldownLabel.OffsetBottom = 0;
			}
		}

		public override void _Process(double delta)
		{
			// 查找當前正在冷卻的技能（優先顯示最短的冷卻時間）
			int bestSkillId = 0;
			long bestRemainingMs = 0;
			
			// 遍歷所有可能的技能ID（1-200，根據實際情況調整）
			for (int skillId = 1; skillId <= 200; skillId++)
			{
				long remainingMs = SkillCooldownManager.GetRemainingCooldownMs(skillId);
				if (remainingMs > 0 && remainingMs >= MIN_DISPLAY_TIME_MS)
				{
					// 選擇最短的冷卻時間（最緊急的）
					if (bestSkillId == 0 || remainingMs < bestRemainingMs)
					{
						bestSkillId = skillId;
						bestRemainingMs = remainingMs;
					}
				}
			}
			
			// 更新顯示
			if (bestSkillId > 0 && bestRemainingMs >= MIN_DISPLAY_TIME_MS)
			{
				_currentSkillId = bestSkillId;
				
				// 顯示倒計時（毫秒）
				long displayMs = bestRemainingMs;
				if (_cooldownLabel != null)
				{
					_cooldownLabel.Text = $"{displayMs}ms";
					_cooldownLabel.Visible = true;
					
					// 根據剩餘時間調整顏色（緊急時變紅）
					if (displayMs <= 500)
						_cooldownLabel.AddThemeColorOverride("font_color", new Color(1f, 0.2f, 0.2f, 1f)); // 紅色
					else if (displayMs <= 1000)
						_cooldownLabel.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0f, 1f)); // 橙色
					else
						_cooldownLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0f, 1f)); // 金黃色
				}
			}
			else
			{
				// 沒有技能在冷卻，隱藏顯示
				_currentSkillId = 0;
				if (_cooldownLabel != null)
					_cooldownLabel.Visible = false;
			}
		}
	}
}
