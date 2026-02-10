// File: Client/Game/GameWorld.SkillEffect.cs
using Godot;
using System;
using Client.Network;
using Client.Data;
using Client.Utility; // 引用 ListSprLoader


namespace Client.Game
{
	public partial class GameWorld
	{
		// 注意：_effectLayer 字段定义已在主文件 GameWorld.cs 中，此处不再重复定义

		// 【先播放後結算】己方施放魔法記錄：(gfxId, 時間戳)，Op57 收到時若為己方剛施放則跳過重複 SpawnEffect
		private static readonly System.Collections.Generic.List<(int gfxId, ulong time)> _recentSelfMagicCasts = new System.Collections.Generic.List<(int, ulong)>();
		private const ulong SelfMagicCastWindowMs = 2500;
		// 【飛行魔法】己方在 UseMagic 已播飛行段時記錄，Op35 收到時跳過重複
		private static readonly System.Collections.Generic.List<(int gfxId, ulong time)> _recentSelfFlyingCasts = new System.Collections.Generic.List<(int, ulong)>();

		// ========================================================================
		//   初始化与绑定 (Init & Bind)
		// ========================================================================

		/// <summary>
		/// 初始化特效系统 (需在 GameWorld._Ready 中调用)
		/// </summary>
		private void InitEffectSystem()
		{
			if (_effectLayer == null)
			{
				_effectLayer = new Node2D();
				_effectLayer.Name = "EffectLayer";
				// ZIndex 20 确保显示在角色(通常Z=0或1)和尸体上方，但在UI下方
				_effectLayer.ZIndex = 20; 
				AddChild(_effectLayer);
			}
			GD.Print("[System] EffectLayer Initialized.");
		}

		// ========================================================================
		//   核心功能 (Core Logic)远程投射物 (光箭/弓箭)
		// 【重要】光箭 Gfx167 魔法正常測試通過，功能正常，不要再修改。
		// ========================================================================
		
		/// <summary>
		/// 响应远程攻击信号，生成飞行特效
		/// </summary>
		/// <param name="attackerId">攻击者ID</param>
		/// <param name="targetId">目标ID</param>
		/// <param name="gfxId">List.spr 中的编号</param>
		/// <param name="sx">起点GridX</param>
		/// <param name="sy">起点GridY</param>
		/// <param name="dx">终点GridX</param>
		/// <param name="dy">终点GridY</param>
		/// <param name="position">世界坐标</param>
		/// <param name="heading">方向 (很重要！光箭需要方向)</param>

		private void OnRangeAttackReceived(int attackerId, int targetId, int gfxId, int sx, int sy, int dx, int dy)
		{
			if (_skinBridge == null) return;

			// 檢查攻擊者是否存在且未死亡
			// 正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0
			if (attackerId > 0 && _entities.TryGetValue(attackerId, out var attackerEntity))
			{
				// 檢查攻擊者是否已死亡
				if (attackerEntity.IsDead)
				{
					GD.Print($"[Combat-Fix] Attacker {attackerId} is dead (IsDead=true), skipping range attack");
					return;
				}
			}
			else if (attackerId > 0)
			{
				// 攻擊者實體不存在，跳過處理
				GD.Print($"[Combat-Fix] Attacker {attackerId} entity not found, skipping range attack");
				return;
			}

			// 【核心原則】Op35 包中的 sx, sy 是攻擊者的座標（服務器確認的座標）
			// 服務器開發者的設計思路：每次攻擊都是一個位置更新的機會
			// 客戶端應該從每一個包含座標的包中提取位置信息並更新
			// 無論是玩家、怪物、其他玩家，都要更新位置，保持位置永遠最新
			
			// 【關鍵修復】如果是玩家自己釋放遠程攻擊（弓箭/魔法），且差距 <= 2格，不更新位置
			// 原因：玩家剛發送位置更新包，服務器可能還沒處理完，返回的是舊位置
			// 如果差距 <= 2格，這是正常的誤差，不應該更新位置，避免玩家被"拉回"
			if (attackerId == _myPlayer?.ObjectId && _entities.TryGetValue(attackerId, out var playerEntity))
			{
				int currentX = playerEntity.MapX;
				int currentY = playerEntity.MapY;
				int diffX = Math.Abs(sx - currentX);
				int diffY = Math.Abs(sy - currentY);
				int diff = Math.Max(diffX, diffY);
				
				if (diff <= 2)
				{
					// 差距 <= 2格，不更新位置，保持客戶端預測
					// 這是正常的誤差（可能是服務器處理延遲），不應該更新位置
					GD.Print($"[Pos-Sync-Fix] Op35 RangeAttack: Player range attack, position diff={diff} <= 2, KEEPING client prediction. Server:({sx},{sy}) Client:({currentX},{currentY}) - Player will NOT be moved");
					// 不更新位置，但更新服務器確認的座標（記錄服務器發送的值）
					_serverConfirmedPlayerX = sx;
					_serverConfirmedPlayerY = sy;
				}
				else
				{
					// 差距 > 2格，使用統一函數更新位置（可能是真實的位置變化）
					UpdateEntityPositionFromServer(attackerId, sx, sy, -1, "Op35 RangeAttack");
				}
			}
			else
			{
				// 不是玩家自己（是怪物或其他玩家），使用統一函數更新位置
				UpdateEntityPositionFromServer(attackerId, sx, sy, -1, "Op35 RangeAttack");
			}
			
			// 【統一位置更新】更新目標位置（如果目標座標與實體位置不一致）
			// 注意：目標座標（dx, dy）是服務器確認的目標位置，但我們優先使用實體的實際位置
			// 只有在實體不存在或位置差距很大時，才使用服務器發送的目標座標
			if (targetId > 0 && _entities.TryGetValue(targetId, out var targetEntity))
			{
				int currentX = targetEntity.MapX;
				int currentY = targetEntity.MapY;
				int diffX = Math.Abs(dx - currentX);
				int diffY = Math.Abs(dy - currentY);
				int diff = Math.Max(diffX, diffY);
				
				// 【座標同步偵探】如果目標是玩家自己，保存服務器認為的玩家座標
				if (targetId == _myPlayer?.ObjectId)
				{
					_lastAttackServerPlayerPos[attackerId] = (dx, dy);
					GD.Print($"[Server-Audit] Op35 RangeAttack: Server thinks player at ({dx},{dy}) from attacker {attackerId}");
				}
				
				// 如果目標位置差距很大（>2格），可能是服務器位置更新，更新目標位置
				if (diff > 2)
				{
					string entityType = targetId == _myPlayer?.ObjectId ? "player" : "entity";
					GD.Print($"[Pos-Sync] Op35 RangeAttack updating target {entityType} {targetId} position: server-confirmed ({dx},{dy}) client-current ({currentX},{currentY}) diff={diff}");
					targetEntity.SetMapPosition(dx, dy, targetEntity.Heading);
					
					// 如果是玩家自己，同時更新服務器確認的座標
					if (targetId == _myPlayer?.ObjectId)
					{
						_serverConfirmedPlayerX = dx;
						_serverConfirmedPlayerY = dy;
					}
				}
			}

			// 【飛行魔法】己方已在 UseMagic 播過飛行段，跳過 Op35 重複
			if (attackerId > 0 && _myPlayer != null && attackerId == _myPlayer.ObjectId && TryConsumeSelfFlyingCast(gfxId))
			{
				GD.Print($"[Magic][Range] 己方剛播飛行 跳過 Op35 重複 GfxId:{gfxId}");
				return;
			}
			// 【非飛行單體魔法】己方已在 UseMagic 先播放（如極道落雷 gfx=10），跳過 Op35 重複 SpawnEffect，僅結算傷害由 ObjectMagicDamage35 處理
			if (attackerId > 0 && _myPlayer != null && attackerId == _myPlayer.ObjectId && !ListSprLoader.IsAction0Fly(gfxId) && TryConsumeSelfMagicCast(attackerId, gfxId))
			{
				GD.Print($"[Magic][Range] 己方剛施放 跳過 Op35 重複 SpawnEffect GfxId:{gfxId}");
				return;
			}

			// --- 1. 座標定位：優先使用實體 Position，確保從「手中」發出 ---
			Vector2 startPos = (attackerId > 0 && _entities.TryGetValue(attackerId, out var atkEnt)) 
				? atkEnt.Position : ConvertGridToWorld(sx, sy);

			GameEntity tgtEnt = null;
			Vector2 endPos;
			
			// 【關鍵修復】如果目標是玩家自己，使用玩家的實際座標（從實體獲取），而不是服務器發送的座標
			// 服務器發送的目標座標可能是過時的，導致怪物攻擊錯誤的位置
			if (targetId > 0 && targetId == _myPlayer?.ObjectId)
			{
				// 目標是玩家自己，使用玩家的實際座標
				tgtEnt = _myPlayer;
				endPos = _myPlayer.Position;
				
				// 【診斷日誌】記錄座標驗證信息
				int playerMapX = _myPlayer.MapX;
				int playerMapY = _myPlayer.MapY;
				int serverTargetX = dx;
				int serverTargetY = dy;
				int diffX = Math.Abs(serverTargetX - playerMapX);
				int diffY = Math.Abs(serverTargetY - playerMapY);
				
				if (diffX > 1 || diffY > 1)
				{
					GD.Print($"[Magic][Range-Coord-Fix] 目標是玩家，使用實際座標！ServerTarget:({serverTargetX},{serverTargetY}) PlayerActual:({playerMapX},{playerMapY}) Diff:({diffX},{diffY}) -> 使用玩家實際座標");
				}
				else
				{
					GD.Print($"[Magic][Range-Coord-Fix] 目標是玩家，座標一致 ServerTarget:({serverTargetX},{serverTargetY}) PlayerActual:({playerMapX},{playerMapY})");
				}
			}
			else if (targetId > 0 && _entities.TryGetValue(targetId, out tgtEnt))
			{
				// 目標是其他實體，使用實體的實際座標
				endPos = tgtEnt.Position;
				
				// 【診斷日誌】記錄座標驗證信息（僅在座標不一致時記錄）
				int targetMapX = tgtEnt.MapX;
				int targetMapY = tgtEnt.MapY;
				int diffX = Math.Abs(dx - targetMapX);
				int diffY = Math.Abs(dy - targetMapY);
				
				if (diffX > 1 || diffY > 1)
				{
					GD.Print($"[Magic][Range-Coord-Warn] 目標實體座標不一致！ServerTarget:({dx},{dy}) EntityActual:({targetMapX},{targetMapY}) Diff:({diffX},{diffY}) -> 使用實體實際座標");
				}
			}
			else
			{
				// 目標不存在或無效，使用服務器發送的座標
				endPos = ConvertGridToWorld(dx, dy);
				GD.Print($"[Magic][Range-Coord-Warn] 目標實體不存在，使用服務器座標 TargetId:{targetId} ServerTarget:({dx},{dy})");
			}

			// [核心修復] 魔法 167 方向性判定
			// 【關鍵修復】使用實際的起點和終點座標計算方向，而不是服務器發送的座標
			// 如果目標是玩家，使用玩家的實際座標；如果目標是其他實體，使用實體的實際座標
			int heading = 0;
			
			// 計算實際的起點和終點網格座標（用於方向計算）
			int actualStartX = sx;
			int actualStartY = sy;
			int actualEndX = dx;
			int actualEndY = dy;
			
			// 如果攻擊者實體存在，使用實體的實際座標
			if (attackerId > 0 && _entities.TryGetValue(attackerId, out var attackerEnt))
			{
				actualStartX = attackerEnt.MapX;
				actualStartY = attackerEnt.MapY;
			}
			
			// 如果目標實體存在，使用實體的實際座標
			if (tgtEnt != null)
			{
				actualEndX = tgtEnt.MapX;
				actualEndY = tgtEnt.MapY;
			}
			
			// 【診斷日誌】記錄方向計算的座標信息
			if (actualStartX != sx || actualStartY != sy || actualEndX != dx || actualEndY != dy)
			{
				GD.Print($"[Magic][Range-Heading-Fix] 使用實際座標計算方向！ServerStart:({sx},{sy}) ActualStart:({actualStartX},{actualStartY}) ServerEnd:({dx},{dy}) ActualEnd:({actualEndX},{actualEndY})");
			}
			
			if (actualStartX != actualEndX || actualStartY != actualEndY)
			{
				int defaultHeading = (attackerId > 0 && _entities.TryGetValue(attackerId, out var attacker)) ? attacker.Heading : 0;
				heading = GetHeading(actualStartX, actualStartY, actualEndX, actualEndY, defaultHeading);
			}
			else if (attackerId > 0 && _entities.TryGetValue(attackerId, out var attacker))
			{
				heading = attacker.Heading;
			}

			// 【gfx 無 0.fly 不當飛行魔法】例如 skill22 極道落雷 gfx=10 為 0.lightning，應在目標處播特效，不飛行
			if (!ListSprLoader.IsAction0Fly(gfxId))
			{
				SpawnEffect(gfxId, endPos, heading, tgtEnt);
				return;
			}

			int attackerHeading = -1;
			int targetHeading = -1;
			if (attackerId > 0 && _entities.TryGetValue(attackerId, out var atkForLog)) attackerHeading = atkForLog.Heading;
			if (targetId > 0 && _entities.TryGetValue(targetId, out var tgtForLog)) targetHeading = tgtForLog.Heading;
			
			// 【診斷日誌】記錄完整的座標信息，包括服務器發送的和實際使用的
			int actualStartGridX = (attackerId > 0 && _entities.TryGetValue(attackerId, out var atkForGrid)) ? atkForGrid.MapX : sx;
			int actualStartGridY = (attackerId > 0 && _entities.TryGetValue(attackerId, out var atkForGrid2)) ? atkForGrid2.MapY : sy;
			int actualEndGridX = (tgtEnt != null) ? tgtEnt.MapX : dx;
			int actualEndGridY = (tgtEnt != null) ? tgtEnt.MapY : dy;
			
			GD.Print($"[Magic][Range] 飛行魔法 GfxId:{gfxId} Attacker:{attackerId} Target:{targetId} ServerGridStart:({sx},{sy}) ActualGridStart:({actualStartGridX},{actualStartGridY}) ServerGridEnd:({dx},{dy}) ActualGridEnd:({actualEndGridX},{actualEndGridY}) WorldStart:{startPos} WorldEnd:{endPos} 角色朝向:{attackerHeading} 魔法朝向(8方向):{heading} 目標朝向:{targetHeading} 播放位置:起點→終點飛行");

			// 3. 創建特效對象
			var effect = new SkillEffect();
			
			// 加入到专用的 EffectLayer，而不是直接 AddChild，这样方便管理层级
			if (_effectLayer == null) { _effectLayer = new Node2D(); AddChild(_effectLayer); }
			_effectLayer.AddChild(effect);

			// 初始位置设为起点
			effect.Position = startPos;
			
			// 【核心修復】飛行魔法（包括弓箭 gfx=66）應該從起點飛向終點，使用 Tween 控制位置
			// 不應該跟隨目標移動，因為箭已經射出，應該沿著固定軌跡飛行
			// 只有非飛行魔法（如極道落雷）才在目標位置播放，不飛行
			// 弓箭等飛行道具：從發起者位置開始，使用 Tween 飛向目標位置
			bool shouldFollowTarget = false; // 飛行魔法不跟隨目標，由 Tween 控制位置
			
			// 初始化動畫；飛行魔法不跟隨目標，由 Tween 控制從起點到終點的飛行
			effect.Init(gfxId, heading, _skinBridge, _audioProvider, tgtEnt, OnChainEffectTriggered, shouldFollowTarget);

			// 【核心修復】飛行魔法使用 Tween 從起點飛向終點，不跟隨目標移動
			// 這樣可以確保弓箭等飛行道具正確顯示從發起者到目標的飛行軌跡
			float dist = startPos.DistanceTo(endPos);
			float speed = 600.0f; // 飛行速度（像素/秒）
			float duration = Mathf.Max(0.1f, dist / speed);

			var tween = CreateTween();
			tween.TweenProperty(effect, "position", endPos, duration);
			// 不在 Tween 回調中 QueueFree，避免與 SkillEffect 內部計時銷毀重複觸發

			GD.Print($"[Magic][Range] Spawn 飛行 SkillEffect Gfx:{gfxId} Heading:{heading} Duration:{duration:F2}s Dist:{dist:F0} 從起點飛向終點 (不跟隨目標)");
		}

		// ========================================================================
		//   Opcode 57 (魔法攻击 S_ObjectAttackMagic) - 统一收纳
		// ========================================================================
		private void OnMagicVisualsReceived(int attackerId, int targetId, int gfxId, int damage, int targetX, int targetY)
		{
			// 施法者表現（與 Op35 物理攻擊一致，結算時更新動作）
			if (attackerId > 0 && _entities.TryGetValue(attackerId, out var atkForSpell))
				atkForSpell.SetAction(GameEntity.ACT_SPELL_DIR);

			// 傷害結算由 OnObjectMagicAttacked 統一處理（封包結算，不在此重複）

			// 0. 【先播放後結算】己方剛施放的魔法：已在本端 UseMagic 播過主段；跳過重複播主段和連貫段
			// 【核心修復】UseMagic 中已播主段，主段播畢會自動觸發 109.effect 連貫段（通過 chainCallback）
			// Op57 中不應再手動播連貫段，避免重複播放
			if (attackerId > 0 && _myPlayer != null && attackerId == _myPlayer.ObjectId && TryConsumeSelfMagicCast(attackerId, gfxId))
			{
				// 己方剛施放：UseMagic 中已播主段，主段播畢會自動觸發連貫段，Op57 完全跳過，僅結算傷害
				GD.Print($"[Magic][Op57] 己方剛施放 跳過重複播放（UseMagic 已播主段+連貫段） GfxId:{gfxId} Target:{targetId} Damage:{damage}");
				return;
			}

			// 1. 寻找特效落点 (优先级：目标实体 > 网格坐标 > 攻击者位置)
			Vector2 endPos = Vector2.Zero;
			Vector2 attackerPos = Vector2.Zero;
			int heading = 0;
			GameEntity targetEnt = null;

			int targetEntityHeading = -1;
			if (targetId > 0 && _entities.TryGetValue(targetId, out var tgtForLog57)) targetEntityHeading = tgtForLog57.Heading;
			GD.Print($"[Magic][Op57] Received Attacker:{attackerId} Target:{targetId} GfxId:{gfxId} Damage:{damage} TargetGrid:({targetX},{targetY}) 目標實體朝向:{targetEntityHeading}");

			int casterHeading = 0;
			// 获取攻击者位置（用于方向计算）
			if (attackerId > 0 && _entities.TryGetValue(attackerId, out var attackerEnt))
			{
				attackerPos = attackerEnt.Position;
				heading = attackerEnt.Heading;
				casterHeading = attackerEnt.Heading;
			}

			// 确定特效落点
			if (targetId > 0 && _entities.TryGetValue(targetId, out targetEnt))
			{
				endPos = targetEnt.Position;
				GD.Print($"[Magic][Op57] 落點=目標實體 TargetId:{targetId} WorldPos:{endPos}");
			}
			else if (targetX != 0)
			{
				endPos = ConvertGridToWorld(targetX, targetY);
				GD.Print($"[Magic][Op57] 落點=網格座標 Grid:({targetX},{targetY}) WorldPos:{endPos}");
			}
			else if (attackerPos != Vector2.Zero)
			{
				endPos = attackerPos;
				GD.Print($"[Magic][Op57] 落點=攻擊者位置 WorldPos:{endPos}");
			}

			// 2. 【魔法方向性修复】检查 DirectionFlag
			// 如果魔法有方向（DirectionFlag=1），根据攻击者到目标的方向计算 heading
			var def = ListSprLoader.Get(gfxId);
			if (def != null)
			{
				string actionsInfo = "";
				foreach (var kv in def.Actions) actionsInfo += $"{kv.Key}({kv.Value?.Name},DirFlag:{kv.Value?.DirectionFlag}) ";
				GD.Print($"[Magic][Op57] List.spr GfxId:{gfxId} Attr:104={def.Attr} Type:102={def.Type} SpriteId:{def.SpriteId} Actions:[{actionsInfo.Trim()}] EffectChain:{(def.EffectChain.Count > 0 ? string.Join(",", def.EffectChain) : "無")}");
			}
			else
				GD.Print($"[Magic][Op57] List.spr 無定義 GfxId:{gfxId} -> 無法讀取 DirectionFlag/Attr");

			// 【徹底重構】使用統一的座標轉換函數
			// 【AOE 有方向】必須依「每個目標」位置算 heading，不可用封包 (x,y)（伺服器 170/171 送的是施法者座標）
			int attackerGridX = 0, attackerGridY = 0, targetGridX = 0, targetGridY = 0;
			if (attackerPos != Vector2.Zero)
			{
				Vector2I grid = CoordinateSystem.PixelToGrid(attackerPos.X, attackerPos.Y);
				attackerGridX = grid.X;
				attackerGridY = grid.Y;
			}
			if (targetEnt != null && endPos != Vector2.Zero)
			{
				Vector2I grid = CoordinateSystem.PixelToGrid(endPos.X, endPos.Y);
				targetGridX = grid.X;
				targetGridY = grid.Y;
			}
			else if (targetX != 0 || targetY != 0)
			{
				targetGridX = targetX;
				targetGridY = targetY;
			}
			else if (endPos != Vector2.Zero)
			{
				Vector2I grid = CoordinateSystem.PixelToGrid(endPos.X, endPos.Y);
				targetGridX = grid.X;
				targetGridY = grid.Y;
			}

			SprActionSequence action0Seq = null;
			if (def != null) def.Actions.TryGetValue(0, out action0Seq);
			if (targetEnt != null) targetEntityHeading = targetEnt.Heading;
			if (def != null && action0Seq != null && action0Seq.DirectionFlag == 1)
			{
				// 有方向魔法：從施法者到「當前目標」的 8 方向（AOE 時每個目標各自算）
				if (attackerPos != Vector2.Zero && endPos != Vector2.Zero && (attackerGridX != targetGridX || attackerGridY != targetGridY))
				{
					heading = GameWorld.GetHeading(attackerGridX, attackerGridY, targetGridX, targetGridY, casterHeading);
					GD.Print($"[Magic][Op57] 有方向魔法 DirFlag=1 角色朝向:{casterHeading} 目標實體朝向:{targetEntityHeading} 魔法Heading:{heading} AttackerGrid:({attackerGridX},{attackerGridY}) 本目標Grid:({targetGridX},{targetGridY}) (8方向)");
				}
				else
				{
					heading = casterHeading;
					GD.Print($"[Magic][Op57] 有方向魔法 DirFlag=1 起終點同格 使用角色朝向:{heading}");
				}
			}
			else
				GD.Print($"[Magic][Op57] 無方向魔法 使用角色朝向:{heading} (Action0.DirectionFlag={(action0Seq != null ? action0Seq.DirectionFlag : -1)})");

			// 3. 执行生成 (通过统一出口)：Op57 為落點播放（在目標/網格處播，非飛行）
			if (endPos != Vector2.Zero && gfxId > 0)
			{
				GD.Print($"[Magic][Op57] SpawnEffect 落點播放 GfxId:{gfxId} WorldPos:{endPos} Heading:{heading} FollowTarget:{(targetEnt != null ? targetEnt.ObjectId : 0)}");
				SpawnEffect(gfxId, endPos, heading, targetEnt);
			}
			else if (gfxId <= 0 || endPos == Vector2.Zero)
				GD.PrintErr($"[Magic][Op57] SKIP SpawnEffect GfxId:{gfxId} endPos:{endPos}");
		}

		// ========================================================================
		//   Opcode 83 (S_OPCODE_EFFECTLOC) - 地面落點特效
		// ========================================================================
		private void OnEffectAtLocation(int gfxId, int x, int y)
		{
			if (gfxId <= 0) return;

			// 對齊 PacketHandler 中的網格座標 → 世界座標
			Vector2 pos = ConvertGridToWorld(x, y);
			GD.Print($"[Magic][Op83] 地面落點特效 GfxId:{gfxId} Grid:({x},{y}) WorldPos:{pos} Heading:0");
			SpawnEffect(gfxId, pos, 0);
		}

		// ========================================================================
		//   网络回调：Opcode 55 (通用特效 S_ObjectEffect) - 修复重影
		// ========================================================================
		// Opcode 55 通常是无方向的 (Heading 0)，或者跟随角色当前方向
			// 主要用于升级光柱、传送光效、物品拾取闪光等
		private void OnObjectEffectReceived(int objectId, int effectId)
		{
		    GameEntity targetEntity = null;
		    Vector2 pos = Vector2.Zero;
		    int heading = 0;

		    if (objectId == _myPlayer?.ObjectId)
		    {
		        targetEntity = _myPlayer;
		    }
		    else if (_entities.TryGetValue(objectId, out var entity))
		    {
		        targetEntity = entity;
		    }

		    if (targetEntity != null)
		    {
		        pos = targetEntity.GlobalPosition;
		        heading = targetEntity.Heading;
        
		        // 核心修復：傳入 targetEntity 讓特效執行跟隨邏輯
		        SpawnEffect(effectId, pos, heading, targetEntity);
		    }
		}

		// ========================================================================
		//   辅助工具：特效生成唯一入口
		// ========================================================================
		public void SpawnEffect(int gfxId, Vector2 position, int heading = 0, GameEntity followTarget = null)
		{
		    if (_skinBridge == null)
		    {
				GD.PrintErr($"[Magic][SpawnEffect] ABORT _skinBridge=null GfxId:{gfxId} Pos:{position} Heading:{heading} Follow:{(followTarget != null ? followTarget.ObjectId : 0)}");
				return;
		    }
		    
		    // 【診斷】檢查 _audioProvider 是否為 null
		    if (_audioProvider == null)
		    {
				GD.PrintErr($"[Magic][SpawnEffect] ABORT _audioProvider=null GfxId:{gfxId} Pos:{position} - 音效無法播放！請檢查 GameWorld._Ready 中 _audioProvider 是否已初始化");
				return;
		    }

		    var effect = new SkillEffect();
		    if (_effectLayer == null) { InitEffectSystem(); }
		    _effectLayer.AddChild(effect);
		    effect.GlobalPosition = position;

		    GD.Print($"[Magic][SpawnEffect] 創建 SkillEffect GfxId:{gfxId} WorldPos:{position} Heading:{heading} FollowTargetId:{(followTarget != null ? followTarget.ObjectId : 0)} HasAudio:{(_audioProvider != null)} (將呼叫 SkillEffect.Init)");
		    // 傳遞跟隨目標 + 109.effect 连环播放回调
		    effect.Init(gfxId, heading, _skinBridge, _audioProvider, followTarget, OnChainEffectTriggered);
		}
		
		// 【109.effect 连环播放】回调函数
		private void OnChainEffectTriggered(int nextGfxId, Vector2 position, int heading, GameEntity followTarget)
		{
			GD.Print($"[Magic][Chain] 連貫魔法 下一段 GfxId:{nextGfxId} Pos:{position} Heading:{heading} Follow:{(followTarget != null ? followTarget.ObjectId : 0)}");
			SpawnEffect(nextGfxId, position, heading, followTarget);
		}

		/// <summary>
		/// 【先播放後結算】記錄己方剛施放的魔法 gfxId，Op57 收到時可跳過重複 SpawnEffect。
		/// 由 UseMagic 在呼叫 SpawnEffect 前呼叫。
		/// </summary>
		public static void RecordSelfMagicCast(int gfxId)
		{
			ulong now = (ulong)Time.GetTicksMsec();
			_recentSelfMagicCasts.Add((gfxId, now));
			// 清理過期
			for (int i = _recentSelfMagicCasts.Count - 1; i >= 0; i--)
			{
				if (now - _recentSelfMagicCasts[i].time > SelfMagicCastWindowMs)
					_recentSelfMagicCasts.RemoveAt(i);
			}
		}

		/// <summary>
		/// 【先播放後結算】若為己方剛施放的魔法則返回 true 並跳過本次 SpawnEffect（僅結算傷害）。
		/// </summary>
		private static bool TryConsumeSelfMagicCast(int attackerId, int gfxId)
		{
			ulong now = (ulong)Time.GetTicksMsec();
			for (int i = _recentSelfMagicCasts.Count - 1; i >= 0; i--)
			{
				if (now - _recentSelfMagicCasts[i].time > SelfMagicCastWindowMs)
					_recentSelfMagicCasts.RemoveAt(i);
			}
			for (int i = 0; i < _recentSelfMagicCasts.Count; i++)
			{
				if (_recentSelfMagicCasts[i].gfxId == gfxId && (now - _recentSelfMagicCasts[i].time) <= SelfMagicCastWindowMs)
				{
					_recentSelfMagicCasts.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 【飛行魔法】記錄己方剛播的飛行段，Op35 收到時可跳過重複。
		/// </summary>
		public static void RecordSelfFlyingCast(int gfxId)
		{
			ulong now = (ulong)Time.GetTicksMsec();
			_recentSelfFlyingCasts.Add((gfxId, now));
			for (int i = _recentSelfFlyingCasts.Count - 1; i >= 0; i--)
			{
				if (now - _recentSelfFlyingCasts[i].time > SelfMagicCastWindowMs)
					_recentSelfFlyingCasts.RemoveAt(i);
			}
		}

		/// <summary>
		/// 【飛行魔法】若為己方剛播的飛行段則返回 true 並消耗一次，Op35 跳過重複播放。
		/// </summary>
		private static bool TryConsumeSelfFlyingCast(int gfxId)
		{
			ulong now = (ulong)Time.GetTicksMsec();
			for (int i = _recentSelfFlyingCasts.Count - 1; i >= 0; i--)
			{
				if (now - _recentSelfFlyingCasts[i].time > SelfMagicCastWindowMs)
					_recentSelfFlyingCasts.RemoveAt(i);
			}
			for (int i = 0; i < _recentSelfFlyingCasts.Count; i++)
			{
				if (_recentSelfFlyingCasts[i].gfxId == gfxId && (now - _recentSelfFlyingCasts[i].time) <= SelfMagicCastWindowMs)
				{
					_recentSelfFlyingCasts.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 将服务器 Grid 坐标转换为 Godot 世界像素坐标
		/// 【徹底重構】使用統一的座標轉換函數
		/// </summary>
		private Vector2 ConvertGridToWorld(int gx, int gy)
		{
			return CoordinateSystem.GridToPixel(gx, gy);
		}

		// [FIX] 彻底弃用几何计算，仅保留方法定义防报错（可选删除）
		private int CalculateHeading(Vector2 from, Vector2 to)
		{
			Vector2 dir = (to - from).Normalized();
		   //  float angle = dir.Angle(); // -PI ~ PI
			
			// 将弧度转换为 Lineage 的 8 方向 (0=Up, 顺时针)
			// Godot: Right=0, Down=90(PI/2), Left=180(PI), Up=-90(-PI/2)
			// Lineage: 
			// 0: Up
			// 1: Up-Right
			// 2: Right
			// 3: Down-Right
			// 4: Down
			// 5: Down-Left
			// 6: Left
			// 7: Up-Left

			float degrees = Mathf.RadToDeg(dir.Angle()) + 90;
			// 修正角度偏移，使其对齐到 0=Up (Godot Up is -90)
			// +90 度 -> Right=90, Down=180, Left=270, Up=0
			if (degrees < 0) degrees += 360;

			// 划分为 8 个扇区 (每个 45度)
			// 0 (Up) 是 337.5 ~ 22.5
			// 简单取整计算:
			return Mathf.FloorToInt((degrees + 22.5f) / 45.0f) % 8;
			
			// Lineage 的方向定义可能与此时计算的 h 略有不同，通常如下：
			// h=0(Up), 1(UR), 2(Right), 3(DR), 4(Down), 5(DL), 6(Left), 7(UL)
			// 这与上述公式结果一致。
		}
	}
}
