using Godot;
using System.Collections.Generic;
using Client.Network;
using Client.UI;

namespace Client.Game
{
    public partial class GameWorld
    {
        private int _currentNpcId; // 当前对话的 NPC

        private void BindNpcSignals()
        {
            if (PacketHandlerRef != null)
            {
                PacketHandlerRef.ShowHtmlReceived += OnShowHtml;
                PacketHandlerRef.ShopBuyOpen += OnShopBuyOpen;
                PacketHandlerRef.ShopSellOpen += OnShopSellOpen;
            }
        }

        // 收到对话包
        private void OnShowHtml(int npcId, string htmlId, string[] args)
        {
            _currentNpcId = npcId;
            GD.Print($"[NPC] Talk: {htmlId}, Args: {string.Join(",", args)}");
            
            // 【修復】當收到 "noitemret" 時，打開空的倉庫窗口而不是對話窗口
            // 這樣用戶就可以開始存儲物品了
            if (htmlId == "noitemret")
            {
                GD.Print($"[Warehouse] 倉庫為空，打開倉庫窗口（可存儲）");
                var context = new WindowContext 
                { 
                    NpcId = npcId, 
                    Type = 2, // 2=存儲
                    ExtraData = new Godot.Collections.Array() // 空列表
                };
                UIManager.Instance.Open(WindowID.WareHouse, context);
                return;
            }
            
            // 【修復】檢查是否為寵物/召喚物面板（"anicom" 或 "moncom"）
            // 如果是，需要設置 summon_object_id 以便 TalkWindow 正確處理命令
            int summonObjectId = 0;
            if (htmlId == "anicom" || htmlId == "moncom")
            {
                // npcId 實際上是寵物/召喚物的 ObjectId
                summonObjectId = npcId;
                npcId = 0; // 清空 npcId，因為這是寵物/召喚物，不是 NPC
            }
            
            // 打开对话窗口
            UIManager.Instance.Open(WindowID.Talk, new WindowContext 
            { 
                ExtraData = new Dictionary<string, object> 
                {
                    { "npc_id", npcId },
                    { "html_id", htmlId },
                    { "summon_object_id", summonObjectId },
                    { "args", args }
                }
            });
        }

        // 收到商店購買列表
        private void OnShopBuyOpen(int npcId, Godot.Collections.Array items)
        {
            _currentNpcId = npcId;
            GD.Print($"[NPC] Open Shop Buy: {items.Count} items.");
            
            UIManager.Instance.Open(WindowID.Shop, new WindowContext 
            {
                ExtraData = new Dictionary<string, object>
                {
                    { "npc_id", npcId },
                    { "type", 0 }, // 0=Buy
                    { "items", items }
                }
            });
        }

        // 收到商店出售列表（服務器已篩選可出售物品）
        private void OnShopSellOpen(int npcId, Godot.Collections.Array items)
        {
            _currentNpcId = npcId;
            GD.Print($"[NPC] Open Shop Sell: npcId={npcId}, {items.Count} items.");
            
            UIManager.Instance.Open(WindowID.Shop, new WindowContext 
            {
                ExtraData = new Dictionary<string, object>
                {
                    { "npc_id", npcId },
                    { "type", 1 }, // 1=Sell
                    { "items", items }
                }
            });
        }

        // --- 交互 API ---
        // 【寵物系統】OnPetStatusChanged 已移至 GameWorld.Pet.cs，這裡不再重複定義

        /// <summary>
        /// 開啟召喚物指令視窗（使用 TalkWindow）
        /// 【修復】現在寵物和召喚物都統一使用 TalkWindow 顯示
        /// 服務器會發送 S_ObjectPet (Opcode 42, "moncom" 或 "anicom")，數據會通過 ShowHtmlReceived 信號傳遞
        /// </summary>
        public void OpenSummonTalkWindow(int summonObjectId)
        {
            if (summonObjectId <= 0) return;
            // 【修復】召喚物現在統一使用 TalkWindow
            // 服務器會自動發送 S_ObjectPet (Opcode 42, "moncom")，數據會通過 ShowHtmlReceived 信號傳遞給 TalkWindow
            // 這裡先打開 TalkWindow，等待服務器發送數據
            UIManager.Instance.Open(WindowID.Talk, new WindowContext
            {
                ExtraData = new Dictionary<string, object>
                {
                    { "html_id", "moncom" },
                    { "npc_id", 0 },
                    { "summon_object_id", summonObjectId },
                    { "args", new string[0] }
                }
            });
        }
        
        // 点击 NPC (在 HandleInput 中调用)
        public void TalkToNpc(int npcId)
        {
            _netSession.Send(C_NpcPacket.MakeTalk(npcId));
        }

        // 发送对话动作 (如 "buy")
        public void SendNpcAction(string action)
        {
            if (_currentNpcId == 0) return;
            _netSession.Send(C_NpcPacket.MakeAction(_currentNpcId, action));
        }

        // 发送购买请求
        public void SendShopBuy(List<ShopItemRequest> items)
        {
            if (_currentNpcId == 0) return;
            // Type 0 = Buy
            _netSession.Send(C_ShopPacket.MakeTransaction(_currentNpcId, 0, items));
        }

        // 发送出售请求
        public void SendShopSell(List<ShopItemRequest> items)
        {
            if (_currentNpcId == 0) return;
            // Type 1 = Sell
            _netSession.Send(C_ShopPacket.MakeTransaction(_currentNpcId, 1, items));
        }

        /// <summary>召喚怪物控制：發送 Opcode 39 (C_NpcTalkAction)，伺服器 SummonSystem.Commander 依 text1 執行。
        /// 指令字串見 docs/summon-control.md：aggressive / defensive / stay / extend / alert / dismiss 等。</summary>
        public void SendSummonCommand(int summonObjectId, string cmd)
        {
            if (summonObjectId <= 0 || string.IsNullOrEmpty(cmd)) return;
            _netSession.Send(C_NpcPacket.MakeAction(summonObjectId, cmd));
        }
    }
}