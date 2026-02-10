using Godot;
using System;
using Client.Network;
using Client.Utility;

namespace Client.Game
{
	public partial class GameWorld
	{
		// =====================================================
		// [Debug] Move Speed Overlay
		// =====================================================
		private CanvasLayer _moveDebugLayer;
		private Label _moveDebugLabel;
		private bool _showMoveDebug = true;
		private long _lastMoveIntervalMs = 0;
		private long _lastMinIntervalMs = 0;
		private long _lastSendIntervalMs = 0;
		private string _lastMoveDebugNote = "";
		private long _lastMoveSpeedLogTime = 0;
		private int _lastMoveSpeedLogGfxId = -1;
		private int _lastMoveSpeedLogActionId = -1;
		private long _lastMoveSpeedLogIntervalMs = -1;

		private void EnsureMoveDebugOverlay()
		{
			if (!_showMoveDebug || _moveDebugLayer != null || !IsInsideTree()) return;

			_moveDebugLayer = new CanvasLayer();
			_moveDebugLayer.Name = "MoveDebugLayer";
			_moveDebugLayer.Layer = 2;

			_moveDebugLabel = new Label();
			_moveDebugLabel.Name = "MoveDebugLabel";
			_moveDebugLabel.Position = new Vector2(8, 64);
			_moveDebugLabel.AddThemeFontSizeOverride("font_size", 14);
			_moveDebugLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1f, 0.2f));
			_moveDebugLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
			_moveDebugLabel.AddThemeConstantOverride("outline_size", 4);
			_moveDebugLabel.Text = "";

			_moveDebugLayer.AddChild(_moveDebugLabel);
			AddChild(_moveDebugLayer);
		}

		private void UpdateMoveDebugOverlay(string note)
		{
			if (!_showMoveDebug) return;
			EnsureMoveDebugOverlay();
			if (_moveDebugLabel == null) return;

			long now = (long)Time.GetTicksMsec();
			long confirmAge = (_lastServerMoveConfirmTime > 0) ? (now - _lastServerMoveConfirmTime) : -1;
			string confirmAgeStr = (confirmAge >= 0) ? $"{confirmAge}ms" : "N/A";

			string visualPos = _myPlayer != null ? $"{_myPlayer.MapX},{_myPlayer.MapY}" : "N/A";
			string basePos = (_serverConfirmedPlayerX >= 0) ? $"{_serverConfirmedPlayerX},{_serverConfirmedPlayerY}" : "N/A";
			string predPos = (_clientPredictedPlayerX >= 0) ? $"{_clientPredictedPlayerX},{_clientPredictedPlayerY}" : "N/A";

			_moveDebugLabel.Text =
				$"MoveInterval: {_lastMoveIntervalMs}ms  MinAllowed: {_lastMinIntervalMs}ms  LastSendΔ: {_lastSendIntervalMs}ms\n" +
				$"Base(Server): {basePos}  Predicted: {predPos}  Visual: {visualPos}\n" +
				$"LastServerConfirm: {confirmAgeStr}  Note: {note}";
		}
		// 僅供攻擊任務使用：本幀內剛執行完 StepTowardsTarget（走完一格或已到目標）時為 true，每幀頭重置。
		// Combat 用此延後「送攻擊」到同一幀送完移動之後，避免伺服器先處理攻擊再處理移動導致距離判定失敗。拾取/對話不依賴此信號。
		private bool _hasArrivedThisFrame = false;
		
		// 【座標同步偵探】全局移動封包計數器，用於追蹤每個移動封包的ID
		private static int _globalMovePacketId = 0;

		// =====================================================
		// [主循環] 移動邏輯心跳
		// =====================================================
		private void UpdateMovementLogic(double delta)
		{
			EnsureMoveDebugOverlay();

			// 1. 每幀開始先重置到達信號
			_hasArrivedThisFrame = false;

			// 2. 基礎檢查
			if (_isPlayerDead)
			{
				StopWalking();
				return;
			}
			if (!_isAutoWalking || _myPlayer == null) return;
			// 3b. 【全專案唯一】受擊僵硬期間禁止移動（開關：GameEntity.DamageStiffnessBlocksMovement）
			if (GameEntity.DamageStiffnessBlocksMovement && _myPlayer.IsInDamageStiffness) return;

			// 3. 獲取權威移動間隔 (從 SprDataTable 讀取伺服器數據)
			// 【關鍵修復】服務器 CheckSpeed.getRightInterval(ACT_TYPE.MOVE) 使用 getAttackSpeed(gfx, gfxMode + 1)
			// 注意：服務器使用 getAttackSpeed 來檢查移動速度，不是 getMoveSpeed！
			// 客戶端必須使用相同的邏輯，否則服務器會拒絕處理移動包
			// gfxMode 通常是 0（空手）或 4（劍）等，對應 _visualBaseAction
			// 【服務器對齊】服務器使用 SprTable.getMoveSpeed(tempCharGfx, currentWeapon)
			int gfxMode = _myPlayer.GetVisualBaseAction(); // 與服務器 currentWeapon 對齊
			long serverMoveInterval = SprDataTable.GetInterval(ActionType.Move, _myPlayer.GfxId, gfxMode);
			float baseInterval = serverMoveInterval / 1000.0f;
			
			// 【診斷日誌】記錄移動間隔計算
			if (baseInterval <= 0)
			{
				GD.PrintErr($"[Move-Interval-Error] Invalid move interval: GfxId={_myPlayer.GfxId} gfxMode={gfxMode} serverInterval={serverMoveInterval}ms baseInterval={baseInterval}s");
				baseInterval = 0.6f; // 回退到默認值
			}
			
			// 【速度修復】根據伺服器 CheckSpeed.java 處理加速/緩速邏輯
			// - 綠水（Haste）：interval * 0.75（AnimationSpeed = 1.333...）
			// - 緩速（Slow）：interval / 0.75 = interval * 1.333...（AnimationSpeed = 0.75）
			_moveInterval = baseInterval;
			if (_myPlayer.AnimationSpeed > 1.0f) 
			{
				// 加速：間隔縮放為 0.75
				_moveInterval *= 0.75f; 
			}
			else if (_myPlayer.AnimationSpeed < 1.0f)
			{
				// 緩速：間隔放大為 1.333...
				_moveInterval /= 0.75f;
			}
			
			// 【服務器對齊】AcceleratorChecker.checkInterval 對移動間隔乘以 CHECK_STRICTNESS
			// jp Config.CHECK_STRICTNESS=102 -> (102-5)/100=0.97
			// 服務器實際要求的最小間隔 = rightInterval / 0.97 ≈ +3.1%
			const float serverCheckStrictness = 0.97f;
			if (serverCheckStrictness > 0)
				_moveInterval /= serverCheckStrictness;
			
			// 【單行診斷】每 3 秒或參數變更時記錄一次，避免刷屏
			long nowLog = (long)Time.GetTicksMsec();
			long intervalMs = (long)(_moveInterval * 1000.0f);
			if (nowLog - _lastMoveSpeedLogTime > 3000 ||
			    _lastMoveSpeedLogGfxId != _myPlayer.GfxId ||
			    _lastMoveSpeedLogActionId != gfxMode ||
			    _lastMoveSpeedLogIntervalMs != intervalMs)
			{
				GD.Print($"[Move-Speed] gfx={_myPlayer.GfxId} actionId={gfxMode} moveSpeedMs={serverMoveInterval} interval={intervalMs}ms");
				_lastMoveSpeedLogTime = nowLog;
				_lastMoveSpeedLogGfxId = _myPlayer.GfxId;
				_lastMoveSpeedLogActionId = gfxMode;
				_lastMoveSpeedLogIntervalMs = intervalMs;
			}

			// 4. 移動計時器
			_moveTimer += (float)delta;
			
			// 【關鍵驗證】記錄移動計時器狀態，用於診斷丟包問題
			// 每 640ms 必須發送移動包，確保服務器知道玩家的最新座標
			if (_moveTimer >= _moveInterval)
			{
				// 計算實際間隔（用於診斷）
				long currentTime = (long)Time.GetTicksMsec();
				long actualInterval = 0;
				if (_lastMovePacketTime > 0)
				{
					actualInterval = currentTime - _lastMovePacketTime;
				}
				
				// 【關鍵日誌】記錄移動包發送間隔，用於驗證每 640ms 發送
				// 如果實際間隔 > 640ms * 1.5，說明可能丟包或延遲
				if (actualInterval > 0 && actualInterval > (long)(_moveInterval * 1000 * 1.5f))
				{
					GD.PrintErr($"[Move-Interval-Warn] Move packet interval too long! Expected: {_moveInterval * 1000:F0}ms Actual: {actualInterval}ms (可能丟包或延遲)");
				}
				else if (actualInterval > 0)
				{
					GD.Print($"[Move-Interval] Move packet interval: {actualInterval}ms (expected: {_moveInterval * 1000:F0}ms)");
				}
			}
			
			// 只有累積到足夠的間隔才執行一步
			if (_moveTimer < _moveInterval) return;
			
			// 5. 執行步進 (Timer 重置在 StepTowardsTarget 內部成功後執行)
			// 注意：這裡不再做"是否到達目標"的判斷，只管往目標走。
			// 到達的判斷由 StepTowardsTarget 內部的 (dx==0 && dy==0) 處理。
			if (_targetMapX != 0 && _targetMapY != 0)
			{
				StepTowardsTarget(_targetMapX, _targetMapY);
			}
		}

		// =====================================================
		// [指令] 开始移动
		// =====================================================
		private void StartWalking(int x, int y)
		{
			// 如果目标没变且已经在走，不做任何事（防止动画重置）
			if (_isAutoWalking && _targetMapX == x && _targetMapY == y) return;
			// 死亡狀態禁止移動
			if (_isPlayerDead) return;
			// 【全專案唯一】受擊僵硬期間不允許開始移動
			if (GameEntity.DamageStiffnessBlocksMovement && _myPlayer != null && _myPlayer.IsInDamageStiffness) return;

			_targetMapX = x;
			_targetMapY = y;
			_isAutoWalking = true;
			_moveTimer = 0; // 立即重置，准备迈第一步

			// 【核心修复】设置正确的走路动作 (武器类型偏移)
			// 原代码: SetAction(0) -> 错误
			// 新代码: GetWeaponWalkAction() -> 拿剑返回4，拿斧返回11
			if (_myPlayer != null) 
			{
				// 【修復】若 list.spr 未定義任何 walk 動作，則不切換走路動畫
				var def = ListSprLoader.Get(_myPlayer.GfxId);
				var walkSeq = ListSprLoader.GetActionSequence(def, GameEntity.ACT_WALK);
				if (walkSeq != null)
				{
					// 设置为行走动作
					_myPlayer.SetAction(GameEntity.ACT_WALK);
				}
			}

			// 【診斷日誌】記錄玩家移動請求，用於驗證座標是否傳給服務器
			GD.Print($"[Move-Diag] Player move request: From ({_myPlayer?.MapX ?? 0},{_myPlayer?.MapY ?? 0}) To ({x},{y}) ServerConfirmed:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY})");
		
			// 【修復】避免瞬間重複發包導致誤判 Overspeed
			long currentTime = (long)Time.GetTicksMsec();
			long timeSinceLastPacket = 0;
			if (_lastMovePacketTime > 0)
			{
				timeSinceLastPacket = currentTime - _lastMovePacketTime;
			}
			
			long expectedIntervalMs = (long)((_moveInterval > 0 ? _moveInterval : 0.64f) * 1000);
			long minIntervalMs = expectedIntervalMs;
			if (timeSinceLastPacket > 0 && timeSinceLastPacket < minIntervalMs)
			{
				// 等待 UpdateMovement 的節拍，避免當前幀多次送包
				return;
			}
			
			// 立即尝试走第一步，让响应更灵敏
			StepTowardsTarget(x, y);
		}

		// =====================================================
		// [指令] 停止移动
		// =====================================================
		private void StopWalking()
		{
			if (!_isAutoWalking) return;
			
			_isAutoWalking = false;

			if (_myPlayer != null)
			{
				// 恢復待機動作
				_myPlayer.SetAction(GameEntity.ACT_BREATH);
			}
			
			// 【寵物系統】檢查移動完成後是否需要繼續喂食
			CheckFeedAfterMove();
		}

		// =====================================================
		// [核心逻辑] 执行单步位移
		// =====================================================
		private void StepTowardsTarget(int tx, int ty)
		{
			if (_myPlayer == null) return;
			
			// [修复] 不要因为“动作忙碌”而禁止移动
			// 现象：法师经常在受击/施法后 _isActionBusy 长时间不释放（动画完成信号丢失/被打断），导致无法行走。
			// 结论：移动必须是最高优先级；服务器会做最终纠正（被拉回）也比“永久不能走”更可接受。

			// 【服務器對齊】移動方向必須基於服務器確認座標（若可用）
			int baseX = (_serverConfirmedPlayerX >= 0) ? _serverConfirmedPlayerX : _myPlayer.MapX;
			int baseY = (_serverConfirmedPlayerY >= 0) ? _serverConfirmedPlayerY : _myPlayer.MapY;

			int cx = _myPlayer.MapX;
			int cy = _myPlayer.MapY;

			// 1. 计算总距离向量
			int dx = tx - baseX;
			int dy = ty - baseY;

			// 如果已经重合，停止移动，并发送到达信号
			if (dx == 0 && dy == 0)
			{
				StopWalking();
				_hasArrivedThisFrame = true; // 告诉 Combat 我到了
				return;
			}

			// 2. [核心算法] 钳制位移量！确保只走相邻格子
			int stepX = Math.Clamp(dx, -1, 1);
			int stepY = Math.Clamp(dy, -1, 1);

			// 3. 【精準目標座標計算】計算下一步的絕對座標
			// 移動距離固定為 1 格（32像素），無論動畫時長是多少
			// 目標座標 = 當前座標 + 朝向向量（8方向）
			int nextX = baseX + stepX;
			int nextY = baseY + stepY;

			// 4. 【朝向計算】計算朝向（基於下一步）
			// 使用統一的 GetHeading 方法，確保與攻擊時的朝向計算一致
			// [FIXED] 此处必须传入 5 个参数，补上 _myPlayer.Heading 作为默认朝向
			int heading = GetHeading(baseX, baseY, nextX, nextY, _myPlayer.Heading);
			
			// 5. [网络] 发送移动包
			// 【徹底重構】簡化移動邏輯：始終使用客戶端當前位置作為起點
			// 服務器會驗證移動包，如果距離 > 1格會拒絕，但我們應該發送客戶端預測的位置
			int beforeX = _myPlayer.MapX;
			int beforeY = _myPlayer.MapY;
			
			// 【徹底重構】簡化移動邏輯：始終使用客戶端預測的位置
			// 服務器會驗證移動包，如果距離 > 1格會拒絕，但我們應該發送客戶端預測的位置
			int actualNextX = nextX;
			int actualNextY = nextY;
			
			// 【調試日誌】記錄移動計算過程
			GD.Print($"[Move-Debug] StepTowardsTarget: Current:({beforeX},{beforeY}) Base:({baseX},{baseY}) Target:({tx},{ty}) Next:({nextX},{nextY}) ServerConfirmed:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY})");
			
			// 【關鍵修復】檢查計算出的下一步是否與當前位置相同
			// 如果相同，說明無法移動（可能是因為服務器確認位置與客戶端位置不一致）
			// 在這種情況下，我們應該使用客戶端預測的位置，確保移動有效
			if (actualNextX == baseX && actualNextY == baseY)
			{
				// 無法移動，可能是因為服務器確認位置與客戶端位置不一致
				// 嘗試從服務器確認位置向最終目標移動
				if (_serverConfirmedPlayerX >= 0 && _serverConfirmedPlayerY >= 0)
				{
					// 計算從服務器確認位置到最終目標的方向
					int targetDx = tx - _serverConfirmedPlayerX;
					int targetDy = ty - _serverConfirmedPlayerY;
					
					// 如果服務器確認位置與目標距離 > 1格，向目標方向移動1格
					if (Math.Abs(targetDx) > 1 || Math.Abs(targetDy) > 1)
					{
						actualNextX = _serverConfirmedPlayerX + Math.Clamp(targetDx, -1, 1);
						actualNextY = _serverConfirmedPlayerY + Math.Clamp(targetDy, -1, 1);
						heading = GetHeading(_serverConfirmedPlayerX, _serverConfirmedPlayerY, actualNextX, actualNextY, _myPlayer.Heading);
					}
					else
					{
						// 服務器確認位置已經接近目標，直接使用目標位置
						actualNextX = tx;
						actualNextY = ty;
						heading = GetHeading(_serverConfirmedPlayerX, _serverConfirmedPlayerY, actualNextX, actualNextY, _myPlayer.Heading);
					}
					
					// 檢查重新計算後的位置是否與當前位置不同
					if (actualNextX == baseX && actualNextY == baseY)
					{
						// 仍然無法移動，可能是因為服務器確認位置就是當前位置
						// 嘗試強制向目標方向移動1格（即使可能被服務器拒絕）
						int forceDx = Math.Clamp(tx - baseX, -1, 1);
						int forceDy = Math.Clamp(ty - baseY, -1, 1);
						if (forceDx != 0 || forceDy != 0)
						{
							actualNextX = baseX + forceDx;
							actualNextY = baseY + forceDy;
							heading = GetHeading(baseX, baseY, actualNextX, actualNextY, _myPlayer.Heading);
						}
						else
						{
							GD.PrintErr($"[Move-Error] Cannot move! Base:({baseX},{baseY}) Target:({tx},{ty}) ServerConfirmed:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY})");
							StopWalking();
							return; // 無法移動，直接返回
						}
					}
				}
				else
				{
					// 沒有服務器確認位置，使用客戶端預測
					// 但這不應該發生，因為 nextX 已經等於 beforeX
					GD.PrintErr($"[Move-Error] Cannot move! Base:({baseX},{baseY}) Target:({tx},{ty}) Next:({nextX},{nextY}) No server confirmed position");
					StopWalking();
					return; // 無法移動，直接返回
				}
			}
			
			// 發送移動包
			long currentTime = (long)Time.GetTicksMsec();
			long timeSinceLastPacket = 0;
			if (_lastMovePacketTime > 0)
			{
				timeSinceLastPacket = currentTime - _lastMovePacketTime;
			}
			
			// 【服務器對齊】嚴格限制發包間隔，避免被判定 Overspeed
			// 服務器 CHECK_STRICTNESS 默認會放寬約 3%，這裡保守取 0.97 倍閾值
			long minIntervalMs = (long)(_moveInterval * 1000);
			_lastMoveIntervalMs = (long)(_moveInterval * 1000);
			_lastMinIntervalMs = minIntervalMs;
			_lastSendIntervalMs = timeSinceLastPacket;
			if (timeSinceLastPacket > 0 && timeSinceLastPacket < minIntervalMs)
			{
				// 等待下一個移動節拍，避免出現「正常走路卻被判太快」的誤判日誌
				_moveTimer = 0;
				return;
			}
			
			// 【關鍵日誌】記錄移動包發送，用於驗證每 640ms 發送
			// 必須確保每 640ms 發送移動包，否則服務器不知道玩家的最新座標
			GD.Print($"[Move-Packet] Sending C_MoveChar(95) -> Base:({baseX},{baseY}) Visual:({beforeX},{beforeY}) To:({actualNextX},{actualNextY}) Heading:{heading} ServerConfirmed:({_serverConfirmedPlayerX},{_serverConfirmedPlayerY}) Interval={_moveInterval:F3}s TimeSinceLastPacket={timeSinceLastPacket}ms");
			
			// 【診斷日誌】記錄發送移動包的詳細信息，用於驗證座標是否傳給服務器
			GD.Print($"[Move-Diag] Player sending move packet:");
			GD.Print($"  Client predicted position: ({actualNextX},{actualNextY})");
			GD.Print($"  Server confirmed position: ({_serverConfirmedPlayerX},{_serverConfirmedPlayerY})");
			GD.Print($"  Base position used for heading: ({baseX},{baseY})");
			GD.Print($"  Time since last packet: {timeSinceLastPacket}ms (expected: {_moveInterval * 1000:F0}ms)");
			
			// 【關鍵驗證】如果距離上次發包時間 > 640ms * 2，說明可能丟包
			if (timeSinceLastPacket > (long)(_moveInterval * 1000 * 2))
			{
				GD.PrintErr($"[Move-Packet-Error] Move packet interval too long! Expected: {_moveInterval * 1000:F0}ms Actual: {timeSinceLastPacket}ms (可能丟包，服務器可能不知道玩家最新座標)");
				GD.PrintErr($"[Move-Packet-Error] 這說明玩家座標沒有及時傳給服務器，服務器可能使用舊座標計算怪物攻擊");
			}
			
			_lastMovePacketTime = currentTime;
			
			// 【座標同步偵探】發送移動包前，增加封包ID並記錄日誌
			_globalMovePacketId++;
			string packetType = "Normal";
			GD.Print($"[Move-Audit] Sent ID:#{_globalMovePacketId} Pos:({actualNextX},{actualNextY}) Head:{heading} Type:{packetType}");
			
			_netSession.Send(C_MoveCharPacket.Make(actualNextX, actualNextY, heading));
			UpdateMoveDebugOverlay($"Sent to ({actualNextX},{actualNextY}) head={heading}");
			
			// 【座標同步】如果服務器長時間未回包，暫時以本地推進作為“服務器確認”
			// 這避免因為無自我確認封包導致 baseX 永遠停在起點
			if (_lastServerMoveConfirmTime == 0 || (currentTime - _lastServerMoveConfirmTime) > (long)(_moveInterval * 1000 * 2))
			{
				_serverConfirmedPlayerX = actualNextX;
				_serverConfirmedPlayerY = actualNextY;
				GD.Print($"[Pos-Estimate] No server confirm. Assume server at ({actualNextX},{actualNextY})");
			}

			// 【修復】移動包發送時，更新時間戳（用於魔法時的位置更新檢查）
			// 注意：_lastPositionUpdateTime 在 GameWorld.Combat.cs 中定義，由於是同一個 partial class，可以直接訪問
			// 定期位置更新機制已刪除，此時間戳僅用於魔法時的位置更新
			_lastPositionUpdateTime = currentTime;
			
			// 6. [本地] 立刻更新坐标 (平滑移动)
			// 【關鍵修復】使用實際發送的位置（可能是中間位置）
			_myPlayer.SetMapPosition(actualNextX, actualNextY, heading);
			
			// 【座標同步】記錄客戶端預測座標（不覆寫服務器確認座標）
			_clientPredictedPlayerX = actualNextX;
			_clientPredictedPlayerY = actualNextY;
			GD.Print($"[Pos-Predict] Client predicted move: ({actualNextX},{actualNextY}) Heading={heading}");

			// 7. [关键] 标记本帧完成了一次移动
			_hasArrivedThisFrame = true;

			// 8. 重置计时器
			_moveTimer = 0;
		}

		// =====================================================
		// [辅助工具] 方向计算 (保留原版)
		// =====================================================

        /*
		 * 将 (dx, dy) 转换为服务器标准 Heading
		 * 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
         * 服务器定义：
         * 0 = N   (0,-1)
         * 1 = NE  (+1,-1)
         * 2 = E   (+1,0)
         * 3 = SE  (+1,+1)
         * 4 = S   (0,+1)
         * 5 = SW  (-1,+1)
         * 6 = W   (-1,0)
         * 7 = NW  (-1,-1)
         */
		// 放在 GameWorld.Movement.cs 或独立的 MathHelper.cs 中
		// 参数说明：如果不移动(dx=0,dy=0)，则返回 defaultHeading (保持原样)
		public static int GetHeading(int fromX, int fromY, int toX, int toY, int defaultHeading)
		{
		    int dx = toX - fromX;
		    int dy = toY - fromY;

		    // 优化后的二分查找逻辑
		    if (dy < 0)
		    {
		        if (dx > 0) return 1; // NE
		        if (dx < 0) return 7; // NW
		        return 0;             // N
		    }
		    else if (dy > 0)
		    {
		        if (dx > 0) return 3; // SE
		        if (dx < 0) return 5; // SW
		        return 4;             // S
		    }
		    else // dy == 0
		    {
		        if (dx > 0) return 2; // E
		        if (dx < 0) return 6; // W
		    }

		    // 只有当 dx=0, dy=0 时，返回原本的朝向，而不是强制转北
		    return defaultHeading;
		}
		
		
		// 辅助方法：预测下一步坐标 (保留备用)
		private (int x, int y) GetNextStep(int x, int y, int h)
		{
			switch (h) {
				case 0: return (x, y - 1); case 1: return (x + 1, y - 1);
				case 2: return (x + 1, y); case 3: return (x + 1, y + 1);
				case 4: return (x, y + 1); case 5: return (x - 1, y + 1);
				case 6: return (x - 1, y); case 7: return (x - 1, y - 1);
			} return (x, y);
		}
	}
}
