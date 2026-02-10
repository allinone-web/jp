using System;
using Client.Utility;
using System.Collections.Generic;

namespace Client.Network
{
    public static class C_SkillBuyPacket
    {
        /// <summary>
        /// 构建购买魔法封包 (对应 Server C_SkillBuyOk.java)
        /// </summary>
        /// <param name="skillIds">要购买的魔法ID列表 (注意：服务器逻辑比较古老，通常是列表索引，需根据 S_SkillBuyList 对应)</param>
        public static byte[] MakeBuyOK(List<int> skillIds)
        {
            var w = new PacketWriter();
            // Opcode: C_SkillBuyOk (通常是 80 或 209，请根据 PacketHandler/Opcode表 确认)
            // 
            // 【JP協議對齊】對齊 jp C_SkillBuyOk.java (如果存在)
            // 注意：jp 版本可能使用不同的 opcode，需要確認
            w.WriteByte(207); // C_OPCODE_SKILLBUYOK (根據 Opcodes.java) 

            // 1. 数量 (WriteH) - 【協議修復】服務器使用 readH() 讀取無符號 short，客戶端應使用 WriteUShort
            w.WriteUShort(skillIds.Count);

            // 2. 循环写入 ID (WriteD)
            foreach(var id in skillIds)
            {
                // 注意：C_SkillBuyOk.java 里有复杂的 ID 转换逻辑 (id -= 3 等)
                // 这通常意味着客户端传的是 UI 列表里的 Index，而不是真实的 SkillId
                // 这里我们暂时透传 ID，具体数值需配合 S_SkillBuyList 解析
                w.WriteInt(id);
            }

            return w.GetBytes();
        }

        /// <summary>
        /// 请求打开魔法购买列表 (对应 C_SkillBuy.java)
        /// </summary>
        public static byte[] MakeRequestList()
        {
            var w = new PacketWriter();
            // 【JP協議對齊】對齊 jp C_SkillBuy.java
            // Opcode: 173 (C_OPCODE_SKILLBUY)
            w.WriteByte(173); 
            return w.GetBytes();
        }
    }
}