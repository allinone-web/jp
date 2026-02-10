using System;

namespace Client.UI
{
    /// <summary>
    /// UI 窗口的唯一标识符
    /// </summary>
    public enum WindowID
    {
        None = 0,
        
        // --- 核心面板 ---
        Character,      // 角色面板 (C) -> 对应 ChaWindow
        Inventory,      // 背包面板 (TAB/I) -> 对应 InvWindow
        Skill,          // 魔法/技能面板 (S)
        
        // --- 功能面板 ---
        Shop,           // NPC 商店 (买/卖)
        Talk,           // NPC 对话
        WareHouse,      // 仓库
        PetWarehouse,   // 寵物倉庫
        PetPanel,       // 寵物面板（顯示血量等信息）
        
        // --- 系统面板 ---
        Options,        // 设置 (Esc)
        Exit,           // 退出游戏确认
        SkinSelect,     // 变身选择
        
        // --- 扩展 ---
        Map,            // 小地图/大地图
        Guild,          // 血盟
        Party,          // 组队
        CombatStats,    // 戰鬥統計窗口

        // ... 其他 ID
        AmountInput   // 数量输入弹窗
    }

    /// <summary>
    /// 窗口打开时传递的上下文数据 (可选)
    /// </summary>
    public class WindowContext
        {
            public int NpcId; // 用于商店/对话，记录是哪个NPC打开的
            public int Type;  // 用于区分商店类型 (买/卖)
            public object ExtraData; // 其他任意数据
        }
}