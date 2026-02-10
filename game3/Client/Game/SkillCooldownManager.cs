// ============================================================================
// [FILE] Client/Game/SkillCooldownManager.cs
// [職責] 技能冷卻管理器：追蹤技能使用時間和冷卻倒計時
// ============================================================================

using Godot;
using System;
using System.Collections.Generic;
using Client.Data;

namespace Client.Game
{
	/// <summary>
	/// 技能冷卻管理器：追蹤每個技能的使用時間和冷卻時間
	/// </summary>
	public static class SkillCooldownManager
	{
		// 技能使用時間戳記錄（skillId -> 使用時間戳（毫秒））
		private static Dictionary<int, long> _skillUseTimestamps = new Dictionary<int, long>();
		
		// 技能冷卻時間緩存（skillId -> reuse_delay（毫秒））
		private static Dictionary<int, int> _skillCooldowns = new Dictionary<int, int>();

		/// <summary>
		/// 記錄技能使用（在 UseMagic 中調用）
		/// </summary>
		public static void RecordSkillUse(int skillId, int cooldownMsOverride = -1)
		{
			if (skillId <= 0) return;
			
			long currentTime = (long)Time.GetTicksMsec();
			_skillUseTimestamps[skillId] = currentTime;
			
			// 確保技能冷卻時間已載入
			EnsureSkillCooldownLoaded(skillId);
			
			// 若提供覆寫值，優先使用（用於與實際施法動作間隔對齊）
			if (cooldownMsOverride >= 0)
			{
				_skillCooldowns[skillId] = cooldownMsOverride;
			}
		}

		/// <summary>
		/// 獲取技能剩餘冷卻時間（秒）
		/// </summary>
		/// <param name="skillId">技能ID</param>
		/// <returns>剩餘冷卻時間（秒），如果技能未使用或冷卻已完成則返回 0</returns>
		public static float GetRemainingCooldown(int skillId)
		{
			long remainingMs = GetRemainingCooldownMs(skillId);
			if (remainingMs <= 0) return 0f;
			// 轉換為秒（向上取整，用於顯示）
			return Mathf.Ceil((float)remainingMs / 1000f);
		}

		/// <summary>
		/// 檢查技能是否在冷卻中
		/// </summary>
		public static bool IsOnCooldown(int skillId)
		{
			return GetRemainingCooldownMs(skillId) > 0;
		}

		/// <summary>
		/// 確保技能冷卻時間已載入（從 skill_list.csv 讀取 reuse_delay）
		/// </summary>
		private static void EnsureSkillCooldownLoaded(int skillId)
		{
			// 如果已經載入，直接返回
			if (_skillCooldowns.ContainsKey(skillId))
				return;

			// 【JP協議對齊】從 server skills.csv 讀取 reuse_delay（單位：毫秒）
			// 服務器 L1SkillUse.setDelay() 直接使用 reuse_delay 作為毫秒
			int reuseDelay = SkillDbData.GetReuseDelay(skillId);
			_skillCooldowns[skillId] = reuseDelay;
		}

		/// <summary>
		/// 設置技能冷卻時間（從服務器封包獲取時調用）
		/// </summary>
		public static void SetSkillCooldown(int skillId, int cooldownMs)
		{
			if (skillId <= 0 || cooldownMs < 0) return;
			_skillCooldowns[skillId] = cooldownMs;
		}

		/// <summary>
		/// 重置所有技能冷卻
		/// </summary>
		public static void Reset()
		{
			_skillUseTimestamps.Clear();
		}

		/// <summary>
		/// 重置指定技能的冷卻
		/// </summary>
		public static void ResetSkill(int skillId)
		{
			if (_skillUseTimestamps.ContainsKey(skillId))
				_skillUseTimestamps.Remove(skillId);
		}

		public static long GetRemainingCooldownMs(int skillId)
		{
			if (skillId <= 0) return 0;
			
			// 檢查是否使用過該技能
			if (!_skillUseTimestamps.TryGetValue(skillId, out long useTime))
				return 0;
			
			// 確保技能冷卻時間已載入
			EnsureSkillCooldownLoaded(skillId);
			
			// 獲取技能冷卻時間（毫秒）
			if (!_skillCooldowns.TryGetValue(skillId, out int cooldownMs))
				return 0;
			
			// 計算剩餘時間
			long currentTime = (long)Time.GetTicksMsec();
			long elapsed = currentTime - useTime;
			long remaining = cooldownMs - elapsed;
			return Math.Max(0, remaining);
		}
	}
}
