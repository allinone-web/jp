// ============================================================================
// [FILE] GameWorld.Combat.cs (UPGRADED VERSION)
//
// [升级内容]
// ✅ 集成任务队列架构（保留你现有的 AutoTask / TaskQueue）
// ✅ _hasArrivedThisFrame 僅用於攻擊：等本幀送完移動再送攻擊；拾取/對話依 dist≤1 即執行
// ✅ 改进状态机（Idle → Chasing → Arrived → Executing）
// ✅ 统一处理攻击 / NPC / 拾取 
// ✅ 弓箭最小距离防抖
// ✅ 弓箭预判射击
// ✅ 完整的包发送与动画系统
//
// [修复内容]
// ✅ 修正 GetHeading 调用：传入 _myPlayer.Heading 作为 defaultHeading
//    解决 "原地攻击/停止移动时朝向强制重置为 North(0)" 的 Bug

// ============================================================================

using Godot;
using System;
using Client.Data;
using System.Collections.Generic;
using Client.Network;
using Client.Data;
using Client.Utility;
using System.Linq;

namespace Client.Game
{
	// GameWorld 的战斗分部类
	public partial class GameWorld
	{
		// ============================================================
		// [新架构] Task / State / Profile 定义
		// ============================================================
		private enum AutoTaskType { Attack, PickUp, TalkNpc }
		private enum AutoTaskPriority { Normal, Forced }
		private enum AutoCombatState { Idle, Chasing, Arrived, Executing }

		private class AutoTask
		{
			public AutoTaskType Type;
			public GameEntity Target;
			public AutoTaskPriority Priority;
			public int? SkillId; // 可選的技能 ID，用於快捷鍵自動攻擊（攻擊類技能或武器）
			public AutoTask(AutoTaskType type, GameEntity target, AutoTaskPriority priority = AutoTaskPriority.Normal, int? skillId = null)
			{
				Type = type;
				Target = target;
				Priority = priority;
				SkillId = skillId;
			}
		}

		private struct CombatProfile
		{
			public float AttackCooldown;      // 攻击间隔
			public bool CanAttackWhileMoving; // 是否允许移动攻击 (弓)
			public int MinAdvancePerAttack;   // 最小推进距离 (防抖)
			public bool UsePredictShot;       // 是否预判射击
		}


		// [核心修復] 嚴格對齊伺服器 CheckSpeed.java 的計時器
		// 【優化】移除 _attackCooldownTimer，統一使用 EnhancedSpeedManager 進行冷卻檢查
		private bool _attackInProgress = false; // 標記攻擊動畫是否正在播放
		private float _smartRetryTimer = 0f; // 【現代遊戲特性】智能重試計時器，根據剩餘冷卻時間動態調整
		private const long MAGIC_BLOCKED_LOG_INTERVAL_MS = 1000;
		private long _lastMagicBlockedLogTime = 0;
		private int _lastMagicBlockedLogSkillId = 0;

		// ============================================================
		// [核心字段] 任务队列与状态
		// ============================================================
		private readonly LinkedList<AutoTask> _taskQueue = new();
		private AutoTask _currentTask;
		private AutoCombatState _combatState = AutoCombatState.Idle;
		
		// 【新增】戰鬥統計系統
		private CombatStats _combatStats = new CombatStats();
		public CombatStats CombatStats => _combatStats;

		
		// 弓箭防抖记录
		private int _lastAttackDist = int.MaxValue;
		private long _lastAttackSentTime = 0;
		private bool _pickupInProgress = false;
		private long _lastPickupSentTime = 0;

		// 法師 Z 自動攻擊使用的技能 ID（預設 4 光箭）；快捷鍵 1-8 可替換
		private int _mageAutoAttackSkillId = 4;

		// 【座標同步修復】服務器確認的玩家座標（從 S_ObjectMoving 接收）
		// 攻擊時使用此座標，確保與服務器一致
		private int _serverConfirmedPlayerX = -1;
		private int _serverConfirmedPlayerY = -1;
		
		// 【座標同步偵探】最後一次收到服務器移動確認的時間戳（僅玩家自己）
		private long _lastServerMoveConfirmTime = 0;
		
		// 【座標同步偵探】客戶端預測的玩家座標（僅用於日誌與差異檢測）
		private int _clientPredictedPlayerX = -1;
		private int _clientPredictedPlayerY = -1;
		
		// 【座標同步偵探】保存最近一次攻擊包中服務器認為的玩家座標（從 Op35 攻擊包中獲取）
		// Key: attackerId, Value: (targetX, targetY) - 服務器認為的目標座標
		private Dictionary<int, (int x, int y)> _lastAttackServerPlayerPos = new();
		
		// 【刪除】定期位置更新機制已刪除
		// 如果正在移動，移動包已經包含位置信息，不需要定期更新
		// 如果沒有移動，也不需要定期更新（服務器會從其他包中獲取位置信息）
		// 保留 _lastPositionUpdateTime 用於魔法時的位置更新（避免過於頻繁）
		private long _lastPositionUpdateTime = 0;
		private const long MIN_POSITION_UPDATE_INTERVAL_MS = 640; // 最小發送間隔640ms（僅用於魔法時的位置更新）
		
		// [注意] _hasArrivedThisFrame 已在 GameWorld.Movement.cs 中定义
		
		// =====================================================================
		// 【統一位置更新函數】從服務器包中更新實體位置
		// 核心原則：
		// 1. 所有實體（玩家、怪物、其他玩家）都要從服務器包中更新位置
		// 2. 如果差距 > 2格且是玩家自己，發送位置更新包給服務器
		// 3. 服務器是權威的，客戶端應該無條件信任服務器座標
		// =====================================================================
		/// <summary>
		/// 統一的位置更新函數：從服務器包中更新實體位置
		/// </summary>
		/// <param name="objectId">實體ID</param>
		/// <summary>
		/// 【服務器對齊】更新實體位置（對齊服務器 S_ObjectMoving 邏輯）
		/// 服務器只有在實體真正移動時才發送 S_ObjectMoving（L1Object.setMove(true)）
		/// 服務器在實體停止移動時會設置 setMove(false)，不會發送移動包
		/// 客戶端應該只在位置真正改變時設置 walk 動作，位置未改變時設置待機動作
		/// </summary>
		/// <param name="objectId">實體ObjectId</param>
		/// <param name="serverX">服務器確認的X座標</param>
		/// <param name="serverY">服務器確認的Y座標</param>
		/// <param name="heading">朝向（如果為-1則不更新朝向）</param>
		/// <param name="source">來源描述（用於日誌）</param>
		private void UpdateEntityPositionFromServer(int objectId, int serverX, int serverY, int heading, string source)
		{
			if (objectId <= 0 || !_entities.TryGetValue(objectId, out var entity))
				return;
			
			int currentX = entity.MapX;
			int currentY = entity.MapY;
			int diffX = Math.Abs(serverX - currentX);
			int diffY = Math.Abs(serverY - currentY);
			int diff = Math.Max(diffX, diffY);
			
			// 【服務器對齊】對齊服務器邏輯：只有當位置真正改變時才更新
			// 服務器 S_ObjectMoving 只有在實體真正移動時才發送（至少移動1格）
			// 如果位置未改變（diff == 0），說明實體停止移動，應該設置待機動作
			if (diff == 0)
			{
				// 【核心修復】位置未改變，設置待機動作（對齊服務器 setMove(false)）
				// 這解決了「站著不動但播放 walk 動畫」的問題
				entity.SetAction(GameEntity.ACT_BREATH);
				return; // 位置一致，無需更新
			}
			
			// 【簡化邏輯】如果是玩家自己，簡單處理：差距 <= 1格時更新，否則保持客戶端預測
			if (objectId == _myPlayer?.ObjectId)
			{
				// 只有當差距 <= 1格時才更新位置和服務器確認座標（服務器確認了客戶端的移動）
				if (diff <= 1)
				{
					// 服務器確認了客戶端的移動，更新位置和服務器確認座標
					_serverConfirmedPlayerX = serverX;
					_serverConfirmedPlayerY = serverY;
					int confirmedHeading = heading >= 0 ? heading : entity.Heading;
					entity.SetMapPosition(serverX, serverY, confirmedHeading);
					GD.Print($"[Pos-Sync] Server confirmed player move: ({serverX},{serverY}) client was at ({currentX},{currentY}) diff={diff}");
				}
				else
				{
					// 差距 > 1格時，保持客戶端預測，不更新位置
					// 但也不更新服務器確認座標為舊值，保持客戶端預測的座標
					GD.Print($"[Pos-Sync-Fix] Server confirmed too old (diff={diff} > 1), keeping client prediction. Server:({serverX},{serverY}) Client:({currentX},{currentY})");
				}
				return;
			}
			
			// 不是玩家自己（怪物或其他玩家），正常更新位置
			string entityType = objectId == _myPlayer?.ObjectId ? "player" : "entity";
			GD.Print($"[Pos-Sync] {source}: Updating {entityType} {objectId} position: server-confirmed ({serverX},{serverY}) client-current ({currentX},{currentY}) diff={diff}");
			
			// 更新實體位置
			int finalHeading = heading >= 0 ? heading : entity.Heading;
			entity.SetMapPosition(serverX, serverY, finalHeading);
		}
		
		// 【刪除】UpdatePositionSync 方法已刪除
		// 如果正在移動，移動包已經包含位置信息，不需要定期更新
		
		// 【修復】位置更新函數：如果正在移動，跳過更新（移動包已經包含位置信息）
		// 僅在特殊情況下使用（如魔法時的位置更新、停止移動時的位置同步）
		private void SendPositionUpdateToServer(string reason, bool force = false)
		{
		    if (_myPlayer == null) return;
		    
		    // 【關鍵修復】如果正在移動，跳過位置更新（移動包已經包含位置信息）
		    if (_isAutoWalking && !force)
		    {
		        GD.Print($"[Pos-Update-Skip] Skipping position update ({reason}): player is moving, move packet already contains position");
		        return;
		    }
		    
		    long currentTime = (long)Time.GetTicksMsec();
		    
		    // 檢查最小發送間隔，但如果是強制發送 (force=true) 則忽略檢查
		    long timeSinceLastUpdate = 0;
		    if (_lastPositionUpdateTime > 0)
		    {
		        timeSinceLastUpdate = currentTime - _lastPositionUpdateTime;
		        if (!force && timeSinceLastUpdate < MIN_POSITION_UPDATE_INTERVAL_MS)
		        {
		            GD.Print($"[Pos-Update-Skip] Skipping position update ({reason}): interval={timeSinceLastUpdate}ms < {MIN_POSITION_UPDATE_INTERVAL_MS}ms");
		            return; // 間隔太短且非強制，跳過
		        }
		    }
		    
		    int clientX = _myPlayer.MapX;
		    int clientY = _myPlayer.MapY;
		    int playerHeading = _myPlayer.Heading;
		    
		    // 【座標同步偵探】發送位置更新包前，增加封包ID並記錄日誌
		    _globalMovePacketId++;
		    string packetType = force ? "Force" : "Normal";
		    GD.Print($"[Move-Audit] Sent ID:#{_globalMovePacketId} Pos:({clientX},{clientY}) Head:{playerHeading} Type:{packetType} Reason:{reason}");
		    
		    // 【關鍵日誌】記錄位置更新包發送，用於驗證服務器是否收到玩家新座標
		    string forceTag = force ? "[FORCE] " : "";
		    GD.Print($"[Pos-Update] {forceTag}Sending position update ({reason}): Client:({clientX},{clientY}) heading={playerHeading} ServerConfirmed:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY}) TimeSinceLastUpdate={timeSinceLastUpdate}ms");
		    
		    // 發送位置更新包
		    _netSession.Send(C_MoveCharPacket.Make(clientX, clientY, playerHeading));
		    _lastPositionUpdateTime = currentTime;
		    
		    // 【關鍵驗證】如果距離上次更新時間 > 640ms * 2，說明可能丟包
		    if (timeSinceLastUpdate > MIN_POSITION_UPDATE_INTERVAL_MS * 2)
		    {
		        GD.PrintErr($"[Pos-Update-Error] Position update interval too long! Expected: {MIN_POSITION_UPDATE_INTERVAL_MS}ms Actual: {timeSinceLastUpdate}ms (可能丟包，服務器可能不知道玩家最新座標)");
		        GD.PrintErr($"[Pos-Update-Error] 這說明玩家座標沒有及時傳給服務器，服務器可能使用舊座標計算怪物攻擊和追擊");
		    }
		}


		
		// 此处直接使用，无需重复声明。
		// [注意] LOGIC_RANGE_CELLS 和 VISUAL_RANGE_CELLS 已在 GameWorld.Entities.cs 中定义
		// 此处直接使用，无需重复声明。





		// ============================================================
		// [任务系统] 队列管理 (新核心)
		// ============================================================
		private void EnqueueTask(AutoTask task)
		{
			if (task.Priority == AutoTaskPriority.Forced)
			{
				_taskQueue.Clear();
				_currentTask = null;
				_combatState = AutoCombatState.Idle;
				StopWalking(); // 强制打断移动
				_taskQueue.AddFirst(task);
				GD.Print($"[Task] Forced Task: {task.Type}");
			}
			else
			{
				_taskQueue.AddLast(task);
			}
		}

		private AutoTask GetCurrentTask()
		{
			if (_currentTask == null && _taskQueue.Count > 0)
			{
				_currentTask = _taskQueue.First.Value;
				_taskQueue.RemoveFirst();
				// 切换任务时重置状态
				_combatState = AutoCombatState.Idle;
				_lastAttackDist = int.MaxValue;
				GD.Print($"[Task] Start: {_currentTask.Type} -> {_currentTask.Target?.RealName}");
			}
			return _currentTask;
		}

		private void FinishCurrentTask()
		{
			_currentTask = null;
			_combatState = AutoCombatState.Idle;
			_lastAttackDist = int.MaxValue;
		}

		// [合并] 停止所有自动行为 (清理队列 + 重置标志位)
		public void StopAutoActions()
		{
			_taskQueue.Clear();
			_currentTask = null;
			_combatState = AutoCombatState.Idle;
			
			// 兼容旧标志位
			_isAutoAttacking = false;
			_isAutoPickup = false;
			_autoTarget = null;
			
			StopWalking();

			GD.Print("[Combat] StopAutoActions called");
		}

		// ============================================================
	// [主循环] UpdateCombatLogic (状态机驱动)
	// [修復] 戰鬥封包頻率控制
	// ============================================================
	private void UpdateCombatLogic(double delta)
		{
			var task = GetCurrentTask();
			if (task == null || _myPlayer == null) return;

			// 任務有效性：目標存在、未死亡（依 IsDead 判定，正確的死亡判斷是 _currentRawAction == ACT_DEATH）
			var target = task.Target;
			if (target == null || !_entities.ContainsKey(target.ObjectId) ||
				(task.Type == AutoTaskType.Attack && target.IsDead))
			{
				FinishCurrentTask();
				return;
			}

			// 状态机分发（使用 task.Target，不再依賴 _autoTarget）
			switch (task.Type)
			{
				case AutoTaskType.Attack:
					ExecuteAttackTask(delta);
					break;
				case AutoTaskType.PickUp:
					ExecutePickupTask();
					break;
				case AutoTaskType.TalkNpc:
					ExecuteTalkNpcTask();
					break;
			}
		}

		// ============================================================
		// [执行单元] Attack Task (攻击任务 - 改进版)
		// ============================================================
		private void ExecuteAttackTask(double delta)
		{
			var target = _currentTask.Target;
			var profile = GetCombatProfile();
			int dist = GetGridDistance(_myPlayer.MapX, _myPlayer.MapY, target.MapX, target.MapY);
			int range = GetAttackRange();

			switch (_combatState)
			{
				case AutoCombatState.Idle:
					// 【關鍵修復】必須走到攻擊範圍內才能攻擊，不應該在距離2格時就攻擊
					// 對於近戰（range=1），必須距離<=1格才能攻擊
					// 對於遠程（range=6），必須距離<=range才能攻擊，但為了確保服務器距離檢查通過，應該先走到距離<=range-1格
					// 服務器距離檢查使用的是玩家在服務器端的座標，如果客戶端預測與服務器不一致，距離檢查會失敗
					if (dist <= range)
					{
						// 如果距離剛好在範圍邊緣（dist == range），先走到更近的位置再攻擊
						// 這可以避免服務器座標不一致導致的距離檢查失敗
						if (dist == range && range > 1)
						{
							_combatState = AutoCombatState.Chasing;
							GD.Print($"[Combat] State: Idle → Chasing (dist={dist} == range={range}, need to get closer)");
						}
						else
						{
							_combatState = AutoCombatState.Executing;
							GD.Print($"[Combat] State: Idle → Executing (in range dist={dist} range={range}, attack immediately)");
							HandleExecutingState(delta, profile, dist, range);
						}
					}
					else
					{
						_combatState = AutoCombatState.Chasing;
						GD.Print($"[Combat] State: Idle → Chasing (dist={dist} > range={range})");
					}
					break;

				case AutoCombatState.Chasing:
					HandleChasingState(dist, range);
					break;

				case AutoCombatState.Arrived:
					HandleArrivedState(delta, profile, dist, range);
					break;

				case AutoCombatState.Executing:
					HandleExecutingState(delta, profile, dist, range);
					break;
			}
		}

		/// <summary>
		/// 追击状态处理
		/// </summary>
		private void HandleChasingState(int dist, int range)
		{
			// 【關鍵修復】必須走到攻擊範圍內才能攻擊
			// 對於近戰（range=1），必須距離<=1格
			// 對於遠程（range>1），為了確保服務器距離檢查通過，應該走到距離<=range-1格
			// 服務器距離檢查使用的是玩家在服務器端的座標，如果客戶端預測與服務器不一致，距離檢查會失敗
			int requiredDist = range > 1 ? range - 1 : range;
			
			if (dist > requiredDist)
			{
				if (!_isAutoWalking)
				{
					var t = _currentTask.Target;
					var next = GetBestNeighborPosition(_myPlayer.MapX, _myPlayer.MapY, t.MapX, t.MapY);
					StartWalking(next.x, next.y);
					GD.Print($"[Combat] Chasing: Moving to ({next.x}, {next.y}) dist={dist} requiredDist={requiredDist}");
				}
			}
			else
			{
				// 距离够了，进入到位状态（等待一帧）
				_combatState = AutoCombatState.Arrived;
				GD.Print($"[Combat] State: Chasing → Arrived (dist={dist} <= requiredDist={requiredDist})");
			}
		}

		/// <summary>
		/// 到位狀態：等本幀 StepTowardsTarget 執行完（_hasArrivedThisFrame）再轉 Executing 並攻擊，避免先送攻擊再送移動導致伺服器距離判定失敗。
		/// </summary>
		private void HandleArrivedState(double delta, CombatProfile profile, int dist, int range)
		{
			if (_isAutoWalking && !_hasArrivedThisFrame) return;

			if (_isAutoWalking)
			{
				StopWalking();
				GD.Print("[Combat] Arrived: Stopped walking");
			}

			_combatState = AutoCombatState.Executing;
			GD.Print("[Combat] State: Arrived → Executing");

			// 同一幀內立即嘗試攻擊，使用「進入 Arrived 時」的 dist/range，避免下一幀目標移動導致 dist&gt;range 而永遠打不到
			HandleExecutingState(delta, profile, dist, range);
		}

		/// <summary>
		/// 执行状态处理 - 实际执行攻击
		/// </summary>
		private void HandleExecutingState(double delta, CombatProfile profile, int dist, int range)
		{
			// 【關鍵修復】檢查目標是否還在範圍內（使用服務器確認的座標）
			// 如果目標移動了，需要重新計算距離
			if (_currentTask?.Target != null)
			{
				var target = _currentTask.Target;
				// 使用服務器確認的玩家座標和目標座標重新計算距離
				int playerX = _serverConfirmedPlayerX >= 0 ? _serverConfirmedPlayerX : _myPlayer.MapX;
				int playerY = _serverConfirmedPlayerY >= 0 ? _serverConfirmedPlayerY : _myPlayer.MapY;
				int targetX = target.MapX;
				int targetY = target.MapY;
				int actualDist = GetGridDistance(playerX, playerY, targetX, targetY);
				
				// 如果實際距離超出範圍，返回追擊狀態
				if (actualDist > range)
				{
					_combatState = AutoCombatState.Chasing;
					GD.Print($"[Combat] Target out of range (dist={actualDist} range={range}), returning to Chasing");
					return;
				}
			}
			
			// 【現代遊戲特性】智能重試機制：根據剩餘冷卻時間動態調整重試間隔
			if (_smartRetryTimer > 0)
			{
				_smartRetryTimer -= (float)delta;
				if (_smartRetryTimer > 0)
				{
					return; // 仍在智能重試間隔中，不重試
				}
				_smartRetryTimer = 0; // 重試間隔完成
			}
			
			// 1. 冷却检查（法師職業 class=3 且開啟 Z 鍵魔法攻擊時用魔法間隔，其他用攻擊間隔）
			// 【優化】移除 _attackCooldownTimer 的重複功能，統一使用 EnhancedSpeedManager
			// _attackInProgress 僅用於標記攻擊動畫是否正在播放，不進行冷卻檢查
			// 冷卻檢查統一在 PerformAttackOnce() 中使用 EnhancedSpeedManager.CanPerformAction()
			// 攻擊動畫完成後，_attackInProgress 會由動畫完成回調重置

			// 2. 距离检查（防止目标逃离）
			if (dist > range)
			{
				_combatState = AutoCombatState.Chasing;
				GD.Print($"[Combat] Target out of range (dist={dist} range={range}), returning to Chasing");
				return;
			}

			// 【服務器對齊】移動攻擊規則：對齊服務器 C_Attack 和 C_AttackBow
			// 服務器只檢查 isLockFreeze()，沒有明確禁止移動中攻擊
			// 但服務器會檢查距離 getDistance(x, y, target.getMap(), this.Areaatk)，使用客戶端發送的座標
			// 因此，遠程攻擊（弓箭/魔法）可以在移動中攻擊，但近戰攻擊需要停止移動以確保距離檢查通過
			if (profile.CanAttackWhileMoving)
			{
				// 遠程攻擊（弓箭/魔法）：可以邊走邊射，但須等本幀 StepTowardsTarget 後再送攻擊（_hasArrivedThisFrame）
				// 確保移動包先發送，避免服務器距離檢查失敗
				if (_isAutoWalking && !_hasArrivedThisFrame)
					return;
			}
			else
			{
				// 近戰攻擊：必須停止移動，確保距離檢查通過（服務器 Areaatk = 2）
				if (_isAutoWalking)
				{
					GD.Print("[Combat-Diag] Executing skip: melee requires not walking (_isAutoWalking=true)");
					return;
				}
			}

			// 4. 最小推进距离 (防抖) - 防止弓在原地疯狂抖动
			if (profile.MinAdvancePerAttack > 0 && 
				dist > range && // 只有在射程外追擊時才應用推進邏輯
				dist >= _lastAttackDist - profile.MinAdvancePerAttack &&
				_lastAttackDist != int.MaxValue)
			{
				// 没有足够接近，不射
				if (profile.CanAttackWhileMoving)
				{
					// 弓继续走
					_combatState = AutoCombatState.Chasing;
				}
				// 如果當前距離沒有比上次攻擊時更近，則繼續走路，不觸發攻擊
				return;
			}

			// 5. 执行攻击
			ExecuteAttackAction(profile, dist);
		}

		/// <summary>
		/// 【優化】執行攻擊動作（支持攻擊預測）
		/// 防止「原地抽風」 (Stuttering)：
		/// 對於弓箭手，如果目標正在逃跑，而你的攻擊冷卻剛好到了，如果沒有這個記錄，角色可能會每走一像素就停下來射一箭，導致看起來像是在原地不停地「抖動」而追不上人。
		/// 強制推進邏輯：
		/// 它確保了在自動追擊模式下，角色必須比上一次攻擊時更靠近目標至少 1 格（或指定的距離），才允許發動下一次攻擊。這保證了追擊的流暢性，讓角色表現得更像真人玩家：先跑近一點，再射擊。
		/// </summary>
		private void ExecuteAttackAction(CombatProfile profile, int dist)
		{
			// 僅用冷卻擋；不再用 IsActionBusy，避免被連續攻擊時僵硬動畫常駐導致永遠無法出刀
			if (_attackInProgress)
			{
				// 【安全保護】若動畫未回調導致卡住，超過 2 倍攻擊間隔則解鎖
				int speedActionId = _myPlayer.GetVisualBaseAction() + 1;
				long interval = SprDataTable.GetInterval(ActionType.Attack, _myPlayer.GfxId, speedActionId);
				long now = (long)Time.GetTicksMsec();
				if (_lastAttackSentTime > 0 && now - _lastAttackSentTime > interval * 2)
				{
					GD.Print($"[Combat-Fix] Attack stuck, force unlock. elapsed={now - _lastAttackSentTime}ms interval={interval}ms");
					_attackInProgress = false;
				}
				else
				{
					return;
				}
			}

			var target = _currentTask.Target;
			
			// 【任務二：攻擊距離判斷】嚴格檢查攻擊距離，禁止在射程外播放攻擊動畫
			int attackRange = GetAttackRange();
			if (dist > attackRange)
			{
				GD.Print($"[Combat-Fix] Too far to attack! Dist:{dist} > Range:{attackRange}. Skip animation & packet.");
				// 距離太遠，返回追擊狀態，繼續移動
				_combatState = AutoCombatState.Chasing;
				return; // 絕對不要播動畫，也不要發 Opcode 攻擊包
			}
			
			int? skillId = _currentTask.SkillId; // 從任務中獲取技能 ID（快捷鍵 1-8 設置）

			// 【核心修復】如果任務指定了技能 ID（快捷鍵 1-8），使用該技能進行攻擊
			// 這樣法師可以使用物理攻擊和弓箭，而不只是魔法
			if (skillId.HasValue)
			{
				long remainingMs = SkillCooldownManager.GetRemainingCooldownMs(skillId.Value);
				if (remainingMs > 0)
				{
					if (ShouldLogMagicBlocked(skillId.Value))
						GD.Print($"[Combat] Hotkey Skill {skillId.Value} blocked (cooldown {remainingMs}ms). Target:{target.ObjectId} Dist:{dist}");
					return;
				}
				if (!CanPerformMagicAction(skillId.Value, out float remainingActionMs))
				{
					if (ShouldLogMagicBlocked(skillId.Value))
						GD.Print($"[Combat] Hotkey Skill {skillId.Value} blocked (action interval {remainingActionMs:F0}ms). Target:{target.ObjectId} Dist:{dist}");
					return;
				}
				if (UseMagic(skillId.Value, target.ObjectId))
				{
					_lastAttackDist = dist;
					_combatState = AutoCombatState.Executing;
					GD.Print($"[Combat] Hotkey Skill {skillId.Value} executed. Target:{target.ObjectId} Dist:{dist}");
				}
				else
				{
					if (ShouldLogMagicBlocked(skillId.Value))
						GD.Print($"[Combat] Hotkey Skill {skillId.Value} blocked (cooldown or invalid target). Target:{target.ObjectId} Dist:{dist}");
					// 變身後或目標遺失時 UseMagic 可能失敗，結束任務避免卡在循環，玩家可重新選怪
					if (!_entities.ContainsKey(target.ObjectId))
					{
						FinishCurrentTask();
						GD.Print("[Combat] Target no longer in entities, task cleared.");
					}
				}
				return;
			}

			// 【法師 Z 鍵魔法攻擊開關】法師職業 (class=3) Z 鍵行為：
			// - 開啟時：使用魔法攻擊（預設 skill 4 光箭；可由快捷鍵 1-8 替換）
			// - 關閉時：使用普通攻擊（支持近戰、弓箭，但不支持魔法）
			if (IsMageClass() && Client.Data.ClientConfig.MageZMagicAttackEnabled && !IsUsingBow())
			{
				long remainingMs = SkillCooldownManager.GetRemainingCooldownMs(_mageAutoAttackSkillId);
				if (remainingMs > 0)
				{
					if (ShouldLogMagicBlocked(_mageAutoAttackSkillId))
						GD.Print($"[Combat] Mage Z Skill {_mageAutoAttackSkillId} blocked (cooldown {remainingMs}ms). Target:{target.ObjectId} Dist:{dist}");
					return;
				}
				if (!CanPerformMagicAction(_mageAutoAttackSkillId, out float remainingActionMs))
				{
					if (ShouldLogMagicBlocked(_mageAutoAttackSkillId))
						GD.Print($"[Combat] Mage Z Skill {_mageAutoAttackSkillId} blocked (action interval {remainingActionMs:F0}ms). Target:{target.ObjectId} Dist:{dist}");
					return;
				}
				if (UseMagic(_mageAutoAttackSkillId, target.ObjectId))
				{
					_lastAttackDist = dist;
					_combatState = AutoCombatState.Executing;
					GD.Print($"[Combat] Mage Z = Skill {_mageAutoAttackSkillId} executed. Target:{target.ObjectId} Dist:{dist}");
				}
				else
				{
					if (ShouldLogMagicBlocked(_mageAutoAttackSkillId))
						GD.Print($"[Combat] Mage Z Skill {_mageAutoAttackSkillId} blocked (cooldown or invalid target). Target:{target.ObjectId} Dist:{dist}");
					if (!_entities.ContainsKey(target.ObjectId))
					{
						FinishCurrentTask();
						GD.Print("[Combat] Target no longer in entities, task cleared.");
					}
				}
				return;
			}

			// 【優化】攻擊預測：遠程攻擊時預測目標移動位置
			int tx, ty;
			if (profile.UsePredictShot && profile.CanAttackWhileMoving)
			{
				// 遠程攻擊（弓箭/魔法）：使用預測位置（優先使用新的 PredictTargetPosition）
				var predicted = PredictTargetPosition(target, attackRange);
				tx = predicted.x;
				ty = predicted.y;
			}
			else
			{
				// 近戰攻擊或未啟用預測：使用當前位置
				tx = target.MapX;
				ty = target.MapY;
			}

			int heading = GetHeading(_myPlayer.MapX, _myPlayer.MapY, tx, ty, _myPlayer.Heading);
			_myPlayer.SetHeading(heading);

			_lastAttackDist = dist;

			// 【現代遊戲特性】PerformAttackOnce 返回 bool，表示是否成功發送攻擊包
			// 如果速度檢查失敗，PerformAttackOnce 返回 false，使用智能重試機制
			// 只有當攻擊包成功發送時，才更新狀態機
			bool attackSent = PerformAttackOnce(target.ObjectId, tx, ty);
			
			if (attackSent)
			{
				_combatState = profile.CanAttackWhileMoving ? AutoCombatState.Chasing : AutoCombatState.Executing;
				GD.Print($"[Combat] Attack executed. Target:{target.ObjectId} Dist:{dist} Type:{(profile.CanAttackWhileMoving ? "Ranged" : "Melee")}");
			}
			else
			{
				// 攻擊包被阻止（速度檢查失敗），智能重試機制已在 PerformAttackOnce 中設置
				// 這裡只記錄日誌，不更新狀態機（避免無限循環）
				GD.Print($"[Combat] Attack blocked (speed check failed). Target:{target.ObjectId} Dist:{dist} - Smart retry active (interval={_smartRetryTimer * 1000:F0}ms)");
			}
		}

		// ============================================================
		// [执行单元] Pickup & NPC Task (改进版)
		// ============================================================
		private void ExecutePickupTask()
		{
			var target = _currentTask.Target;
			// 只有 102.type(9) 才是地面物品，才可以被拾取；作為第一優先的判斷依據
			if (target == null || !IsGroundItem(target))
			{
				FinishCurrentTask();
				return;
			}
			int dist = GetGridDistance(_myPlayer.MapX, _myPlayer.MapY, target.MapX, target.MapY);

			// 走向物品：距離 > 1 時先移動；伺服器 ItemInstance.pickup 僅在 getDistance(x,y,map,1) 時接受，故發送封包前必須 ≤1 格
			if (dist > 1)
			{
				if (!_isAutoWalking)
				{
					StartWalking(target.MapX, target.MapY);
					GD.Print($"[Pickup] Moving to item at ({target.MapX}, {target.MapY})");
				}
				return;
			}
			// 【拾取修復】避免每幀重複發包，節流 1000ms
			long now = (long)Time.GetTicksMsec();
			if (_pickupInProgress && now - _lastPickupSentTime < 1000)
			{
				return;
			}
			// 【服務器對齊】距離已 ≤1 即發送拾取，對齊伺服器 getDistance(物品座標,map,1)
			// 服務器 ItemInstance.pickup 使用物品實際座標 getX(), getY() 檢查距離，而不是客戶端發送的 x, y
			// 客戶端發送的 x, y 僅用於日誌記錄，實際距離檢查使用物品的實際座標
			StopWalking();
			// 【服務器對齊】全部拾取：送 target.ItemCount（S_ObjectAdd 的 Exp）；伺服器要求 getCount()>=count 且 count>0，超出時 toDelete 不 insert
			int pickCount = (target.ItemCount > 0 && target.ItemCount <= int.MaxValue) ? target.ItemCount : 1;
			// 【服務器對齊】對齊服務器 C_ItemPickup.java：readH()=x, readH()=y, readD()=inv_id, readD()=count
			// 服務器使用物品實際座標 getX(), getY() 檢查距離，客戶端發送的 x, y 僅用於日誌記錄
			_netSession.Send(C_ItemPickupPacket.Make(target.ObjectId, target.MapX, target.MapY, pickCount));
			_pickupInProgress = true;
			_lastPickupSentTime = now;
			_myPlayer.SetAction(GameEntity.ACT_PICKUP);
			GD.Print($"[Pickup] Sent objectId={target.ObjectId} x={target.MapX} y={target.MapY} count={pickCount} (服務器將使用物品實際座標檢查距離)");
			// 【核心修復】不要立即 FinishCurrentTask，等待服務器響應（S_InventoryAdd 或 S_ObjectDelete）
			// 如果物品全部被拾取，服務器會發送 S_ObjectDelete (Opcode 21)，OnObjectDeleted 會自動清理任務
			// 如果物品部分被拾取，服務器會發送 S_ObjectAdd (Opcode 0)，物品數量會更新，任務可以繼續
			// 只有在收到服務器響應後才 FinishCurrentTask
		}

		private void ExecuteTalkNpcTask()
		{
			var target = _currentTask.Target;
			int dist = GetGridDistance(_myPlayer.MapX, _myPlayer.MapY, target.MapX, target.MapY);

			if (dist > 1)
			{
				if (!_isAutoWalking)
				{
					var next = GetBestNeighborPosition(_myPlayer.MapX, _myPlayer.MapY, target.MapX, target.MapY);
					StartWalking(next.x, next.y);
					GD.Print($"[NPC] Moving to NPC at ({next.x}, {next.y})");
				}
				return;
			}
			// 距離已 ≤1 即執行，與拾取一致，不依賴 _hasArrivedThisFrame
			StopWalking();
			TalkToNpc(target.ObjectId);
			GD.Print($"[NPC] Talking to NPC: {target.ObjectId}");
			FinishCurrentTask();
		}

		// ============================================================
		// [辅助逻辑] Profile & Prediction (保留原有)
		// ============================================================
		private CombatProfile GetCombatProfile()
		{
			// 【服務器對齊】若當前任務是魔法攻擊（技能有射程），一律採用遠程行為（可邊走邊施法）
			if (HasActiveMagicAttack())
			{
				return new CombatProfile { 
					AttackCooldown = 0.7f, 
					CanAttackWhileMoving = true, 
					MinAdvancePerAttack = 1, 
					UsePredictShot = true 
				};
			}

			// 【修復】法師職業 (class=3) 根據 MageZMagicAttackEnabled 配置決定攻擊模式
			// - 開啟時：遠程魔法攻擊（CanAttackWhileMoving = true）
			// - 關閉時：近戰攻擊（CanAttackWhileMoving = false）
			if (IsMageClass())
			{
				if (Client.Data.ClientConfig.MageZMagicAttackEnabled)
				{
					// 法師魔法模式：遠程攻擊
					return new CombatProfile { 
						AttackCooldown = 0.7f, 
						CanAttackWhileMoving = true, 
						MinAdvancePerAttack = 1, 
						UsePredictShot = true 
					};
				}
				else
				{
					// 法師普通攻擊模式：近戰攻擊
					return new CombatProfile { 
						AttackCooldown = 0.6f, 
						CanAttackWhileMoving = false, 
						MinAdvancePerAttack = 0, 
						UsePredictShot = false 
					};
				}
			}

			if (_myPlayer.IsUsingBow())
				return new CombatProfile { 
					AttackCooldown = 0.8f, 
					CanAttackWhileMoving = true, 
					MinAdvancePerAttack = 1, 
					UsePredictShot = true 
				};
			if (_myPlayer.IsUsingSpear())
				return new CombatProfile { 
					AttackCooldown = 0.7f, 
					CanAttackWhileMoving = false, 
					MinAdvancePerAttack = 0, 
					UsePredictShot = false 
				};
			
			// 默认近战
			return new CombatProfile { 
				AttackCooldown = 0.6f, 
				CanAttackWhileMoving = false, 
				MinAdvancePerAttack = 0, 
				UsePredictShot = false 
			};
		}

		private (int x, int y) PredictRangedTarget(GameEntity t)
		{
			int dx = 0, dy = 0;
			switch (t.Heading)
			{
				case 0: dy = -1; break; 
				case 1: dx = 1; dy = -1; break;
				case 2: dx = 1; break;  
				case 3: dx = 1; dy = 1; break;
				case 4: dy = 1; break;  
				case 5: dx = -1; dy = 1; break;
				case 6: dx = -1; break; 
				case 7: dx = -1; dy = -1; break;
			}
			return (t.MapX + dx, t.MapY + dy);
		}

		// ========================================================================
		// [入口] 开始自动攻击 (适配旧接口)
		// ========================================================================
		/// <summary>當前攻擊任務的目標（供 Skill/UI 等使用）；無攻擊任務時為 null。</summary>
		internal GameEntity GetCurrentAttackTarget() =>
			_currentTask != null && _currentTask.Type == AutoTaskType.Attack ? _currentTask.Target : null;

		/// <summary>當前任務的目標（任意類型，供治癒等單體魔法使用）；點選其他玩家時為 PickUp 任務，需用此取得目標。</summary>
		internal GameEntity GetCurrentTaskTarget() =>
			_currentTask?.Target;

		public void StartAutoAttack(GameEntity target)
		{
			if (target == null) return;
			if (_isAutoAttacking && _currentTask?.Target == target && AlignmentHelper.IsNpc(target.Lawful))
				return;

			GD.Print($"[Combat] Enqueue Task: {target.RealName} (ID: {target.ObjectId})");
			EnqueueTask(new AutoTask(AutoTaskType.Attack, target));
			_isAutoAttacking = true;
			_isAutoPickup = false;
		}

		// ========================================================================
		// [核心] Z 鍵 = 一律重掃範圍內最佳目標並設為攻擊任務；無目標則清除攻擊任務
		// ========================================================================
		public void ScanForAutoTarget()
		{
			ScanForAutoTargetWithSkill(null);
		}

		/// <summary>
		/// 掃描範圍內最佳目標並設為攻擊任務，支持指定技能 ID（用於快捷鍵 1-8）
		/// 當 skillId 為 null 時，使用默認攻擊方式（法師用魔法，其他職業用物理攻擊）
		/// 當 skillId 不為 null 時，使用指定的技能進行攻擊
		/// </summary>
		public void ScanForAutoTargetWithSkill(int? skillId)
		{
			if (_myPlayer == null) return;
			// 已選拾取目標（地面物品）時按快捷鍵：不覆蓋任務，讓 UpdateCombatLogic 繼續執行 ExecutePickupTask（走向物品並拾取）
			if (_currentTask != null && _currentTask.Type == AutoTaskType.PickUp)
				return;

			// 【修復】如果快捷鍵指定了技能 ID，優先使用當前攻擊目標（如果存在且有效）
			// 這樣可以讓快捷鍵攻擊類魔法立即使用當前目標，而不需要重新掃描
			if (skillId.HasValue && _currentTask != null && _currentTask.Type == AutoTaskType.Attack)
			{
				var currentTarget = _currentTask.Target;
				if (currentTarget != null && !currentTarget.IsDead && _entities.ContainsKey(currentTarget.ObjectId))
				{
					// 使用當前目標，但更新技能 ID
					GD.Print($"[Combat-Scan] Using current target with skill {skillId.Value}: {currentTarget.RealName} (ID:{currentTarget.ObjectId})");
					EnqueueTask(new AutoTask(AutoTaskType.Attack, currentTarget, AutoTaskPriority.Forced, skillId));
					_isAutoAttacking = true;
					_isAutoPickup = false;
					_combatState = AutoCombatState.Idle; // 重置狀態，立即執行新技能
					return;
				}
			}

			// 遍歷尋找最佳可攻擊目標（排除死亡、超過距離）
			GameEntity bestTarget = null;
			int minScore = 9999;

			foreach (var entity in _entities.Values)
			{
				if (entity == _myPlayer) continue;
				// 排除死亡目標：正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0
				if (entity.IsDead) continue;
				// 不可以刪除修改。。排除地面物品等：僅選擇 102.type（5）/（10）有血量，才可選為攻擊目標，此規則此文字備注不可以刪除。
				// 【修復】list.spr 缺失時無法判定 102.type，若為怪物則允許選擇，避免 Z 無目標
				var def = Client.Utility.ListSprLoader.Get(entity.GfxId);
				if (def != null)
				{
					if (def.Type != 5 && def.Type != 10) continue;
				}
				else
				{
					if (!AlignmentHelper.IsMonster(entity.Lawful)) continue;
				}
				// 不可以刪除修改。排除己方召喚/寵物，不當成自動尋怪目標
				if (_mySummonObjectIds != null && _mySummonObjectIds.Contains(entity.ObjectId)) continue;
				int dist = GetGridDistance(_myPlayer.MapX, _myPlayer.MapY, entity.MapX, entity.MapY);
				if (dist > 15) continue;

				int score = dist * 10;
				if (AlignmentHelper.IsMonster(entity.Lawful)) score += 0;
				else if (AlignmentHelper.IsNpc(entity.Lawful)) score += 500;
				else score += 1000;

				if (score < minScore) { minScore = score; bestTarget = entity; }
			}

			if (bestTarget != null)
			{
				string skillInfo = skillId.HasValue ? $" Skill:{skillId.Value}" : "";
				GD.Print($"[Combat-Scan] Target Selected: {bestTarget.RealName} (ID:{bestTarget.ObjectId}, Action:{bestTarget.CurrentAction}, Dist:{minScore/10}{skillInfo})");
				_hud?.AddSystemMessage($"Auto Target:{bestTarget.RealName}");
				// 強制換成這次掃到的那隻：清空佇列並置入新攻擊任務（帶技能 ID）
				EnqueueTask(new AutoTask(AutoTaskType.Attack, bestTarget, AutoTaskPriority.Forced, skillId));
				_isAutoAttacking = true;
				_isAutoPickup = false;
				_combatState = AutoCombatState.Idle; // 重置狀態，立即執行新任務
			}
			else
			{
				// 無目標：清除攻擊任務，下次按快捷鍵不會被「已有任務」擋住
				if (_currentTask != null && _currentTask.Type == AutoTaskType.Attack)
					FinishCurrentTask();
			}
		}

		// ========================================================================
		// [原有核心] 执行单次攻击 (被 HandleAttackExecution 调用)
		// ========================================================================
		/// <summary>
		/// 執行單次攻擊（增強版：使用現代遊戲最佳實踐）
		/// </summary>
		/// <param name="targetId">目標ID</param>
		/// <param name="targetX">目標X座標</param>
		/// <param name="targetY">目標Y座標</param>
public bool PerformAttackOnce(int targetId, int targetX, int targetY)
{
    if (_myPlayer == null) return false;
    // 【JP協議對齊】速度檢查用 gfxMode+1（對齊 SprTable.getAttackSpeed(gfxId, gfxMode+1)）
    int speedActionId = _myPlayer.GetVisualBaseAction() + 1;
    int actionId = IsUsingBow() ? GameEntity.ACT_ATTACK_BOW : GameEntity.ACT_ATTACK;
    
    // 1. 速度檢查
    if (!EnhancedSpeedManager.CanPerformAction(ActionType.Attack, _myPlayer.GfxId, speedActionId, out float remainingMs))
    {
        float retryInterval = EnhancedSpeedManager.GetSmartRetryInterval(remainingMs);
        _smartRetryTimer = retryInterval;
        if (remainingMs > 50) 
            GD.Print($"[SpeedManager] Attack on cooldown. remaining={remainingMs:F0}ms speedActionId={speedActionId} gfxMode={_myPlayer.GetVisualBaseAction()}");
        return false;
    }

    if (targetId == 0 && _isAutoAttacking) return false;

    // 【優化】_attackInProgress 僅用於標記攻擊動畫是否正在播放
    // 冷卻檢查已由 EnhancedSpeedManager.CanPerformAction() 完成
    _attackInProgress = true;

    // ====================================================================
    // 【座標同步與朝向修正】
    // ====================================================================
    int clientX = _myPlayer.MapX;
    int clientY = _myPlayer.MapY;
    
    // 獲取服務器確認座標（若未初始化則信賴客戶端）
    int serverX = _serverConfirmedPlayerX >= 0 ? _serverConfirmedPlayerX : clientX;
    int serverY = _serverConfirmedPlayerY >= 0 ? _serverConfirmedPlayerY : clientY;

    // 計算攻擊用的座標：使用服務器確認的座標
    int calcX = serverX;
    int calcY = serverY;
    
    // 計算距離與朝向
    int distance = GetGridDistance(calcX, calcY, targetX, targetY);
    bool isMoving = _isAutoWalking;

    GD.Print($"[Combat-Diag] Attack targetId={targetId} Dist={distance} Pos:({calcX},{calcY})");

    // 【任務二：攻擊距離判斷】嚴格檢查攻擊距離，禁止在射程外播放攻擊動畫
    int attackRange = GetAttackRange();
    if (distance > attackRange)
    {
        GD.Print($"[Combat-Fix] Too far to attack! Dist:{distance} > Range:{attackRange}. Skip animation & packet.");
        _attackInProgress = false; // 重置攻擊狀態，允許下次嘗試
        return false; // 絕對不要播動畫，也不要發 Opcode 攻擊包
    }

    // 設置朝向 (使用計算出的座標)
    if (targetX != 0 && targetY != 0 && (targetX != calcX || targetY != calcY))
    {
        int attackHeading = GetHeading(calcX, calcY, targetX, targetY, _myPlayer.Heading);
        _myPlayer.SetHeading(attackHeading);
    }

    // 發送攻擊封包 (注意：這裡傳入的是我們計算認為正確的玩家座標)
    // 只有通過距離檢查，才執行發包和播動畫
    if (IsUsingBow())
    {
        SendAttackBowPacket(targetId, targetX, targetY, calcX, calcY, isMoving);
        _myPlayer.SetAction(GameEntity.ACT_ATTACK_BOW);
    }
    else
    {
        SendAttackPacket(targetId, targetX, targetY, calcX, calcY, isMoving);
        _myPlayer.PlayAttackAnimation(targetX, targetY);
    }
    
    // 【關鍵修復】在實際發送封包後才更新時間戳，確保與服務器同步
    // 這樣可以避免因距離檢查失敗等原因導致時間戳被提前更新
    EnhancedSpeedManager.RecordActionPerformed(ActionType.Attack);
    _lastAttackSentTime = (long)Time.GetTicksMsec();
    
    return true;
}



		// ========================================================================
		// [原有逻辑] 网络发包、距离计算、武器判断 (完整保留)
		// ========================================================================

		private void SendAttackPacket(int tid, int x, int y, int playerX, int playerY, bool isMoving)
		{
			var w = new PacketWriter();
			// 【JP協議對齊】C_OPCODE_ATTACK = 68
			w.WriteByte(68);
			w.WriteInt(tid);
			w.WriteUShort(x);  // 目標座標（協議規定）
			w.WriteUShort(y);  // 目標座標（協議規定）
			_netSession.Send(w.GetBytes());
			// 【座標同步診斷】記錄發送攻擊包的完整信息，用於與服務器端日誌對比
			GD.Print($"[Combat-Packet] Sent Attack(68) -> TargetID:{tid} TargetGrid:({x},{y}) PlayerGrid:({playerX},{playerY}) IsMoving:{isMoving}");
		}

		private void SendAttackBowPacket(int tid, int x, int y, int playerX, int playerY, bool isMoving)
		{
			var w = new PacketWriter();
			// 【JP協議對齊】C_OPCODE_ARROWATTACK = 247
			w.WriteByte(247);
			w.WriteInt(tid);
			w.WriteUShort(x);  // 目標座標（協議規定）
			w.WriteUShort(y);  // 目標座標（協議規定）
			_netSession.Send(w.GetBytes());
			// 【座標同步診斷】記錄發送攻擊包的完整信息，用於與服務器端日誌對比
			GD.Print($"[Combat-Packet] Sent AttackBow(247) -> TargetID:{tid} TargetGrid:({x},{y}) PlayerGrid:({playerX},{playerY}) IsMoving:{isMoving}");
		}

		/// <summary>
		/// 【優化】獲取最佳鄰近位置（用於追擊）
		/// </summary>
		private (int x, int y) GetBestNeighborPosition(int myX, int myY, int targetX, int targetY)
		{
			// 簡單的8方向最近鄰居算法
			int dx = targetX - myX;
			int dy = targetY - myY;
			
			// 標準化方向（-1, 0, 1）
			int stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
			int stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);
			
			return (myX + stepX, myY + stepY);
		}
		
		/// <summary>
		/// 【新增】攻擊預測：預測目標移動位置並提前射擊
		/// 用於遠程攻擊（弓箭/魔法），當目標正在移動時，預測其下一步位置並提前射擊
		/// </summary>
		/// <param name="target">目標實體</param>
		/// <param name="attackRange">攻擊範圍</param>
		/// <returns>預測的目標位置 (x, y)，如果無法預測則返回當前位置</returns>
		private (int x, int y) PredictTargetPosition(GameEntity target, int attackRange)
		{
			if (target == null) return (0, 0);
			
			// 如果目標不在移動，直接返回當前位置
			if (target.CurrentAction == GameEntity.ACT_BREATH || target.CurrentAction == GameEntity.ACT_DAMAGE)
			{
				return (target.MapX, target.MapY);
			}
			
			// 【攻擊預測】如果目標正在移動（ACT_WALK），預測其下一步位置
			// 預測邏輯：假設目標繼續朝當前方向移動
			// 注意：這是一個簡化的預測，實際遊戲中可能需要更複雜的AI預測
			if (target.CurrentAction == GameEntity.ACT_WALK || 
			    (target.CurrentAction >= 4 && target.CurrentAction <= 7) || // 劍行走
			    (target.CurrentAction >= 11 && target.CurrentAction <= 14) || // 斧行走
			    (target.CurrentAction >= 20 && target.CurrentAction <= 23) || // 弓行走
			    (target.CurrentAction >= 24 && target.CurrentAction <= 27) || // 矛行走
			    (target.CurrentAction >= 40 && target.CurrentAction <= 43)) // 杖行走
			{
				// 根據目標朝向預測下一步位置
				int heading = target.Heading;
				int predictedX = target.MapX;
				int predictedY = target.MapY;
				
				// 8方向移動預測（對齊服務器移動邏輯）
				switch (heading)
				{
					case 0: predictedY--; break; // 北
					case 1: predictedX++; predictedY--; break; // 東北
					case 2: predictedX++; break; // 東
					case 3: predictedX++; predictedY++; break; // 東南
					case 4: predictedY++; break; // 南
					case 5: predictedX--; predictedY++; break; // 西南
					case 6: predictedX--; break; // 西
					case 7: predictedX--; predictedY--; break; // 西北
				}
				
				// 檢查預測位置是否在攻擊範圍內
				int distToPredicted = GetGridDistance(_myPlayer.MapX, _myPlayer.MapY, predictedX, predictedY);
				if (distToPredicted <= attackRange)
				{
					GD.Print($"[Combat-Predict] Predicting target movement: ({target.MapX},{target.MapY}) -> ({predictedX},{predictedY}) heading={heading} dist={distToPredicted}");
					return (predictedX, predictedY);
				}
			}
			
			// 無法預測或預測位置超出範圍，返回當前位置
			return (target.MapX, target.MapY);
		}

		/// <summary>
		/// 【關鍵重構】統一距離計算方法：使用 Euclidean distance 對齊服務器邏輯
		/// 服務器使用：Math.sqrt(dx*dx + dy*dy)，然後轉換為 int
		/// 這確保客戶端和服務器的距離檢查一致，避免攻擊失敗
		/// </summary>
		private int GetGridDistance(int x1, int y1, int x2, int y2)
		{
			long dx = x2 - x1;
			long dy = y2 - y1;
			double distance = Math.Sqrt(dx * dx + dy * dy);
			return (int)distance; // 對齊服務器邏輯：Math.sqrt 後轉換為 int
		}

		/// <summary>
		/// 攻擊距離（格數）唯一來源：list.spr 102.type(8) 或 102.type(9) 括號內數字；無則依武器/角色 fallback。禁止其他處再寫死攻擊距離。
		/// 【任務二修復】硬性規定攻擊範圍：
		/// - 物理攻擊：2格（考慮到服務器判定誤差，1格太嚴苛，2格為緩衝）
		/// - 弓箭/魔法：12格（或讀取 skill_list 的 range 字段，若無則默認 12）
		/// </summary>
		private int GetAttackRange()
		{
			int magicRange = GetActiveMagicAttackRange();
			if (magicRange > 0)
				return magicRange;
			var def = ListSprLoader.Get(_myPlayer.GfxId);
			if (def != null && (def.Type == 8 || def.Type == 9))
				return def.Type;
			return GetAttackRangeFallback();
		}

		private int GetActiveMagicAttackRange()
		{
			int skillId = 0;
			if (_currentTask != null && _currentTask.SkillId.HasValue)
				skillId = _currentTask.SkillId.Value;
			else if (IsMageClass() && ClientConfig.MageZMagicAttackEnabled && !IsUsingBow())
				skillId = _mageAutoAttackSkillId;
			if (skillId <= 0) return 0;
			return GetSkillAttackRange(skillId);
		}

		private int GetSkillAttackRange(int skillId)
		{
			var entry = SkillListData.Get(skillId);
			string type = (entry?.Type ?? "").Trim().ToLowerInvariant();
			if (type == "buff" || type == "none" || type == "item")
				return 0;
			int ranged = SkillDbData.GetRanged(skillId);
			if (ranged > 0)
				return ranged;
			if (entry != null && entry.Range > 0)
				return entry.Range;
			return 12;
		}

		private bool HasActiveMagicAttack()
		{
			int skillId = 0;
			if (_currentTask != null && _currentTask.SkillId.HasValue)
				skillId = _currentTask.SkillId.Value;
			else if (IsMageClass() && Client.Data.ClientConfig.MageZMagicAttackEnabled && !IsUsingBow())
				skillId = _mageAutoAttackSkillId;
			if (skillId <= 0) return false;
			return GetSkillAttackRange(skillId) > 0;
		}

		private bool CanPerformMagicAction(int skillId, out float remainingMs)
		{
			remainingMs = 0;
			if (_myPlayer == null) return false;
			int magicActionId = SkillDbData.GetActionId(skillId);
			if (magicActionId <= 0) magicActionId = GameEntity.ACT_SPELL_DIR;
			return EnhancedSpeedManager.CanPerformAction(ActionType.Magic, _myPlayer.GfxId, magicActionId, out remainingMs);
		}

		private bool ShouldLogMagicBlocked(int skillId)
		{
			long now = (long)Time.GetTicksMsec();
			if (skillId == _lastMagicBlockedLogSkillId && now - _lastMagicBlockedLogTime < MAGIC_BLOCKED_LOG_INTERVAL_MS)
				return false;
			_lastMagicBlockedLogSkillId = skillId;
			_lastMagicBlockedLogTime = now;
			return true;
		}

		/// <summary>
		/// 【服務器對齊】硬性規定攻擊範圍，對齊服務器 PcInstance.Attack() 和 PcInstance.AttackBow()
		/// - 物理攻擊：2格（對齊服務器 Areaatk = 2）
		/// - 弓箭/魔法：12格（對齊服務器 Areaatk = 12）
		/// 服務器使用 getDistance(x, y, target.getMap(), this.Areaatk) 檢查距離
		/// </summary>
		private int GetAttackRangeFallback()
		{
			// 【修復】法師職業根據 MageZMagicAttackEnabled 配置決定攻擊範圍
			// - 開啟時：魔法攻擊範圍為 12格
			// - 關閉時：近戰攻擊範圍為 2格
			if (IsUsingBow()) return 12;
			if (IsMageClass())
			{
				if (Client.Data.ClientConfig.MageZMagicAttackEnabled)
					return 12; // 魔法攻擊範圍
				else
					return 2; // 近戰攻擊範圍
			}
			// 【服務器對齊】物理攻擊範圍為 2格（對齊服務器 Areaatk = 2）
			// 注意：長矛雖然是長距離武器，但服務器 Areaatk = 2，所以這裡也返回 2
			return 2; // 物理攻擊硬性規定為 2格（包括長矛）
		}

		/// <summary>
		/// 根據 actionId 判斷怪物的攻擊範圍（對齊服務器 Areaatk）
		/// 服務器使用 Areaatk 判斷攻擊範圍（近戰=2，遠程=12）
		/// 【對齊服務器】服務器 PcInstance.Attack() 使用 Areaatk = 2 (近戰), Areaatk = 12 (遠程)
		/// 正確的 actionId 定義：
		/// - 18.spell direction, 19.spell no direction: 魔法攻擊遠程（Areaatk=12）
		/// - 21.attack bow: 弓箭攻擊遠程（Areaatk=12）
		/// - 1.attack, 5.attack sword, 12.attack Axe, 30.alt attack: 近戰（Areaatk=2）
		/// - 24.walk Spear, 41.attack staff: 長矛類（Areaatk=2）
		/// </summary>
		private int GetMonsterAttackRange(int actionId)
		{
			// 魔法攻擊：18, 19（對齊服務器 Areaatk = 12）
			if (actionId == 18 || actionId == 19)
			{
				return 12;
			}
			// 弓箭攻擊：21（對齊服務器 Areaatk = 12）
			if (actionId == 21)
			{
				return 12;
			}
			// 近戰攻擊：1, 5, 12, 30, 24, 41（對齊服務器 Areaatk = 2）
			// 注意：服務器使用 getDistance(x, y, target.getMap(), 2) 檢查，所以這裡返回 2
			return 2;
		}

		private bool IsUsingBow() 
		{ 
			return _myPlayer != null && _myPlayer.IsUsingBow();
		} 
		
		private bool IsUsingSpear() 
		{ 
			return _myPlayer != null && _myPlayer.IsUsingSpear();
		}

		/// <summary>
		/// 法師職業判定：依職業 class=3（對齊 C_CreateCharPacket.ClassType.Wizard）。若 MyCharInfo.Type 尚未同步（如 DevMode 先進世界再收角色列表），從 CharacterList 依 CurrentCharName 補齊。
		/// </summary>
		private bool IsMageClass()
		{
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			if (boot?.MyCharInfo == null) return false;
			if (boot.MyCharInfo.Type == 0 && boot.CharacterList != null)
			{
				foreach (var c in boot.CharacterList)
				{
					if (c.Name == boot.CurrentCharName) { boot.MyCharInfo.Type = c.Type; break; }
				}
			}
			return boot.MyCharInfo.Type == 3;
		}

		/// <summary>法師 Z 自動攻擊時使用的技能 ID；快捷鍵 1-8 設為技能時會呼叫此方法替換。</summary>
		public void SetMageAutoAttackSkill(int skillId)
		{
			_mageAutoAttackSkillId = skillId;
		}

		// [注意] GetHeading 已在 Movement.cs 中定义，此处直接调用，不需重写。

		// ========================================================================
		// [表现回调] 打击感与特效 (完整保留)
		// ========================================================================
		private void SubscribeEntityEvents(GameEntity entity)
		{
			entity.OnAttackKeyFrameHit += HandleEntityAttackHitAdapter;
		}

		private void UnsubscribeEntityEvents(GameEntity entity)
		{
			entity.OnAttackKeyFrameHit -= HandleEntityAttackHitAdapter;
		}

		/// <summary>
		/// 【適配器】將 OnAttackKeyFrameHit 事件（Action&lt;int, int&gt;）適配到 HandleEntityAttackHit 方法
		/// </summary>
		private void HandleEntityAttackHitAdapter(int targetId, int damage)
		{
			// 調用完整版本，使用默認參數（isCritical=false, isMagic=false）
			HandleEntityAttackHit(targetId, damage, false, false);
		}

		/// <summary>
		/// 【優化】處理實體攻擊命中（支持傷害類型判斷和戰鬥統計）
		/// </summary>
		/// <param name="targetId">目標ID</param>
		/// <param name="damage">傷害值（<=0 表示 MISS）</param>
		/// <param name="isCritical">是否暴擊（可選，默認 false）</param>
		/// <param name="isMagic">是否魔法傷害（可選，默認 false）</param>
		private void HandleEntityAttackHit(int targetId, int damage, bool isCritical = false, bool isMagic = false)
		{
			bool inWorld = _entities.TryGetValue(targetId, out var target);
			GD.Print($"[HitChain] HandleEntityAttackHit target={targetId} damage={damage} isCritical={isCritical} isMagic={isMagic} inWorld={inWorld}");
			
			if (inWorld && target != null)
			{
				// 【優化】判斷傷害類型並顯示對應的飄字
				GameEntity.DamageType damageType;
				if (damage <= 0)
				{
					damageType = GameEntity.DamageType.Miss;
				}
				else if (isCritical)
				{
					damageType = GameEntity.DamageType.Critical;
				}
				else if (isMagic)
				{
					damageType = GameEntity.DamageType.Magic;
				}
				else
				{
					damageType = GameEntity.DamageType.Normal;
				}
				
				target.OnDamagedVisual(damage, damageType);
				
				// 【新增】戰鬥統計：記錄傷害
				if (target == _myPlayer)
				{
					// 玩家受到傷害
					_combatStats.RecordDamageTaken(damage);
				}
				else if (_myPlayer != null && _myPlayer.ObjectId > 0)
				{
					// 玩家造成傷害（需要檢查攻擊者是否為玩家）
					// 注意：這裡無法直接判斷攻擊者，需要從攻擊包中獲取
					// 暫時先記錄，後續可以通過攻擊包中的 attackerId 判斷
				}
				
				// 怪物血條：僅當已有伺服器血條數據時才依傷害估算；否則等 S_HpMeter(128) 為準
				if (target != _myPlayer && damage > 0 && target.HpRatio >= 0)
				{
					int newRatio = Math.Max(0, target.HpRatio - damage);
					target.SetHpRatio(newRatio);
				}
				// 已死亡目標不再播放受擊僵硬，避免封包順序導致「先收到 ObjectAction(8) 再收到命中」時死亡動畫被覆蓋
				if (damage > 0 && !target.IsDead)
				{
					target.SetAction(GameEntity.ACT_DAMAGE);
					target.OnDamageStutter();
					GD.Print($"[HitChain] Target {targetId} SetAction(DAMAGE)+Stutter done");
				}
				else if (damage <= 0)
					GD.Print($"[HitChain] Target {targetId} MISS (damage<=0), only OnDamagedVisual");
				else
					GD.Print($"[HitChain] Target {targetId} already dead (IsDead=true), skip DAMAGE animation");
			}
			else
				GD.PrintErr($"[HitChain] HandleEntityAttackHit target {targetId} NOT in _entities");
		}

		// ========================================================================
		// [事件回调] 服务器交互
		// ========================================================================

		private void OnObjectMoved(int objectId, int x, int y, int heading)
		{
			// 【核心修復】將「檢查實體是否存在」作為第一優先級
			// 如果實體已經存在於世界中，必須無條件更新位置！
			// 這解決了怪物移動封包中斷的問題：即使實體存在，如果被誤判為不存在或距離過濾，會導致位置 Desync
			if (_entities.TryGetValue(objectId, out var entity))
			{
				// 【關鍵修復】實體存在，無條件更新位置，然後直接返回，不執行緩存邏輯
				UpdateEntityPositionFromServer(objectId, x, y, heading, "S_ObjectMoving");
				
				// 【關鍵修復】記錄移動包確認時間，用於診斷丟包問題（僅玩家自己）
				if (objectId == _myPlayer?.ObjectId)
				{
					_lastServerMoveConfirmTime = (long)Time.GetTicksMsec();

					// 【座標同步驗證】比較服務器座標與客戶端預測座標
					if (_clientPredictedPlayerX >= 0 && _clientPredictedPlayerY >= 0)
					{
						int pdx = Math.Abs(x - _clientPredictedPlayerX);
						int pdy = Math.Abs(y - _clientPredictedPlayerY);
						int pdiff = Math.Max(pdx, pdy);
						GD.Print($"[Pos-Verify] Server({x},{y}) vs Predicted({_clientPredictedPlayerX},{_clientPredictedPlayerY}) diff={pdiff}");
					}
					
					UpdateMoveDebugOverlay($"Confirm ({x},{y})");

					long currentTime = (long)Time.GetTicksMsec();
					if (_lastMovePacketTime > 0)
					{
						long timeSinceLastPacket = currentTime - _lastMovePacketTime;
						
						// 【關鍵驗證】檢查服務器確認移動包的時間間隔
						// 如果間隔 > 640ms * 2，說明可能丟包或延遲
						if (timeSinceLastPacket > 1280)
						{
							GD.PrintErr($"[Move-Confirm-Error] Server confirmed player move interval too long! ({x},{y}) client predicted: ({entity.MapX},{entity.MapY}) timeSinceLastPacket={timeSinceLastPacket}ms (可能丟包，服務器可能不知道玩家最新座標)");
						}
						else
						{
							GD.Print($"[Move-Confirm] Server confirmed player move: ({x},{y}) client predicted: ({entity.MapX},{entity.MapY}) timeSinceLastPacket={timeSinceLastPacket}ms");
						}
					}
					else
					{
						GD.Print($"[Move-Confirm] Server confirmed player move: ({x},{y}) client predicted: ({entity.MapX},{entity.MapY}) (first confirmation)");
					}
					_lastMovePacketTime = currentTime;
				}
				
				// 記錄移動包信息
				GD.Print($"[Move-Packet] Received S_ObjectMoving objectId={objectId} nextPos=({x},{y}) heading={heading} currentPos=({entity.MapX},{entity.MapY})");
				
				// 【診斷日誌】如果是怪物移動包，記錄詳細信息
				if (objectId != _myPlayer?.ObjectId)
				{
					int monsterDistanceToPlayer = GetEntityDistance(x, y);
					
					// 【關鍵診斷】記錄怪物收到移動包的時間戳
					long currentTime = (long)Time.GetTicksMsec();
					long timeSinceLastMove = 0;
					if (_lastMonsterMoveTime.ContainsKey(objectId) && _lastMonsterMoveTime[objectId] > 0)
					{
						timeSinceLastMove = currentTime - _lastMonsterMoveTime[objectId];
					}
					_lastMonsterMoveTime[objectId] = currentTime;
					
					GD.Print($"[Move-Diag] Monster {objectId} received move packet:");
					GD.Print($"  From: ({entity.MapX},{entity.MapY}) To: ({x},{y})");
					GD.Print($"  Distance to player: {monsterDistanceToPlayer} cells");
					GD.Print($"  Player at: ({_myPlayer?.MapX},{_myPlayer?.MapY})");
					GD.Print($"  Time since last move: {timeSinceLastMove}ms (0=first move packet)");
				}
				
				// 處理完畢，直接返回，不要執行下面的緩存邏輯
				return;
			}
			
			// 2. 只有找不到實體時，才執行距離判斷和緩存邏輯
			// 【診斷日誌】記錄找不到實體的情況，用於排查傷害為0的問題
			// 【核心修復】對齊服務器邏輯：服務器使用 Euclidean distance 和 14格過濾
			// 服務器代碼：L1Object.updateObject() 使用 getDistance(o, 14) 判斷是否發送 S_ObjectMoving
			// 客戶端必須使用相同的距離計算方法（Euclidean distance）和相同的過濾範圍（14格）
			int distanceToPlayer = 0;
			if (_myPlayer != null)
			{
				long dx = x - _myPlayer.MapX;
				long dy = y - _myPlayer.MapY;
				distanceToPlayer = (int)Math.Sqrt(dx * dx + dy * dy); // Euclidean distance（對齊服務器邏輯）
			}
			
			// 【關鍵修復】如果距離玩家較近（<=14格），緩存移動包，等待實體創建後應用
			// 這解決了時序問題：服務器可能先發送移動包，然後才發送創建包
			// 使用 14格 對齊服務器邏輯（服務器使用 getDistance(o, 14)）
			if (distanceToPlayer <= 14)
			{
				// 【關鍵修復】更新緩存的移動包（如果已存在，使用最新的）
				_pendingMovePackets[objectId] = (x, y, heading);
				GD.Print($"[Move-Cache] ObjId={objectId} entity not found but within range (dist={distanceToPlayer} <= 14), caching move packet: ({x},{y}) heading={heading}");
			}
			else
			{
				GD.Print($"[Pos-Sync-Warn] ObjID:{objectId} Server_Grid:({x},{y}) heading={heading} Entity_NOT_FOUND distanceToPlayer={distanceToPlayer} (超出14格範圍，不緩存移動包)");
			}
		}

		/// <summary>Op35 單體魔法傷害：依封包 magicFlag==6 判定，與 actionId 無關；變身時 action 可能非 17/18/19。</summary>
		private void OnObjectMagicDamage35(int attackerId, int targetId, int damage)
		{
			// 【核心修復】檢查攻擊者是否存在且未死亡
			// 如果攻擊者不存在或已死亡，跳過傷害結算
			// 正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0
			if (attackerId > 0 && _entities.TryGetValue(attackerId, out var attacker))
			{
				// 檢查攻擊者是否已死亡
				if (attacker.IsDead)
				{
					GD.Print($"[Combat-Fix] OnObjectMagicDamage35: Attacker {attackerId} is dead (IsDead=true), skipping damage");
					return; // 完全跳過，不結算傷害
				}
			}
			else if (attackerId > 0)
			{
				// 攻擊者實體不存在，跳過傷害結算
				GD.Print($"[Combat-Fix] OnObjectMagicDamage35: Attacker {attackerId} entity not found, skipping damage");
				return; // 完全跳過，不結算傷害
			}
			
			// 【新增】戰鬥統計：開始戰鬥
			_combatStats.StartCombat();
			
			// 【優化】判斷是否為玩家攻擊（用於統計）
			bool isPlayerAttack = (_myPlayer != null && attackerId == _myPlayer.ObjectId);
			bool isMagic = true; // Op35 魔法傷害
			bool isCritical = false; // TODO: 從服務器包中獲取暴擊標記
			
			// 只有當攻擊者存在且未死亡時，才結算傷害
			if (targetId > 0)
			{
				// 【優化】記錄玩家魔法攻擊統計
				if (isPlayerAttack)
				{
					_combatStats.RecordDamageDealt(damage, isCritical, damage <= 0, false, false);
				}
				// 【優化】使用新的 HandleEntityAttackHit 方法，支持傷害類型
				HandleEntityAttackHit(targetId, damage, isCritical, isMagic);
			}
		}

		/// <summary>
		/// 【優化】處理攻擊包（支持戰鬥統計和傷害類型判斷）
		/// </summary>
		private void OnObjectAttacked(int attackerId, int targetId, int actionId, int damage)
		{
			GD.Print($"[Combat-Packet] Received Op35 attacker={attackerId} target={targetId} action={actionId} damage={damage}");
			
			// 【新增】戰鬥統計：開始戰鬥
			_combatStats.StartCombat();
			
			// 【優化】判斷是否為玩家攻擊（用於統計）
			bool isPlayerAttack = (_myPlayer != null && attackerId == _myPlayer.ObjectId);
			
			// 【優化】簡單的暴擊判斷（傷害值異常高時可能是暴擊）
			// 注意：服務器沒有明確的暴擊標記，這裡使用啟發式判斷
			// 可以通過比較傷害值與預期傷害範圍來判斷（需要更多數據支持）
			// TODO: 從服務器包中獲取暴擊標記，或通過傷害值判斷
			bool isMagic = (actionId == 18 || actionId == 19); // 魔法動作ID
			if (attackerId == _myPlayer.ObjectId)
			{
				_attackInProgress = false;
			}

			// 傷害結算：Op35 單體魔法已由 ObjectMagicDamage35 結算，此處僅做動畫 + 物理 PrepareAttack
			bool isOp35Magic = PacketHandlerRef != null && PacketHandlerRef.LastOp35WasMagic;
			
			// 【核心修復】檢查攻擊者是否存在且未死亡
			// 如果攻擊者不存在或已死亡，完全跳過處理（包括傷害結算）
			// 正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0
			if (_entities.TryGetValue(attackerId, out var attacker))
			{
				// 檢查攻擊者是否已死亡
				if (attacker.IsDead)
				{
					GD.Print($"[Combat-Fix] Attacker {attackerId} is dead (IsDead=true), skipping attack and damage");
					return; // 完全跳過，不結算傷害
				}
			}
			else if (attackerId > 0)
			{
				// 攻擊者實體不存在，完全跳過處理（包括傷害結算）
				GD.Print($"[Combat-Fix] Attacker {attackerId} entity not found, skipping attack and damage");
				return; // 完全跳過，不結算傷害
			}
			
			// 【核心修復】只有當攻擊者存在且未死亡時，才處理攻擊動畫和傷害結算
			if (attacker != null)
			{
				attacker.SetAction(actionId);
				
				// 【修復】如果是怪物攻擊玩家，調整朝向面向目標（使用最新的玩家座標）
				// 由於客戶端預測移動時會立即更新 _serverConfirmedPlayerX/Y，所以它始終是最新的
				if (targetId > 0 && targetId == _myPlayer?.ObjectId)
				{
					// 【座標同步偵探】優先使用攻擊包中服務器認為的玩家座標
					// 如果最近一次攻擊包中有座標信息，使用它；否則使用 _serverConfirmedPlayerX/Y
					int targetX = _myPlayer.MapX;
					int targetY = _myPlayer.MapY;
					if (_lastAttackServerPlayerPos.TryGetValue(attackerId, out var serverPos))
					{
						targetX = serverPos.x;
						targetY = serverPos.y;
						_lastAttackServerPlayerPos.Remove(attackerId); // 使用後清除
					}
					else
					{
						// 如果沒有攻擊包座標，使用服務器確認的座標
						targetX = _serverConfirmedPlayerX >= 0 ? _serverConfirmedPlayerX : _myPlayer.MapX;
						targetY = _serverConfirmedPlayerY >= 0 ? _serverConfirmedPlayerY : _myPlayer.MapY;
					}
					
				// 【座標同步偵探】記錄服務器認為的玩家座標
				int clientX = _myPlayer.MapX;
				int clientY = _myPlayer.MapY;
				GD.Print($"[Server-Audit] Server thinks I am at: ({targetX},{targetY}) | My Current: ({clientX},{clientY})");
				
				// 調整攻擊者朝向
				int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
				attacker.SetHeading(newHeading);
				
				// 【關鍵修復】根據攻擊類型動態判斷攻擊範圍（對齊服務器 Areaatk）
				// 服務器使用 Areaatk 判斷攻擊範圍（近戰=2，遠程=12）
				// 客戶端沒有 Areaatk 信息，但可以根據 actionId 判斷攻擊類型
				// 【核心修復】攻擊動畫應該播放，只要距離在範圍內，不管 damage 是否為 0
				// damage=0（MISS）只影響目標是否播放受擊動畫，不應該影響攻擊者是否播放攻擊動畫
				int dist = GetGridDistance(attacker.MapX, attacker.MapY, targetX, targetY);
				int attackRange = GetMonsterAttackRange(actionId); // 根據 actionId 判斷攻擊範圍
				bool shouldPlayAttack = dist <= attackRange; // 只檢查距離，不檢查 damage
				
				// 【關鍵驗證】記錄怪物攻擊時的座標信息，用於診斷丟包問題
				// 如果怪物攻擊時使用的玩家座標與客戶端預測座標差距 > 2格，說明可能丟包
				int playerX = _myPlayer.MapX;
				int playerY = _myPlayer.MapY;
				int coordDiff = Math.Max(Math.Abs(targetX - playerX), Math.Abs(targetY - playerY));
					
					// 【診斷日誌】記錄怪物攻擊時的詳細信息，用於分析故障
					// 1. 怪物位置
					// 2. 服務器認為的玩家位置（targetX, targetY）
					// 3. 客戶端預測的玩家位置（playerX, playerY）
					// 4. 服務器確認的玩家位置（_serverConfirmedPlayerX, _serverConfirmedPlayerY）
					// 5. 距離和攻擊範圍
					// 6. 怪物是否在移動（檢查是否有 ACT_WALK 動作）
					// 7. 攻擊時間戳，用於追蹤攻擊間隔
					int monsterX = attacker.MapX;
					int monsterY = attacker.MapY;
					int monsterCurrentAction = attacker.CurrentAction;
					int monsterVisualBaseAction = attacker.VisualBaseAction;
					bool isMonsterWalking = monsterCurrentAction == (GameEntity.ACT_WALK + monsterVisualBaseAction);
					
					// 【關鍵診斷】記錄攻擊時間戳，用於追蹤攻擊間隔和移動包缺失
					long currentTime = (long)Time.GetTicksMsec();
					if (!_lastMonsterAttackTime.ContainsKey(attackerId))
					{
						_lastMonsterAttackTime[attackerId] = 0;
					}
					long timeSinceLastAttack = _lastMonsterAttackTime[attackerId] > 0 ? currentTime - _lastMonsterAttackTime[attackerId] : 0;
					_lastMonsterAttackTime[attackerId] = currentTime;
					
					// 【關鍵診斷】記錄怪物最後一次收到移動包的時間
					if (!_lastMonsterMoveTime.ContainsKey(attackerId))
					{
						_lastMonsterMoveTime[attackerId] = 0;
					}
					long timeSinceLastMove = _lastMonsterMoveTime[attackerId] > 0 ? currentTime - _lastMonsterMoveTime[attackerId] : 0;
					
					GD.Print($"[Combat-Diag] Monster {attackerId} attacking player:");
					GD.Print($"  Monster position: ({monsterX},{monsterY}) action={monsterCurrentAction} isWalking={isMonsterWalking}");
					GD.Print($"  Server thinks player at: ({targetX},{targetY})");
					GD.Print($"  Client predicted player at: ({playerX},{playerY})");
					GD.Print($"  Server confirmed player at: ({_serverConfirmedPlayerX},{_serverConfirmedPlayerY})");
					GD.Print($"  Coord diff: {coordDiff} dist={dist} range={attackRange} damage={damage}");
					GD.Print($"  Time since last attack: {timeSinceLastAttack}ms");
					GD.Print($"  Time since last move packet: {timeSinceLastMove}ms (0=never received)");
					
					// 【關鍵診斷】如果怪物距離 > 攻擊範圍，但沒有收到移動包，記錄警告
					if (dist > attackRange && timeSinceLastMove > 2000)
					{
						GD.PrintErr($"[Combat-Error] Monster {attackerId} should move but NO move packet received!");
						GD.PrintErr($"  Monster at: ({monsterX},{monsterY}) Player at: ({playerX},{playerY}) Distance: {dist} > Range: {attackRange}");
						GD.PrintErr($"  Last move packet: {timeSinceLastMove}ms ago (服務器可能沒有發送移動包，或怪物AI沒有調用StartMove)");
					}
					
					// 【核心修復】如果座標不同步（差距 > 2格），必須馬上進入獲得座標的流程
					// 使用服務器發送的座標（targetX, targetY）更新玩家座標，然後在新座標播放攻擊
					// 這是獲得新座標的最合理方式：服務器發送的攻擊包中包含目標座標，這是服務器認為的玩家座標
					if (coordDiff > 2)
					{
						GD.PrintErr($"[Combat-Coord-Error] Monster {attackerId} attacking player at ({targetX},{targetY}) but client predicted player at ({playerX},{playerY}) diff={coordDiff} (可能丟包，服務器可能不知道玩家最新座標)");
						GD.PrintErr($"[Combat-Coord-Error] 這說明玩家座標沒有及時傳給服務器，或者服務器沒有更新玩家座標");
						
						// 【獲得座標流程】使用服務器發送的座標更新玩家座標
						// 服務器發送的攻擊包中包含目標座標，這是服務器認為的玩家座標
						// 更新玩家座標到服務器座標，然後在新座標播放攻擊
						_serverConfirmedPlayerX = targetX;
						_serverConfirmedPlayerY = targetY;
						_myPlayer.SetMapPosition(targetX, targetY, _myPlayer.Heading);
						GD.Print($"[Combat-Coord-Fix] Updated player position to server coordinate: ({targetX},{targetY}) (was: ({playerX},{playerY}))");
						
						// 重新計算距離和攻擊範圍（使用更新後的座標）
						dist = GetGridDistance(attacker.MapX, attacker.MapY, targetX, targetY);
						shouldPlayAttack = dist <= attackRange; // 只檢查距離，不檢查 damage
					}
					else
					{
						GD.Print($"[Combat-Coord] Monster {attackerId} attacking player at ({targetX},{targetY}) client predicted: ({playerX},{playerY}) diff={coordDiff} dist={dist} range={attackRange}");
					}
					
					if (shouldPlayAttack && !isOp35Magic)
					{
						// 【核心修復】即使 damage=0（MISS），也應該播放攻擊動畫
						// damage=0 只影響目標是否播放受擊動畫，不應該影響攻擊者是否播放攻擊動畫
						attacker.PrepareAttack(targetId, damage);
						GD.Print($"[HitChain] PrepareAttack attacker={attackerId} target={targetId} damage={damage} dist={dist} range={attackRange}");
					}
					else if (!shouldPlayAttack)
					{
						// 【核心修復】當收到攻擊包但距離 > 攻擊範圍時，必須馬上進入獲得座標的流程
						// 更新座標，然後在新座標播放攻擊
						// 如果座標不對，要進入獲得座標的流程，用最合理方式獲得新座標，而不是原地不動
						
						// 【診斷日誌】記錄怪物應該移動但沒有移動的情況
						// 檢查怪物是否應該移動（距離 > 攻擊範圍），但沒有收到移動包
						GD.Print($"[Combat-Diag] Monster {attackerId} should move but not moving:");
						GD.Print($"  Monster at: ({monsterX},{monsterY}) Player at: ({playerX},{playerY})");
						GD.Print($"  Distance: {dist} Attack range: {attackRange}");
						GD.Print($"  Monster current action: {monsterCurrentAction} isWalking: {isMonsterWalking}");
						GD.Print($"  Server thinks player at: ({targetX},{targetY})");
						GD.Print($"  Waiting for server move packet (S_ObjectMoving) for monster {attackerId}");
						
						if (dist > attackRange && damage > 0)
						{
							// 【獲得座標流程】當距離 > 攻擊範圍時，說明座標可能不同步
							// 使用服務器發送的座標（targetX, targetY）更新玩家座標
							// 這是獲得新座標的最合理方式：服務器發送的攻擊包中包含目標座標
							if (coordDiff > 0)
							{
								// 座標不同步，更新玩家座標到服務器座標
								_serverConfirmedPlayerX = targetX;
								_serverConfirmedPlayerY = targetY;
								_myPlayer.SetMapPosition(targetX, targetY, _myPlayer.Heading);
								GD.Print($"[Combat-Coord-Fix] Distance > range, updated player position to server coordinate: ({targetX},{targetY}) (was: ({playerX},{playerY}))");
								
								// 重新計算距離（使用更新後的座標）
								dist = GetGridDistance(attacker.MapX, attacker.MapY, targetX, targetY);
								shouldPlayAttack = dist <= attackRange; // 只檢查距離，不檢查 damage
								
								// 如果更新座標後距離 <= 攻擊範圍，播放攻擊動畫
								if (shouldPlayAttack && !isOp35Magic)
								{
									attacker.PrepareAttack(targetId, damage);
									GD.Print($"[HitChain] PrepareAttack (after coord fix) attacker={attackerId} target={targetId} damage={damage} dist={dist} range={attackRange}");
								}
								else
								{
									// 【關鍵修復】更新座標後仍然距離 > 攻擊範圍，怪物應該繼續移動追擊
									// 不要設置 ACT_BREATH，因為這會讓怪物停止移動
									// 保持當前動作（可能是 ACT_WALK），等待服務器移動包更新位置
									// 只調整朝向，不改變動作，確保怪物能夠繼續移動
									GD.Print($"[Combat-Fix] Monster {attackerId} too far (dist={dist} > range={attackRange}) after coord fix, keeping current action. Will wait for server move packet to continue movement.");
									GD.Print($"[Combat-Diag] Monster {attackerId} should receive S_ObjectMoving packet to move from ({monsterX},{monsterY}) towards player at ({targetX},{targetY})");
								}
							}
							else
							{
								// 【關鍵修復】座標同步，但距離 > 攻擊範圍，怪物應該繼續移動追擊
								// 不要設置 ACT_BREATH，因為這會讓怪物停止移動
								// 保持當前動作（可能是 ACT_WALK），等待服務器移動包更新位置
								// 只調整朝向，不改變動作，確保怪物能夠繼續移動
								GD.Print($"[Combat-Fix] Monster {attackerId} too far (dist={dist} > range={attackRange}), keeping current action. Will wait for server move packet to continue movement.");
								GD.Print($"[Combat-Diag] Monster {attackerId} should receive S_ObjectMoving packet to move from ({monsterX},{monsterY}) towards player at ({targetX},{targetY})");
							}
						}
						else
						{
							// 【關鍵修復】damage <= 0，怪物應該繼續移動追擊
							// 不要設置 ACT_BREATH，因為這會讓怪物停止移動
							// 保持當前動作（可能是 ACT_WALK），等待服務器移動包更新位置
							// 只調整朝向，不改變動作，確保怪物能夠繼續移動
							GD.Print($"[Combat-Fix] Monster {attackerId} too far (dist={dist} > range={attackRange}) or damage={damage} <= 0, keeping current action. Will wait for server move packet to continue movement.");
							GD.Print($"[Combat-Diag] Monster {attackerId} should receive S_ObjectMoving packet to move from ({monsterX},{monsterY}) towards player at ({targetX},{targetY})");
						}
					}
					
					GD.Print($"[Combat-Fix] Monster {attackerId} attacking player at ({targetX},{targetY}) (client-predicted:({_myPlayer.MapX},{_myPlayer.MapY})), adjusted heading to {newHeading}");
				}
				else if (targetId > 0 && _entities.TryGetValue(targetId, out var targetEntity))
				{
					// 目標是其他實體，使用當前座標
					int targetX = targetEntity.MapX;
					int targetY = targetEntity.MapY;
					
					int newHeading = GetHeading(attacker.MapX, attacker.MapY, targetX, targetY, attacker.Heading);
					attacker.SetHeading(newHeading);
					
					// 【關鍵修復】如果距離太遠（>2格），不播放攻擊動畫
					// 服務器發送攻擊包時，如果距離太遠，不應該播放攻擊動畫
					// 因為怪物應該先移動到攻擊範圍內，然後再攻擊
					// 【核心修復】攻擊動畫應該播放，只要距離在範圍內，不管 damage 是否為 0
					// damage=0（MISS）只影響目標是否播放受擊動畫，不應該影響攻擊者是否播放攻擊動畫
					int dist = GetGridDistance(attacker.MapX, attacker.MapY, targetX, targetY);
					bool shouldPlayAttack = dist <= 2; // 只檢查距離，不檢查 damage
					
					if (shouldPlayAttack && !isOp35Magic)
					{
						attacker.PrepareAttack(targetId, damage);
						GD.Print($"[HitChain] PrepareAttack attacker={attackerId} target={targetId} damage={damage} dist={dist}");
					}
					else if (!shouldPlayAttack)
					{
						GD.Print($"[Combat-Fix] Monster {attackerId} too far (dist={dist} > 2), skipping attack animation. Will wait for server move packet.");
					}
					else if (damage <= 0)
					{
						// damage=0（MISS）時，攻擊動畫已播放，但目標不播放受擊動畫
						GD.Print($"[Combat-Fix] Monster {attackerId} attack MISS (damage={damage}), attack animation played but target won't play damage animation.");
					}
					
					GD.Print($"[Combat-Fix] Monster {attackerId} attacking target {targetId} at ({targetX},{targetY}), adjusted heading to {newHeading}");
				}
				else if (targetId > 0 && !isOp35Magic)
				{
					// 目標實體不存在，但攻擊包已收到，仍然播放攻擊動畫（可能是目標已死亡或離開視野）
					attacker.PrepareAttack(targetId, damage);
					GD.Print($"[HitChain] PrepareAttack attacker={attackerId} target={targetId} damage={damage} (target entity not found)");
				}
			}
			// 【核心修復】刪除此分支：如果攻擊者不存在或已死亡，不應結算傷害
			// 原代碼：else if (targetId > 0 && !isOp35Magic) HandleEntityAttackHit(targetId, damage);
			// 這會導致攻擊者不存在或已死亡時，傷害仍然被結算，造成玩家血量瘋狂減少

			// 【刪除】被攻擊者不需要調整朝向，只要被動等待攻擊即可
			// 攻擊者發起攻擊時，只要面對被攻擊者就可以，無論被攻擊者的朝向在哪裡
			// 如果被攻擊者改變了方向，攻擊者不需要跟著改變朝向
		}

		/// <summary>Op57 魔法封包：封包結算傷害（立即 HandleEntityAttackHit），不經 keyframe；視覺僅由 OnMagicVisualsReceived 處理，此處不重複 SpawnEffect。</summary>
		/// <param name="x">攻擊者座標X（服務器確認的座標）</param>
		/// <param name="y">攻擊者座標Y（服務器確認的座標）</param>
		private void OnObjectMagicAttacked(int attackerId, int targetId, int gfxId, int damage, int x, int y)
		{
			// 【核心原則】Op57 包中的 x, y 是攻擊者座標（服務器確認的座標）
			// 服務器開發者的設計思路：每次攻擊都是一個位置更新的機會
			// 客戶端應該從每一個包含座標的包中提取位置信息並更新
			// 無論是玩家、怪物、其他玩家，都要更新位置，保持位置永遠最新
			
			// 【關鍵修復】如果是玩家自己釋放魔法，且差距 <= 2格，不更新位置
			// 原因：玩家剛發送位置更新包，服務器可能還沒處理完，返回的是舊位置
			// 如果差距 <= 2格，這是正常的誤差，不應該更新位置，避免玩家被"拉回"
			if (attackerId == _myPlayer?.ObjectId && _entities.TryGetValue(attackerId, out var playerEntity))
			{
				int currentX = playerEntity.MapX;
				int currentY = playerEntity.MapY;
				int diffX = Math.Abs(x - currentX);
				int diffY = Math.Abs(y - currentY);
				int diff = Math.Max(diffX, diffY);
				
				if (diff <= 2)
				{
					// 差距 <= 2格，不更新位置，保持客戶端預測
					// 這是正常的誤差（可能是服務器處理延遲），不應該更新位置
					GD.Print($"[Pos-Sync-Fix] Op57: Player magic attack, position diff={diff} <= 2, KEEPING client prediction. Server:({x},{y}) Client:({currentX},{currentY}) - Player will NOT be moved");
					// 不更新位置，但更新服務器確認的座標（記錄服務器發送的值）
					_serverConfirmedPlayerX = x;
					_serverConfirmedPlayerY = y;
				}
				else
				{
					// 差距 > 2格，使用統一函數更新位置（可能是真實的位置變化）
					UpdateEntityPositionFromServer(attackerId, x, y, -1, "Op57");
				}
			}
			else
			{
				// 不是玩家自己（是怪物或其他玩家），使用統一函數更新位置
				UpdateEntityPositionFromServer(attackerId, x, y, -1, "Op57");
			}
			
			// 【新增】戰鬥統計：開始戰鬥
			_combatStats.StartCombat();
			
			// 【優化】判斷是否為玩家攻擊（用於統計）
			bool isPlayerAttack = (_myPlayer != null && attackerId == _myPlayer.ObjectId);
			bool isMagic = true; // Op57 魔法攻擊
			bool isCritical = false; // TODO: 從服務器包中獲取暴擊標記
			
			if (_entities.TryGetValue(attackerId, out var attacker))
				attacker.SetAction(GameEntity.ACT_SPELL_DIR);
			// 魔法原則：本地先播效果、封包結算傷害；Op57 一律在此立即結算，不經 PrepareAttack/keyframe
			if (targetId > 0)
			{
				// 【優化】記錄玩家魔法攻擊統計
				if (isPlayerAttack)
				{
					_combatStats.RecordDamageDealt(damage, isCritical, damage <= 0, false, false);
				}
				// 【優化】使用新的 HandleEntityAttackHit 方法，支持傷害類型
				HandleEntityAttackHit(targetId, damage, isCritical, isMagic);
			}
			// 視覺特效由 OnMagicVisualsReceived 統一生成（含己方跳過重複、AOE 落點等），此處不再 SpawnEffect，避免龍捲風等群體魔法重複播放

			// 【刪除】被攻擊者不需要調整朝向，只要被動等待攻擊即可
			// 攻擊者發起攻擊時，只要面對被攻擊者就可以，無論被攻擊者的朝向在哪裡
			// 如果被攻擊者改變了方向，攻擊者不需要跟著改變朝向
		}
		
		// 響應服務端 S_HpMeter (Opcode 128)，更新世界對象的血條比例。僅依伺服器數據，不依 list.spr。
		private void OnObjectHitRatio(int objectId, int ratio)
		{
			if (!_entities.TryGetValue(objectId, out var entity)) return;
			// ratio 255 (0xFF) = 伺服器表示無血條，記為 -1 並隱藏
			if (ratio == 255)
			{
				entity.SetHpRatio(-1);
				return;
			}
			entity.SetHpRatio(ratio);
			if (entity == _myPlayer)
				entity.SetHealthBarVisible(ratio < 100);
			else
				entity.SetHealthBarVisible(ClientConfig.ShowMonsterHealthBar);
		}

		/// <summary>依設定刷新所有怪物頭頂血條顯示/隱藏。OptionsWindow 開關變更時呼叫。僅有伺服器血條數據者會顯示。</summary>
		public void RefreshMonsterHealthBars()
		{
			foreach (var kv in _entities)
			{
				var e = kv.Value;
				if (e == null || e == _myPlayer) continue;
				e.SetHealthBarVisible(e.HasServerHp() && ClientConfig.ShowMonsterHealthBar);
			}
		}
	}
}
