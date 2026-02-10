using Godot;
using System.Collections.Generic;
using Client.Data;
using Client.Game;

namespace Client.UI
{
	public partial class ChaWindow : GameWindow
	{
		private Label[] _infoLabels;
		private TextureRect[] _itemSlots;
		/// <summary>暫時顯示測試用數據（血條%、邪惡值、經驗、防禦、命中/暴擊等），後續會做正式 UI。</summary>
		private Label _tempDataLabel;
		
		// 【新增】存储当前格子的物品数据，用于点击事件
		private Dictionary<int, InventoryItem> _slotItems = new Dictionary<int, InventoryItem>();

		// 装备映射表 (Type -> SlotIndex)
		private readonly Dictionary<int, int> _slotTypeToIndex = new Dictionary<int, int>
		{
			{ 1, 2 },   // 武器
			{ 22, 0 },  // 头盔
			{ 2, 1 },   // 盔甲
			{ 19, 3 },  // 盾牌
			{ 20, 4 },  // 手套
			{ 21, 5 },  // 鞋子
			{ 4, 7 },   // 斗篷
			{ 10, 6 },  // 项链
			{ 25, 8 },  // 腰带
			{ 18, 9 },  // 戒指1
			{ 23, 11 }, // 耳环
			// 注意：戒指2 通常需要特殊处理，这里暂且只映射主戒指
		};
		
		// Tooltip 引用
		private static CanvasLayer _tooltipLayer;
		private static Control _currentTooltipInstance;

		public override void _Ready()
		{
			base._Ready(); // 【重要】必须调用基类 _Ready 以启用拖拽和关闭按钮

			// 自动绑定 UI
			// 你的 ChaWindow.tscn 结构应该是 BaseWindow -> ContentContainer -> ...
			// 所以我们去 ContentContainer 下面找
			var container = FindChild("ContentContainer", true, false);
			if (container == null) container = this; // 兼容旧结构

			// 2. 绑定装备格子 (TextureRect1 ~ 12)
			_itemSlots = new TextureRect[12];
			for (int i = 0; i < 12; i++)
			{
				var slot = container.FindChild($"TextureRect{i+1}", true, false) as TextureRect;
				_itemSlots[i] = slot;
				
				if (slot != null)
				{
					slot.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
					slot.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
					
					// 【核心修复】开启鼠标输入，绑定事件
					slot.MouseFilter = MouseFilterEnum.Stop; // 拦截鼠标
					
					// 使用局部变量闭包捕获索引
					int index = i; 
					slot.GuiInput += (eventArgs) => OnSlotGuiInput(index, eventArgs);
					slot.MouseEntered += () => OnSlotMouseEntered(index);
					slot.MouseExited += OnSlotMouseExited;
				}
			}

			// 3. 绑定属性标签 (LVLabel1 ~ 17)
			_infoLabels = new Label[17]; 
			for (int i = 0; i < 17; i++)
			{
				_infoLabels[i] = container.FindChild($"LVLabel{i + 1}", true, false) as Label;
			}

			// 4. 暫時方案：測試數據 Label（與性別等同一區塊風格），後續會做正式 UI
			var infoNode = container.FindChild("VBoxContainer", true, false)?.FindChild("Info", true, false);
			_tempDataLabel = infoNode?.FindChild("TempDataLabel", true, false) as Label;
			if (_tempDataLabel == null && infoNode != null)
			{
				_tempDataLabel = new Label();
				_tempDataLabel.Name = "TempDataLabel";
				_tempDataLabel.AutowrapMode = TextServer.AutowrapMode.Off;
				_tempDataLabel.ClipText = false;
				_tempDataLabel.AddThemeFontSizeOverride("font_size", 11);
				_tempDataLabel.Position = new Vector2(10, 418);
				_tempDataLabel.Size = new Vector2(300, 120);
				_tempDataLabel.Text = "--- 測試數據 ---";
				infoNode.AddChild(_tempDataLabel);
			}
		}

		// --- 交互逻辑 ---

		private void OnSlotGuiInput(int index, InputEvent @event)
		{
			if (!_slotItems.ContainsKey(index)) return;
			var item = _slotItems[index];
			if (item == null) return;

			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				if (mb.DoubleClick)
				{
					GD.Print($"[ChaWindow] 请求卸下装备: {item.Name} (OID: {item.ObjectId})");
					
					// 发送请求给 GameWorld
					// 注意：这里需要一种方式访问 GameWorld，最安全的是通过 Signal 或 Root 查找
					var gameWorld = GetNodeOrNull<Client.Game.GameWorld>("/root/Boot/World");
					if (gameWorld != null)
					{
						// 卸下装备通常也是调用 UseItem (C_UseItem)，服务器会切换状态
						gameWorld.UseItem(item.ObjectId);
					}
				}
			}
		}

		private void OnSlotMouseEntered(int index)
		{
			if (!_slotItems.ContainsKey(index)) return;
			var item = _slotItems[index];
			if (item == null) return;

			// 显示 Tooltip
			if (_tooltipLayer == null || !IsInstanceValid(_tooltipLayer)) { 
				_tooltipLayer = new CanvasLayer(); _tooltipLayer.Layer = 128; GetTree().Root.AddChild(_tooltipLayer); 
			}

			var scene = GD.Load<PackedScene>("res://Client/UI/Scenes/Components/ItemTooltip.tscn");
			if (scene != null) {
				var tooltip = scene.Instantiate() as Control;
				_tooltipLayer.AddChild(tooltip);
				_currentTooltipInstance = tooltip;
				
				// 设置位置跟随鼠标略微偏移
				tooltip.Position = GetViewport().GetMousePosition() + new Vector2(15, 15);

				var method = tooltip.GetType().GetMethod("Setup");
				if (method != null) method.Invoke(tooltip, new object[] { item });
			}
		}

		private void OnSlotMouseExited()
		{
			if (_currentTooltipInstance != null && IsInstanceValid(_currentTooltipInstance))
			{
				_currentTooltipInstance.QueueFree();
				_currentTooltipInstance = null;
			}
		}

		public override void _Process(double delta)
		{
			// 如果 Tooltip 显示中，让它跟随鼠标
			if (_currentTooltipInstance != null && IsInstanceValid(_currentTooltipInstance))
			{
				_currentTooltipInstance.Position = GetViewport().GetMousePosition() + new Vector2(15, 15);
			}
		}

		// --- 供外部调用刷新 ---
		public void UpdateData(CharacterInfo info, List<InventoryItem> items)
		{
			UpdateInfo(info);
			UpdateEquipment(items);
		}

		// --- 核心修复：完整的数据映射 ---
		private void UpdateInfo(CharacterInfo info)
		{
			if (info == null) return;

			// 请根据你实际的 UI 摆放位置调整下面的索引！
			// SetText(0) 代表 LVLabel1, SetText(1) 代表 LVLabel2...
			
			SetText(0, info.Name);                         // LVLabel1: 名称
			SetText(1, "Lv." + info.Level);                // LVLabel2: 等级
			SetText(2, info.ClanName ?? "无");             // LVLabel3: 血盟
			SetText(3, info.CurrentHP + " / " + info.MaxHP); // LVLabel4: HP
			SetText(4, "AC: " + info.Ac);                       // LVLabel5: 防禦值
			SetText(5, info.Str.ToString());                     // LVLabel6: Str
			SetText(6, info.Dex.ToString());                     // LVLabel7: Dex
			SetText(7, info.Con.ToString());                     // LVLabel8: Con
			SetText(8, info.Int.ToString());                     // LVLabel9: Int
			SetText(9, info.Wis.ToString());                     // LVLabel10: Wis
			SetText(10, info.Cha.ToString());                    // LVLabel11: Cha
			SetText(11, info.CurrentMP + " / " + info.MaxMP);   // LVLabel12: MP
			SetText(12, "Lawful: " + info.Lawful);                // LVLabel13: 正義值
			SetText(13, "Exp: " + info.Exp);                     // LVLabel14: 經驗
			SetText(14, "类: " + info.Type);                     // LVLabel15: 職業
			SetText(15, "性: " + (info.Sex==0?"男":"女"));        // LVLabel16: 性別
			SetText(16, FormatWorldTime());                      // LVLabel17: 遊戲世界時間

			// 暫時方案：同一區塊顯示測試用數據（後續會做正式 UI）
			if (_tempDataLabel != null)
				_tempDataLabel.Text = BuildTempDataText(info);
		}

		/// <summary>組裝暫時測試數據字串（血條%、邪惡值、經驗、防禦、命中/暴擊等），與顯示性別那區塊同風格。</summary>
		private string BuildTempDataText(CharacterInfo info)
		{
			int hpPct = (info != null && info.MaxHP > 0) ? (100 * info.CurrentHP / info.MaxHP) : 0;
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("--- 測試數據 ---");
			sb.AppendLine($"血條%: {hpPct}");
			sb.AppendLine($"邪惡值: {info?.Lawful ?? 0}");
			sb.AppendLine($"經驗: {info?.Exp ?? 0}");
			sb.AppendLine($"防禦(AC): {info?.Ac ?? 0}");
			var gw = GetNodeOrNull<GameWorld>("/root/Boot/World");
			var stats = gw?.CombatStats;
			if (stats != null)
			{
				sb.AppendLine($"命中: {stats.TotalHits}");
				sb.AppendLine($"未命中: {stats.TotalMisses}");
				sb.AppendLine($"暴擊: {stats.TotalCriticals}");
				sb.AppendLine($"命中率: {stats.HitRate:F1}%");
				sb.AppendLine($"暴擊率: {stats.CriticalRate:F1}%");
				sb.AppendLine($"總傷害: {stats.TotalDamageDealt}");
				sb.AppendLine($"承受傷害: {stats.TotalDamageTaken}");
			}
			return sb.ToString().TrimEnd();
		}

		private string FormatWorldTime()
		{
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			if (boot == null) return "";
			int sec = boot.WorldTimeSeconds;
			int secInDay = ((sec % 86400) + 86400) % 86400;
			int h = secInDay / 3600;
			int m = (secInDay % 3600) / 60;
			return $"{h:D2}:{m:D2}";
		}

		private void UpdateEquipment(List<InventoryItem> items)
		{
			if (_itemSlots == null) return;
			
			// 清空
			foreach (var slot in _itemSlots) if (slot != null) slot.Texture = null;
			_slotItems.Clear();

			if (items == null) return;

			// 2. 填充新数据
			foreach (var item in items)
			{
				// 只显示已装备的物品
				if (item.IsEquipped && _slotTypeToIndex.TryGetValue(item.Type, out int index))
				{
					// 记录数据供交互使用
					_slotItems[index] = item;

					// 加载图标
					string path = $"res://Assets/Items/{item.GfxId}.png"; // 使用 IconId/GfxId
					if (_itemSlots[index] != null)
					{
						if (ResourceLoader.Exists(path))
							_itemSlots[index].Texture = ResourceLoader.Load<Texture2D>(path);
						else 
							GD.PrintErr($"[ChaWindow] 缺失图标: {path}");
					}
				}
			}
		}

		private void SetText(int index, string s)
		{
			if (_infoLabels == null || index < 0 || index >= _infoLabels.Length || _infoLabels[index] == null) return;
			_infoLabels[index].Text = s;
		}
	}
}
