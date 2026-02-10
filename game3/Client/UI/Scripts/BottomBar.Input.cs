using Godot; // 【修复】必须添加这个，分部类不共享引用
using System;
using Client.Game;
using Client;

namespace Client.UI
{
    public partial class BottomBar
    {
        private float _attackPressTime = 0f;
        private bool _isAttackPressed = false;

        private void InitInputLogic()
        {
            if (_btnAttack == null) return;

            // 确保不获取焦点
            _btnAttack.FocusMode = FocusModeEnum.None;
            // 确保拦截点击
            _btnAttack.MouseFilter = Control.MouseFilterEnum.Stop;

            _btnAttack.ButtonDown += () => { _isAttackPressed = true; OnAttackButtonPressed(); };
            _btnAttack.ButtonUp += () => { _isAttackPressed = false; _attackPressTime = 0f; };
        }

        public override void _Process(double delta)
        {
            // 1. 处理 1-8 键盘快捷键触发（焦點在 ChatInput/LineEdit/TextEdit 時不觸發，避免打字 1-8 誤觸快捷鍵）
            var focus = GetViewport().GuiGetFocusOwner();
            if (focus is not LineEdit && focus is not TextEdit)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (Input.IsActionJustPressed($"hotkey_{i + 1}"))
                    {
                        GD.Print($"[BottomBar] Hotkey {i + 1} pressed, configs count: {_hotkeyConfigs.Count}");
                        ExecuteHotkey(i);
                    }
                }
            }

            // 2. 处理攻击按钮长按 (强制攻击逻辑)
            if (_isAttackPressed)
            {
                _attackPressTime += (float)delta;
                if (_attackPressTime > 0.5f)
                {
                    // 此处预留给未来的强制攻击逻辑
                }
            }
        }

        public void ExecuteHotkey(int index)
        {
            if (!_hotkeyConfigs.ContainsKey(index))
            {
                GD.Print($"[BottomBar] ExecuteHotkey: Slot {index} is empty");
                return;
            }
            
            var data = _hotkeyConfigs[index];
            GD.Print($"[BottomBar] ExecuteHotkey: Slot {index}, Type={data.Type}, Id={data.Id}");
            // 【修復快捷鍵 8】使用 Boot.Instance 獲取 GameWorld，確保在 HUD 場景修改後仍能正常工作
            var world = Boot.Instance?.GetNodeOrNull<GameWorld>("World");
            if (world == null)
            {
                GD.PrintErr("[BottomBar] ExecuteHotkey: GameWorld not found! (tried /root/Boot/World and Boot.Instance.World)");
                return;
            }

            if (data.Type == "item")
            {
                world.UseItem(data.Id);
            }
            else if (data.Type == "skill")
            {
                // 【核心修復】檢查技能類型：如果是攻擊類技能，使用自動攻擊邏輯（掃描目標、追擊、攻擊）
                // 這樣法師可以使用物理攻擊和弓箭，而不只是魔法
                // 非攻擊類技能（buff、heal 等）仍然立即使用，不觸發自動攻擊
                var entry = Client.Data.SkillListData.Get(data.Id);
                string skillType = (entry?.Type ?? "").Trim().ToLowerInvariant();
                
                // 攻擊類技能：使用自動攻擊邏輯（類似 Z 鍵）
                if (skillType == "attack" || string.IsNullOrEmpty(skillType))
                {
                    // 使用 ScanForAutoTargetWithSkill 掃描目標並設置攻擊任務
                    world.ScanForAutoTargetWithSkill(data.Id);
                }
                else
                {
                    // 非攻擊類技能（buff、heal 等）：立即使用，不觸發自動攻擊
                    // 【向後兼容】法師 Z 自動攻擊改為使用此技能（僅當不是攻擊類技能時）
                    world.SetMageAutoAttackSkill(data.Id);
                    world.UseMagic(data.Id);
                }
            }
        }

        private void OnAttackButtonPressed()
        {
            // 【修復快捷鍵 8】使用 Boot.Instance 獲取 GameWorld，確保在 HUD 場景修改後仍能正常工作
            var world = Boot.Instance?.GetNodeOrNull<GameWorld>("World");
            if (world == null) return;

            // 调用智能搜寻
            var target = world.GetSmartTarget(false); 

            if (target != null)
            {
                if (target.Lawful < 0)
                {
                    GD.Print($"[UI] Smart Attack: {target.RealName}");
                    world.StartAutoAttack(target);
                }
                else
                {
                    GD.Print($"[UI] Smart Talk: {target.RealName}");
                    world.TalkToNpc(target.ObjectId);
                }
            }
            else
            {
                // 2. 【新增反馈】如果没有目标，执行"强制攻击/原地挥刀"
                // 这样你就知道按钮肯定按到了
                GD.Print("[UI] No target nearby. Forcing Attack Animation.");
                world.PerformAttackOnce(0, world._myPlayer.MapX, world._myPlayer.MapY);
            }
        }
    }
}