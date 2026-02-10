using Godot;
using System;

namespace Client.UI
{
	public partial class HotkeySlot : TextureRect
	{
		[Export] public int SlotIndex; // 在编辑器里分别设为 0-7
		
		private Texture2D _defaultIcon;
		private TextureRect _iconLayer; // 【修復】用於顯示圖標的子節點
		private BottomBar _parentBar;
		private Texture2D _backgroundTexture; // 【修復】保存背景圖片

		public override void _Ready()
		{
			// 【修復】使用 GetNodeOrNull 並檢查，避免路徑錯誤導致崩潰
			_parentBar = GetNodeOrNull<BottomBar>("../.."); // HotkeySlot -> HotkeyGrid -> MainHBox -> BottomBar
			if (_parentBar == null)
			{
				GD.PrintErr($"[HotkeySlot] ❌ Failed to find BottomBar parent! Path: ../..");
				// 嘗試其他可能的路徑
				_parentBar = GetNodeOrNull<BottomBar>("../../..");
				if (_parentBar == null)
				{
					GD.PrintErr($"[HotkeySlot] ❌ Also failed with path: ../../..");
				}
			}
			_defaultIcon = GD.Load<Texture2D>("res://Assets/default_item.png");
			
			// 【修復】保存場景中設置的背景圖片
			_backgroundTexture = Texture;
			
			// 【關鍵修復】確保可以接收拖拽事件
			MouseFilter = MouseFilterEnum.Stop;
			
			// 基础样式
			ExpandMode = ExpandModeEnum.IgnoreSize;
			StretchMode = StretchModeEnum.KeepAspectCentered;
			CustomMinimumSize = new Vector2(32, 32);
			
			// 【修復】確保背景圖片始終顯示
			if (Texture == null)
			{
				Texture = GD.Load<Texture2D>("res://Assets/default_menu.png");
				_backgroundTexture = Texture;
			}
			
			// 【修復】創建圖標層（子 TextureRect）用於顯示物品/技能圖標
			_iconLayer = new TextureRect();
			_iconLayer.Name = "IconLayer";
			_iconLayer.ExpandMode = ExpandModeEnum.IgnoreSize;
			_iconLayer.StretchMode = StretchModeEnum.KeepAspectCentered;
			_iconLayer.MouseFilter = MouseFilterEnum.Ignore;
			_iconLayer.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(_iconLayer);
		}
	
		// 處理點擊事件
		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				// 檢查是否正在拖拽
				var dragData = GetViewport().GuiGetDragData();
				if (dragData.VariantType == Variant.Type.Nil)
				{
					// 沒有拖拽，這是普通點擊
					if (_parentBar != null)
					{
						_parentBar.ExecuteHotkey(SlotIndex);
					}
				}
			}
		}

		// --- 拖拽核心 1：判断能不能放下 ---
		public override bool _CanDropData(Vector2 atPosition, Variant data)
		{
			// 檢查數據是否為空
			if (data.VariantType == Variant.Type.Nil)
			{
				GD.Print($"[HotkeySlot] _CanDropData: data is Nil");
				return false;
			}
			
			// 檢查是否為 Dictionary 類型
			if (data.VariantType != Variant.Type.Dictionary)
			{
				GD.Print($"[HotkeySlot] _CanDropData: data is not Dictionary, type={data.VariantType}");
				return false;
			}
			
			try
			{
				var dict = data.AsGodotDictionary();
				if (dict == null)
				{
					GD.PrintErr($"[HotkeySlot] _CanDropData: dict is null");
					return false;
				}
				
				// 檢查是否包含必要的 Key
				if (!dict.ContainsKey("type"))
				{
					GD.Print($"[HotkeySlot] _CanDropData: missing 'type' key");
					return false;
				}
				if (!dict.ContainsKey("id"))
				{
					GD.Print($"[HotkeySlot] _CanDropData: missing 'id' key");
					return false;
				}
				
				string type = dict["type"].AsString();
				bool canDrop = type == "item" || type == "skill";
				GD.Print($"[HotkeySlot] _CanDropData: Slot {SlotIndex}, type={type}, canDrop={canDrop}");
				return canDrop;
			}
			catch (Exception e)
			{
				GD.PrintErr($"[HotkeySlot] _CanDropData 錯誤: {e.Message}");
				return false;
			}
		}

		// --- 拖拽核心 2：放下后的处理 ---
		public override void _DropData(Vector2 atPosition, Variant data)
		{
			GD.Print($"[HotkeySlot] _DropData called on Slot {SlotIndex}");
			
			if (data.VariantType != Variant.Type.Dictionary)
			{
				GD.PrintErr($"[HotkeySlot] _DropData: data is not Dictionary, type={data.VariantType}");
				return;
			}
			
			try
			{
				var dict = data.AsGodotDictionary();
				if (dict == null)
				{
					GD.PrintErr($"[HotkeySlot] _DropData: dict is null");
					return;
				}
				
				// 驗證數據完整性
				if (!dict.ContainsKey("type") || !dict.ContainsKey("id"))
				{
					GD.PrintErr($"[HotkeySlot] _DropData: 數據格式不完整. Keys: {string.Join(", ", dict.Keys)}");
					return;
				}
				
				// 【修復】檢查 _parentBar 是否為 null
				if (_parentBar == null)
				{
					GD.PrintErr($"[HotkeySlot] _DropData: _parentBar is null! Cannot process drop.");
					// 嘗試重新獲取
					_parentBar = GetNodeOrNull<BottomBar>("../..");
					if (_parentBar == null)
					{
						GD.PrintErr($"[HotkeySlot] _DropData: Failed to re-acquire _parentBar");
						return;
					}
				}
				
				// 通知 BottomBar 更新数据逻辑（會自動更新圖標層）
				GD.Print($"[HotkeySlot] Calling OnSlotDropped on parent bar...");
				_parentBar.OnSlotDropped(SlotIndex, dict);
				
				GD.Print($"[HotkeySlot] ✅ Slot {SlotIndex} 成功接收拖拽數據: type={dict["type"].AsString()}, id={dict["id"].AsInt32()}");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[HotkeySlot] _DropData 錯誤: {e.Message}\n{e.StackTrace}");
			}
		}
	}
}
