using Godot;
using System;
using System.Collections.Generic;

namespace Client.UI
{
    public partial class UIManager : Node
    {
        public static UIManager Instance { get; private set; }

        // --- 窗口注册表 (配置) ---
        // 这里存储 WindowID 对应的 .tscn 路径
        // 后续我们将把这个字典暴露给编辑器，方便拖拽赋值
        private Dictionary<WindowID, string> _windowPaths = new Dictionary<WindowID, string>
        {
            { WindowID.Character, "res://Client/UI/Scenes/Windows/ChaWindow.tscn" },
            { WindowID.Inventory, "res://Client/UI/Scenes/Windows/InvWindow.tscn" },
            { WindowID.Skill, "res://Client/UI/Scenes/Windows/SkillWindow.tscn" },
            { WindowID.Talk, "res://Client/UI/Scenes/Windows/TalkWindow.tscn" },
            { WindowID.Shop,      "res://Client/UI/Scenes/Windows/ShopWindow.tscn" },
            { WindowID.Options,      "res://Client/UI/Scenes/Windows/OptionsWindow.tscn" },
            { WindowID.WareHouse,      "res://Client/UI/Scenes/Windows/WareHouseWindow.tscn" },
            { WindowID.PetWarehouse, "res://Client/UI/Scenes/Windows/PetWarehouseWindow.tscn" },
            // 【修復】PetPanel 已廢棄，改為使用 TalkWindow
            // { WindowID.PetPanel, "res://Client/UI/Scenes/Windows/PetPanelWindow.tscn" },
            { WindowID.CombatStats, "res://Client/UI/Scenes/Windows/CombatStatsWindow.tscn" },
            { WindowID.AmountInput, "res://Client/UI/Scenes/Windows/AmountInputWindow.tscn" },   
            { WindowID.SkinSelect, "res://Client/UI/Scenes/Windows/SkinSelectWindow.tscn" },   

           // ... 后续添加 Shop, Skill 等
        };

        // --- 运行时缓存 ---
        // 已经实例化出来的窗口对象
        private Dictionary<WindowID, GameWindow> _windows = new Dictionary<WindowID, GameWindow>();

        // --- 容器 ---
        // 所有窗口都会被加到这个节点下 (确保在 HUD 之上)
        private CanvasLayer _windowLayer;

        public override void _Ready()
        {
            Instance = this;
            
            // 创建一个独立的画布层，层级 10，确保盖住 HUD (通常层级1)
            _windowLayer = new CanvasLayer();
            _windowLayer.Layer = 10; 
            _windowLayer.Name = "WindowsLayer";
            AddChild(_windowLayer);

            GD.Print("[UI] UIManager Ready.");
        }

        // --- 核心 API ---

        /// <summary>
        /// 打开指定窗口
        /// </summary>
        public void Open(WindowID id, WindowContext context = null)
        {
            var window = GetWindow(id);
            if (window != null)
            {
                // ✅ 无论窗口当前是关是开，都必须调用 OnOpen 来刷新数据 (Context)
                // 之前的代码在 else 里只 BringToFront，导致 NPC 对话数据没传进去
                window.OnOpen(context);
                
                // 确保可见并置顶
                if (!window.Visible) window.Visible = true;
                BringToFront(window);
            }
        }

        /// <summary>
        /// 关闭指定窗口
        /// </summary>
        public void Close(WindowID id)
        {
            if (_windows.TryGetValue(id, out var window)) window.Close();
        }

        /// <summary>
        /// 切换窗口状态 (开 <-> 关)
        /// </summary>
        public void Toggle(WindowID id)
        {
            var window = GetWindow(id);
            if (window != null) window.Toggle();
        }

        /// <summary>
        /// 获取窗口实例 (如果不存在则自动创建)
        /// </summary>
        public GameWindow GetWindow(WindowID id)
        {
            if (_windows.TryGetValue(id, out var existingWindow)) return existingWindow;

            // 2. 检查是否有路径配置
            if (!_windowPaths.ContainsKey(id))
            {
                GD.PrintErr($"[UI] 未注册的窗口 ID: {id}，请在 UIManager._windowPaths 中添加配置。");
                return null;
            }

            // 3. 加载并实例化
            string path = _windowPaths[id];
            try
            {
                if (!ResourceLoader.Exists(path))
                {
                    GD.PrintErr($"[UI] 找不到场景文件: {path}");
                    return null;
                }

                var scene = ResourceLoader.Load<PackedScene>(path);
                if (scene == null)
                {
                    GD.PrintErr($"[UI] 加载场景返回空: {path}");
                    return null;
                }
                var node = scene.Instantiate();

                if (_windowLayer == null)
                {
                    _windowLayer = new CanvasLayer();
                    _windowLayer.Layer = 10;
                    _windowLayer.Name = "WindowsLayer";
                    AddChild(_windowLayer);
                }
                if (node is GameWindow win)
                {
                    win.ID = id; // 赋予 ID
                    _windowLayer.AddChild(win); // 添加到显示层
                    _windows.Add(id, win); // 加入缓存
                    win.Close(); // 默认创建后先隐藏
                    return win;
                }
                else
                {
                    GD.PrintErr($"[UI] 场景 {path} 的根节点必须继承自 GameWindow!");
                    node.QueueFree();
                    return null;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[UI] 创建窗口失败 {id}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将窗口提到最上层 (Z-Index)
        /// </summary>
        public void BringToFront(GameWindow window)
        {
            window.MoveToFront();
        }

        // --- 辅助：检查某个窗口是否打开 ---
        public bool IsOpen(WindowID id)
        {
            return _windows.TryGetValue(id, out var win) && win.Visible;
        }
    }
}