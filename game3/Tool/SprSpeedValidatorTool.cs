using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Tool
{
    /// <summary>
    /// 命令行工具：驗證 list.spr 與 SprDataTable.cs 的速度數據一致性
    /// 使用方法：在項目根目錄運行此腳本
    /// </summary>
    public class SprSpeedValidatorTool
    {
        // 角色 GfxId 映射
        private static readonly Dictionary<int, string> CharacterNames = new()
        {
            { 0, "王子 (Prince)" },
            { 1, "公主/騎士 (Princess/Knight)" },
            { 138, "妖精 (Elf Male)" },
            { 734, "法師 (Mage)" }
        };

        // 動作類型分類
        private enum ActionCategory
        {
            Walk,           // 0.walk, 4.walk sword, 11.walk axe, 20.walk bow, 24.walk Spear, 40.walk staff
            MeleeAttack,    // 1.attack, 5.attack sword, 12.attack axe, 30.alt attack
            RangedAttack,   // 21.attack bow
            LongRangeMelee, // 24.walk Spear, 41.attack staff (長矛類)
            MagicRanged,    // 18.spell direction, 19.spell no direction (魔法攻擊遠程)
        }

        // 動作 ID 到類型的映射
        private static readonly Dictionary<int, ActionCategory> ActionIdToCategory = new()
        {
            // Walk
            { 0, ActionCategory.Walk },
            { 4, ActionCategory.Walk },
            { 11, ActionCategory.Walk },
            { 20, ActionCategory.Walk },
            { 24, ActionCategory.Walk },
            { 40, ActionCategory.Walk },
            
            // Melee Attack
            { 1, ActionCategory.MeleeAttack },
            { 5, ActionCategory.MeleeAttack },
            { 12, ActionCategory.MeleeAttack },
            { 30, ActionCategory.MeleeAttack },
            
            // Ranged Attack
            { 21, ActionCategory.RangedAttack },
            
            // Long Range Melee
            { 41, ActionCategory.LongRangeMelee },
            
            // Magic Ranged
            { 18, ActionCategory.MagicRanged },
            { 19, ActionCategory.MagicRanged },
        };

        private class ActionSpeedData
        {
            public int GfxId;
            public string Name;
            public int ActionId;
            public string ActionName;
            public ActionCategory Category;
            public int TotalMs; // 總時長（毫秒）
            public List<int> FrameDurations; // 每幀時長（毫秒）
        }

        private class CharacterSpeedData
        {
            public int GfxId;
            public string Name;
            public Dictionary<int, ActionSpeedData> Actions = new();
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("list.spr 與 SprDataTable.cs 速度數據驗證工具");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            string listSprPath = "Assets/list.spr";
            if (!File.Exists(listSprPath))
            {
                Console.WriteLine($"錯誤: 找不到文件: {listSprPath}");
                Console.WriteLine("請確保在項目根目錄運行此工具");
                return;
            }

            // 1. 從 list.spr 提取數據
            Console.WriteLine("正在解析 list.spr...");
            var listSprData = ParseListSpr(listSprPath);
            Console.WriteLine($"解析完成，找到 {listSprData.Count} 個角色");
            Console.WriteLine();

            // 2. 從 SprDataTable.cs 讀取數據
            Console.WriteLine("正在載入 SprDataTable.cs 數據...");
            var sprDataTableData = LoadSprDataTableData();
            Console.WriteLine($"載入完成，找到 {sprDataTableData.Count} 個角色的數據");
            Console.WriteLine();

            // 3. 生成對比報告
            Console.WriteLine("正在生成對比報告...");
            GenerateComparisonReport(listSprData, sprDataTableData);
            
            Console.WriteLine();
            Console.WriteLine("驗證完成！報告已保存到 Tool/spr_speed_comparison.txt");
        }

        private static Dictionary<int, CharacterSpeedData> ParseListSpr(string filePath)
        {
            var result = new Dictionary<int, CharacterSpeedData>();
            
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            CharacterSpeedData currentChar = null;
            string line;
            
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 解析角色定義行: #0	208	prince
                var headerMatch = Regex.Match(line, @"^#(\d+)\s+(\d+)\s+(.+)$");
                if (headerMatch.Success)
                {
                    int gfxId = int.Parse(headerMatch.Groups[1].Value);
                    if (CharacterNames.ContainsKey(gfxId))
                    {
                        currentChar = new CharacterSpeedData
                        {
                            GfxId = gfxId,
                            Name = CharacterNames[gfxId]
                        };
                        result[gfxId] = currentChar;
                    }
                    else
                    {
                        currentChar = null;
                    }
                    continue;
                }

                if (currentChar == null) continue;

                // 解析動作行: 0.walk(1 4,24.0:4 24.1:4[300 24.2:4 24.3:4)
                var actionMatch = Regex.Match(line, @"(\d+)\.([a-zA-Z0-9_\s]+)\(([^)]+)\)");
                if (!actionMatch.Success) continue;

                int actionId = int.Parse(actionMatch.Groups[1].Value);
                string actionName = actionMatch.Groups[2].Value.Trim();
                string content = actionMatch.Groups[3].Value.Trim();

                if (!ActionIdToCategory.ContainsKey(actionId)) continue;

                // 解析幀數據
                var parts = content.Split(',');
                if (parts.Length < 2) continue;

                // 解析幀 tokens: 24.0:4 24.1:4[300 24.2:4 24.3:4
                var frameTokens = parts[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var frameDurations = new List<int>();
                int totalMs = 0;

                foreach (var token in frameTokens)
                {
                    // 移除音效標記 [300, <97 等，但保留 A.B:C 格式
                    string cleanToken = token;
                    
                    // 移除 [數字 格式的音效標記（但保留前面的 A.B:C）
                    cleanToken = Regex.Replace(cleanToken, @"\[(\d+)", "");
                    
                    // 移除 <數字 格式的音效標記
                    if (cleanToken.Contains("<"))
                    {
                        int idx = cleanToken.IndexOf("<");
                        cleanToken = cleanToken.Substring(0, idx);
                    }
                    
                    // 移除其他修飾符 !, > 等
                    cleanToken = cleanToken.Replace("!", "").Replace(">", "").Trim();
                    
                    if (string.IsNullOrEmpty(cleanToken)) continue;

                    // 解析 A.B:C 格式（支持負數 A，如 -1.0:4）
                    var frameMatch = Regex.Match(cleanToken, @"(-?\d+)\.(\d+):(\d+)");
                    if (frameMatch.Success)
                    {
                        int durationUnit = int.Parse(frameMatch.Groups[3].Value);
                        int frameMs = durationUnit * 40; // DurationUnit * 40ms
                        frameDurations.Add(frameMs);
                        totalMs += frameMs;
                    }
                }

                if (frameDurations.Count > 0)
                {
                    var actionData = new ActionSpeedData
                    {
                        GfxId = currentChar.GfxId,
                        Name = currentChar.Name,
                        ActionId = actionId,
                        ActionName = actionName,
                        Category = ActionIdToCategory[actionId],
                        TotalMs = totalMs,
                        FrameDurations = frameDurations
                    };
                    currentChar.Actions[actionId] = actionData;
                }
            }

            return result;
        }

        private static Dictionary<int, Dictionary<int, int>> LoadSprDataTableData()
        {
            // 從 SprDataTable.cs 中提取的數據
            // 格式: (gfxId, actionId, intervalMs)
            var rawData = new List<(int gfx, int action, int interval)>
            {
                // 王子 (0)
                (0, 0, 640), (0, 1, 840), (0, 4, 640), (0, 5, 1000), (0, 11, 640), (0, 12, 880),
                (0, 20, 640), (0, 21, 1600), (0, 24, 640), (0, 25, 880), (0, 18, 880), (0, 19, 800),
                (0, 40, 640), (0, 41, 880),
                
                // 公主/騎士 (1)
                (1, 0, 640), (1, 1, 880), (1, 4, 640), (1, 5, 960), (1, 11, 640), (1, 12, 1000),
                (1, 20, 640), (1, 21, 1520), (1, 24, 640), (1, 25, 1040), (1, 18, 880), (1, 19, 800),
                (1, 40, 640), (1, 41, 1000),
                
                // 妖精 (138)
                (138, 0, 640), (138, 1, 760), (138, 4, 640), (138, 5, 800), (138, 11, 640), (138, 12, 1040),
                (138, 18, 880), (138, 19, 800), (138, 20, 640), (138, 21, 960), (138, 24, 640), (138, 25, 1080),
                (138, 40, 640), (138, 41, 920),
                
                // 法師 (734)
                (734, 0, 640), (734, 1, 960), (734, 4, 640), (734, 5, 1120), (734, 11, 640), (734, 12, 1040),
                (734, 18, 880), (734, 19, 800), (734, 20, 640), (734, 21, 2280), (734, 24, 640), (734, 25, 1200),
                (734, 40, 640), (734, 41, 1200),
            };

            var result = new Dictionary<int, Dictionary<int, int>>();
            foreach (var (gfx, action, interval) in rawData)
            {
                if (!result.ContainsKey(gfx))
                    result[gfx] = new Dictionary<int, int>();
                result[gfx][action] = interval;
            }

            return result;
        }

        private static void GenerateComparisonReport(
            Dictionary<int, CharacterSpeedData> listSprData,
            Dictionary<int, Dictionary<int, int>> sprDataTableData)
        {
            var report = new StringBuilder();
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine("list.spr 與 SprDataTable.cs 速度數據對比報告");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();
            report.AppendLine($"生成時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();
            report.AppendLine("說明:");
            report.AppendLine("  - list.spr: 從 list.spr 文件解析的動作總時長（所有幀 DurationUnit * 40ms 的總和）");
            report.AppendLine("  - SprDataTable: 從 SprDataTable.cs 中定義的間隔時間（毫秒）");
            report.AppendLine("  - 差異: 兩者的差值（毫秒）");
            report.AppendLine();

            foreach (var gfxId in CharacterNames.Keys.OrderBy(x => x))
            {
                if (!listSprData.ContainsKey(gfxId))
                {
                    report.AppendLine($"[警告] GfxId {gfxId} ({CharacterNames[gfxId]}) 在 list.spr 中未找到");
                    continue;
                }

                var charData = listSprData[gfxId];
                report.AppendLine($"## GfxId {gfxId}: {charData.Name}");
                report.AppendLine("-".PadRight(80, '-'));

                // 按類型分組
                var byCategory = charData.Actions.Values
                    .GroupBy(a => a.Category)
                    .OrderBy(g => g.Key);

                foreach (var categoryGroup in byCategory)
                {
                    report.AppendLine();
                    report.AppendLine($"### {GetCategoryName(categoryGroup.Key)}");
                    report.AppendLine();

                    foreach (var action in categoryGroup.OrderBy(a => a.ActionId))
                    {
                        int sprDataTableValue = 0;
                        bool hasSprDataTable = sprDataTableData.ContainsKey(gfxId) &&
                                             sprDataTableData[gfxId].ContainsKey(action.ActionId);

                        if (hasSprDataTable)
                        {
                            sprDataTableValue = sprDataTableData[gfxId][action.ActionId];
                        }

                        report.AppendLine($"  Action {action.ActionId}.{action.ActionName}:");
                        report.AppendLine($"    list.spr:     {action.TotalMs}ms (幀: {string.Join("+", action.FrameDurations)}ms = {action.TotalMs}ms)");
                        
                        if (hasSprDataTable)
                        {
                            int diff = action.TotalMs - sprDataTableValue;
                            string status = diff == 0 ? "✓ 一致" : $"✗ 不一致 (差異: {diff}ms)";
                            string diffDesc = diff > 0 ? $"list.spr 比 SprDataTable 多 {diff}ms" : $"list.spr 比 SprDataTable 少 {Math.Abs(diff)}ms";
                            report.AppendLine($"    SprDataTable: {sprDataTableValue}ms {status}");
                            if (diff != 0)
                            {
                                report.AppendLine($"    說明: {diffDesc}");
                            }
                        }
                        else
                        {
                            report.AppendLine($"    SprDataTable: [缺失]");
                        }
                        report.AppendLine();
                    }
                }

                report.AppendLine();
            }

            // 統計摘要
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine("統計摘要");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();

            int totalActions = 0;
            int matchedActions = 0;
            int mismatchedActions = 0;
            int missingActions = 0;
            var mismatchDetails = new List<string>();

            foreach (var gfxId in CharacterNames.Keys.OrderBy(x => x))
            {
                if (!listSprData.ContainsKey(gfxId)) continue;

                var charData = listSprData[gfxId];
                foreach (var action in charData.Actions.Values)
                {
                    totalActions++;
                    bool hasSprDataTable = sprDataTableData.ContainsKey(gfxId) &&
                                         sprDataTableData[gfxId].ContainsKey(action.ActionId);

                    if (hasSprDataTable)
                    {
                        int sprDataTableValue = sprDataTableData[gfxId][action.ActionId];
                        if (action.TotalMs == sprDataTableValue)
                            matchedActions++;
                        else
                        {
                            mismatchedActions++;
                            mismatchDetails.Add($"GfxId {gfxId} Action {action.ActionId}.{action.ActionName}: list.spr={action.TotalMs}ms, SprDataTable={sprDataTableValue}ms, 差異={action.TotalMs - sprDataTableValue}ms");
                        }
                    }
                    else
                    {
                        missingActions++;
                    }
                }
            }

            report.AppendLine($"總動作數: {totalActions}");
            report.AppendLine($"一致: {matchedActions} ({matchedActions * 100.0 / totalActions:F1}%)");
            report.AppendLine($"不一致: {mismatchedActions} ({mismatchedActions * 100.0 / totalActions:F1}%)");
            report.AppendLine($"缺失: {missingActions} ({missingActions * 100.0 / totalActions:F1}%)");
            report.AppendLine();

            if (mismatchDetails.Count > 0)
            {
                report.AppendLine("不一致詳情:");
                report.AppendLine("-".PadRight(80, '-'));
                foreach (var detail in mismatchDetails)
                {
                    report.AppendLine($"  {detail}");
                }
                report.AppendLine();
            }

            // 保存報告
            string reportPath = "Tool/spr_speed_comparison.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
            
            // 同時輸出到控制台
            Console.WriteLine(report.ToString());
        }

        private static string GetCategoryName(ActionCategory category)
        {
            return category switch
            {
                ActionCategory.Walk => "行走 (Walk)",
                ActionCategory.MeleeAttack => "近戰攻擊 (Melee Attack)",
                ActionCategory.RangedAttack => "遠程攻擊 (Ranged Attack - Bow)",
                ActionCategory.LongRangeMelee => "長距離近戰 (Long Range Melee - Spear/Staff)",
                ActionCategory.MagicRanged => "魔法遠程 (Magic Ranged)",
                _ => category.ToString()
            };
        }
    }
}
