// ============================================================================
// [FILE] Client/Game/GameEntity.CombatFx.cs


// [戰鬥表現文件] 職責：處理打擊判定與血條回饋


// Visuals.cs（渲染執行）
// GameEntity.cs（狀態控制）、
// ListSprLoader.cs（數據解析）和 
// CustomCharacterProvider.cs（資源分發）

// 職責分配表 (Architect's Master Plan)
// GameEntity.cs (主文件): 存放核心數據屬性與動作常量。
// GameEntity.Action.cs (動作狀態機): 唯一的 SetAction 實作地。處理姿勢偏移（Weapon Offset）。
// GameEntity.Visuals.cs (渲染執行): 負責 AnimatedSprite2D 的圖層管理與幀同步。
// GameEntity.Movement.cs (位移邏輯): 負責坐標平滑移動，並請求 SetAction。
// GameEntity.CombatFx.cs (戰鬥表現): 負責血條、飄字，並在受擊時請求 SetAction。
// GameEntity.Shadow.cs (陰影): 專門負責腳下陰影。
// GameEntity.UI.cs (頭頂 UI): 專門負責名字、聊天氣泡。

// ============================================================================

using Godot;
using System;

namespace Client.Game
{
    public partial class GameEntity
    {




		// =====================================================================
		// [SECTION] Attack Animation: PlayAttackAnimation (攻击表现)
		// =====================================================================

		/// <summary>
		/// 【核心修復】攻擊動畫：根據目標座標調整朝向，確保攻擊時面對被攻擊者
		/// 注意：如果外部已經調整過朝向（例如 PerformAttackOnce），這裡會檢查是否需要再次調整
		/// 使用 GameWorld.GetHeading 確保與移動邏輯一致，避免雙方同時調整朝向的衝突
		/// </summary>
		public void PlayAttackAnimation(int tx, int ty)
		{
			// 【核心修复】攻击前先根据目标坐标调整朝向
			// 只有当目标坐标有效 (非0) 且不等于自己当前坐标时才计算
			if (tx != 0 && ty != 0 && (tx != MapX || ty != MapY))
			{
				// 【朝向同步修復】使用 GameWorld.GetHeading 確保與移動邏輯一致
				// 避免使用不同的朝向計算邏輯導致衝突
				int newHeading = GameWorld.GetHeading(MapX, MapY, tx, ty, Heading);
				
				// 调用 Visuals 的 SetHeading，这会更新 Heading 属性
				// 注意：如果方向没变，SetHeading 内部会自动跳过刷新，性能无损
				// 這樣即使外部已經調整過朝向，這裡也不會重複調整（因為 SetHeading 會檢查）
				SetHeading(newHeading);
			}

			// 设置攻击动作 (原地播放)
			SetAction(ACT_ATTACK); 
		}

		public void PrepareAttack(int targetId, int damage)
        {
            _pendingAttacks.Add((targetId, damage));
            GD.Print($"[HitChain] PrepareAttack ObjId={ObjectId} pending target={targetId} damage={damage} count={_pendingAttacks.Count}");
        }
		// =====================================================================
		// [SECTION] Damage Visual
		// =====================================================================
		// OnDamageStutter 實作於 GameEntity.Movement.cs（停止位移 Tween，造成僵直感）

		// =====================================================================
		// [SECTION] Floating Text (優化版：支持多種傷害類型)
		// =====================================================================
		
		/// <summary>
		/// 傷害類型枚舉
		/// </summary>
		public enum DamageType
		{
			Normal,      // 普通傷害（紅色）
			Critical,    // 暴擊傷害（金色/橙色，更大字體）
			Magic,       // 魔法傷害（藍色/紫色）
			Heal,        // 治療（綠色）
			Miss,        // 未命中（灰色）
			Block,       // 格擋（黃色）
			Dodge        // 閃避（淺灰色）
		}
		
		/// <summary>
		/// 【優化】顯示傷害數字（支持多種傷害類型）
		/// </summary>
		/// <param name="dmg">傷害值（<=0 表示 MISS）</param>
		/// <param name="type">傷害類型</param>
		public void OnDamagedVisual(int dmg, DamageType type = DamageType.Normal)
		{
			GD.Print($"[HitChain] OnDamagedVisual ObjId={ObjectId} dmg={dmg} type={type} -> ShowFloatingText");
			// 僅飄字；受擊僵硬動畫 (ACT_DAMAGE) 由 GameWorld.Combat.HandleEntityAttackHit 在「命中關鍵幀」且 damage>0 時唯一觸發
			ShowFloatingText(dmg, type);
		}
		
		/// <summary>
		/// 【向後兼容】舊版接口，自動判斷傷害類型
		/// </summary>
		public void OnDamagedVisual(int dmg)
		{
			DamageType type = dmg <= 0 ? DamageType.Miss : DamageType.Normal;
			OnDamagedVisual(dmg, type);
		}
		
		/// <summary>
		/// 【優化】顯示浮動文字（支持多種傷害類型和動畫效果）
		/// </summary>
		private void ShowFloatingText(int dmg, DamageType type = DamageType.Normal)
		{
			var label = new Label();
			label.ZIndex = 100; // 確保在最上層
			
			// 設置字體與描邊
			label.AddThemeConstantOverride("outline_size", 4);
			label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
			
			// 【優化】根據傷害類型設置文字和顏色
			switch (type)
			{
				case DamageType.Miss:
					label.Text = "MISS";
					label.Modulate = new Color(0.7f, 0.7f, 0.7f); // 灰色
					break;
				case DamageType.Block:
					label.Text = "BLOCK";
					label.Modulate = new Color(1.0f, 0.8f, 0.2f); // 黃色
					break;
				case DamageType.Dodge:
					label.Text = "DODGE";
					label.Modulate = new Color(0.8f, 0.8f, 0.9f); // 淺灰色
					break;
				case DamageType.Critical:
					label.Text = $"CRIT! {dmg}";
					label.Modulate = new Color(1.0f, 0.6f, 0.0f); // 金色/橙色
					label.AddThemeFontSizeOverride("font_size", 24); // 暴擊字體更大
					break;
				case DamageType.Magic:
					label.Text = dmg.ToString();
					label.Modulate = new Color(0.4f, 0.6f, 1.0f); // 藍色/紫色
					break;
				case DamageType.Heal:
					label.Text = $"+{dmg}";
					label.Modulate = new Color(0.2f, 1.0f, 0.2f); // 綠色
					break;
				case DamageType.Normal:
				default:
					label.Text = dmg.ToString();
					label.Modulate = new Color(1, 0.2f, 0.1f); // 鮮紅色
					break;
			}

			AddChild(label);
			
			// 【優化】根據傷害類型調整動畫效果
			float scaleStart = 0.5f;
			float scalePeak = type == DamageType.Critical ? 3.0f : 2.5f; // 暴擊放大更多
			float scaleEnd = type == DamageType.Critical ? 1.5f : 1.0f;
			float moveDistance = type == DamageType.Critical ? 100f : 80f; // 暴擊飛更遠
			
			// 初始狀態：縮小並透明
			label.Scale = new Vector2(scaleStart, scaleStart);
			label.Position = new Vector2(-20, -60);
			label.PivotOffset = label.Size / 2;

			var tween = CreateTween();
			// 1. 衝擊彈出（暴擊效果更強）
			tween.Parallel().TweenProperty(label, "scale", new Vector2(scalePeak, scalePeak), 0.1f).SetTrans(Tween.TransitionType.Back);
			tween.Parallel().TweenProperty(label, "position:y", label.Position.Y - 30, 0.1f);
			
			// 2. 緩慢漂浮與縮小
			tween.Chain().Parallel().TweenProperty(label, "scale", new Vector2(scaleEnd, scaleEnd), 0.2f);
			tween.Parallel().TweenProperty(label, "position:y", label.Position.Y - moveDistance, 0.7f).SetTrans(Tween.TransitionType.Sine);
			
			// 3. 漸隱消失
			tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.7f);
			
			// 4. 銷毀
			tween.Chain().TweenCallback(Callable.From(label.QueueFree));
		}

        public void SetHpRatio(int ratio)
        {
            // 僅 0-100 為有效伺服器血條；<0 表示「無血條數據」（如 0xFF）
            if (ratio >= 0 && ratio <= 100)
            {
                _hpRatio = ratio;
                if (_healthBar != null)
                    _healthBar.Value = _hpRatio;
            }
            else
            {
                _hpRatio = -1;
                SetHealthBarVisible(false);
            }
            // 血量=0 時頭頂名字不顯示並馬上銷毀，血條隱藏
            if (_hpRatio == 0)
            {
                if (_nameLabel != null)
                {
                    _nameLabel.Visible = false;
                    _nameLabel.QueueFree();
                    _nameLabel = null;
                }
                SetHealthBarVisible(false);
            }
            // 可見性由 GameWorld 依「主角/怪物」與設定統一設定（SetHealthBarVisible）
        }
        
        /// <summary>重載：接受 float 參數（用於復活時設置 1.0f）</summary>
        public void SetHpRatio(float ratio)
        {
            SetHpRatio((int)(ratio * 100f));
        }

        /// <summary>設定頭頂血條顯示/隱藏。主角由 OnHPUpdated 驅動；怪物由 Opcode 104 + 設定開關驅動。</summary>
        public void SetHealthBarVisible(bool visible)
        {
            if (_healthBar != null)
                _healthBar.Visible = visible;
        }

        /// <summary>黑夜遮罩啟用時：強制隱藏頭頂名字與血條；關閉時僅恢復名字顯示，血條由 GameWorld 依邏輯重設。</summary>
        internal void SetNightOverlayActive(bool isNight)
        {
            if (_nameLabel != null)
                _nameLabel.Visible = !isNight;
            if (_healthBar != null && isNight)
                _healthBar.Visible = false;
        }
        /// 命中觸發。由 Visuals.cs 在動畫幀讀到 '!' 時回調。

        public void OnAnimationKeyFrame()
        {
            GD.Print($"[HitChain] OnAnimationKeyFrame ObjId={ObjectId} triggered={_isAttackHitTriggered} pending={_pendingAttacks.Count}");
            if (!_isAttackHitTriggered && _pendingAttacks.Count > 0)
            {
                _isAttackHitTriggered = true;
                foreach (var atk in _pendingAttacks)
                {
                    GD.Print($"[HitChain] Invoke OnAttackKeyFrameHit target={atk.TargetId} damage={atk.Damage}");
                    OnAttackKeyFrameHit?.Invoke(atk.TargetId, atk.Damage);
                }
                _pendingAttacks.Clear();
            }
        }

        // [補全] 修復 Entities.cs 中的調用報錯
        public void ShowChat(string text)
        {
            if (_chatBubble == null) return;
            _chatBubble.Text = text;
            _chatBubble.Visible = true;
            GetTree().CreateTimer(3.0f).Timeout += () => _chatBubble.Visible = false;
        }
    }
}
