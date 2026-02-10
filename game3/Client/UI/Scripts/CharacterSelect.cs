using Godot;
using Client;
using Client.Data; // [重要] 统一使用 Client.Data
using System.Collections.Generic;
using System.Threading.Tasks;
using Skins.CustomFantasy;
namespace UI
{
    public partial class CharacterSelect : Control
    {
        private int _selectedIndex = -1;
        private List<Node> _slots = new();
        private SpriteFrames _emptySlotFrames = null!;
        private bool _isNavigating = false;

        private readonly string[] _fieldPrefixes = { 
            "角色名称", "血盟", "种族", "倾向", "等级", 
            "力量", "敏捷", "体质", "精神", "魅力", "智力", 
            "体力", "魔力", "防御" 
        };

        public override void _Ready()
        {
            GD.Print(">>> [UI] CharacterSelect _Ready (New Architecture)...");

            // [新架构] BGM
            if (Boot.Instance != null) Boot.Instance.PlayBgm(0); // music0

            // 1. 加载背景 (保持 AssetManager)
            var bg = GetNodeOrNull<TextureRect>("Background");
            if (bg != null) {
                bg.Texture = AssetManager.Instance.GetUITexture("105.img");
                bg.MouseFilter = MouseFilterEnum.Ignore;
            }
            var bg2 = GetNodeOrNull<TextureRect>("Background2");
            if (bg2 != null) {
                bg2.Texture = AssetManager.Instance.GetUITexture("104.img");
                bg2.MouseFilter = MouseFilterEnum.Ignore;
            }

            // 2. 空槽位资源
            _emptySlotFrames = AssetManager.Instance.CreateCharacterFrames(79, 92, 103);

            // 3. 初始化槽位
            _slots.Clear();
            for (int i = 0; i < 5; i++)
            {
                var slot = GetNodeOrNull($"Control/CharContainer/Slot{i}");
                if (slot != null)
                {
                    _slots.Add(slot);
                    var clickArea = slot.GetNodeOrNull<TextureButton>("SpriteRoot/ClickArea");
                    if (clickArea != null)
                    {
                        int index = i;
                        clickArea.MouseFilter = MouseFilterEnum.Stop;
                        
                        if (clickArea.IsConnected(BaseButton.SignalName.Pressed, Callable.From(() => OnSlotSelected(index))))
                            clickArea.Disconnect(BaseButton.SignalName.Pressed, Callable.From(() => OnSlotSelected(index)));
                        
                        clickArea.Pressed += () => OnSlotSelected(index);
                    }
                }
            }

            // 4. 绑定底部按钮
            var btnArea = GetNodeOrNull<Control>("Control/VBoxContainer/ButtonArea");
            if (btnArea != null)
            {
                SetupAndBindBtn(btnArea, "BtnConfirm", "61.img", "62.img", OnConfirmPressed); 
                SetupAndBindBtn(btnArea, "BtnCancel", "63.img", "64.img", OnLogoutPressed); 
                SetupAndBindBtn(btnArea, "BtnDelete", "308.img", "309.img", OnDeletePressed);
            }

            // 5. [新架构] 监听 Boot 数据更新
            if (Boot.Instance != null)
            {
                Boot.Instance.CharacterListUpdated -= RefreshUI;
                Boot.Instance.CharacterListUpdated += RefreshUI;
                
                // 立即刷新
                RefreshUI();
            }
        }

        public override void _ExitTree()
        {
            if (Boot.Instance != null)
                Boot.Instance.CharacterListUpdated -= RefreshUI;
        }

        private void SetupAndBindBtn(Node parent, string nodeName, string normalImg, string hoverImg, System.Action action)
        {
            var node = parent.GetNodeOrNull(nodeName);
            if (node == null) return;

            if (node is TextureButton texBtn)
            {
                texBtn.TextureNormal = AssetManager.Instance.GetUITexture(normalImg);
                var h = AssetManager.Instance.GetUITexture(hoverImg);
                texBtn.TexturePressed = h;
                texBtn.TextureHover = h;
                
                if (texBtn.IsConnected(BaseButton.SignalName.Pressed, Callable.From(action)))
                    texBtn.Disconnect(BaseButton.SignalName.Pressed, Callable.From(action));
                texBtn.Pressed += action;
            }
        }

        private void RefreshUI()
        {
            if (Boot.Instance == null) return;
            var list = Boot.Instance.CharacterList;
            
            GD.Print($">>> [UI] RefreshUI triggered. List Count: {list.Count}");

            for (int i = 0; i < 5; i++)
            {
                if (i >= _slots.Count) break;
                var sprite = _slots[i].GetNodeOrNull<AnimatedSprite2D>("SpriteRoot/CharSprite");
                if (sprite == null) continue;

                if (i < list.Count) {
                    var info = list[i];
                    // 确保使用 info 中的 Type 和 Sex
                    sprite.SpriteFrames = LoadClassFrames(info.Type, info.Sex);
                    sprite.Play("idle");
                    sprite.Visible = true;
                } else {
                    sprite.SpriteFrames = _emptySlotFrames;
                    sprite.Play("idle"); 
                    sprite.Visible = true;
                }
            }
            // 刷新时不自动重置选中状态，除非当前选中项失效
            if (_selectedIndex >= list.Count) {
                 _selectedIndex = -1;
                 ResetUIState();
            }
        }

        private void ResetUIState()
        {
            foreach(var slot in _slots) {
                var light = slot.GetNodeOrNull<AnimatedSprite2D>("SpriteRoot/LightEffect2");
                if(light != null) light.Visible = false;
            }
            var panel = GetNodeOrNull<Control>("Control/VBoxContainer/DetailPanel");
            if(panel != null) {
                for(int i=1; i<=14; i++) {
                    var label = panel.GetNodeOrNull<Label>($"charLabel{i}");
                    if(label != null) label.Text = $"{_fieldPrefixes[i-1]} : --";
                }
            }
        }

        private void OnSlotSelected(int index)
        {
            if (_isNavigating) return;
            _selectedIndex = index;
            GD.Print($">>> [UI] 选中槽位: {index}");

            var list = Boot.Instance.CharacterList;
            
            // 点击空槽位 -> 创建角色
            if (index >= list.Count)
            {
                GD.Print(">>> [UI] 点击空槽位，跳转创建角色...");
                // [新架构] 使用 Boot 跳转
                Boot.Instance.ToCharacterCreateScene();
                return;
            }

            // 选中已有角色
            for (int i = 0; i < 5; i++)
            {
                if (i >= _slots.Count) break;
                var sprite = _slots[i].GetNodeOrNull<AnimatedSprite2D>("SpriteRoot/CharSprite");
                var light = _slots[i].GetNodeOrNull<AnimatedSprite2D>("SpriteRoot/LightEffect2");

                if (i == index) {
                    if (sprite != null && sprite.SpriteFrames.HasAnimation("walk")) sprite.Play("walk");
                    if (light != null) { light.Visible = true; light.Play("default"); }
                    
                    // [修复] 确保数据读取并显示
                    if (i < list.Count) UpdateDetailLabels(list[i]);
                } else {
                    if (sprite != null && sprite.SpriteFrames.HasAnimation("idle")) sprite.Play("idle");
                    if (light != null) light.Visible = false;
                }
            }
        }

        private async void OnConfirmPressed()
        {
            if (_selectedIndex == -1) {
                GD.Print(">>> [UI] 请先选择一个槽位");
                return;
            }

            var list = Boot.Instance.CharacterList;

            // 情况 A: 选中了已有角色 -> 进入游戏
            if (_selectedIndex < list.Count)
            {
                var name = list[_selectedIndex].Name;
                GD.Print($">>> [UI] 请求进入世界: {name}");
                // [新架构] Boot 调用
                Boot.Instance.ToGameWorldScene(name);
            }
            // 情况 B: 选中了空槽位 -> 跳转新建角色
            else
            {
                if (_isNavigating) return;
                _isNavigating = true;
                GD.Print(">>> [UI] 确认新建角色，跳转中...");
                await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
                // [新架构] Boot 跳转
                Boot.Instance.ToCharacterCreateScene();
            }
        }

        private void OnLogoutPressed()
        {
            GD.Print(">>> [UI] 返回登录页面 (Logout)");
            Boot.Instance.LoadScene("res://Client/UI/Scenes/Login.tscn");
        }

        private void OnDeletePressed()
        {
            if (_selectedIndex < 0)
            {
                GD.Print(">>> [UI] 请先选择要删除的角色");
                return;
            }
            var list = Boot.Instance?.CharacterList;
            if (list == null || _selectedIndex >= list.Count)
            {
                GD.Print(">>> [UI] 无法删除：选中的是空槽位");
                return;
            }
            string name = list[_selectedIndex].Name;
            GD.Print($">>> [UI] 请求删除角色: {name}");
            Boot.Instance.Action_DeleteCharacter(name);
            _selectedIndex = -1;
            ResetUIState();
        }

        private SpriteFrames LoadClassFrames(int classType, int sex)
        {
            // 王族
            if (classType == 0) return sex == 0 ? AssetManager.Instance.CreateCharacterFrames(138, 151, 162) : AssetManager.Instance.CreateCharacterFrames(163, 176, 187);
            // 骑士
            if (classType == 1) return sex == 0 ? AssetManager.Instance.CreateCharacterFrames(213, 226, 237) : AssetManager.Instance.CreateCharacterFrames(238, 251, 264);
            // 妖精
            if (classType == 2) return sex == 0 ? AssetManager.Instance.CreateCharacterFrames(332, 345, 356) : AssetManager.Instance.CreateCharacterFrames(188, 201, 212);
            // 法师
            if (classType == 3) return sex == 0 ? AssetManager.Instance.CreateCharacterFrames(362, 375, 386) : AssetManager.Instance.CreateCharacterFrames(621, 634, 645);
            return null;
        }

        private void UpdateDetailLabels(Client.Data.CharacterInfo data)
        {
            var panel = GetNodeOrNull<Control>("Control/VBoxContainer/DetailPanel");
            if (panel == null) return;
            
            // [适配] 将 int Type 转为 string
            string className = GetClassName(data.Type);

            string[] values = { 
                data.Name, 
                string.IsNullOrEmpty(data.ClanName) ? "无" : data.ClanName, 
                className, 
                data.Lawful.ToString(), 
                data.Level.ToString(), 
                data.Str.ToString(), 
                data.Dex.ToString(), 
                data.Con.ToString(), 
                data.Wis.ToString(), 
                data.Cha.ToString(), 
                data.Int.ToString(), // Intel -> Int
                data.Hp.ToString(), 
                data.Mp.ToString(), 
                data.Ac.ToString() 
            };
            for (int i = 1; i <= 14; i++) {
                var label = panel.GetNodeOrNull<Label>($"charLabel{i}");
                if (label != null) label.Text = $"{_fieldPrefixes[i-1]} : {values[i-1]}";
            }
        }

        // [新增] 职业名称映射辅助
        private string GetClassName(int type)
        {
            switch(type)
            {
                case 0: return "王族";
                case 1: return "骑士";
                case 2: return "妖精";
                case 3: return "法师";
                default: return "未知";
            }
        }

        private void ResetDetailLabelsOnly()
        {
            var panel = GetNodeOrNull<Control>("Control/VBoxContainer/DetailPanel");
            if (panel != null) {
                for(int i=1; i<=14; i++) {
                    var label = panel.GetNodeOrNull<Label>($"charLabel{i}");
                    if(label != null) label.Text = $"{_fieldPrefixes[i-1]} : --";
                }
            }
        }
    }
}