using Godot;
using Client.Game;
using Client.Network; 
using Client.Utility; 

namespace Client.UI
{
    public partial class ShopWindow : GameWindow
    {
        private ItemList _list;
        private Button _btnBuy;
        private int _npcId;
        private Godot.Collections.Array _shopItems;

        public override void _Ready()
        {
            base._Ready();
            
            _list = FindChild("ShopList", true, false) as ItemList;
            _btnBuy = FindChild("BuyButton", true, false) as Button;

            if (_list == null || _btnBuy == null)
            {
                GD.PrintErr("[ShopWindow] ❌ 未找到 UI 节点！请在编辑器中创建 VBoxContainer -> ItemList (命名为 ShopList) 和 Button (命名为 BuyButton)");
                return;
            }

            // 【選中效果】設置深色選中背景
            var selectedStyle = new StyleBoxFlat();
            selectedStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f); // 深色半透明
            _list.AddThemeStyleboxOverride("selected", selectedStyle);

            _list.ItemActivated += OnItemDoubleClicked;
            _btnBuy.Pressed += OnBuyPressed;
        }

        public override void OnOpen(WindowContext context)
        {
            if (context.ExtraData == null) return;
            var data = context.ExtraData as System.Collections.Generic.Dictionary<string, object>;
            _npcId = (int)data["npc_id"];
            _shopItems = (Godot.Collections.Array)data["items"];
            RefreshList();
        }

        private void RefreshList()
        {
            if (_list == null) return;
            _list.Clear();

            foreach (Godot.Collections.Dictionary item in _shopItems)
            {
                string rawName = (string)item["name"];
                int price = (int)item["price"];

                string displayName = rawName;
                bool isResolved = false;

                // 1. DescTable 优先
                if (DescTable.Instance != null)
                {
                    string descResult = DescTable.Instance.ResolveName(rawName);
                    if (descResult != rawName && !string.IsNullOrEmpty(descResult))
                    {
                        displayName = descResult;
                        isResolved = true;
                    }
                }

                // 2. StringTable 兜底
                if (!isResolved && StringTable.Instance != null)
                {
                    string stringResult = StringTable.Instance.ResolveName(rawName);
                    if (stringResult != rawName && !string.IsNullOrEmpty(stringResult))
                    {
                        displayName = stringResult;
                        isResolved = true;
                    }
                }

                // ✅ 调试信息：如果依然没翻译成功，打印到控制台，方便你查找丢失的文件
                if (!isResolved)
                {
                    GD.PrintErr($"[Shop] ⚠️ 未知物品名称: {rawName} (未在 desc.txt 或 string.txt 中找到)");
                }

                _list.AddItem($"{displayName} - {price} 金币");
            }
        }

        private void OnBuyPressed()
        {
            if (_list == null) return;
            var selected = _list.GetSelectedItems();
            if (selected.Length == 0) return;
            
            int idx = selected[0];
            var itemData = (Godot.Collections.Dictionary)_shopItems[idx];
            int orderId = (int)itemData["order_id"];
            
            var list = new System.Collections.Generic.List<ShopItemRequest>();
            list.Add(new ShopItemRequest { Id = orderId, Count = 1 });
            
            var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
            world?.SendShopBuy(list);
        }
        
        private void OnItemDoubleClicked(long index)
        {
            OnBuyPressed();
        }
    }
}