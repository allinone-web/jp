using Godot;
using System;
using Client.Data;

namespace Client.Game
{
	// =========================================================================
	// [FILE] GameWorld.CharacterSync.cs
	// 说明：角色属性/状态同步。负责 HUD 更新 + Boot.MyCharInfo 同步 + 窗口刷新。
	// =========================================================================
	public partial class GameWorld
	{
		// =====================================================================
		// [SECTION] Stats Updates (HP/MP & Boot 同步)
		// 说明：HP/MP 更新时必须：
		// - 更新 HUD
		// - 同步写入 Boot.MyCharInfo，避免面板显示旧数据
		// - 对主角实体同步血条比例
		// =====================================================================
		private void OnHPUpdated(int current, int max)
		{
			_hud?.UpdateHP(current, max);
			UpdateBootStats(c => { c.CurrentHP = current; c.MaxHP = max; });

			// 更新主角頭頂血條（比例 + 可見性：受傷時顯示）。僅 102.type(5)/(10) 顯示血條
			if (_myPlayer != null && max > 0)
			{
				int ratio = (int)((float)current / max * 100);
				_myPlayer.SetHpRatio(ratio);
				_myPlayer.SetHealthBarVisible(_myPlayer.ShouldShowHealthBar() && ratio < 100);
			}

			// Bugfix：死亡 (HP=0) 时强制 MP 显示归零
			if (current == 0)
			{
				_hud?.UpdateMP(0, 0);
				// 【死亡動畫】主角死亡時播放死亡動作，並停止自動行為
				if (_myPlayer != null)
				{
					_myPlayer.SetAction(GameEntity.ACT_DEATH);
					StopAutoActions();
					StopWalking();
				}
				
				// 【死亡狀態】若尚未標記死亡，立即鎖定死亡狀態並啟動復活計時
				if (!_isPlayerDead)
				{
					_isPlayerDead = true;
					_resurrectionTimer = RESURRECTION_TIME;
					ShowDeathDialogIfNeeded();
				}
			}
		}

		private void OnMPUpdated(int current, int max)
		{
			_hud?.UpdateMP(current, max);
			UpdateBootStats(c => { c.CurrentMP = current; c.MaxMP = max; });
		}
		// =====================================================================
		// [SECTION END] Stats Updates
		// =====================================================================


		// =====================================================================
		// [SECTION] Character Data Sync (Opcode 5 / Opcode 12 分流)
		// 说明：
		// - Opcode 5：基础信息（含 Name）
		// - Opcode 12：状态更新（通常不含 Name，不能覆盖 Name）
		// =====================================================================
		private void OnCharacterInfoReceived(CharacterInfo newData)
		{
			UpdateBootStats(c => {
				c.Name = newData.Name; // 这里有名字
				c.ClanName = newData.ClanName;
				c.Type = newData.Type;
				c.Sex = newData.Sex;
				c.Lawful = newData.Lawful;
				c.Str = newData.Str; c.Dex = newData.Dex; c.Con = newData.Con;
				c.Wis = newData.Wis; c.Cha = newData.Cha; c.Int = newData.Int;
				c.Level = newData.Level; c.Ac = newData.Ac;
			});
		}

		private void OnCharacterStatsUpdated(CharacterInfo newData)
		{
			UpdateBootStats(c => {
				// 注意：这里绝不覆盖 Name
				c.Str = newData.Str; c.Dex = newData.Dex; c.Con = newData.Con;
				c.Wis = newData.Wis; c.Cha = newData.Cha; c.Int = newData.Int;
				c.Level = newData.Level; c.Ac = newData.Ac;
				c.CurrentHP = newData.CurrentHP; c.MaxHP = newData.MaxHP;
				c.CurrentMP = newData.CurrentMP; c.MaxMP = newData.MaxMP;
			});
		}
		// =====================================================================
		// [SECTION END] Character Data Sync
		// =====================================================================


		// =====================================================================
		// [SECTION] Boot Bridge: UpdateBootStats (统一写入入口)
		// 说明：
		// - 统一对 Boot.MyCharInfo 写入
		// - 写入后刷新窗口（角色/背包/技能）保证 UI 立即同步
		// =====================================================================
		private void UpdateBootStats(Action<CharacterInfo> updateAction)
		{
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			if (boot != null && boot.MyCharInfo != null)
			{
				updateAction(boot.MyCharInfo);
				RefreshWindows();
			}
		}
		
		
		
		// =====================================================================
		// [SECTION END] Boot Bridge: UpdateBootStats
		// =====================================================================
	}
}
