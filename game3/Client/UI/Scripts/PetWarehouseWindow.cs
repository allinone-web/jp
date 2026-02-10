using Godot;
using System.Collections.Generic;
using Client.Game;
using Client.Data;
using Client.Utility;

namespace Client.UI
{
	/// <summary>
	/// 寵物倉庫窗口：顯示可領取的寵物列表
	/// 對齊服務器 S_ObjectPet (Opcode 49, option=12)
	/// </summary>
	public partial class PetWarehouseWindow : GameWindow
	{
		private ItemList _list;
		private Button _retrieveButton;
		private int _npcId;
		private List<PetWarehouseItem> _petItems = new List<PetWarehouseItem>();
		private GameWorld _gameWorld;

		public override void _Ready()
		{
			base._Ready();
			
			_list = FindChild("PetList", true, false) as ItemList;
			_retrieveButton = FindChild("RetrieveButton", true, false) as Button;
			_gameWorld = GetNodeOrNull<GameWorld>("/root/Boot/World");
			
			if (_list == null || _retrieveButton == null)
			{
				GD.PrintErr("[PetWarehouse] ❌ 未找到 UI 節點！請在編輯器中創建 ItemList (命名為 PetList) 和 Button (命名為 RetrieveButton)");
				return;
			}

			// 設置選中效果
			var selectedStyle = new StyleBoxFlat();
			selectedStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
			_list.AddThemeStyleboxOverride("selected", selectedStyle);

			_list.ItemActivated += OnItemDoubleClicked;
			_retrieveButton.Pressed += OnRetrieveButtonPressed;
		}

		public override void OnOpen(WindowContext context = null)
		{
			base.OnOpen(context);
			if (context == null) return;
			
			_npcId = context.NpcId;
			
			// 解析寵物列表
			if (context.ExtraData is Godot.Collections.Array items)
			{
				RefreshPetList(items);
			}
		}

		/// <summary>刷新寵物列表</summary>
		private void RefreshPetList(Godot.Collections.Array items)
		{
			if (_list == null) return;
			
			_list.Clear();
			_petItems.Clear();
			
			foreach (Godot.Collections.Dictionary data in items)
			{
				var petItem = new PetWarehouseItem
				{
					InvId = (int)data["invId"],
					Type = (int)data["type"],
					GfxId = (int)data["gfxid"],
					Bless = (int)data["bless"],
					Count = (int)data["count"],
					IsDefinite = ((int)data["isDefinite"]) != 0,
					Name = (string)data["name"]
				};
				_petItems.Add(petItem);
				
				// 解析物品名稱（服務器已格式化為 "項圈 [Lv.X 寵物名]"）
				string displayName = petItem.Name;
				if (DescTable.Instance != null)
				{
					string descResult = DescTable.Instance.ResolveName(petItem.Name);
					if (descResult != petItem.Name && !string.IsNullOrEmpty(descResult))
					{
						displayName = descResult;
					}
				}
				
				_list.AddItem(displayName);
			}
			
			GD.Print($"[PetWarehouse] 刷新寵物列表：{_petItems.Count} 個寵物");
		}

		private void OnItemDoubleClicked(long index)
		{
			OnRetrieveButtonPressed();
		}

		private void OnRetrieveButtonPressed()
		{
			if (_list == null) return;
			var selected = _list.GetSelectedItems();
			if (selected.Length == 0)
			{
				_gameWorld?.AddSystemMessage("請先選擇要領取的寵物");
				return;
			}
			
			int idx = (int)selected[0];
			if (idx >= 0 && idx < _petItems.Count)
			{
				var petItem = _petItems[idx];
				_gameWorld?.RetrievePet(_npcId, petItem.InvId);
				GD.Print($"[PetWarehouse] 領取寵物：{petItem.Name} (InvId:{petItem.InvId})");
			}
		}

		/// <summary>供外部調用，刷新寵物列表</summary>
		public void UpdatePetList(Godot.Collections.Array items)
		{
			if (items != null)
			{
				RefreshPetList(items);
			}
		}
	}

	/// <summary>寵物倉庫物品數據</summary>
	public class PetWarehouseItem
	{
		public int InvId;
		public int Type;
		public int GfxId;
		public int Bless;
		public int Count;
		public bool IsDefinite;
		public string Name;
	}
}
