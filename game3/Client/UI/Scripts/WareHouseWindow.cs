using Godot;
using System.Collections.Generic;
using Client.Data;
using Client.Game;
using Client.Network;
using Client.Utility;

namespace Client.UI
{
    public partial class WareHouseWindow : GameWindow
    {
        private ItemList _list;
        private Button _actionButton;
        private int _npcId;
        private int _opType; // 2=存儲, 3=取出
        
        // 存儲模式：背包物品列表
        private List<InventoryItem> _inventoryItems = new List<InventoryItem>();
        
        // 取出模式：倉庫物品列表
        private List<WarehouseItem> _warehouseItems = new List<WarehouseItem>();
        
        private GameWorld _gameWorld;

        public override void _Ready()
        {
            base._Ready();
            
            _list = FindChild("WarehouseList", true, false) as ItemList;
            _actionButton = FindChild("ActionButton", true, false) as Button;
            _gameWorld = GetNodeOrNull<GameWorld>("/root/Boot/World");
            
            if (_list == null || _actionButton == null)
            {
                GD.PrintErr("[WareHouseWindow] ❌ 未找到 UI 节点！请在编辑器中创建 VBoxContainer -> ItemList (命名为 WarehouseList) 和 Button (命名为 ActionButton)");
                return;
            }

            // 【選中效果】設置深色選中背景
            var selectedStyle = new StyleBoxFlat();
            selectedStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f); // 深色半透明
            _list.AddThemeStyleboxOverride("selected", selectedStyle);

            _list.ItemActivated += OnItemDoubleClicked;
            _actionButton.Pressed += OnActionButtonPressed;
        }

        public override void OnOpen(WindowContext context = null)
        {
            base.OnOpen(context);
            if (context == null) return;
            
            _npcId = context.NpcId;
            int newOpType = context.Type; // 2=存儲, 3=取出
            
            // 【修復】更新模式（即使窗口已經打開，也要更新模式以確保正確顯示）
            _opType = newOpType;
            
            // 根據模式設置按鈕文字和刷新列表
            if (_opType == 2) // 存儲模式
            {
                if (_actionButton != null) _actionButton.Text = "存入";
                RefreshInventoryList();
            }
            else if (_opType == 3) // 取出模式
            {
                if (_actionButton != null) _actionButton.Text = "取出";
                if (context.ExtraData is Godot.Collections.Array items)
                {
                    RefreshWarehouseList(items);
                }
                else
                {
                    RefreshWarehouseList(new Godot.Collections.Array());
                }
            }
            else
            {
                // 【修復】如果 option 不是 2 或 3，根據是否有物品列表判斷模式
                // 服務器發送的 option 可能是其他值（如 5, 9），但如果有物品列表，應該是取出模式
                if (context.ExtraData is Godot.Collections.Array items && items.Count > 0)
                {
                    _opType = 3; // 取出模式
                    if (_actionButton != null) _actionButton.Text = "取出";
                    RefreshWarehouseList(items);
                }
                else
                {
                    _opType = 2; // 存儲模式
                    if (_actionButton != null) _actionButton.Text = "存入";
                    RefreshInventoryList();
                }
            }
        }

        /// <summary>刷新背包物品列表（存儲模式）</summary>
        private void RefreshInventoryList()
        {
            if (_list == null || _gameWorld == null) return;
            
            _list.Clear();
            _inventoryItems.Clear();
            
            // 從 GameWorld 獲取背包物品（只顯示未裝備的）
            var allItems = _gameWorld.GetInventoryItems();
            if (allItems == null) return;
            
            foreach (var item in allItems)
            {
                if (item.IsEquipped) continue; // 跳過已裝備的物品
                
                _inventoryItems.Add(item);
                
                // 解析物品名稱
                string displayName = item.Name;
                if (DescTable.Instance != null)
                {
                    string descResult = DescTable.Instance.ResolveName(item.Name);
                    if (descResult != item.Name && !string.IsNullOrEmpty(descResult))
                    {
                        displayName = descResult;
                    }
                }
                
                _list.AddItem($"{displayName} ({item.Count})");
            }
            
            GD.Print($"[WareHouse] 刷新背包列表：{_inventoryItems.Count} 個物品");
        }

        /// <summary>刷新倉庫物品列表（取出模式）</summary>
        private void RefreshWarehouseList(Godot.Collections.Array items)
        {
            if (_list == null) return;
            
            _list.Clear();
            _warehouseItems.Clear();
            
            foreach (Godot.Collections.Dictionary data in items)
            {
                var warehouseItem = new WarehouseItem
                {
                    Uid = (int)data["uid"],
                    Type = (int)data["type"],
                    GfxId = (int)data["gfxid"],
                    Bless = (int)data["bless"],
                    Count = (int)data["count"],
                    IsIdentified = ((int)data["isid"]) != 0,
                    Name = (string)data["name"]
                };
                _warehouseItems.Add(warehouseItem);
                
                // 解析物品名稱
                string displayName = warehouseItem.Name;
                if (DescTable.Instance != null)
                {
                    string descResult = DescTable.Instance.ResolveName(warehouseItem.Name);
                    if (descResult != warehouseItem.Name && !string.IsNullOrEmpty(descResult))
                    {
                        displayName = descResult;
                    }
                }
                
                _list.AddItem($"{displayName} ({warehouseItem.Count})");
            }
            
            GD.Print($"[WareHouse] 刷新倉庫列表：{_warehouseItems.Count} 個物品");
        }

        private void OnItemDoubleClicked(long index)
        {
            OnActionButtonPressed();
        }

        private void OnActionButtonPressed()
        {
            if (_list == null) return;
            var selected = _list.GetSelectedItems();
            if (selected.Length == 0) return;
            
            int idx = (int)selected[0];
            
            if (_opType == 2) // 存儲模式
            {
                if (idx >= 0 && idx < _inventoryItems.Count)
                {
                    var item = _inventoryItems[idx];
                    SendRequest(item.ObjectId, item.Count); // 存儲全部數量
                    GD.Print($"[WareHouse] 存儲：{item.Name} (ObjectId:{item.ObjectId}, Count:{item.Count})");
                }
            }
            else if (_opType == 3) // 取出模式
            {
                if (idx >= 0 && idx < _warehouseItems.Count)
                {
                    var item = _warehouseItems[idx];
                    SendRequest(item.Uid, item.Count); // 取出全部數量
                    GD.Print($"[WareHouse] 取出：{item.Name} (Uid:{item.Uid}, Count:{item.Count})");
                }
            }
        }

        /// <summary>發送倉庫操作請求</summary>
        private void SendRequest(int itemId, int count)
        {
            if (_npcId == 0 || count <= 0) return;

            var packet = C_WarehousePacket.Make(_npcId, itemId, count, _opType);
            if (packet != null && _gameWorld != null)
            {
                _gameWorld.SendPacket(packet);
                
                // 【修復】取出成功後，主動請求刷新倉庫列表
                if (_opType == 3) // 取出模式
                {
                    // 延遲一小段時間後請求刷新（等待服務器處理完成）
                    CallDeferred(nameof(RequestWarehouseRefresh));
                }
            }
        }

        /// <summary>請求刷新倉庫列表（取出成功後調用）</summary>
        private void RequestWarehouseRefresh()
        {
            if (_npcId == 0 || _gameWorld == null) return;
            
            // 發送 "retrieve" 動作請求服務器發送新的倉庫列表
            _gameWorld.SendNpcAction("retrieve");
            GD.Print($"[WareHouse] 請求刷新倉庫列表");
        }

        /// <summary>供外部調用，刷新倉庫窗口（當收到新的倉庫列表時）</summary>
        public void UpdateWarehouseItems(Godot.Collections.Array items)
        {
            // 【修復】無論當前模式，如果收到倉庫物品列表，都應該切換到取出模式並刷新
            // 因為服務器發送倉庫列表時，表示用戶正在查看/操作倉庫物品
            if (items != null && items.Count >= 0)
            {
                _opType = 3; // 取出模式
                if (_actionButton != null) _actionButton.Text = "取出";
                RefreshWarehouseList(items);
                GD.Print($"[WareHouse] 更新倉庫列表：{items.Count} 個物品");
            }
        }

        /// <summary>供外部調用，刷新背包顯示（當背包更新時）</summary>
        public void UpdateInventoryDisplay()
        {
            if (_opType == 2) // 只有存儲模式才需要刷新
            {
                RefreshInventoryList();
            }
        }
    }
}
