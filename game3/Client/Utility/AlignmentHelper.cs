using Godot;

namespace Client.Utility
{
    public static class AlignmentHelper
    {
        /// <summary>
        /// 是否为 NPC (对话对象)
        /// 依据日志分析：服务端发送的 NPC Lawful 为 1000 或 32767
        /// </summary>
        public static bool IsNpc(int lawful)
        {
            // 规则 A: 用户指定的 NPC 特征值
            if (lawful == 1000) return true;
            
            // 规则 B: 正义值满值 (通常也是 NPC)
            if (lawful >= 32767) return true;

            // 规则 C: 原有的 66536 判定 (虽然溢出后读不到，但保留作为参考)
            if (lawful == 66536) return true;

            return false;
        }

        /// <summary>
        /// 是否为可自动攻击的对象 (怪物 + 红名玩家)
        /// 规则：小于等于 0
        /// </summary>
        public static bool IsMonster(int lawful)
        {
            // 注意：必须先排除 NPC！
            // 因为如果 NPC 是 1000，这里不会误判。
            // 但如果有些 NPC 溢出成了 0，这里会把它当怪打 (目前无法避免，除非有 GfxID 列表)
            if (IsNpc(lawful)) return false;
			// 0 = 怪物 (日志证实大量怪是0)
            // <0 = 红名/邪恶 (日志证实有 -32766)

            return lawful <= 0;
        }

        /// <summary>
        /// 是否为普通玩家 (为了避免误伤)
        /// 规则：在 1 到 32767 之间 (正义玩家)
        /// </summary>
        public static bool IsLawfulPlayer(int lawful)
        {
            if (IsNpc(lawful)) return false;
            return lawful > 0;
        }

        /// <summary>
        /// 【修復】獲取名字顏色（根據邪惡值漸變）
        /// 規則：
        /// - NPC (lawful >= 32767 或 lawful == 1000): 綠色
        /// - 怪物 (lawful == 0 且非 NPC): 紅色
        /// - 正義玩家 (lawful > 0): 從白色(0) 漸變到深藍色(32767)
        /// - 邪惡玩家 (lawful < 0): 從白色(0) 漸變到紫紅色(-32767)
        /// - 紫名（攻擊正義玩家時）: 紫色（需要額外檢查 _isPinkName）
        /// </summary>
        public static Color GetNameColor(int lawful, bool isPinkName = false)
        {
            // 【服務器對齊】紫名優先級最高（攻擊正義玩家時觸發）
            // 服務器規則：lawful >= 65536 時才會觸發紫名，但紫名狀態由 Opcode 106 單獨控制
            if (isPinkName)
                return new Color(1.0f, 0.4f, 1.0f); // 紫色 (紫名)
            
            // 1. NPC -> 綠色（表示安全/服務）
            // 服務器規則：lawful >= 32767 或 lawful == 1000
            if (IsNpc(lawful)) return new Color(0.2f, 1.0f, 0.2f); // 綠色 (NPC)
            
            // 2. 怪物 (lawful == 0 且非 NPC) -> 紅色
            if (lawful == 0 && !IsNpc(lawful)) return new Color(1.0f, 0.2f, 0.2f); // 紅色 (怪物)
            
            // 3. 正義玩家 (lawful > 0): 從白色(0) 漸變到深藍色(32767)
            // 顏色變化：白色(1,1,1) -> 淺藍(0.6,0.8,1.0) -> 藍色(0.4,0.6,1.0) -> 深藍(0.2,0.4,1.0)
            if (lawful > 0)
            {
                // 將 lawful 映射到 0.0 - 1.0 範圍（最大 32767）
                float ratio = Mathf.Clamp(lawful / 32767.0f, 0.0f, 1.0f);
                
               	// 漸變：白色 -> 淺藍 -> 藍色 -> 深藍
               	if (ratio < 0.33f)
               	{
               		// 0-33%: 白色 -> 淺藍
               		float t = ratio / 0.33f;
               		return new Color(
               			Mathf.Lerp(1.0f, 0.6f, t),
               			Mathf.Lerp(1.0f, 0.8f, t),
               			Mathf.Lerp(1.0f, 1.0f, t)
               		);
               	}
               	else if (ratio < 0.66f)
               	{
               		// 33-66%: 淺藍 -> 藍色
               		float t = (ratio - 0.33f) / 0.33f;
               		return new Color(
               			Mathf.Lerp(0.6f, 0.4f, t),
               			Mathf.Lerp(0.8f, 0.6f, t),
               			Mathf.Lerp(1.0f, 1.0f, t)
               		);
               	}
               	else
               	{
               		// 66-100%: 藍色 -> 深藍
               		float t = (ratio - 0.66f) / 0.34f;
               		return new Color(
               			Mathf.Lerp(0.4f, 0.2f, t),
               			Mathf.Lerp(0.6f, 0.4f, t),
               			Mathf.Lerp(1.0f, 1.0f, t)
               		);
               	}
            }
            
            // 4. 邪惡玩家 (lawful < 0): 從白色(0) 漸變到紫紅色(-32767)
            // 顏色變化：白色(1,1,1) -> 淺紅(1.0,0.6,0.6) -> 紅色(1.0,0.3,0.3) -> 紫紅(1.0,0.2,0.4)
            if (lawful < 0)
            {
                // 將 -lawful 映射到 0.0 - 1.0 範圍（最大 32767）
                float ratio = Mathf.Clamp(-lawful / 32767.0f, 0.0f, 1.0f);
                
               	// 漸變：白色 -> 淺紅 -> 紅色 -> 紫紅色
               	if (ratio < 0.33f)
               	{
               		// 0-33%: 白色 -> 淺紅
               		float t = ratio / 0.33f;
               		return new Color(
               			Mathf.Lerp(1.0f, 1.0f, t),
               			Mathf.Lerp(1.0f, 0.6f, t),
               			Mathf.Lerp(1.0f, 0.6f, t)
               		);
               	}
               	else if (ratio < 0.66f)
               	{
               		// 33-66%: 淺紅 -> 紅色
               		float t = (ratio - 0.33f) / 0.33f;
               		return new Color(
               			Mathf.Lerp(1.0f, 1.0f, t),
               			Mathf.Lerp(0.6f, 0.3f, t),
               			Mathf.Lerp(0.6f, 0.3f, t)
               		);
               	}
               	else
               	{
               		// 66-100%: 紅色 -> 紫紅色
               		float t = (ratio - 0.66f) / 0.34f;
               		return new Color(
               			Mathf.Lerp(1.0f, 1.0f, t),
               			Mathf.Lerp(0.3f, 0.2f, t),
               			Mathf.Lerp(0.3f, 0.4f, t) // 增加藍色分量，變成紫紅色
               		);
               	}
            }
            
            // 5. 中立 (0) -> 白色（應該不會到達這裡，因為上面已經處理了）
            return new Color(1, 1, 1);
        }

        /// <summary>
        /// 获取调试用的身体颜色
        /// </summary>
        public static Color GetBodyColor(int lawful)
        {
            if (IsNpc(lawful)) return new Color(0, 0, 1); // 蓝 (NPC)
            if (lawful <= 0) return new Color(1, 0, 0);   // 红 (怪)
            return new Color(1, 1, 1);                    // 白 (普通玩家)
        }
    }
}