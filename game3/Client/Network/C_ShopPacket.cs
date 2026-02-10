using System;
using System.Collections.Generic;
using Client.Utility;

namespace Client.Network
{
    public static class C_ShopPacket
    {
        // 对应服务器 Opcodes.java 中的: put(Integer.valueOf(40), new C_Shop());
        // type: 0=Buy (买), 1=Sell (卖), 2=WarehousePut (存倉庫), 3=WarehouseGet (取倉庫), 12=PetGet (領取寵物)
        public static byte[] MakeTransaction(int objectId, int type, List<ShopItemRequest> items)
        {
            var w = new PacketWriter();
            
            // 【JP協議對齊】對齊 jp C_Shop.java
            // Opcode: 16 (C_OPCODE_SHOP)
            w.WriteByte(16); 
            
            w.WriteInt(objectId);
            w.WriteByte(type);
            // 【協議修復】服務器使用 readH() 讀取無符號 short，客戶端應使用 WriteUShort
            w.WriteUShort(items.Count);
            
            foreach (var item in items)
            {
                // 注意：买入时 item.Id 是 OrderId (序号)，卖出时 item.Id 是物品的 ObjectId
                // 領取寵物時：item.Id 是項圈的 inv_id，item.Count 是填充（服務器讀取但不使用）
                w.WriteInt(item.Id);    
                w.WriteInt(item.Count); 
            }
            
            return w.GetBytes();
        }
        
        /// <summary>
        /// 創建領取寵物封包（對齊伺服器 PetShopInstance.PetGet）
        /// </summary>
        /// <param name="npcId">寵物倉庫NPC的ObjectId</param>
        /// <param name="collarInvId">項圈的inv_id（背包中的ObjectId）</param>
        public static byte[] MakePetGet(int npcId, int collarInvId)
        {
            var w = new PacketWriter();
            w.WriteByte(16); // 【JP協議對齊】C_OPCODE_SHOP (jp)
            w.WriteInt(npcId);
            w.WriteByte(12); // type=12 表示 PetGet
            w.WriteUShort(1); // count=1，只領取一個寵物
            w.WriteInt(collarInvId); // 項圈的inv_id
            w.WriteInt(0); // 填充（服務器讀取但不使用）
            return w.GetBytes();
        }
    }

    public class ShopItemRequest
    {
        public int Id;    
        public int Count;
    }
}