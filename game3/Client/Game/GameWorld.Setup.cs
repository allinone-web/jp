// ============================================================================
// [FILE] GameWorld.Setup.cs
// 说明：把 _Ready 里的初始化块拆成 Setup 小函数，目的：
// - 提升可读性与可维护性
// - 保持执行顺序不变（严格按 _Ready 中的调用顺序执行）
//
// 规则：
// - 只做“搬家”与“注释分组”，不改逻辑、不改路径、不改日志（除非原本就有日志）
// [UPDATE] 修复 SetupInitMapProvider 使用 AssetMapProvider。
// ============================================================================

using Godot;
using Client.Data;
using Client.Network;
using Client.UI;
using Skins.CustomFantasy; // 确保能引用 AssetMapProvider

namespace Client.Game
{
	public partial class GameWorld
	{
		// =====================================================================
		// [SECTION] Setup: Resolve Runtime References (NetSession / PacketHandlerRef)
		// 说明：获取 Boot 节点下由启动流程创建的真实连接与 PacketHandler。
		// =====================================================================
		private void SetupResolveRuntimeRefs()
		{
			_netSession = GetNodeOrNull<GodotTcpSession>("/root/Boot/NetSession");
			if (PacketHandlerRef == null)
				PacketHandlerRef = GetNodeOrNull<PacketHandler>("/root/Boot/PacketHandler");
		}
		// =====================================================================
		// [SECTION END] Setup: Resolve Runtime References
		// =====================================================================


		// =====================================================================
		// [SECTION] Setup: Ensure UIManager (Auto-Start)
		// 说明：确保 UIManager.Instance 可用，否则按键打开窗口会报错。
		// =====================================================================
		private void SetupEnsureUIManager()
		{
			if (UIManager.Instance == null)
			{
				var uiMgr = new UIManager();
				uiMgr.Name = "UIManager";
				AddChild(uiMgr);
				GD.Print("[GameWorld] UIManager Auto-Started.");
			}
		}
		// =====================================================================
        // [SECTION] Setup: Init Map Provider (新增)
        // 说明：实例化具体的地图加载器。
        // =====================================================================
        private void SetupInitMapProvider()
        {
            // 【修复】使用 AssetMapProvider
            if (_mapProvider == null)
            {
                _mapProvider = new AssetMapProvider();
                GD.Print("[GameWorld] AssetMapProvider Initialized.");
            }
        }
        // =====================================================================

		// =====================================================================
		// [SECTION END] Setup: Ensure UIManager
		// =====================================================================


		// =====================================================================
		// [SECTION] Setup: Bind PacketHandler Signals
		// 说明：实际绑定逻辑集中在 GameWorld.Bindings.cs 的 BindPacketHandlerSignals()。
		// =====================================================================
		private void SetupBindPacketHandlerSignals()
		{
			BindPacketHandlerSignals();
		}
		// =====================================================================
		// [SECTION END] Setup: Bind PacketHandler Signals
		// =====================================================================


		// =====================================================================
		// [SECTION] Setup: Init Camera
		// 说明：创建 Camera2D 并设为当前摄像机，保持原始 Zoom。
		// =====================================================================
		private void SetupInitCamera()
		{
			_camera = new Camera2D();
			AddChild(_camera);
			_camera.MakeCurrent();
			_camera.Zoom = new Vector2(1.5f, 1.5f);
		}
		// =====================================================================
		// [SECTION END] Setup: Init Camera
		// =====================================================================


		// =====================================================================
		// [SECTION] Setup: Init HUD and Chat
		// 说明：实例化 HUDScene，并连接 ChatSubmitted -> HandleChatInput（密語 /w 發 C_122，其餘發 C_190）。
		// =====================================================================
		private void SetupInitHUDAndChat()
		{
			if (HUDScene != null)
			{
				_hud = HUDScene.Instantiate<Client.UI.HUD>();
				AddChild(_hud);
				_hud.Layer = 2; // 高於日夜遮罩(Layer 1)，確保快捷列/血條不被黑色層遮住
				_hud.ChatSubmitted += HandleChatInput;
			}

			// 拖放物品到地面：若有 ItemDropZone 則綁定發送 C_54；若無則在 HUD 下建立一個
			var dropZone = GetNodeOrNull<Client.UI.ItemDropZone>("ItemDropZone");
			if (dropZone == null)
				dropZone = _hud?.GetNodeOrNull<Client.UI.ItemDropZone>("ItemDropZone");
			if (dropZone == null && _hud != null)
			{
				dropZone = new Client.UI.ItemDropZone();
				dropZone.Name = "ItemDropZone";
				_hud.AddChild(dropZone);
				dropZone.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			}
			if (dropZone != null)
			{
				dropZone.ItemDropped += OnItemDroppedToGround;
				GD.Print("[GameWorld] ItemDropZone connected -> C_DropItem(54)");
			}
		}

		private void OnItemDroppedToGround(int itemObjectId)
		{
			SendDropItem(itemObjectId, 1);
		}
		// =====================================================================
		// [SECTION END] Setup: Init HUD and Chat
		// =====================================================================


		// =====================================================================
		// [SECTION] Setup: Init Day/Night Overlay (遊戲世界時間 → 全螢幕日夜 Shader)
		// 說明：依 ClientConfig.DayNightOverlayEnabled 決定是否建立；關閉時不建立以省性能。
		// =====================================================================
		private void SetupInitDayNightOverlay()
		{
			if (!ClientConfig.DayNightOverlayEnabled) return;
			if (GetNodeOrNull("DayNightOverlay") != null) return;
			var scene = GD.Load<PackedScene>("res://Client/UI/Scenes/DayNightOverlay.tscn"); // 【修復】路徑已移動到 UI/Scenes
			if (scene == null) return;
			var overlay = scene.Instantiate();
			overlay.Name = "DayNightOverlay";
			AddChild(overlay);
			(overlay as DayNightOverlay)?.Connect(DayNightOverlay.SignalName.DarknessChanged, Callable.From<float, int>(OnDarknessChanged));
		}

		/// <summary>黑夜系統開關：開啟時建立遮罩層（若尚未存在），關閉時徹底銷毀並還原頭頂名字/血條。</summary>
		internal void SetDayNightOverlayEnabled(bool enabled)
		{
			ClientConfig.DayNightOverlayEnabled = enabled;
			ClientConfig.Save();
			if (enabled)
			{
				if (GetNodeOrNull("DayNightOverlay") == null)
					SetupInitDayNightOverlay();
			}
			else
			{
				var overlay = GetNodeOrNull("DayNightOverlay");
				if (overlay != null)
					overlay.QueueFree();
				OnDarknessChanged(0f, 0);
			}
		}
		// =====================================================================
		// [SECTION END] Setup: Init Day/Night Overlay
		// =====================================================================


		// =====================================================================
		// [SECTION] Setup: Notify Boot World Ready
		// 说明：通知 Boot：WorldScene 已准备好，让 Boot 释放缓存封包或切换状态。
		// =====================================================================
		private void SetupNotifyBootWorldReady()
		{
			var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
			if (boot != null)
			{
				GD.Print("[GameWorld] Notifying Boot to release cached packets...");
				boot.NotifyWorldSceneReady();
			}
		}
		// =====================================================================
		// [SECTION END] Setup: Notify Boot World Ready
		// =====================================================================

	}
}
