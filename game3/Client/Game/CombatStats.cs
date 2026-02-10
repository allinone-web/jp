// ============================================================================
// [FILE] Client/Game/CombatStats.cs
// [職責] 戰鬥統計系統：DPS、總傷害、命中率、暴擊率等
// ============================================================================

using Godot;
using System;
using System.Collections.Generic;

namespace Client.Game
{
	/// <summary>
	/// 戰鬥統計數據
	/// </summary>
	public class CombatStats
	{
		// 傷害統計
		public long TotalDamageDealt { get; private set; } = 0;
		public long TotalDamageTaken { get; private set; } = 0;
		public long TotalHealing { get; private set; } = 0;
		
		// 攻擊統計
		public int TotalAttacks { get; private set; } = 0;
		public int TotalHits { get; private set; } = 0;
		public int TotalMisses { get; private set; } = 0;
		public int TotalCriticals { get; private set; } = 0;
		public int TotalBlocks { get; private set; } = 0;
		public int TotalDodges { get; private set; } = 0;
		
		// 時間統計
		private ulong _combatStartTime = 0;
		private ulong _lastDamageTime = 0;
		
		/// <summary>命中率（百分比）</summary>
		public float HitRate => TotalAttacks > 0 ? (TotalHits / (float)TotalAttacks) * 100f : 0f;
		
		/// <summary>暴擊率（百分比）</summary>
		public float CriticalRate => TotalHits > 0 ? (TotalCriticals / (float)TotalHits) * 100f : 0f;
		
		/// <summary>DPS（每秒傷害）</summary>
		public float DPS
		{
			get
			{
				if (_combatStartTime == 0) return 0f;
				ulong currentTime = Time.GetTicksMsec();
				ulong elapsed = currentTime - _combatStartTime;
				if (elapsed <= 0) return 0f;
				return (TotalDamageDealt / (float)elapsed) * 1000f;
			}
		}
		
		/// <summary>開始戰鬥統計</summary>
		public void StartCombat()
		{
			if (_combatStartTime == 0)
				_combatStartTime = Time.GetTicksMsec();
		}
		
		/// <summary>記錄傷害（造成）</summary>
		public void RecordDamageDealt(int damage, bool isCritical = false, bool isMiss = false, bool isBlock = false, bool isDodge = false)
		{
			TotalAttacks++;
			_lastDamageTime = Time.GetTicksMsec();
			
			if (isMiss)
			{
				TotalMisses++;
			}
			else if (isDodge)
			{
				TotalDodges++;
			}
			else if (isBlock)
			{
				TotalBlocks++;
			}
			else if (damage > 0)
			{
				TotalHits++;
				TotalDamageDealt += damage;
				if (isCritical)
				{
					TotalCriticals++;
				}
			}
		}
		
		/// <summary>記錄傷害（承受）</summary>
		public void RecordDamageTaken(int damage)
		{
			TotalDamageTaken += damage;
		}
		
		/// <summary>記錄治療</summary>
		public void RecordHealing(int amount)
		{
			TotalHealing += amount;
		}
		
		/// <summary>重置統計</summary>
		public void Reset()
		{
			TotalDamageDealt = 0;
			TotalDamageTaken = 0;
			TotalHealing = 0;
			TotalAttacks = 0;
			TotalHits = 0;
			TotalMisses = 0;
			TotalCriticals = 0;
			TotalBlocks = 0;
			TotalDodges = 0;
			_combatStartTime = 0;
			_lastDamageTime = 0;
		}
		
		/// <summary>獲取統計摘要（用於顯示）</summary>
		public string GetSummary()
		{
			return $"DPS: {DPS:F1}\n" +
			       $"總傷害: {TotalDamageDealt}\n" +
			       $"命中率: {HitRate:F1}%\n" +
			       $"暴擊率: {CriticalRate:F1}%\n" +
			       $"攻擊次數: {TotalAttacks}\n" +
			       $"命中: {TotalHits} | 未命中: {TotalMisses} | 暴擊: {TotalCriticals}";
		}
	}
}
