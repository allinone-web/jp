using Godot;
using System;
using System.Collections.Generic;
using Client.Network;
using Client.Data;
using Client.UI; 

namespace Client.Game
{
	// GameWorld 的背包与拾取分部类
	public partial class GameWorld
	{
		//   数据存储 (Data)
		// 本地背包数据缓存
		// 1. 列表缓存：用于 UI 显示 (InvWindow 依然依赖 List 顺序)
		private List<InventoryItem> _myItems = new List<InventoryItem>();

		// 2. 字典缓存：用于逻辑快速查找 (O(1) 复杂度)
		// 假设你有一个存储物品的字典 (如果没有，请加上这个定义)
        // Key: ObjectId, Value: InventoryItem
		private Dictionary<int, InventoryItem> _inventory = new Dictionary<int, InventoryItem>();

		// ========================================================================
		//   初始化与绑定 (Setup)
		// ========================================================================
		private void BindInventorySignals()
		{
			if (PacketHandlerRef != null)
			{
				// 监听背包数据
				PacketHandlerRef.InventoryListReceived += OnInventoryListReceived;
				PacketHandlerRef.InventoryItemAdded += OnInventoryItemAdded;
				PacketHandlerRef.InventoryItemUpdated += OnInventoryItemUpdated;
				PacketHandlerRef.InventoryItemNameUpdated += OnInventoryItemNameUpdated;
				
				// 【关键修复】监听物品删除 (Opcode 23)
				if (PacketHandlerRef.HasSignal("InventoryItemDeleted"))
				{
					PacketHandlerRef.Connect("InventoryItemDeleted", new Callable(this, MethodName.OnInventoryItemDeleted));
				}
				else
				{
					GD.PrintErr("[Inventory] PacketHandler 缺少 InventoryItemDeleted 信号！无法处理物品删除。");
				}
				
				// 装备状态改变
				PacketHandlerRef.ItemEquipStatusChanged += OnItemEquipStatusChanged;

				// S_144 物品祝福/詛咒：更新本地 Bless，ItemTooltip 依 item.Bless 顯示金色
				PacketHandlerRef.ItemColorReceived += OnItemColorReceived;
			}
		}

		private void OnItemColorReceived(int objectId, int status)
		{
			if (_inventory.TryGetValue(objectId, out var item))
			{
				item.Bless = status;
				RefreshWindows();
			}
		}

		// ========================================================================
		//   公開 API：供 UI 獲取背包數據
		// ========================================================================
		/// <summary>獲取背包物品列表（供 WareHouseWindow 等 UI 使用）</summary>
		public List<InventoryItem> GetInventoryItems()
		{
			return new List<InventoryItem>(_myItems);
		}

		/// <summary>根據 ObjectId 獲取背包物品（供 WareHouseWindow 等 UI 使用）</summary>
		public InventoryItem GetInventoryItem(int objectId)
		{
			if (_inventory.TryGetValue(objectId, out var item))
				return item;
			return null;
		}
		// ========================================================================
		//   自动拾取逻辑 (Logic) - [完全保留]
		// ========================================================================
		private void UpdateInventoryLogic(double delta)
		{
			// 【修復】檢測服務器無回應的 UseItem 請求
			if (_lastUseItemTime > 0 && _lastUseItemObjectId > 0)
			{
				ulong currentTime = Time.GetTicksMsec();
				ulong elapsed = currentTime - _lastUseItemTime;
				
				// 如果超過 2 秒沒有收到回應，顯示提示
				if (elapsed > 2000)
				{
					if (_inventory.TryGetValue(_lastUseItemObjectId, out var item))
					{
						_hud?.AddSystemMessage($"無法裝備 {item.Name}：服務器無回應（可能是等級不足、職業不符、或變身狀態限制）");
					}
					
					// 清除標記
					_lastUseItemTime = 0;
					_lastUseItemObjectId = 0;
				}
			}
			
			// 使用主类定义的 _isAutoPickup 和 _autoTarget
			if (!_isAutoPickup || _autoTarget == null) return;

			// 检查目标是否存在
			if (!_entities.ContainsKey(_autoTarget.ObjectId))
			{
				// GD.Print("[Pickup] Target disappeared. Stop.");
				_autoTarget = null;
				_isAutoPickup = false;
				return;
			}

			// 计算距离
			int dist = Math.Max(Math.Abs(_myPlayer.MapX - _autoTarget.MapX), 
								Math.Abs(_myPlayer.MapY - _autoTarget.MapY));

			if (dist <= 1) // 走到距离1 (相邻或重叠)
			{
				StopWalking(); 
				PerformPickup(_autoTarget);
				
				// 拾取动作完成后重置
				_autoTarget = null;
				_isAutoPickup = false;
			}
			else
			{
				// 距离不够，自动走过去
				StartWalking(_autoTarget.MapX, _autoTarget.MapY);
			}
		}

		// --- 开始拾取 (由 HandleInput 调用) ---
		private void StartPickup(GameEntity target)
		{
			if (target == null) return;
			// GD.Print($"[Pickup] Target Locked: {target.Name}");
			
			_autoTarget = target;
			_isAutoPickup = true;
			_isAutoAttacking = false;
			_isAutoWalking = false;
			
			UpdateInventoryLogic(0);
		}

		// --- 发送拾取包 ---
		private void PerformPickup(GameEntity target)
		{
			// 全部拾取：送 target.ItemCount；伺服器要求 getCount()>=count 且 count>0
			int pickCount = (target.ItemCount > 0 && target.ItemCount <= int.MaxValue) ? target.ItemCount : 1;
			_netSession.Send(C_ItemPickupPacket.Make(target.ObjectId, target.MapX, target.MapY, pickCount));
			_myPlayer.SetAction(GameEntity.ACT_PICKUP);
		}
		
		// ========================================================================
		//   网络回调 (Callbacks) - [修改为调用 RefreshWindows]
		// ========================================================================

		// 收到完整背包列表
		private void OnInventoryListReceived(Godot.Collections.Array<InventoryItem> items)
		{
			
			// 1. 更新本地数据
			_myItems.Clear();
			_inventory.Clear();
			
			// 填充新数据
			foreach(var item in items) 
			{
			// GD.Print($"[Inventory] Loaded {_myItems.Count} items.");
				_myItems.Add(item);
				_inventory[item.ObjectId] = item;
			// 2. [核心修复] 登录时初始化武器外观
				// 如果该物品已装备且是武器，立即应用外观
				if (item.IsEquipped && IsWeapon(item.Type))
				{
					// 必须延迟一帧，等待 _myPlayer 初始化完成
					CallDeferred(nameof(ApplyInitialWeapon), item.Type);
				}
			}

			RefreshWindows(); 
		}


		// 延迟调用辅助方法 (用于登录初始化)
		private void ApplyInitialWeapon(int type)
        {
            // [修复故障 3 & 4] 使用转换函数
            if (_myPlayer != null) 
            {
                // [修復] 直接調用 SetWeaponType，不要手動轉換
                // SetWeaponType 內部會處理 ConvertServerTypeToVisualType
                _myPlayer.SetWeaponType(type);
            }
        }


		// 【服務器對齊】獲得新物品（對齊服務器 S_InventoryAdd Opcode 22）
		// 服務器 ItemInstance.pickup 在成功 insert 後會發送 S_InventoryAdd
		// 如果物品全部被拾取，服務器會調用 toDelete()，發送 S_ObjectRemove (Opcode 21)
		private void OnInventoryItemAdded(InventoryItem item)
		{
			_myItems.Add(item);
			_inventory[item.ObjectId] = item; // 同步字典
			_hud?.AddSystemMessage($"獲得: {item.Name}");
			// 【拾取修復】背包新增時，解除拾取節流（允許下一次拾取）
			_pickupInProgress = false;
			
			// 【拾取修復】如果當前有拾取任務，且物品已成功添加到背包，清理拾取任務
			// 注意：如果物品全部被拾取，服務器會發送 S_ObjectRemove (Opcode 21)，OnObjectDeleted 會清理任務
			// 如果物品部分被拾取，服務器會發送 S_ObjectAdd (Opcode 0)，物品數量會更新，任務可以繼續
			// 這裡只處理物品成功添加到背包的情況，物品實體的刪除由 OnObjectDeleted 處理
			
			// 刷新 UI
			RefreshWindows();
		}

		// 物品状态/数量更新 (Opcode 111)，detailInfo 為鑑定後擴充資料（攻擊/防禦/職業等）
		private void OnInventoryItemUpdated(int objectId, string name, int count, int status, string detailInfo)
		{
			// 同时更新 List 和 Dictionary
			if (_inventory.TryGetValue(objectId, out var item))
			{
				// 【关键修复】如果数量 <= 0，直接移除
				if (count <= 0)
				{
					_myItems.Remove(item);
					_inventory.Remove(objectId);
					GD.Print($"[Inventory] Item {name} count is 0. Removed.");
				}
				else
				{
					item.Name = name;
					item.Count = count;
					item.Ident = (status != 0) ? 1 : 0;
					item.DetailInfo = detailInfo ?? "";
					if (status >= 1 || name.Contains("裝備") || name.Contains("手中"))
						item.IsEquipped = true;
					else
						item.IsEquipped = false;
				}

				RefreshWindows();
			}
		}
		
		// 物品名稱/裝備狀態更新 (Opcode 195)
		private void OnInventoryItemNameUpdated(int objectId, string name, bool isEquipped)
		{
			if (_inventory.TryGetValue(objectId, out var item))
			{
				bool equipChanged = item.IsEquipped != isEquipped;
				item.Name = name;
				item.IsEquipped = isEquipped;
				
				if (equipChanged)
				{
					OnItemEquipStatusChanged(objectId, isEquipped);
					return;
				}
				
				RefreshWindows();
			}
		}

		// 4. 物品删除 (Opcode 23)
		private void OnInventoryItemDeleted(int objectId)
		{
			// 从字典中移除
			if (_inventory.ContainsKey(objectId))
			{
				_inventory.Remove(objectId);
			}

			// 从列表中移除
			int idx = _myItems.FindIndex(x => x.ObjectId == objectId);
			if (idx != -1)
			{
				// GD.Print($"[Inventory] Item Deleted: {_myItems[idx].Name} (ID: {objectId})");
				_myItems.RemoveAt(idx);
				RefreshWindows();
			}
		}

		// 5. 装备状态改变 (穿上/脱下) - Opcode 24
		private void OnItemEquipStatusChanged(int objectId, bool isEquipped)
		{
			// 【修復】清除超時檢測標記
			if (objectId == _lastUseItemObjectId)
			{
				_lastUseItemTime = 0;
				_lastUseItemObjectId = 0;
			}
			
			// 优先使用字典查找 (速度快)
			if (_inventory.TryGetValue(objectId, out var item))
			{
				// 更新状态
				item.IsEquipped = isEquipped;
				GD.Print($"[Inventory] Item {item.Name} Equipped: {isEquipped} (Type:{item.Type})");

				// [核心修复] 立即通知主角更新外观
				// 只有当物品是武器时才触发
				if (IsWeapon(item.Type))
				{
					if (_myPlayer != null)
					{
						if (isEquipped)
						{
                            // [修復] 直接調用 SetWeaponType，傳入服務端類型
                            _myPlayer.SetWeaponType(item.Type);
						}
						else
						{
							// 卸下武器：恢復空手 (0)
                            // 這裡傳入 0，SetWeaponType(0) 會處理為空手
							_myPlayer.SetWeaponType(0); 
						}
					}
				}

				// 刷新 UI
				RefreshWindows();
			}
		}

		/// <summary>丟棄/刪除背包物品（對齊伺服器 C_DeleteInventoryItem opcode 118）。由刪除按鈕等呼叫。</summary>
		public void DeleteItem(int itemObjectId)
		{
			_netSession?.Send(C_DeleteInventoryItemPacket.Make(itemObjectId));
			GD.Print($"[Inventory] DeleteItem sent: objectId={itemObjectId}");
		}

		/// <summary>將物品丟到地面（對齊 jp C_DropItem opcode 54）。拖到 ItemDropZone 時由 OnItemDroppedToGround 呼叫。</summary>
		public void SendDropItem(int itemObjectId, int count)
		{
			if (_netSession == null) return;
			int x = _myPlayer != null ? _myPlayer.MapX : 0;
			int y = _myPlayer != null ? _myPlayer.MapY : 0;
			_netSession.Send(C_DropItemPacket.Make(x, y, itemObjectId, count));
		}

		// 判斷是否為武器類型
		private bool IsWeapon(int type)
		{
			// 根据 L1J 标准：1=Sword ... 12=Claw
			// 1=剑, 2=匕首, 3=双剑, 4=弓, 5=矛, 6=斧, 7=杖, 11=双刀, 12=爪
			// 箭(Arrow=10) 虽然是 Type 10 但不是主手武器，需排除
			return (type >= 1 && type <= 7) || type == 11 || type == 12;
		}

		/// <summary>取得當前裝備的武器之 ObjectId（即伺服器 InvID，用於技能 9 擬似魔法武器）。無裝備武器時回傳 0。</summary>
		public int GetEquippedWeaponObjectId()
		{
			foreach (var item in _myItems)
			{
				if (item != null && item.IsEquipped && IsWeapon(item.Type))
					return item.ObjectId;
			}
			return 0;
		}

		/// <summary>是否為防具類型（對齊 server ItemsTable armor type=16、BlessedArmor getType()==16）。</summary>
		private static bool IsArmor(int type)
		{
			return type == 16;
		}

		/// <summary>取得當前裝備的盔甲之 ObjectId（即伺服器 InvID，用於技能 15 鎧甲護持）。無裝備盔甲時回傳 0。</summary>
		public int GetEquippedArmorObjectId()
		{
			foreach (var item in _myItems)
			{
				if (item != null && item.IsEquipped && IsArmor(item.Type))
					return item.ObjectId;
			}
			return 0;
		}

		// ========================================================================
		//   交互逻辑 (Interaction)
		// ========================================================================
		// 【協議對齊】C_UseItem 封包格式依伺服器 use_type 決定後續欄位（與 ItemTable._useTypes / C_UseItem.java 一致）

		/// <summary>依伺服器 use_type 寫入 C_UseItem 的 objectId 之後的參數。useType 即 item.Type（伺服器下發），不依 ItemId。</summary>
		private void AppendC_UseItemPayload(PacketWriter w, int useType)
		{
			if (useType == ItemUseType.Sosc)
			{
				// 變身卷軸(sosc)：readS() → 由 UsePolymorphScroll 發送；此處雙擊無選擇則送空字串
				w.WriteString("");
			}
			else if (useType == ItemUseType.Identify || useType == ItemUseType.Choice || useType == ItemUseType.Dai || useType == ItemUseType.Zel)
			{
				// 鑑定、choice、武器強化、防具強化：readD()
				w.WriteInt(0);
			}
			else if (useType == ItemUseType.Ntele || useType == ItemUseType.Btele)
			{
				// 傳送卷軸、祝福傳送：readH(mapId)+readD(bookmarkId)
				w.WriteShort(0);
				w.WriteInt(0);
			}
			else if (useType == ItemUseType.Blank)
			{
				// 空白卷軸：readC(skillid)
				w.WriteByte(0);
			}
			else if (useType == ItemUseType.SpellBuff)
			{
				// スペルスクロール：readD()，對自己時送自身 ObjectId
				w.WriteInt(_myObjectId > 0 ? _myObjectId : 0);
			}
			else if (useType == 5 || useType == 17 || useType == 39)
			{
				// spell_long、spell_short、spell_point：readD()+readH()+readH()
				w.WriteInt(0);
				w.WriteShort(0);
				w.WriteShort(0);
			}
			else if (useType == ItemUseType.Res)
			{
				// 復活卷軸：readD()
				w.WriteInt(0);
			}
			else if (useType == ItemUseType.FishingRod)
			{
				// 釣竿：readH()+readH()
				w.WriteShort(0);
				w.WriteShort(0);
			}
			else
			{
				// normal、weapon、armor、其餘：readC()
				w.WriteByte(0);
			}
		}

		// 使用/装备物品 (UI Slot 点击回调)
		public void UseItem(int objectId)
		{
			if (_netSession == null)
			{
				GD.PrintErr($"[UseItem] 錯誤：網絡會話為 null，無法發送封包");
				_hud?.AddSystemMessage("網絡連接錯誤，無法使用物品");
				return;
			}

			if (!_inventory.TryGetValue(objectId, out var item))
			{
				GD.PrintErr($"[UseItem] 錯誤：找不到物品 ObjectId={objectId}");
				_hud?.AddSystemMessage("找不到該物品");
				return;
			}

			GD.Print($"[UseItem] 發送封包：Opcode=44, ObjectId={objectId}, Name={item.Name}, Type={item.Type}, Count={item.Count}");
			_lastUseItemTime = Time.GetTicksMsec();
			_lastUseItemObjectId = objectId;

			var w = new PacketWriter();
			w.WriteByte(44); // 【JP協議對齊】C_OPCODE_USEITEM (jp)
			w.WriteInt(objectId);
			AppendC_UseItemPayload(w, item.Type);

			_netSession.Send(w.GetBytes());

			// 樂觀更新：立即減少本地數量，讓背包介面 Count 即時更新；伺服器 Opcode 111 回傳後會再次同步
			if (item.Count > 1)
			{
				item.Count--;
			}
			CallDeferred(nameof(_DeferredRefreshAfterUseItem));
		}
		
		// 【修復】用於檢測服務器無回應的變量
		private ulong _lastUseItemTime = 0;
		private int _lastUseItemObjectId = 0;
		
		/// <summary>當收到系統消息時調用，清除 UseItem 超時檢測（表示服務器有回應）</summary>
		public void OnSystemMessageReceived()
		{
			_lastUseItemTime = 0;
			_lastUseItemObjectId = 0;
		}

		/// <summary>變身卷軸：發送 C_ItemClick(28) + objectId + polyName，與伺服器 ScrollPolymorph.clickItem(bp.readS()) 對齊。</summary>
		public void UsePolymorphScroll(int scrollObjectId, string polyDb)
		{
			if (string.IsNullOrEmpty(polyDb)) return;
			var w = new PacketWriter();
			w.WriteByte(44); // 【JP協議對齊】C_OPCODE_USEITEM (jp)
			w.WriteInt(scrollObjectId);
			w.WriteString(polyDb);
			_netSession.Send(w.GetBytes());
			CallDeferred(nameof(_DeferredRefreshAfterUseItem));
		}

		/// <summary>鑑定卷軸：發送 C_ItemClick(28) + 卷軸 objectId + 目標裝備 objectId，與伺服器 ScrollLabeledKERNODWEL.clickItem(bp.readD()) 對齊。</summary>
		public void UseIdentifyScroll(int scrollObjectId, int targetItemObjectId)
		{
			var w = new PacketWriter();
			w.WriteByte(44); // 【JP協議對齊】C_OPCODE_USEITEM (jp)
			w.WriteInt(scrollObjectId);
			w.WriteInt(targetItemObjectId);
			_netSession.Send(w.GetBytes());
			CallDeferred(nameof(_DeferredRefreshAfterUseItem));
		}

		private void _DeferredRefreshAfterUseItem()
		{
			RefreshWindows();
		}
		
		// 【修復】檢測服務器無回應的情況（合併到上面的 UpdateInventoryLogic）
		// 注意：此方法已在第53行定義，此處為重複定義，已刪除
	}
}
