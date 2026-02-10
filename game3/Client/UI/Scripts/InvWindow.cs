using Godot;
using System.Collections.Generic;
using Client.Data;
using Client.Game;

namespace Client.UI
{
	public partial class InvWindow : GameWindow
	{
		private GridContainer _grid;
		private PackedScene _slotScene;
		private Button _deleteBtn;
		private Button _checkBtn;
		private HashSet<int> _selectedItemObjectIds = new HashSet<int>(); // 【多選模式】選中的物品 ID 集合
		/// <summary>鑑定卷軸模式：雙擊卷軸後暫存卷軸 ObjectId，再單擊目標裝備即鑑定。</summary>
		private int? _pendingIdentifyScrollId;
		
		// 【優化】緩存 GameWorld 引用，避免重複查找
		private GameWorld _gameWorld;

		public override void _Ready()
		{
			base._Ready();

			_grid = FindChild("Grid", true, false) as GridContainer;
			if (_grid == null) GD.PrintErr("[InvWindow] 找不到 'Grid' 节点！");

			_slotScene = ResourceLoader.Load<PackedScene>("res://Client/UI/Scenes/Components/InventorySlot.tscn");

			// 【優化】緩存 GameWorld 引用，避免重複查找
			_gameWorld = GetNodeOrNull<GameWorld>("/root/Boot/World");

			// 【修復】查找正確的按鈕名稱 "delete" 和 "check"
			_deleteBtn = FindChild("delete", true, false) as Button;
			if (_deleteBtn != null)
				_deleteBtn.Pressed += OnDeleteBtnPressed;
			else
				GD.PrintErr("[InvWindow] 找不到 'delete' 节点，請在場景中新增刪除按鈕。");

			_checkBtn = FindChild("check", true, false) as Button;
			if (_checkBtn != null)
				_checkBtn.Pressed += OnCheckBtnPressed;
			else
				GD.PrintErr("[InvWindow] 找不到 'check' 节点，請在場景中新增鑑定按鈕。");
		}

		private void OnDeleteBtnPressed()
		{
			// 【多選模式】刪除所有選中的物品
			if (_selectedItemObjectIds.Count == 0) 
			{
				_gameWorld?.AddSystemMessage("請先選擇要刪除的物品");
				return;
			}
			
			if (_gameWorld != null)
			{
				foreach (var itemId in _selectedItemObjectIds)
				{
					_gameWorld.DeleteItem(itemId);
					GD.Print($"[InvWindow] 刪除物品：ObjectId={itemId}");
				}
				_selectedItemObjectIds.Clear();
			}
		}

		private void OnCheckBtnPressed()
		{
			// 檢查是否有待鑑定的卷軸
			if (!_pendingIdentifyScrollId.HasValue)
			{
				_gameWorld?.AddSystemMessage("請先雙擊鑑定卷軸，然後單擊要鑑定的裝備");
				return;
			}

			// 【多選模式】鑑定所有選中的裝備
			if (_selectedItemObjectIds.Count == 0)
			{
				_gameWorld?.AddSystemMessage("請先選擇要鑑定的裝備");
				return;
			}

			// 使用鑑定卷軸鑑定選中的裝備
			if (_gameWorld != null)
			{
				foreach (var itemId in _selectedItemObjectIds)
				{
					_gameWorld.UseIdentifyScroll(_pendingIdentifyScrollId.Value, itemId);
					GD.Print($"[InvWindow] 鑑定物品：ObjectId={itemId}");
				}
				_pendingIdentifyScrollId = null;
				_selectedItemObjectIds.Clear();
			}
		}

		// 刷新背包
		public void RefreshInventory(List<InventoryItem> items)
		{
			if (_grid == null || _slotScene == null) return;

			// 清空旧数据
			foreach (var child in _grid.GetChildren()) child.QueueFree();

			// 【優化】使用緩存的 GameWorld 引用
			if (_gameWorld == null)
			{
				_gameWorld = GetNodeOrNull<GameWorld>("/root/Boot/World");
			}

			// 生成新数据
			foreach (var item in items)
			{
				var slot = _slotScene.Instantiate() as InventorySlot;
				_grid.AddChild(slot);
				slot.SetItem(item);
				
				// 【修復】恢復選中狀態
				bool isSelected = _selectedItemObjectIds.Contains(item.ObjectId);
				slot.SetSelected(isSelected);
				
				slot.ItemClicked += (id) =>
				{
					if (_pendingIdentifyScrollId.HasValue)
					{
						// 鑑定卷軸模式：單擊只用於取消選擇，不觸發鑑定（改為雙擊目標裝備）
						if (id == _pendingIdentifyScrollId.Value)
						{
							_pendingIdentifyScrollId = null;
						}
						return;
					}
					// 【多選模式】切換選中狀態
					if (_selectedItemObjectIds.Contains(id))
					{
						_selectedItemObjectIds.Remove(id);
						slot.SetSelected(false); // 【修復】更新選中視覺
					}
					else
					{
						_selectedItemObjectIds.Add(id);
						slot.SetSelected(true); // 【修復】更新選中視覺
					}
				};
				slot.ItemDoubleClicked += (id) =>
				{
					if (_gameWorld == null)
					{
						_gameWorld = GetNodeOrNull<GameWorld>("/root/Boot/World");
					}
					if (_pendingIdentifyScrollId.HasValue)
					{
						if (id == _pendingIdentifyScrollId.Value)
						{
							_pendingIdentifyScrollId = null;
							return;
						}
						_gameWorld?.UseIdentifyScroll(_pendingIdentifyScrollId.Value, id);
						_pendingIdentifyScrollId = null;
						return;
					}
					if (IsIdentifyScroll(item))
					{
						_pendingIdentifyScrollId = id;
						_gameWorld?.AddSystemMessage("請雙擊要鑑定的裝備");
						return;
					}
					if (IsPolymorphScroll(item) && UIManager.Instance != null)
					{
						UIManager.Instance.Open(WindowID.SkinSelect, new WindowContext { ExtraData = id });
						return;
					}
					_gameWorld?.UseItem(id);
				};
			}
		}

		/// <summary>是否為變身卷軸。依伺服器 use_type（item.Type），與 etc_items.use_type='sosc' 對應。</summary>
		private static bool IsPolymorphScroll(InventoryItem item)
		{
			return item != null && item.Type == ItemUseType.Sosc;
		}

		/// <summary>是否為鑑定卷軸。依伺服器 use_type（item.Type），與 etc_items.use_type='identify' 對應。</summary>
		private static bool IsIdentifyScroll(InventoryItem item)
		{
			return item != null && item.Type == ItemUseType.Identify;
		}
	}
}
