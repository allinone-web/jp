using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 【服務器對齊】對齊 server/database/SprTable.java
/// SprTable.java 從數據庫 sprite_frame 表讀取數據，根據 action 分類為：
/// - moveSpeed: 0, 4, 11, 20, 24, 40, 46, 50
/// - attackSpeed: 1, 5, 12, 21, 25, 30, 31, 41, 47, 51
/// - dirSpellSpeed: 18
/// - nodirSpellSpeed: 19
/// 
/// 服務器方法：
/// - getMoveSpeed(sprid, actid): 先查 actid，無則回退到 actid=0
/// - getAttackSpeed(sprid, actid): 先查 actid，無則回退到 actid=1
/// - getDirSpellSpeed(sprid, actid): 先查 actid，無則回退到 actid=18
/// - getNodirSpellSpeed(sprid, actid): 先查 actid，無則回退到 actid=19
/// </summary>
public static class SprDataTable
{
    // 【服務器對齊】對齊 SprTable.java switch (actid) 分類
    // 服務器代碼：
    // case 0, 4, 11, 20, 24, 40, 46, 50: spr.moveSpeed.put(actid, speed);
    private static readonly HashSet<int> MoveActionIds = new HashSet<int> { 0, 4, 11, 20, 24, 40, 46, 50 };
    
    // 【服務器對齊】對齊 SprTable.java switch (actid) 分類
    // 服務器代碼：
    // case 1, 5, 12, 21, 25, 30, 31, 41, 47, 51: spr.attackSpeed.put(actid, speed);
    private static readonly HashSet<int> AttackActionIds = new HashSet<int> { 1, 5, 12, 21, 25, 30, 31, 41, 47, 51 };
    
    // 【服務器對齊】對齊 SprTable.java switch (actid) 分類
    // 服務器代碼：
    // case 18: spr.dirSpellSpeed.put(actid, speed);
    // case 19: spr.nodirSpellSpeed.put(actid, speed);
    private static readonly HashSet<int> SpellActionIds = new HashSet<int> { 18, 19 };

    // Data structure mirroring server: Dictionary<gfxId, Dictionary<actionId, interval>>
    private static readonly Dictionary<int, Dictionary<int, int>> _moveIntervals = new Dictionary<int, Dictionary<int, int>>();
    private static readonly Dictionary<int, Dictionary<int, int>> _attackIntervals = new Dictionary<int, Dictionary<int, int>>();
    private static readonly Dictionary<int, Dictionary<int, int>> _spellIntervals = new Dictionary<int, Dictionary<int, int>>();

    static SprDataTable()
    {
        PopulateData();
    }

    private static void PopulateData()
    {
        // Data extracted from server/datebase_182_2026-01-21.sql
        var rawData = new List<(int gfx, int action, int frame)>
        {
            (0, 0, 640), (0, 1, 840), (0, 4, 640), (0, 5, 1000), (0, 11, 640), (0, 12, 880), (0, 20, 640), (0, 21, 1600), (0, 24, 640), (0, 25, 880), (0, 18, 880), (0, 19, 800), (0, 40, 640), (0, 41, 880),
            (1, 0, 640), (1, 1, 880), (1, 4, 640), (1, 5, 960), (1, 11, 640), (1, 12, 1000), (1, 20, 640), (1, 21, 1520), (1, 24, 640), (1, 25, 1040), (1, 18, 880), (1, 19, 800), (1, 40, 640), (1, 41, 1000),
            (29, 0, 960), (29, 1, 1720), (29, 19, 1720),
            (30, 1, 920), (30, 0, 640), (30, 19, 800), (30, 18, 880),
            (32, 0, 960), (32, 1, 1800), (32, 18, 880), (32, 19, 800),
            (37, 0, 640), (37, 1, 800), (37, 4, 640), (37, 5, 760), (37, 11, 640), (37, 12, 1120), (37, 18, 880), (37, 19, 800), (37, 20, 640), (37, 21, 960), (37, 24, 640), (37, 25, 1120), (37, 40, 640), (37, 41, 920),
            (48, 0, 640), (48, 1, 1000), (48, 4, 640), (48, 5, 920), (48, 11, 640), (48, 12, 920), (48, 18, 800), (48, 19, 800), (48, 20, 640), (48, 21, 1840), (48, 24, 640), (48, 25, 1000), (48, 40, 640), (48, 41, 920),
            (49, 0, 1280), (49, 1, 1920), (49, 19, 800), (49, 18, 880),
            (52, 0, 1640), (52, 1, 1040), (52, 19, 800), (52, 18, 880),
            (53, 0, 480), (53, 1, 840), (53, 18, 880),
            (54, 0, 560), (54, 1, 1480), (54, 19, 800), (54, 18, 880),
            (56, 0, 800), (56, 1, 1320), (56, 19, 800), (56, 18, 880),
            (57, 20, 800), (57, 21, 1160), (57, 19, 680), (57, 18, 720),
            (61, 0, 640), (61, 1, 880), (61, 4, 640), (61, 5, 880), (61, 11, 640), (61, 12, 880), (61, 18, 880), (61, 19, 800), (61, 20, 640), (61, 21, 1920), (61, 24, 640), (61, 25, 920), (61, 40, 640), (61, 41, 880),
            (94, 4, 640), (94, 5, 920), (94, 18, 880), (94, 19, 800),
            (95, 0, 480), (95, 1, 880), (95, 19, 800), (95, 18, 880),
            (96, 0, 480), (96, 1, 1320), (96, 19, 800), (96, 18, 880),
            (138, 0, 640), (138, 1, 760), (138, 4, 640), (138, 5, 800), (138, 11, 640), (138, 12, 1040), (138, 18, 880), (138, 19, 800), (138, 20, 640), (138, 21, 960), (138, 24, 640), (138, 25, 1080), (138, 40, 640), (138, 41, 920),
            (144, 0, 1280), (144, 1, 720), (144, 19, 800), (144, 18, 880),
            (145, 0, 800), (145, 1, 920), (145, 19, 800), (145, 18, 880),
            (146, 0, 480), (146, 1, 840), (146, 19, 800), (146, 18, 880),
            (152, 0, 640), (152, 1, 840), (152, 18, 880), (152, 19, 800),
            (173, 0, 640), (173, 1, 1480),
            (183, 0, 640), (183, 1, 1480), (183, 18, 880),
            (185, 0, 640), (185, 1, 1480),
            (187, 0, 640), (187, 1, 1480),
            (240, 0, 640), (240, 1, 600), (240, 18, 880), (240, 19, 800),
            (255, 0, 1080), (255, 1, 640), (255, 19, 760), (255, 18, 760),
            (734, 0, 640), (734, 1, 960), (734, 4, 640), (734, 5, 1120), (734, 11, 640), (734, 12, 1040), (734, 18, 880), (734, 19, 800), (734, 20, 640), (734, 21, 2280), (734, 24, 640), (734, 25, 1200), (734, 40, 640), (734, 41, 1200),
            (784, 0, 640), (784, 1, 1040), (784, 19, 800), (784, 18, 880),
            (786, 0, 640), (788, 0, 640),
            (894, 0, 640), (894, 1, 840), (894, 18, 880), (894, 19, 800),
            (929, 0, 640), (929, 1, 1440),
            (931, 0, 480), (931, 1, 880),
            (934, 0, 640), (934, 1, 720),
            (936, 0, 480), (936, 1, 960),
            (938, 0, 480), (938, 1, 800),
            (945, 0, 1920), (947, 0, 1280),
            (951, 0, 640), (951, 1, 1040),
            (979, 0, 640), (979, 1, 800),
            (1011, 0, 480), (1011, 1, 840), (1011, 18, 880),
            (1020, 0, 960), (1020, 1, 1320), (1020, 18, 880), (1020, 19, 800),
            (1022, 0, 760), (1022, 1, 920), (1022, 18, 880), (1022, 19, 800),
            (1037, 0, 640), (1037, 1, 880),
            (1039, 0, 640), (1039, 1, 760),
            (1047, 0, 640), (1047, 1, 800),
            (1052, 0, 640), (1052, 1, 1040), (1052, 18, 880),
            (1059, 0, 1280), (1059, 1, 1080), (1059, 18, 880), (1059, 19, 800),
            (1096, 0, 800), (1096, 19, 800), (1096, 18, 880), (1096, 1, 1120),
            (1098, 0, 960), (1098, 1, 1280), (1098, 18, 880), (1098, 19, 800),
            (1104, 0, 480), (1104, 1, 840), (1104, 18, 880), (1104, 19, 800),
            (1106, 0, 640), (1106, 1, 880), (1106, 18, 880), (1106, 19, 800),
            (1108, 0, 640), (1108, 1, 880), (1108, 18, 880), (1108, 19, 800),
            (1110, 0, 640), (1110, 1, 840), (1110, 18, 880), (1110, 19, 800),
            (1125, 0, 640), (1125, 1, 1000), (1125, 18, 880),
            (1128, 0, 640), (1128, 1, 1160),
            (1180, 0, 640), (1180, 1, 960), (1180, 18, 880),
            (1186, 0, 640), (1186, 1, 1000), (1186, 4, 640), (1186, 5, 1120), (1186, 11, 640), (1186, 12, 1080), (1186, 18, 880), (1186, 19, 800), (1186, 20, 640), (1186, 21, 2240), (1186, 24, 640), (1186, 25, 1160), (1186, 40, 640), (1186, 41, 1160),
            (1202, 0, 960), (1202, 1, 1160),
            (1204, 0, 640), (1204, 1, 1280),
            (2323, 20, 640), (2323, 21, 1000), (2323, 19, 800), (2323, 18, 880)
        };

        foreach (var (gfx, action, frame) in rawData)
        {
            if (MoveActionIds.Contains(action))
            {
                if (!_moveIntervals.ContainsKey(gfx)) _moveIntervals[gfx] = new Dictionary<int, int>();
                _moveIntervals[gfx][action] = frame;
            }
            else if (AttackActionIds.Contains(action))
            {
                if (!_attackIntervals.ContainsKey(gfx)) _attackIntervals[gfx] = new Dictionary<int, int>();
                _attackIntervals[gfx][action] = frame;
            }
            else if (SpellActionIds.Contains(action))
            {
                if (!_spellIntervals.ContainsKey(gfx)) _spellIntervals[gfx] = new Dictionary<int, int>();
                _spellIntervals[gfx][action] = frame;
            }
        }
    }

    /// <summary>
    /// 【服務器對齊】對齊 SprTable.java 的 getMoveSpeed/getAttackSpeed/getDirSpellSpeed/getNodirSpellSpeed
    /// 服務器邏輯：
    /// - getMoveSpeed: 先查 actid，無則回退到 actid=0
    /// - getAttackSpeed: 先查 actid，無則回退到 actid=1
    /// - getDirSpellSpeed: 先查 actid，無則回退到 actid=18
    /// - getNodirSpellSpeed: 先查 actid，無則回退到 actid=19
    /// </summary>
    public static long GetInterval(ActionType type, int gfxId, int actionId)
    {
        switch (type)
        {
            case ActionType.Attack:
                // 【服務器對齊】對齊 SprTable.getAttackSpeed(sprid, actid)
                // 服務器邏輯：先查 actid，無則回退到 actid=1
                return GetIntervalFromDict(_attackIntervals, gfxId, actionId, 1, 600);
            case ActionType.Move:
                // 【服務器對齊】對齊 SprTable.getMoveSpeed(sprid, actid)
                // 服務器邏輯：先查 actid，無則回退到 actid=0
                return GetIntervalFromDict(_moveIntervals, gfxId, actionId, 0, 600);
            case ActionType.Magic:
                // 【服務器對齊】對齊 SprTable.getDirSpellSpeed/getNodirSpellSpeed
                // 服務器邏輯：先查 actid，無則回退到 actid=18 (dir) 或 actid=19 (nodir)
                // 客戶端不區分 18/19，同時檢查兩者
                return GetIntervalFromDict(_spellIntervals, gfxId, actionId, 19, 1000, 18);
            default:
                return 500;
        }
    }

    /// <summary>
    /// 【服務器對齊】對齊 SprTable.java 的回退邏輯
    /// 服務器邏輯：
    /// - getMoveSpeed: 先查 actid，無則回退到 actid=0
    /// - getAttackSpeed: 先查 actid，無則回退到 actid=1
    /// - getDirSpellSpeed: 先查 actid，無則回退到 actid=18
    /// - getNodirSpellSpeed: 先查 actid，無則回退到 actid=19
    /// </summary>
    private static int GetIntervalFromDict(Dictionary<int, Dictionary<int, int>> dict, int gfxId, int primaryActionId, int fallbackActionId, int defaultInterval, int? secondaryActionId = null)
    {
        if (dict.TryGetValue(gfxId, out var actions))
        {
            // 【服務器對齊】先查 primaryActionId（對齊服務器先查 actid）
            if (primaryActionId != 0 && actions.TryGetValue(primaryActionId, out int interval))
            {
                return interval;
            }
            // 【服務器對齊】魔法特殊處理：同時檢查 18 和 19
            if (secondaryActionId.HasValue && actions.TryGetValue(secondaryActionId.Value, out int secondaryInterval))
            {
                return secondaryInterval;
            }
            // 【服務器對齊】回退到 fallbackActionId（對齊服務器回退邏輯）
            if (actions.TryGetValue(fallbackActionId, out int fallbackInterval))
            {
                return fallbackInterval;
            }
        }
        return defaultInterval;
    }
}
