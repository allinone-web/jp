using Godot;
using System;
using System.Collections.Generic;
using Client.Network;
using Client.Data;
using Client.UI;

namespace Client.Game
{
	/// <summary>
	/// 寵物系統：抓寵、喂食、寵物倉庫、寵物面板
	/// </summary>
	public partial class GameWorld
	{
		// ========================================================================
		// 寵物系統數據
		// ========================================================================
		/// <summary>我的寵物 ObjectId 列表（用於判斷點擊的是否為自己的寵物）</summary>
		private HashSet<int> _myPetObjectIds = new HashSet<int>();
		
		/// <summary>當前選中的目標（用於喂食）</summary>
		private GameEntity _feedTarget = null;
		
		/// <summary>當前選中的物品（用於喂食）</summary>
		private int _feedItemObjectId = 0;
		
		// ========================================================================
		// 初始化與綁定
		// ========================================================================
		private void BindPetSignals()
		{
			if (PacketHandlerRef != null)
			{
				PacketHandlerRef.PetWarehouseListReceived += OnPetWarehouseListReceived;
				// 【修復】不再使用 PetPanelReceived，改為使用 ShowHtmlReceived
				// PacketHandlerRef.PetPanelReceived += OnPetPanelReceived;
				PacketHandlerRef.PetStatusChanged += OnPetStatusChanged;
			}
		}
		
		// ========================================================================
		// 喂食邏輯（抓寵物）
		// ========================================================================
		
		/// <summary>
		/// 喂食怪物（用於抓寵物）
		/// 流程：1. 攻擊怪物造成傷害 2. 從背包拖動肉（item id=12，nameidN=23）給怪物
		/// </summary>
		/// <param name="targetObjectId">目標怪物ObjectId</param>
		/// <param name="itemObjectId">物品ObjectId（肉，item id=12，nameidN=23）</param>
		/// <param name="count">數量（通常為1）</param>
		public void FeedMonster(int targetObjectId, int itemObjectId, int count = 1)
		{
			if (_netSession == null)
			{
				GD.PrintErr("[Pet] 網絡會話為 null，無法發送喂食封包");
				return;
			}
			
			// 檢查目標是否存在
			if (!_entities.TryGetValue(targetObjectId, out var target))
			{
				GD.PrintErr($"[Pet] 目標不存在: ObjectId={targetObjectId}");
				_hud?.AddSystemMessage("目標不存在");
				return;
			}
			
			// 檢查物品是否存在
			if (!_inventory.TryGetValue(itemObjectId, out var item))
			{
				GD.PrintErr($"[Pet] 物品不存在: ObjectId={itemObjectId}");
				_hud?.AddSystemMessage("物品不存在");
				return;
			}
			
			// 檢查距離（必須在1格內）
			int dist = GetGridDistance(_myPlayer.MapX, _myPlayer.MapY, target.MapX, target.MapY);
			if (dist > 1)
			{
				_hud?.AddSystemMessage("距離太遠，無法喂食");
				// 自動走向目標
				StartWalking(target.MapX, target.MapY);
				_feedTarget = target;
				_feedItemObjectId = itemObjectId;
				return;
			}
			
			// 發送喂食封包（C_GiveItem Opcode 244）
			_netSession.Send(C_GiveItemPacket.Make(targetObjectId, itemObjectId, count));
			GD.Print($"[Pet] 發送喂食封包: Target={targetObjectId} Item={itemObjectId} Count={count}");
			
			// 播放喂食動作
			_myPlayer.SetAction(GameEntity.ACT_PICKUP); // 使用拾取動作作為喂食動作
		}
		
		/// <summary>
		/// 檢查移動完成後是否需要繼續喂食
		/// </summary>
		private void CheckFeedAfterMove()
		{
			if (_feedTarget != null && _feedItemObjectId > 0)
			{
				int dist = GetGridDistance(_myPlayer.MapX, _myPlayer.MapY, _feedTarget.MapX, _feedTarget.MapY);
				if (dist <= 1)
				{
					FeedMonster(_feedTarget.ObjectId, _feedItemObjectId, 1);
					_feedTarget = null;
					_feedItemObjectId = 0;
				}
			}
		}
		
		// ========================================================================
		// 寵物倉庫系統
		// ========================================================================
		
		/// <summary>
		/// 打開寵物倉庫（與NPC對話，發送 type=12 的 C_Shop 封包）
		/// </summary>
		/// <param name="npcId">寵物倉庫NPC的ObjectId</param>
		public void OpenPetWarehouse(int npcId)
		{
			if (_netSession == null)
			{
				GD.PrintErr("[Pet] 網絡會話為 null，無法打開寵物倉庫");
				return;
			}
			
			// 發送 C_Shop type=12 請求寵物列表
			// 服務器會返回 S_ObjectPet (Opcode 49, option=12) 寵物列表
			var w = new PacketWriter();
			w.WriteByte(16); // C_OPCODE_SHOP
			w.WriteInt(npcId);
			w.WriteByte(12); // type=12 表示請求寵物倉庫列表
			w.WriteUShort(0); // count=0，只是請求列表
			_netSession.Send(w.GetBytes());
			
			GD.Print($"[Pet] 請求寵物倉庫列表: NpcId={npcId}");
		}
		
		/// <summary>
		/// 領取寵物（從寵物倉庫）
		/// </summary>
		/// <param name="npcId">寵物倉庫NPC的ObjectId</param>
		/// <param name="collarInvId">項圈的inv_id（背包中的ObjectId）</param>
		public void RetrievePet(int npcId, int collarInvId)
		{
			if (_netSession == null)
			{
				GD.PrintErr("[Pet] 網絡會話為 null，無法領取寵物");
				return;
			}
			
			// 發送 C_Shop type=12 領取寵物封包
			_netSession.Send(C_ShopPacket.MakePetGet(npcId, collarInvId));
			GD.Print($"[Pet] 領取寵物: NpcId={npcId} CollarInvId={collarInvId}");
		}
		
		/// <summary>
		/// 處理收到的寵物倉庫列表
		/// </summary>
		private void OnPetWarehouseListReceived(int npcId, Godot.Collections.Array items)
		{
			GD.Print($"[Pet] 收到寵物倉庫列表: NpcId={npcId} Count={items.Count}");
			
			// 打開寵物倉庫窗口
			var context = new WindowContext
			{
				NpcId = npcId,
				Type = 12, // 標記為寵物倉庫
				ExtraData = items
			};
			UIManager.Instance?.Open(WindowID.PetWarehouse, context);
		}
		
		// ========================================================================
		// 寵物面板系統
		// ========================================================================
		
		/// <summary>
		/// 【已廢棄】處理收到的寵物/召喚物面板數據
		/// 【修復】現在改為使用 TalkWindow 顯示，此方法不再使用
		/// </summary>
		private void OnPetPanelReceived(int petId, string panelType, Godot.Collections.Dictionary petData)
		{
			// 【修復】不再使用 PetPanelWindow，改為使用 TalkWindow
			// 數據已通過 ShowHtmlReceived 信號傳遞給 TalkWindow
			GD.Print($"[Pet] OnPetPanelReceived 已廢棄，改用 TalkWindow");
		}
		
		/// <summary>
		/// 【修復】打開寵物/召喚物面板（點擊時調用）
		/// 現在改為使用 TalkWindow 顯示，服務器會發送 S_ObjectPet (Opcode 42) 面板數據（"anicom" 或 "moncom"）
		/// </summary>
		/// <param name="petObjectId">寵物/召喚物的ObjectId</param>
		private void OpenPetPanel(int petObjectId)
		{
			// 【修復】客戶端無法直接請求寵物面板，需要服務器主動發送
			// 通常點擊寵物/召喚物時，服務器會自動發送 S_ObjectPet (Opcode 42)
			// 數據會通過 ShowHtmlReceived 信號傳遞給 TalkWindow
			GD.Print($"[Pet] 請求打開寵物/召喚物面板（使用 TalkWindow）: ObjectId={petObjectId}");
		}
		
		// ========================================================================
		// 寵物狀態管理
		// ========================================================================
		
		/// <summary>
		/// 處理寵物狀態變更（Opcode 79）
		/// </summary>
		private void OnPetStatusChanged(int petId, int status)
		{
			GD.Print($"[Pet] 寵物狀態變更: PetId={petId} Status={status}");
			
			// 【寵物系統】標記為己方寵物
			_myPetObjectIds ??= new HashSet<int>();
			_myPetObjectIds.Add(petId);
			
			// 更新寵物狀態（如果需要）
			if (_entities.TryGetValue(petId, out var pet))
			{
				// 可以根據狀態更新寵物的視覺表現
				// 0=休息, 1=攻擊, 2=跟隨, 3=防禦, 4=停留, 5=警戒
			}
			
			// 【修復】不再使用 PetPanelWindow，改為使用 TalkWindow
			// 如果 TalkWindow 已打開且顯示的是寵物面板，可以通過重新請求服務器數據來更新
			// 這裡暫時不處理，因為服務器會在需要時主動發送更新
		}
		
		/// <summary>
		/// 檢查項圈物品（item id=308）並更新顯示
		/// </summary>
		private void UpdateCollarItems()
		{
			// 項圈物品的名稱格式：服務器會發送 "項圈 [Lv.X 寵物名]"
			// 這個格式已經在服務器端處理，客戶端只需要顯示即可
			RefreshWindows();
		}
	}
}
