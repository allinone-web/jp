using Godot;
using System;
using System.Collections.Generic;
using System.Linq; // 需要 Linq 进行排序

namespace Client.Game
{
    public partial class GameWorld
    {
        /// <summary>
        /// 智能寻找最近的目标
        /// </summary>
        /// <param name="ignoreNpc">是否忽略 NPC (只选怪物)</param>
        /// <returns>最近的有效实体，如果没有则返回 null</returns>
        public GameEntity GetSmartTarget(bool ignoreNpc)
        {
            if (_myPlayer == null) return null;

            GameEntity bestTarget = null;
            float minDistance = float.MaxValue;
            Vector2 myPos = _myPlayer.GlobalPosition;

            // 遍历所有实体
            foreach (var entity in _entities.Values)
            {
                // 1. 排除自己
                if (entity == _myPlayer) continue;

                // 2. 排除已死亡實體：正確的死亡判斷是 _currentRawAction == ACT_DEATH（收到死亡動作），而不是僅依賴 HpRatio <= 0
                if (entity.IsDead) continue;
                // 3. 僅有伺服器血條數據的實體可為目標（不依 list.spr）
                if (!entity.HasServerHp()) continue;

                // 4. 判斷類型：Lawful < 0 = 可攻擊（怪物/紅名）；Lawful >= 0 = NPC/玩家（按 Z 時不選，應彈對話）
                bool isMonster = entity.Lawful < 0;

                if (ignoreNpc && !isMonster) continue;

                // 5. 计算距离 (使用平方距离比较性能更好，但这里用 DistanceTo 直观)
                float dist = myPos.DistanceTo(entity.GlobalPosition);

                // 6. 范围限制 (可选：太远的比如超过屏幕的就不选)
                if (dist > 800) continue; 

                // 7. 更新最近目标
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = entity;
                }
            }

            return bestTarget;
        }

        // --- 辅助：获取最近的攻击位 (用于自动移动) ---
        // 之前在 Combat.cs 里提到过，这里可以复用或封装
        public (int x, int y) GetBestStandPosition(int targetX, int targetY)
        {
            // 简单的 8 方向寻找最近空位算法 (暂时简单返回目标旁边)
            // 实际逻辑在 Combat.cs 的 GetBestAttackPosition 已经实现，这里留作接口扩展
            return (targetX, targetY); 
        }
    }
}