using Godot;
using System;
using System.Collections.Generic;

namespace Client.UI
{
    public partial class BottomBar : Control
    {
        // 快捷位数据定义
        public class HotkeyData
        {
            public string Type; // "item" 或 "skill"
            public int Id;
            public string IconPath;
        }

        private Dictionary<int, HotkeyData> _hotkeyConfigs = new Dictionary<int, HotkeyData>();
        
        private HBoxContainer _hotkeyContainer;
        // 【修复】改为 BaseButton，兼容 Button 和 TextureButton
        private BaseButton _btnAttack; 

        public override void _Ready()
        {
            // 获取引用
            _hotkeyContainer = GetNodeOrNull<HBoxContainer>("MainHBox/HotkeyGrid");
            _btnAttack = GetNodeOrNull<BaseButton>("MainHBox/BtnAttack");

            if (_hotkeyContainer == null) GD.PrintErr("[BottomBar] ❌ HotkeyGrid not found!");
            
            // 【修復】確保 _Process 被調用
            ProcessMode = ProcessModeEnum.Always;
            
            InitMenuButtons();
            InitHotkeySlots();
            InitInputLogic();
            
            // 1. 生成文字提示（小字體在按鈕上方）
            // 【重構】新順序：4個按鈕 + 8個快捷欄 + Z按鍵（最右邊）
            AddKeyLabels();

            // 3. 【新增】自动修复图标尺寸
            FixIconStyles();

            // 4. 【新增】读取本地存档
            InitConfig();

            GD.Print("[BottomBar] UI System Initialized.");
        }

        // --- 自动修复图标样式 (解决图标巨大的问题) ---
        private void FixIconStyles()
        {
            // 修复攻击按钮
            FixButton(_btnAttack);

            // 修复菜单按钮
            var menuBox = GetNodeOrNull("MainHBox/MenuHBox");
            if (menuBox != null)
            {
                foreach(var node in menuBox.GetChildren())
                    if (node is BaseButton btn) FixButton(btn);
            }
        }

        private void FixButton(BaseButton btn)
        {
            if (btn is TextureButton tb)
            {
                tb.IgnoreTextureSize = true; // 强制忽略原图尺寸
                tb.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered; // 保持比例居中
                // 确保它有最小尺寸 (如果你在编辑器里没设)
                if (tb.CustomMinimumSize == Vector2.Zero) 
                    tb.CustomMinimumSize = new Vector2(32, 32);
            }
        }


        // 動態添加字符提示（小字體在按鈕上方）
        private void AddKeyLabels()
        {
            // 【重構】新順序：4個按鈕 + 8個快捷欄 + Z按鍵（最右邊）
            
            // 1. 給菜單按鈕加提示 B, S, C, Q（在按鈕上方）
            var menuBox = GetNodeOrNull("MainHBox/MenuHBox");
            if (menuBox != null)
            {
                CreateSmallLabelAbove(menuBox.GetNodeOrNull<Control>("BtnInv"), "B");
                CreateSmallLabelAbove(menuBox.GetNodeOrNull<Control>("BtnSkill"), "S");
                CreateSmallLabelAbove(menuBox.GetNodeOrNull<Control>("BtnCha"), "C");
                CreateSmallLabelAbove(menuBox.GetNodeOrNull<Control>("BtnSetting"), "Q");
            }

            // 2. 給 8 個快捷欄加 "1" ~ "8"（在按鈕上方）
            if (_hotkeyContainer != null)
            {
                int index = 1;
                foreach (var child in _hotkeyContainer.GetChildren())
                {
                    if (child is Control slot) 
                    { 
                        CreateSmallLabelAbove(slot, index.ToString()); 
                        index++; 
                    }
                }
            }

            // 3. 給攻擊按鈕加提示 Z（在按鈕上方，最右邊）
            if (_btnAttack != null)
            {
                CreateSmallLabelAbove(_btnAttack, "Z");
            }
        }

        // 輔助方法：在按鈕上方創建一個非常小的 Label
        private void CreateSmallLabelAbove(Control parent, string text)
        {
            if (parent == null) return;

            var lbl = new Label();
            lbl.Text = text;
            // 設置非常小的字體
            lbl.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.8f)); // 白色半透明
            lbl.AddThemeFontSizeOverride("font_size", 8); // 非常小的字體
            
            // 布局：在按鈕上方
            lbl.LayoutMode = 1; // Anchors Layout
            lbl.SetAnchorsPreset(LayoutPreset.TopWide);
            lbl.OffsetTop = -12; // 在按鈕上方 12 像素
            lbl.OffsetBottom = -4; // 底部對齊到按鈕頂部上方
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.VerticalAlignment = VerticalAlignment.Center;
            lbl.MouseFilter = MouseFilterEnum.Ignore; // 忽略鼠標事件，不阻礙點擊

            parent.AddChild(lbl);
        }

        
    }
}