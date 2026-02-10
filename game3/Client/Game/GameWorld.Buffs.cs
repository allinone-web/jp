using Godot;
using System;
using Client.Network;

namespace Client.Game
{
    public partial class GameWorld
    {
        // ========================================================================
        //   Buff 系统初始化与绑定
        // ========================================================================
        private void BindBuffSignals()
        {
            if (PacketHandlerRef != null)
            {
                PacketHandlerRef.BuffSpeedReceived += OnBuffSpeedReceived;
                PacketHandlerRef.BuffAquaReceived += OnBuffAquaReceived;
                PacketHandlerRef.BuffShieldReceived += OnBuffShieldReceived;
                PacketHandlerRef.BuffBlindReceived += OnBuffBlindReceived;
            }
        }

        // ------------------------------------------------------------------------
        // 隱身術 (Opcode 52)：自己顯示半透明，其他玩家完全不可見
        // ------------------------------------------------------------------------
        private void OnObjectInvisReceived(int objectId, bool invis)
        {
            bool isSelf = (_myPlayer != null && _myPlayer.ObjectId == objectId);
            GameEntity entity = null;
            if (isSelf)
                entity = _myPlayer;
            else if (_entities != null && _entities.TryGetValue(objectId, out var e))
                entity = e;

            if (entity != null)
                entity.SetInvisible(invis, isSelf);
        }

        // ========================================================================
        //   Buff 回调逻辑
        // ========================================================================

        // 处理加速/勇敢/緩速 (Op 41=Haste/加速術/緩速, 98=Brave/勇敢藥水)
        // 封包: writeD(objId), writeC(speed), writeH(time)
        // - Op41 + speed=1: 加速術（Haste）
        // - Op41 + speed=2: 緩速（Slow）
        // - Op98 + speed=1: 勇敢藥水（Brave）
        // 【服務器速度倍數】根據 CheckSpeed.java：
        // - 綠水（Haste）：interval * 0.75（速度提升 33.3%，動畫速度 = 1/0.75 = 1.333...）
        // - 勇敢（Brave）：interval * 0.75（攻擊速度提升 33.3%）
        // - 緩速（Slow）：interval / 0.75 = interval * 1.333...（速度降低 25%，動畫速度 = 0.75）
        private void OnBuffSpeedReceived(int entityId, int type, int speed, int time)
        {
            GameEntity entity = null;
            if (entityId == _myPlayer?.ObjectId)
                entity = _myPlayer;
            else if (_entities != null && _entities.TryGetValue(entityId, out var e))
                entity = e;

            if (entity == null) return;

            // jp: S_OPCODE_SKILLHASTE=149, S_OPCODE_SKILLBRAVE=200；其他服可能 41/98
            if (type == 41 || type == 149) // 加速術或緩速
            {
                if (speed == 1) // 加速術（Haste）
                {
                    entity.AnimationSpeed = 1.0f / 0.75f;
                    EnhancedSpeedManager.SetHaste(true);
                    EnhancedSpeedManager.SetSlow(false);
                    if (entity == _myPlayer)
                    {
                        GD.Print($"[Buff] 加速術 -> 生效 Time:{time} AnimationSpeed:{entity.AnimationSpeed} (影響移動/攻擊/魔法)");
                        _hud?.AddBuffIcon("haste", time);
                    }
                }
                else if (speed == 2) // 緩速（Slow）
                {
                    entity.AnimationSpeed = 0.75f;
                    EnhancedSpeedManager.SetHaste(false);
                    EnhancedSpeedManager.SetSlow(true);
                    if (entity == _myPlayer)
                    {
                        GD.Print($"[Buff] 緩速 -> 生效 Time:{time} AnimationSpeed:{entity.AnimationSpeed}");
                        _hud?.AddBuffIcon("slow", time);
                    }
                }
                else // speed == 0，解除效果
                {
                    entity.AnimationSpeed = 1.0f;
                    EnhancedSpeedManager.SetHaste(false);
                    EnhancedSpeedManager.SetSlow(false);
                    if (entity == _myPlayer)
                    {
                        GD.Print($"[Buff] 加速/緩速 -> 解除");
                        _hud?.RemoveBuffIcon("haste");
                        _hud?.RemoveBuffIcon("slow");
                    }
                }
            }
            else if (type == 98 || type == 200) // 勇敢藥水
            {
                bool active = (speed != 0);
                EnhancedSpeedManager.SetBrave(active);
                if (entity == _myPlayer)
                {
                    GD.Print($"[Buff] 勇敢 -> " + (active ? "生效" : "解除") + $" Time:{time}");
                    if (active)
                        _hud?.AddBuffIcon("brave", time);
                    else
                        _hud?.RemoveBuffIcon("brave");
                }
            }
        }

        // 处理水下呼吸 (Op 119 或 jp 對應封包)
        private void OnBuffAquaReceived(int entityId, int time)
        {
            if (entityId == _myPlayer?.ObjectId)
            {
                GD.Print($"[Buff] 获得水下呼吸效果，持续 {time} 秒");
                _hud?.AddSystemMessage("你感觉呼吸变得轻松了。");
                _hud?.AddBuffIcon("aqua", time);
            }
        }

        // 处理护盾 (Op 69 - S_OPCODE_SKILLICONSHIELD)
        private void OnBuffShieldReceived(int time, int type)
        {
            GD.Print($"[Buff] 获得魔法护盾效果，持续 {time} 秒 (Type: {type})");
            _hud?.AddSystemMessage("你被魔法护盾保护着。");
            _hud?.AddBuffIcon("shield", time);
        }

        // 处理失明 (Op 10)
        private void OnBuffBlindReceived(int type)
        {
            GD.Print($"[Buff] 致盲效果更新: {type}");
            
            if (type >= 1)
            {
                _hud?.AddSystemMessage("你的眼睛什么都看不见了！");
                // TODO: 可以在 HUD 上盖一层黑色遮罩 (CanvasLayer)
                // UIManager.Instance.ShowBlindEffect(true);
            }
            else
            {
                _hud?.AddSystemMessage("你的视力恢复了。");
                // UIManager.Instance.ShowBlindEffect(false);
            }
        }
    }
}