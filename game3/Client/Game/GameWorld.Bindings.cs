// ============================================================================
// [FILE] GameWorld.Bindings.cs
// 说明：本文件仅负责“信号绑定/桥接绑定”，让 GameWorld.cs 保持为主控调度层。
// 规则：不改任何功能逻辑，只搬运并集中管理绑定段落。
// [UPDATE] 绑定 MapChanged 信号
// ============================================================================

using Godot;
using Client.Network;
using Client.Data;

namespace Client.Game
{
	public partial class GameWorld
	{
		// =====================================================================
		// [SECTION] PacketHandler Signal Bindings (网络信号绑定总入口)
		// 说明：
		// - 将 PacketHandlerRef 的所有事件绑定到 GameWorld 的回调
		// - 同时调用各子系统 BindXXXSignals()（这些方法由你现有分部类提供）
		// =====================================================================
		private void BindPacketHandlerSignals()
		{
			if (PacketHandlerRef != null)
			{
				GD.Print("[GameWorld] ✅ PacketHandler Linked.");

				// -------------------------------------------------------------
				// [BLOCK] 对象生命周期 (Spawn/Delete)
				// -------------------------------------------------------------
				PacketHandlerRef.ObjectSpawned += OnObjectSpawned;
				PacketHandlerRef.ObjectDeleted += OnObjectDeleted;
				// -------------------------------------------------------------
				// [BLOCK END] 对象生命周期
				// -------------------------------------------------------------


				// -------------------------------------------------------------
				// [BLOCK] 移动与定位
				// -------------------------------------------------------------
				PacketHandlerRef.ObjectMoved += OnObjectMoved; // 实现在你现有分部类中
				PacketHandlerRef.ObjectHeadingChanged += OnObjectHeadingChanged;
				// -------------------------------------------------------------
				// [BLOCK END] 移动与定位
				// -------------------------------------------------------------
				// -------------------------------------------------------------
				// [NEW] 地图同步信号绑定
				// -------------------------------------------------------------
				PacketHandlerRef.MapChanged += OnMapChanged;
				// -------------------------------------------------------------


				// -------------------------------------------------------------
				// [BLOCK] 战斗与状态
				// -------------------------------------------------------------
				PacketHandlerRef.ObjectAttacked += OnObjectAttacked;
				PacketHandlerRef.Connect(PacketHandler.SignalName.ObjectMagicDamage35, Callable.From<int, int, int>(OnObjectMagicDamage35));

				PacketHandlerRef.ObjectHitRatio += OnObjectHitRatio;
				PacketHandlerRef.ObjectAction += OnObjectAction;
				PacketHandlerRef.ObjectEffect += OnObjectEffect;
				PacketHandlerRef.ObjectRestore += OnObjectRestore;
				// -------------------------------------------------------------
                // 【核心修复】绑定远程攻击信号 (光箭/弓箭)
                PacketHandlerRef.ObjectRangeAttacked += OnRangeAttackReceived;

                // 魔法攻击 (Opcode 57)：視覺由 OnMagicVisualsReceived 處理，傷害由 OnObjectMagicAttacked 立即結算（封包結算，不經 keyframe）
                PacketHandlerRef.ObjectMagicAttacked += (atk, tgt, gfx, dmg, x, y) => OnMagicVisualsReceived(atk, tgt, gfx, dmg, x, y);
                PacketHandlerRef.ObjectMagicAttacked += OnObjectMagicAttacked;

				// 地面特效 (Opcode 83 - S_OPCODE_EFFECTLOC)
				PacketHandlerRef.EffectAtLocation += OnEffectAtLocation;

                // -------------------------------------------------------------
                // [核心修复] 绑定外观模式信号 (Opcode 29)
                // -------------------------------------------------------------
                PacketHandlerRef.ObjectVisualModeChanged += OnObjectVisualModeChanged;

                // -------------------------------------------------------------
                // [隱身術] 綁定 S_ObjectInvis (Opcode 52)：自己半透明、他人完全不可見
                // -------------------------------------------------------------
                PacketHandlerRef.ObjectInvisReceived += OnObjectInvisReceived;

                // -------------------------------------------------------------
                // [變身] 綁定變身/外觀更新信號 (Opcode 39 - S_ObjectPoly)
                // -------------------------------------------------------------
                PacketHandlerRef.ObjectVisualUpdated += OnObjectVisualUpdated;

				// -------------------------------------------------------------
				// [BLOCK] 属性与 UI 同步
				// -------------------------------------------------------------
				PacketHandlerRef.HPUpdated += OnHPUpdated;
				PacketHandlerRef.MPUpdated += OnMPUpdated;
				PacketHandlerRef.ObjectChat += OnObjectChat;

				// Op5 / Op12 分流
				PacketHandlerRef.CharacterInfoReceived += OnCharacterInfoReceived;
				PacketHandlerRef.CharacterStatsUpdated += OnCharacterStatsUpdated;
				
				// 【新增】邪惡值和狀態更新
				PacketHandlerRef.ObjectLawfulChanged += OnObjectLawfulChanged;
				PacketHandlerRef.ObjectPoisonReceived += OnObjectPoisonReceived;
				PacketHandlerRef.ObjectPinkNameReceived += OnObjectPinkNameReceived;
				// -------------------------------------------------------------
				// [BLOCK END] 属性与 UI 同步
				// -------------------------------------------------------------
				// 召喚/寵物狀態：登記為己方召喚，供點擊開 TalkWindow(moncom) 與 Z 尋怪排除
				PacketHandlerRef.PetStatusChanged += OnPetStatusChanged;

				// S_MessageYN (155) Yes/No 選項：顯示 UI 並回傳 C_Attr(61)
				PacketHandlerRef.YesNoRequestReceived += OnYesNoRequestReceived;

				// S_Bookmarks (11) 記憶座標：快取供選單「記憶座標」顯示
				PacketHandlerRef.BookmarkReceived += OnBookmarkReceived;

				// S_Teleport (4)、S_IdentifyDesc (43)、S_253 (InputAmount)、S_81 (ChangeName)
				PacketHandlerRef.TeleportReceived += OnTeleportReceived;
				PacketHandlerRef.IdentifyDescReceived += OnIdentifyDescReceived;
				PacketHandlerRef.InputAmountRequested += OnInputAmountRequested;
				PacketHandlerRef.ObjectNameChanged += OnObjectNameChanged;

				// -------------------------------------------------------------
				// [BLOCK] 子系统信号绑定（由各分部类提供实现）
				// -------------------------------------------------------------
				BindInventorySignals();

				BindSkillSignals();

				BindNpcSignals();

				// buff（保持你原逻辑：重复调用 BindNpcSignals 不做改动）
				BindBuffSignals();
				
				// 寵物系統
				BindPetSignals();

				// 【藥水/技能音效】S_OPCODE_SOUND(84)：伺服器請求播放音效（綠水等）；實現在 GameWorld.cs OnServerSoundReceived
				PacketHandlerRef.Connect(PacketHandler.SignalName.ServerSoundReceived, Callable.From<int>(OnServerSoundReceived));
				// -------------------------------------------------------------
				// [BLOCK END] 子系统信号绑定
				// -------------------------------------------------------------
			}
		}
		// =====================================================================
		// [SECTION END] PacketHandler Signal Bindings
		// =====================================================================
	}
}
