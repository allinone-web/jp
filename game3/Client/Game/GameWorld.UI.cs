using Godot;
using Client.UI;
using Client.Network;

namespace Client.Game
{
	// =========================================================================
	// [FILE] GameWorld.UI.cs
	// 说明：UI 刷新与 UI 桥接（系统消息、Options 按钮回调）。
	// =========================================================================
	public partial class GameWorld
	{
		// =====================================================================
		// [SECTION] UI Refresh: RefreshWindows (数据变化时刷新各窗口)
		// 说明：在网络回调或打开窗口时调用，根据窗口是否可见决定刷新内容。
		// =====================================================================
		private void RefreshWindows()
		{
			// 1) 刷新背包（若打开）
			var invWin = UIManager.Instance.GetWindow(WindowID.Inventory) as InvWindow;
			if (invWin != null && invWin.Visible)
			{
				invWin.RefreshInventory(_myItems);
			}

			// 2) 刷新角色（若打开）
			var chaWin = UIManager.Instance.GetWindow(WindowID.Character) as ChaWindow;
			if (chaWin != null && chaWin.Visible)
			{
				var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
				if (boot != null)
				{
					chaWin.UpdateData(boot.MyCharInfo, _myItems);
				}
			}

			// 3) 刷新技能面板（若打开）
			if (UIManager.Instance.IsOpen(WindowID.Skill))
			{
				var win = UIManager.Instance.GetWindow(WindowID.Skill) as Client.UI.SkillWindow;
				if (win != null)
				{
					win.RefreshSkills(GetLearnedSkillMasks());
				}
			}

			// 4) 刷新倉庫窗口（若打开）
			var warehouseWin = UIManager.Instance.GetWindow(WindowID.WareHouse) as Client.UI.WareHouseWindow;
			if (warehouseWin != null && warehouseWin.Visible)
			{
				warehouseWin.UpdateInventoryDisplay();
			}
		}
		// =====================================================================
		// [SECTION END] UI Refresh: RefreshWindows
		// =====================================================================


		// =====================================================================
		// [SECTION] HUD/System Message Bridge
		// 说明：将系统消息输出到 HUD（如存在）。
		// =====================================================================
		private void OnSystemMessage(string message)
		{
			_hud?.AddSystemMessage(message);
		}

		/// <summary>供 InvWindow 等呼叫，在 HUD 聊天區顯示系統訊息。</summary>
		public void AddSystemMessage(string message)
		{
			_hud?.AddSystemMessage(message);
		}

		/// <summary>依當前角色從 ClientConfig 刷新快捷欄（切換角色進入世界時由 Boot 呼叫）。</summary>
		internal void RefreshBottomBarHotkeysFromConfig()
		{
			var bar = _hud?.GetNodeOrNull<Client.UI.BottomBar>("BottomBar");
			bar?.RefreshHotkeysFromConfig();
		}
		// =====================================================================
		// [SECTION END] HUD/System Message Bridge
		// =====================================================================


		// =====================================================================
		// [SECTION] Options / Quit Bridge (菜单按钮回调入口)
		// 说明：给 OptionsWindow 调用的接口（目前保持原逻辑）。
		// =====================================================================
		/// <summary>Restart = 送 C_OPCODE_RESTART(71) 給伺服器，由伺服器重置玩家並傳回地圖/角色狀態封包。</summary>
		public void SendRestartRequest()
		{
			GD.Print("[GameWorld] SendRestartRequest -> Send C_OPCODE_RESTART(71) to server");
			UIManager.Instance?.Close(WindowID.Options);
			UIManager.Instance?.Close(WindowID.Talk);
			_deathDialogShown = false;

			if (_netSession == null)
			{
				GD.PrintErr("[GameWorld] SendRestartRequest failed: net session is null");
				return;
			}

			var w = new PacketWriter();
			w.WriteByte(71); // C_OPCODE_RESTART (jp)
			_netSession.Send(w.GetBytes());
		}

		/// <summary>清空所有實體與玩家鎖定，退回選角後再進世界時由新角色正確設 _myPlayer。同時清空技能掩碼，避免重登後切換角色仍顯示上一角技能。</summary>
		internal void ClearWorldState()
		{
			StopAutoActions();
			// 攝影機從舊玩家身上移回 GameWorld，並設為當前鏡頭，避免重登後看不到新角色
			if (_camera != null && IsInstanceValid(_camera))
			{
				_camera.Reparent(this);
				_camera.Position = Vector2.Zero;
				_camera.MakeCurrent();
			}
			_myPlayer = null;
			_myObjectId = 0;
			_mySummonObjectIds?.Clear();
			_myPetObjectIds?.Clear();
			_isAutoWalking = false;
			_targetMapX = 0;
			_targetMapY = 0;
			_moveTimer = 0;
			// 清空背包，重登後由伺服器 S_InventoryList 重新載入新角色背包
			_myItems.Clear();
			_inventory.Clear();
			_bookmarks.Clear();
			// 清空技能掩碼，重登/切換角色後由伺服器 S_SkillAdd 重新載入，避免妖精看到法師魔法等混合問題
			for (int i = 0; i < _skillMasks.Length; i++)
				_skillMasks[i] = 0;
			// 若技能視窗已開啟則刷新為空，避免重登後按 S 仍顯示上一角技能
			if (UIManager.Instance != null && UIManager.Instance.IsOpen(WindowID.Skill))
			{
				var skillWin = UIManager.Instance.GetWindow(WindowID.Skill) as Client.UI.SkillWindow;
				if (skillWin != null) skillWin.RefreshSkills(GetLearnedSkillMasks());
			}
			// 若背包視窗已開啟則刷新為空，避免重登後仍顯示上一角物品
			if (UIManager.Instance != null && UIManager.Instance.IsOpen(WindowID.Inventory))
			{
				var invWin = UIManager.Instance.GetWindow(WindowID.Inventory) as Client.UI.InvWindow;
				if (invWin != null) invWin.RefreshInventory(_myItems);
			}
			foreach (var kv in _entities)
			{
				var entity = kv.Value;
				if (entity != null && IsInstanceValid(entity))
				{
					UnsubscribeEntityEvents(entity);
					entity.QueueFree();
				}
			}
			_entities.Clear();
		}

		public void SendQuitRequest()
		{
			_netSession?.Send(C_QuitGamePacket.Make());
			GD.Print("发送退出请求...");
			GetTree().Quit();
		}

		/// <summary>返回角色選單。發送 C_ReturnToLogin(218) 後斷線並切換到選角場景。對齊 jp C_ReturnToLogin。</summary>
		public void SendReturnToLoginRequest()
		{
			if (_netSession != null)
			{
				_netSession.Send(C_ReturnToLoginPacket.Make());
				_netSession.Disconnect();
			}
			UIManager.Instance?.Close(WindowID.Options);
			UIManager.Instance?.Close(WindowID.Talk);
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			boot?.ToCharacterSelectScene();
		}

		/// <summary>玩家死亡時彈出重生/退出提示視窗（TalkWindow），避免重複彈出。</summary>
		private void ShowDeathDialogIfNeeded()
		{
			if (_deathDialogShown) return;
			_deathDialogShown = true;

			// 使用 TalkWindow 顯示本地 HTML（Assets/text/html/restart*.html）
			UIManager.Instance?.Open(WindowID.Talk, new WindowContext
			{
				ExtraData = new System.Collections.Generic.Dictionary<string, object>
				{
					{ "npc_id", 0 },
					{ "summon_object_id", 0 },
					{ "html_id", "html/restart" },
					{ "args", new string[0] }
				}
			});
		}


		/// <summary>發送數量確認封包 C_Amount(109)。當 AmountInputWindow 因伺服器請求（如 S_OPCODE_INPUTAMOUNT 253）開啟時，確認回調應呼叫此方法。</summary>
		public void SendAmountResponse(int objectId, int amount, byte c, string s)
		{
			_netSession?.Send(C_AmountPacket.Make(objectId, amount, c, s ?? ""));
		}

		/// <summary>S_MessageYN(155) Yes/No 請求：使用通用對話窗口 TalkWindow 載入 html/yesno，選擇後發送 C_Attr(61)。</summary>
		private void OnYesNoRequestReceived(int type, int yesNoCount, string msg1, string msg2, string msg3)
		{
			var args = new string[] { msg1 ?? "", msg2 ?? "", msg3 ?? "" };
			UIManager.Instance?.Open(WindowID.Talk, new WindowContext
			{
				ExtraData = new System.Collections.Generic.Dictionary<string, object>
				{
					{ "npc_id", 0 },
					{ "summon_object_id", 0 },
					{ "html_id", "html/yesno" },
					{ "args", args },
					{ "yesno_type", type }
				}
			});
		}

		/// <summary>發送 Yes/No 回傳封包 C_Attr(61)。對齊 jp C_Attr：writeC(61), writeH(attrCode)。</summary>
		public void SendYesNoResponse(int type, bool yes)
		{
			ushort attrCode = (ushort)(yes ? type : 0);
			_netSession?.Send(C_AttrPacket.MakeWithAttr(attrCode));
		}

		private void OnBookmarkReceived(string name, int mapId, int id, int x, int y)
		{
			_bookmarks.Add((name, mapId, id, x, y));
		}

		/// <summary>取得目前快取的記憶座標列表（S_11 累積）。</summary>
		public System.Collections.Generic.List<(string name, int mapId, int id, int x, int y)> GetBookmarks()
		{
			return new System.Collections.Generic.List<(string, int, int, int, int)>(_bookmarks);
		}

		/// <summary>發送傳送到記憶座標 C_41 (type 0x0b)。</summary>
		public void SendTeleportToBookmark(string name, int mapId, int x, int y)
		{
			_netSession?.Send(C_SendLocationPacket.MakeTeleportToBookmark(name, mapId, x, y));
		}

		/// <summary>發送增加記憶座標 C_134（當前位置以 name 記錄）。</summary>
		public void SendAddBookmark(string name)
		{
			_netSession?.Send(C_BookmarkPacket.Make(name));
		}

		/// <summary>發送刪除記憶座標 C_223。</summary>
		public void SendDeleteBookmark(string bookmarkName)
		{
			_netSession?.Send(C_BookmarkDeletePacket.Make(bookmarkName));
			_bookmarks.RemoveAll(b => b.name == bookmarkName);
		}

		/// <summary>發送拒絕名單 C_101（遮斷/解除）。對齊 jp C_Exclude。</summary>
		public void SendExclude(string name)
		{
			_netSession?.Send(C_ExcludePacket.Make(name ?? ""));
		}

		/// <summary>激活 HUD 輸入框，提示用戶輸入記憶座標名稱；下一次提交時當作書籤名稱發送 C_134（由 HUD 回調處理，不當聊天送出）。</summary>
		public void RequestBookmarkNameInput()
		{
			_hud?.SetInputPrompt("請輸入你想要記住座標的名稱");
			_hud?.SetSubmitCallback(text =>
			{
				string name = (text ?? "").Trim();
				if (!string.IsNullOrEmpty(name))
				{
					SendAddBookmark(name);
					AddSystemMessage("[color=green]已發送增加記憶座標請求。[/color]");
				}
			});
			_hud?.FocusChatInput();
		}

		/// <summary>發送封包到服務器（用於倉庫等需要直接發送封包的場景）</summary>
		public void SendPacket(byte[] data)
		{
			if (data == null || _netSession == null) return;
			_netSession.Send(data);
		}

		// =====================================================================
		// [SECTION] S_4 / S_43 / S_253 / S_81 回調
		// =====================================================================
		private void OnTeleportReceived(int objectId)
		{
			// 傳送動畫可選：目前僅收包，地圖切換由 S_150 處理
		}

		private void OnIdentifyDescReceived(int descId, string message)
		{
			RefreshWindows();
		}

		private void OnInputAmountRequested(int objectId, int max, string htmlId)
		{
			int objId = objectId;
			string hId = htmlId ?? "";
			var ctx = new WindowContext
			{
				ExtraData = new System.Collections.Generic.Dictionary<string, object>
				{
					{ "prompt", $"請輸入數量 (1-{max})" },
					{ "max", max },
					{ "objectId", objId },
					{ "htmlId", hId },
					{ "onConfirm", (System.Action<int>)(amount => SendAmountResponse(objId, amount, 0, hId)) }
				}
			};
			UIManager.Instance?.Open(WindowID.AmountInput, ctx);
		}

		private void OnObjectNameChanged(int objectId, string name)
		{
			if (_entities.TryGetValue(objectId, out var entity))
				entity.SetDisplayName(name ?? "");
		}

		// =====================================================================
		// [SECTION END] Options / Quit Bridge
		// =====================================================================
	}
}
