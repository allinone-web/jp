using System;
using System.Collections.Generic;
using Client.Utility;

namespace Client.Network
{
    public static class C_NpcPacket
    {
        // 【JP協議對齊】對齊 jp C_NpcTalk.java
        // Opcode: 58 (C_OPCODE_NPCTALK)
        public static byte[] MakeTalk(int objectId)
        {
            var w = new PacketWriter();
            
            // 【JP協議對齊】對齊 jp C_NpcTalk.java
            w.WriteByte(58); // C_OPCODE_NPCTALK 
            
            w.WriteInt(objectId);
            return w.GetBytes();
        }

        // 對齊 C_NpcTalkAction.java: readD()=obj_id, readS()=text1, readS()=text2
        public static byte[] MakeAction(int objectId, string action)
        {
            var w = new PacketWriter();
            w.WriteByte(37); // C_OPCODE_NPCACTION
            w.WriteInt(objectId);
            w.WriteString(action ?? "");
            w.WriteString("");
            return w.GetBytes();
        }
    }
}
