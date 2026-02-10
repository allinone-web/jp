using Godot;
using System;

namespace Client.UI
{
    public partial class BottomBar
    {
        private void InitMenuButtons()
        {
            // 获取按钮节点 (使用 BaseButton 以兼容 TextureButton 或 Button)
            var btnInv = GetNodeOrNull<BaseButton>("MainHBox/MenuHBox/BtnInv");
            var btnSkill = GetNodeOrNull<BaseButton>("MainHBox/MenuHBox/BtnSkill");
            var btnCha = GetNodeOrNull<BaseButton>("MainHBox/MenuHBox/BtnCha");
            var btnSetting = GetNodeOrNull<BaseButton>("MainHBox/MenuHBox/BtnSetting");

            // 绑定事件：复用 UIManager.Instance.Toggle
            if (btnInv != null) 
                btnInv.Pressed += () => UIManager.Instance.Toggle(WindowID.Inventory);
            
            if (btnSkill != null) 
                btnSkill.Pressed += () => UIManager.Instance.Toggle(WindowID.Skill);
            
            if (btnCha != null) 
                btnCha.Pressed += () => UIManager.Instance.Toggle(WindowID.Character);
            
            if (btnSetting != null) 
                btnSetting.Pressed += () => UIManager.Instance.Toggle(WindowID.Options);

            GD.Print("[BottomBar] Menu Buttons Initialized.");
        }
    }
}