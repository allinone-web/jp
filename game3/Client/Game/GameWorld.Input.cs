using Godot;
using Client.UI;
using Client.Utility; // 引用 Helper

namespace Client.Game
{
	// =========================================================================
	// [FILE] GameWorld.Input.cs
	// 说明：输入入口与点击分发。只做“路由/分发”，具体行为由 Combat/Inventory/Movement/Npc 等分部类实现。
	// =========================================================================
	public partial class GameWorld
	{
		// [新增] 双击强制攻击开关 (默认关闭，方便调试)
		[Export] public bool EnableDoubleClickAttack = false;

		// =====================================================================
		// [SECTION] Godot Input: _UnhandledInput (键盘/鼠标输入入口)
		// 说明：
		// - 键盘：C 打开角色面板 / B 或 Tab 打开背包 / S 打开技能 / Q 打开选项 / Z 攻击
		// - 鼠标：左键触发 HandleInput（移动/拾取/寵物餵食/選擇對象）
		// - 拖動：處理從背包拖動物品到實體（喂食怪物）
		// 【簡化鼠標邏輯】攻擊功能全部由 Z 按鍵處理，鼠標不再包含攻擊功能
		// =====================================================================
		public override void _UnhandledInput(InputEvent @event)
		{
			// 【寵物系統】處理拖動物品到實體（喂食）
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && _myPlayer != null)
			{
				// 檢查是否有拖動數據
				var dragData = GetViewport().GuiGetDragData();
				if (dragData.VariantType == Variant.Type.Dictionary)
				{
					var dict = dragData.AsGodotDictionary();
					if (dict.ContainsKey("type") && dict["type"].AsString() == "item" && dict.ContainsKey("id"))
					{
						int itemObjectId = dict["id"].AsInt32();
						Vector2 globalMousePos = GetGlobalMousePosition();
						GameEntity targetEntity = GetClickedEntity(globalMousePos);
						
						if (targetEntity != null)
						{
							// 檢查是否為可喂食的怪物（非玩家、非NPC、非地面物品）
							if (!targetEntity.IsDead && 
							    AlignmentHelper.IsMonster(targetEntity.Lawful) &&
							    !IsGroundItem(targetEntity))
							{
								// 檢查物品是否為肉（item id=12，nameidN=23）
								if (_inventory.TryGetValue(itemObjectId, out var item) && item.ItemId == 12)
								{
									FeedMonster(targetEntity.ObjectId, itemObjectId, 1);
									// 清除拖動預覽（Godot 4.x 中 SetDragPreview 已移除，使用其他方式）
									return; // 已處理，不再執行其他邏輯
								}
							}
						}
					}
				}
			}
			
			// 1) 处理键盘按下
			if (@event is InputEventKey k && k.Pressed && !k.Echo)
			{
				// 按 C 打开角色面板
				if (k.Keycode == Key.C)
				{
					UIManager.Instance.Toggle(WindowID.Character);
					RefreshWindows(); // 打开时刷新一下数据
				}

				// 按 B 或 Tab 打开背包
				if (k.Keycode == Key.B || k.Keycode == Key.Tab)
				{
					UIManager.Instance.Toggle(WindowID.Inventory);
					RefreshWindows();
				}

				// 按 S 打开技能
				if (k.Keycode == Key.S)
				{
					UIManager.Instance.Toggle(WindowID.Skill);
					RefreshWindows();
				}

				// 按 Q 打开选项
				if (k.Keycode == Key.Q)
				{
					UIManager.Instance.Toggle(WindowID.Options);
					RefreshWindows();
				}

				// 按 K 打开戰鬥統計
				if (k.Keycode == Key.K)
				{
					UIManager.Instance.Toggle(WindowID.CombatStats);
				}

				// Z 键自动攻击 (现在通过 TaskQueue 路由)
				if (k.Keycode == Key.Z || Input.IsActionJustPressed("action_attack"))
				{
					if (_isPlayerDead) return;
					GD.Print("[Combat-Diag] Z / action_attack pressed -> ScanForAutoTarget");
					ScanForAutoTarget(); 
				}


			}

			// 2) 处理鼠标点击 (移动/攻击/拾取/NPC)
			if (@event is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left && _myPlayer != null)
				HandleInput(m);
		}
		// =====================================================================
		// [SECTION] Input Routing: HandleInput (点击行为分发)
		// 【簡化鼠標邏輯】鼠標功能簡化：只負責移動、拾取、寵物餵食、選擇對象
		// 攻擊功能全部由 Z 按鍵處理，鼠標不再包含攻擊功能
		// =====================================================================
		private void HandleInput(InputEventMouseButton mouse)
		{
			if (_isPlayerDead) return;
			// 【徹底重構】使用統一的座標轉換
			// GetGlobalMousePosition() 返回全局座標，需要轉換為 GameWorld 的本地座標
			Vector2 globalMousePos = GetGlobalMousePosition();
			Vector2 localMousePos = ToLocal(globalMousePos);

			// 寻找点击目标（使用全局座標，因為實體的 GlobalPosition 也是全局座標）
			GameEntity clickedEntity = GetClickedEntity(globalMousePos);

			// 1. 点击实体 -> 第一優先：102.type(9) 地面物品可拾取；召喚/寵物開 TalkWindow；否則選擇對象（不攻擊）
			if (clickedEntity != null)
			{
			// 【寵物系統】檢查是否為自己的寵物
			// 【修復】改為使用 TalkWindow 顯示，服務器會自動發送 S_ObjectPet (Opcode 42)
			if (_myPetObjectIds != null && _myPetObjectIds.Contains(clickedEntity.ObjectId))
			{
				// 點擊寵物時，服務器會自動發送 S_ObjectPet (Opcode 42, "anicom")
				// 數據會通過 ShowHtmlReceived 信號傳遞給 TalkWindow
				OpenPetPanel(clickedEntity.ObjectId);
				return;
			}
				
				if (_mySummonObjectIds != null && _mySummonObjectIds.Contains(clickedEntity.ObjectId))
				{
					OpenSummonTalkWindow(clickedEntity.ObjectId);
					return;
				}
				
				// 【簡化鼠標邏輯】只有 102.type(9) 才是地面物品，才可以被拾取；作為第一優先的判斷依據
				if (IsGroundItem(clickedEntity))
				{
					EnqueueTask(new AutoTask(AutoTaskType.PickUp, clickedEntity));
					return;
				}
				
				// 僅有伺服器血條數據時才區分對話/攻擊：Lawful>=0 彈對話，Lawful<0 可攻擊（選目標）
				if (clickedEntity.HasServerHp())
				{
					if (clickedEntity.Lawful >= 0)
					{
						TalkToNpc(clickedEntity.ObjectId);
						return;
					}
					_hud?.AddSystemMessage($"選擇目標: {clickedEntity.RealName} (按 Z 鍵攻擊)");
					return;
				}
				// 無血條數據（未收 S_HpMeter / 非生物）：視為點空地移動
				else
				{
					// 非地面物品、非怪物、非 NPC（如其他玩家）→ 視為點空地，移動
					StopAutoActions();
					Vector2I grid = CoordinateSystem.PixelToGrid(localMousePos.X, localMousePos.Y);
					int gx = grid.X;
					int gy = grid.Y;
					GD.Print($"[Input-Coord-Fix] ClickEntity->Move: GlobalMouse=({globalMousePos.X:F1},{globalMousePos.Y:F1}) LocalMouse=({localMousePos.X:F1},{localMousePos.Y:F1}) Grid=({gx},{gy})");
					StartWalking(gx, gy);
				}
			}
			// 2. 点击空地 -> 纯移动（世界像素轉地圖格：使用本地座標轉換）
			else
			{
				// 移动不走 TaskQueue，直接覆盖并停止其他行为
				StopAutoActions();
				
				Vector2I grid = CoordinateSystem.PixelToGrid(localMousePos.X, localMousePos.Y);
				int gx = grid.X;
				int gy = grid.Y;
				GD.Print($"[Input-Coord-Fix] ClickEmpty->Move: GlobalMouse=({globalMousePos.X:F1},{globalMousePos.Y:F1}) LocalMouse=({localMousePos.X:F1},{localMousePos.Y:F1}) Grid=({gx},{gy})");
				StartWalking(gx, gy);
			}
		}

		// =====================================================================
		// [SECTION] Input Helpers (自动行为与点选辅助)
		// =====================================================================
		
		// StopAutoActions 现已移至 Combat.cs 统一管理，但 Input.cs 作为 partial 类
		// 可以直接调用 Combat.cs 中的定义。
		// 这里保留 GetClickedEntity 辅助函数

		/// <summary>僅 102.type(9) 為地面物品，才可被拾取；作為第一優先的判斷依據。</summary>
		private static bool IsGroundItem(GameEntity e) =>
			e != null && ListSprLoader.Get(e.GfxId)?.Type == 9;

		private GameEntity GetClickedEntity(Vector2 mousePos)
		{
			GameEntity result = null;
			float minDst = 1000f;
			// mousePos 為 GetGlobalMousePosition()，須與實體全域座標比對，避免鏡頭偏移時點不到
			foreach (var entity in _entities.Values)
			{
				if (entity == _myPlayer) continue;
				// 排除死亡：僅對「有血條」實體排除死亡；地面物品（無血條）可點選以觸發拾取
				// 正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0
				if (entity.ShouldShowHealthBar() && entity.IsDead) continue;

				float dst = entity.GlobalPosition.DistanceTo(mousePos);
				if (dst < 40 && dst < minDst)
				{
					minDst = dst;
					result = entity;
				}
			}
			return result;
		}
	}
}
