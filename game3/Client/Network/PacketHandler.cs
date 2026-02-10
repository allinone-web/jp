// ============================================================================
// [SECTION] Imports / Using directives
// 说明：本区仅包含命名空间引用；不包含任何运行逻辑。
// ============================================================================
// jan 28.7pm
// ============================================================================
// [SECTION END] Imports / Using directives
// ============================================================================

/*
动作 (Action)	你发送 (Client Send)	你接收 (Client Receive)
攻击 (Attack)	23 (对应 C_Attack)	35 (对应 S_ObjectAttack)
移动 (Move)	10 (对应 C_Moving)	18 (对应 S_ObjectMoving)
拾取 (Pickup)	11 (对应 C_ItemPickup)	22 (S_InventoryAdd) 或 26 (S_ItemCount)
使用物品 (Use)	28 (对应 C_ItemClick)	34 (S_SkillHaste) 或 14 (S_InventoryBress) 等
装备列表下发（S_InventoryList，opcode 65）
装备/卸下触发（S_InventoryEquipped，opcode 24）


武器外观切换所需的权威数据（GfxMode）解析了该数据包（Opcode 29


数据库里的 "sword", "bow" 等字符串，被转换成了数字，并存入了 Item 对象的 type 属性中（通过 setType 方法）。所以，获取这个值的正确方法名就是 getType()。

数值映射表（第 55-75 行）： 这是服务器内部定义的绝对真理，请务必记录下来，客户端必须适配这个：

"arrow" -> 1

"axe" (斧) -> 2

"bow" (弓) -> 3 <-- 注意！服务器里弓是 3，不是 4！

"spear" (矛) -> 4 <-- 注意！服务器里矛是 4，不是 5！

"sword" (剑) -> 5 <-- 注意！服务器里剑是 5，不是 1！

"wand" (杖) -> 6

"twohand" (双手剑) -> 24

*/

// ============================================================================
// [SECTION] PacketHandler.cs - Network Packet Dispatch & Parsers
// 目标：仅通过注释对代码进行分段说明，不修改任何功能逻辑。
// 规则：仅允许增删注释；不得改动任何可执行代码行。
// ============================================================================
//
//. 创建角色流程：先发67询问，必须带type，必须使用已经写好的helper类。再发12传输具体数据.不是112.
//

using Godot;
using System;
using System.Text;
using Client.Data; 
using Client.Utility; // 引用 Utility 以使用 DescTable
using System.Collections.Generic; // 【必须添加这一行到文件最顶部】
using Client.UI; // ✅ 必须添加此行，解决 UIManager 和 WindowID 找不到的问题
using Client.Game; // [核心修復] 確保能識別 GameEntity 類別


namespace Client.Network
{
	public partial class PacketHandler : Node
	{
		// [新增] 用于跨包存储角色数量 (从 Opcode 3 获取，供 Opcode 4 使用)
		private int _cachedCharCount = 0;

		/// <summary>最後一次收到的遊戲世界時間（秒），供 DayNightOverlay 就緒時補發，避免遮罩晚於封包建立而沒收到。</summary>
		private int _lastWorldTimeSeconds = -1;
		public int LastWorldTimeSeconds => _lastWorldTimeSeconds;

		/// <summary>上一包 Op35 是否為單體魔法（magicFlag==6），供 OnObjectAttacked 決定是否做 PrepareAttack。</summary>
		private bool _lastOp35WasMagic = false;
		public bool LastOp35WasMagic => _lastOp35WasMagic;

		// ====================================================================
		// [核心修复] 必须补充缓存字典定义，否则 ParseObjectMoving 会报错
		// ====================================================================
		// [FIX] 去重缓存：避免 S_ObjectMoving / S_ObjectHeading 重复落地导致的抖动与日志刷屏
		private readonly Dictionary<int, int> _lastHeadingByObjectId = new();
		private readonly Dictionary<int, (int X, int Y, int Heading)> _lastMoveByObjectId = new();
		// [核心修復] 補全缺失的實體列表字典宣告，解決 CS0103 錯誤
		private Dictionary<int, GameEntity> _entities = new();
		// ====================================================================
		// [核心修复] C# 事件定义 (供 Boot.cs 订阅)
		// 必须添加这几行，否则 Boot.cs 会报错 CS1061
		// ====================================================================
		public event Action<int> OnLoginCharacterCount;
		public event Action<CharacterInfo> OnLoginCharacterItem;
		public event Action OnLoginSuccess;     // 替换旧的 Signal 逻辑，或者两者并存
		public event Action<string> OnLoginFailed;
		// ====================================================================
		// [SECTION] Godot Signals (事件信号定义)
		// 说明：所有信号仅用于向外部(Boot/GameWorld/UI)广播解析结果。
		// ====================================================================

		// --- 信号定义 (保持不变) ---
		[Signal] public delegate void ServerVersionReceivedEventHandler();
		// 登录相关
		[Signal] public delegate void LoginSuccessEventHandler();
		[Signal] public delegate void LoginResultEventHandler(bool success, string msg);
		[Signal] public delegate void LoginFailedEventHandler(string message);
		
		// 角色相关
		// 注意：这个旧的 Array 信号可以保留，也可以不因，但我们主要用上面的 Event
		[Signal] public delegate void CharacterListReceivedEventHandler(Godot.Collections.Array<Client.Data.CharacterInfo> list);
		[Signal] public delegate void CharacterInfoReceivedEventHandler(CharacterInfo charInfo);
		[Signal] public delegate void CreateCharacterFailEventHandler(int reason);
		
		// 完整属性更新信号 (解决 C 面板属性为 0 的问题)
		[Signal] public delegate void CharacterStatsUpdatedEventHandler(CharacterInfo info);

		// 世界对象与战斗
		[Signal] public delegate void ObjectSpawnedEventHandler(WorldObject obj);
		[Signal] public delegate void ObjectMovedEventHandler(int id, int x, int y, int heading);
		
		// 战斗与状态。Op35 一律發 ObjectAttacked(attackerId, targetId, actionId, damage)；近戰/弓箭/魔法共用此路徑；action 為動畫動作 ID（伺服器 S_ObjectAttack 為 getGfxMode()+1，S_ObjectAttackMagic 單體為 17/18/19）
		[Signal] public delegate void ObjectAttackedEventHandler(int attackerId, int targetId, int actionId, int damage);

		// Op35 單體魔法傷害：依封包 writeC(6)（magicFlag==6）判定，與 actionId 無關；變身時 action 可能非 17/18/19 仍為魔法
		[Signal] public delegate void ObjectMagicDamage35EventHandler(int attackerId, int targetId, int damage);

		// 远程/弓箭射击信号 (支持光箭/弓箭飞行) (Attacker, Target, GfxId, StartX, StartY, DestX, DestY)
		// 参数: 攻击者, 目标, 特效ID, 起点X, 起点Y, 终点X, 终点Y
		[Signal] public delegate void ObjectRangeAttackedEventHandler(int attackerId, int targetId, int gfxId, int sx, int sy, int dx, int dy);

		// 【修复】魔法攻击信号：增加 x, y 参数，用于支持地面魔法/无目标魔法定位
		[Signal] public delegate void ObjectMagicAttackedEventHandler(int attackerId, int targetId, int gfxId, int damage, int x, int y);

		// 血条更新
		[Signal] public delegate void ObjectHitRatioEventHandler(int objectId, int ratio);
	
		[Signal] public delegate void ObjectDeletedEventHandler(int objectId);
		// 【新增】朝向改变 (Opcode 28)
		[Signal] public delegate void ObjectHeadingChangedEventHandler(int objectId, int newHeading);
		
		// 【新增】对象特效 (Opcode 55)
		[Signal] public delegate void ObjectEffectEventHandler(int objectId, int effectId);
		
		// 【新增】復活 (Opcode 17)
		[Signal] public delegate void ObjectRestoreEventHandler(int objectId, int gfxMode, int reviverId, int gfx);
		
		// 【新增】对象血量比例更新信号 (0-100)
		[Signal] public delegate void ObjectActionEventHandler(int objectId, int actionId);
		
		// 【新增】寵物狀態變更 (Opcode 79) - [FIX] 這是修復 CS0117 錯誤的關鍵！
		[Signal] public delegate void PetStatusChangedEventHandler(int petId, int status);
		
		// 【新增】寵物系統信號
		[Signal] public delegate void PetWarehouseListReceivedEventHandler(int npcId, Godot.Collections.Array items);
		[Signal] public delegate void PetPanelReceivedEventHandler(int petId, string panelType, Godot.Collections.Dictionary petData);

		// 【新增信号】用于视觉状态更新
		[Signal] public delegate void ObjectLightChangedEventHandler(int objectId, int lightValue);
		[Signal] public delegate void ObjectVisualModeChangedEventHandler(int objectId, int mode);

		// 聊天与系统
		[Signal] public delegate void ObjectChatEventHandler(int objectId, string text, int type);
		[Signal] public delegate void SystemMessageEventHandler(string message); 
		
		// 属性与背包
		[Signal] public delegate void HPUpdatedEventHandler(int current, int max);
		[Signal] public delegate void MPUpdatedEventHandler(int current, int max);
		
		// 背包相关
		[Signal] public delegate void InventoryListReceivedEventHandler(Godot.Collections.Array<InventoryItem> items);
		[Signal] public delegate void InventoryItemAddedEventHandler(InventoryItem item);
		[Signal] public delegate void InventoryItemUpdatedEventHandler(int objectId, string name, int count, int status, string detailInfo);
		[Signal] public delegate void InventoryItemNameUpdatedEventHandler(int objectId, string name, bool isEquipped);
		[Signal] public delegate void ItemEquipStatusChangedEventHandler(int objectId, bool isEquipped);
		[Signal] public delegate void InventoryItemDeletedEventHandler(int objectId);
		
		// Skill Signals
		[Signal] public delegate void SkillAddedEventHandler(int type, int[] masks);
		[Signal] public delegate void SkillDeletedEventHandler(int[] masks);
		[Signal] public delegate void SkillBuyListReceivedEventHandler(int npcId, int[] skillIds);
	
		// [新增] 服務端通知播放音效 (Opcode 74)
		[Signal] public delegate void ServerSoundReceivedEventHandler(int soundId);

		// 【新增】位置型魔法特效 (Opcode 83 - S_OPCODE_EFFECTLOC)
		// 用於只給一個地面座標+特效ID，而沒有具體對象ID的情況
		[Signal] public delegate void EffectAtLocationEventHandler(int gfxId, int x, int y);

		// npc-shop， 1. 信号定义 (必须放在类的一级层级，不要放在方法里)
		[Signal] public delegate void ShowHtmlReceivedEventHandler(int npcId, string htmlId, string[] args);
		[Signal] public delegate void ShopBuyOpenEventHandler(int npcId, Godot.Collections.Array items);
		[Signal] public delegate void ShopSellOpenEventHandler(int npcId, Godot.Collections.Array items);


		// S_ObjectInvis (Opcode 52) - 隱身術：objectId + ck(1=隱身, 0=現形)
		[Signal] public delegate void ObjectInvisReceivedEventHandler(int objectId, bool invis);

		//buff
		[Signal] public delegate void BuffSpeedReceivedEventHandler(int entityId, int type, int speed, int time);
		[Signal] public delegate void BuffAquaReceivedEventHandler(int entityId, int time);
		[Signal] public delegate void BuffShieldReceivedEventHandler(int time, int type); // Shield 包里没有 objId，只针对自己
		[Signal] public delegate void BuffBlindReceivedEventHandler(int type); // 1=Blind, 0=Off
		[Signal] public delegate void ObjectPoisonReceivedEventHandler(int objectId, bool isPoison); // 【新增】中毒狀態 (Opcode 50)
		[Signal] public delegate void ObjectPinkNameReceivedEventHandler(int objectId, int duration); // 【新增】紫名狀態 (Opcode 106)

		// map-瞬移
		[Signal] public delegate void MapChangedEventHandler(int mapId);

		// 遊戲世界時間 (Opcode 33 S_WorldStatPacket / Opcode 12 內 writeD(WORLDTIME))，用於白天黑夜效果
		[Signal] public delegate void GameTimeReceivedEventHandler(int worldTimeSeconds);

		//变身，正义值更新
		[Signal] public delegate void ObjectVisualUpdatedEventHandler(int objectId, int gfxId, int actionId, int heading);
		[Signal] public delegate void ObjectLawfulChangedEventHandler(int objectId, int lawful);

		// warehouse
		[Signal] public delegate void WarehouseListReceivedEventHandler(int npcId, Godot.Collections.Array items, int option);

		// 【JP】S_MessageYN (155) Yes/No 選項；UI 顯示後可發 C_Attr 61 回傳選擇
		[Signal] public delegate void YesNoRequestReceivedEventHandler(int type, int yesNoCount, string msg1, string msg2, string msg3);

		// 【JP】S_Bookmarks (11) 單條記憶座標；可累積後在選單「記憶座標」顯示，點擊發 C_41 傳送
		[Signal] public delegate void BookmarkReceivedEventHandler(string name, int mapId, int id, int x, int y);
		// 【JP】S_ItemColor (144) 物品祝福/詛咒狀態
		[Signal] public delegate void ItemColorReceivedEventHandler(int objectId, int status);
		// 【JP】S_SelectList (208) 損壞武器/寵物列表
		[Signal] public delegate void SelectListReceivedEventHandler(int price, Godot.Collections.Array items);
		// 【JP】S_OwnCharStatus2 (216) 角色狀態2
		[Signal] public delegate void OwnCharStatus2ReceivedEventHandler(int str, int int_, int wis, int dex, int con, int cha, int weight240);

		// 【JP】S_OPCODE_TELEPORT = 4 — 傳送動畫
		[Signal] public delegate void TeleportReceivedEventHandler(int objectId);
		// 【JP】S_OPCODE_IDENTIFYDESC = 43 — 鑑定描述
		[Signal] public delegate void IdentifyDescReceivedEventHandler(int descId, string message);
		// 【JP】S_OPCODE_INPUTAMOUNT = 253 — 數量輸入請求
		[Signal] public delegate void InputAmountRequestedEventHandler(int objectId, int max, string htmlId);
		// 【JP】S_OPCODE_CHANGENAME = 81 — 物件名稱變更
		[Signal] public delegate void ObjectNameChangedEventHandler(int objectId, string name);

		// ====================================================================
		// [SECTION END] Godot Signals (事件信号定义)
		// ====================================================================

		// ====================================================================
		// [SECTION] Packet Dispatch Entry
		// 说明：HandlePacket 是网络收包入口，按 opcode 分发到各解析函数。
		// ====================================================================

		public void HandlePacket(byte[] data)
		{
			if (data == null || data.Length == 0) return;
			using var reader = new PacketReader(data);
			int opcode = reader.ReadByte();

			switch (opcode)
			{

			// ------------------------------------------------------------
			// [GROUP] Handshake / ServerVersion
			// ------------------------------------------------------------
			// 【JP協議對齊】S_OPCODE_SERVERVERSION = 151
			// 對齊 jp S_ServerVersion.java 結構：
			// writeC(151), writeC(0x00), writeC(0x01), writeD(SERVER_VERSION), 
			// writeD(CACHE_VERSION), writeD(AUTH_VERSION), writeD(NPC_VERSION),
			// writeD(0x0), writeC(0x00), writeC(0x00), writeC(CLIENT_LANGUAGE),
			// writeD(SERVER_TYPE), writeD(UPTIME), writeH(0x01)
			case 151: // S_OPCODE_SERVERVERSION (jp)
				GD.Print($"[RX] Handshake OK (Op: {opcode})");
				// 讀取服務器版本信息（客戶端可能不需要全部，但必須讀取以對齊）
				reader.ReadByte(); // 0x00
				reader.ReadByte(); // 0x01 (第幾個伺服器)
				reader.ReadInt();  // SERVER_VERSION
				reader.ReadInt();  // CACHE_VERSION
				reader.ReadInt();  // AUTH_VERSION
				reader.ReadInt();  // NPC_VERSION
				reader.ReadInt();  // server start time
				reader.ReadByte(); // 0x00
				reader.ReadByte(); // 0x00
				reader.ReadByte(); // CLIENT_LANGUAGE
				reader.ReadInt();  // SERVER_TYPE
				reader.ReadInt();  // UPTIME
				reader.ReadUShort(); // 0x01
				EmitSignal(SignalName.ServerVersionReceived);
				break;

			// ------------------------------------------------------------
			// [GROUP] Disconnect
			// ------------------------------------------------------------
			// 【JP協議對齊】S_OPCODE_DISCONNECT = 95
			// 伺服器格式：
			// - writeH(content) + writeD(0) 或
			// - writeC(id) + writeD(0)
			case 95: // S_OPCODE_DISCONNECT (jp)
				int remain = reader.Remaining;
				int code = 0;
				if (remain >= 2)
				{
					code = reader.ReadUShort();
				}
				else if (remain >= 1)
				{
					code = reader.ReadByte();
				}
				if (reader.Remaining >= 4)
				{
					reader.ReadInt(); // padding
				}
				GD.PrintErr($"[RX] Server Disconnect (Op: {opcode}) Code={code}");
				EmitSignal(SignalName.SystemMessage, $"Server Disconnected (Code: {code})");
				break;

			// 【JP】S_OPCODE_BOOKMARKS = 11 — 單條記憶座標 (name, map, id, x, y)
			case 11:
				try
				{
					string bName = reader.ReadString();
					int bMap = reader.ReadUShort();
					int bId = reader.ReadInt();
					int bX = reader.ReadUShort();
					int bY = reader.ReadUShort();
					EmitSignal(SignalName.BookmarkReceived, bName ?? "", bMap, bId, bX, bY);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 11 parse: {e.Message}"); }
				break;

			// 【JP】S_OPCODE_ITEMCOLOR = 144 — 物品祝福/詛咒
			case 144:
				try
				{
					int itemId = reader.ReadInt();
					int status = reader.ReadByte();
					EmitSignal(SignalName.ItemColorReceived, itemId, status);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 144 parse: {e.Message}"); }
				break;

			// 【JP】S_OPCODE_PARALYSIS = 165 — 麻痺/睡魔/凍結/暈眩等
			case 165:
				try { while (reader.Remaining > 0) reader.ReadByte(); } catch { }
				break;

			// 【JP】S_OPCODE_SELECTLIST = 208 — 損壞武器列表 (維修用)
			case 208:
				try
				{
					int price = reader.ReadInt();
					int cnt = reader.ReadUShort();
					var items = new Godot.Collections.Array();
					for (int i = 0; i < cnt; i++)
					{
						var ent = new Godot.Collections.Dictionary();
						ent["itemId"] = reader.ReadInt();
						ent["durability"] = reader.ReadByte();
						items.Add(ent);
					}
					EmitSignal(SignalName.SelectListReceived, price, items);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 208 parse: {e.Message}"); }
				break;

			// 【JP】S_OPCODE_OWNCHARSTATUS2 = 216 — 角色狀態2 (六圍+負重)
			case 216:
				try
				{
					int str = reader.ReadByte();
					int int_ = reader.ReadByte();
					int wis = reader.ReadByte();
					int dex = reader.ReadByte();
					int con = reader.ReadByte();
					int cha = reader.ReadByte();
					int weight240 = reader.ReadByte();
					EmitSignal(SignalName.OwnCharStatus2Received, str, int_, wis, dex, con, cha, weight240);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 216 parse: {e.Message}"); }
				break;

			// 【JP】S_OPCODE_INITPACKET = 161 — 初始化（握手後）；僅解析，不修改加密
			case 161:
				try { while (reader.Remaining > 0) reader.ReadByte(); } catch { }
				break;

			// ------------------------------------------------------------
			// [GROUP] Login Flow (Fail / Success / Character list & info)
			// ------------------------------------------------------------
			// 【JP協議對齊】S_OPCODE_LOGINRESULT = 51
			// 對齊 jp S_LoginResult.java 結構：
			// writeC(51), writeC(reason), writeD(0x00000000) x 3
			// REASON_LOGIN_OK = 0x00, REASON_USER_OR_PASS_WRONG = 0x08
			case 51: // S_OPCODE_LOGINRESULT (jp)
				int reasonCode = reader.ReadByte();
				reader.ReadInt(); // 0x00000000
				reader.ReadInt(); // 0x00000000
				reader.ReadInt(); // 0x00000000
				
				string failMsg = $"Login Failed Code: {reasonCode}";
				
				// 翻譯常見錯誤碼（對齊 jp S_LoginResult.java）
				if (reasonCode == 0x00) 
				{
					// REASON_LOGIN_OK - 登錄成功，但這裡不應該觸發失敗
					GD.Print($"[PacketHandler] Login OK (Code: {reasonCode})");
					// 【JP協議對齊】jp 伺服器需要先收到 C_OPCODE_COMMONCLICK(53) 才會下發角色數量/列表
					// 登錄成功由 S_OPCODE_CHARAMOUNT 處理
					try
					{
						if (Client.Boot.Instance != null)
						{
							Client.Boot.Instance.Action_CommonClick();
						}
					}
					catch (Exception e)
					{
						GD.PrintErr($"[PacketHandler] Action_CommonClick failed: {e.Message}");
					}
					break;
				}
				if (reasonCode == 0x08) failMsg = "User or password wrong"; // REASON_USER_OR_PASS_WRONG
				if (reasonCode == 0x16) failMsg = "Account already in use"; // REASON_ACCOUNT_IN_USE
				
				GD.Print($"[PacketHandler] {failMsg}");
				
				// [重點] 必須發送 LoginFailed，因為 Boot.cs 監聽的是這個！
				OnLoginFailed?.Invoke(failMsg);
				EmitSignal(SignalName.LoginFailed, failMsg); 
				break;

			// ----------------------------------------------------------------
			// 【JP協議對齊】Opcode 126: 登錄成功，獲取並緩存角色數量
			// 對齊 jp S_CharAmount.java 結構：
			// writeC(126), writeC(value), writeC(maxAmount)
			// ----------------------------------------------------------------
			case 126: // S_OPCODE_CHARAMOUNT (jp) - 登錄成功
				_cachedCharCount = reader.ReadByte(); // 讀取並緩存數量！
				int maxSlots = reader.ReadByte(); // 最大角色槽位數
				GD.PrintRich($"[b][color=green][RX] Login Success! Count: {_cachedCharCount} MaxSlots: {maxSlots}[/color][/b]");
				
				// 【關鍵修復】通知 Boot 準備接收多少個角色
				OnLoginCharacterCount?.Invoke(_cachedCharCount);
				// 通知 Boot 登錄成功
				OnLoginSuccess?.Invoke();
				EmitSignal(SignalName.LoginSuccess); 
				break;
			
			// ----------------------------------------------------------------
			// 【JP協議對齊】Opcode 153: 創角狀態 (S_CharCreateStatus)
			// 對齊 jp S_CharCreateStatus.java 結構：
			// writeC(153), writeC(reason), writeD(0x00000000), writeD(0x0000)
			// ----------------------------------------------------------------
			case 153: // S_OPCODE_NEWCHARWRONG (jp)
				try
				{
					int reason = reader.ReadByte();
					reader.ReadInt();
					reader.ReadInt();

					if (reason == 0x02)
					{
						GD.Print("[PacketHandler] CreateChar OK (Code: 2)");
						break;
					}

					string createFailMsg = $"CreateChar Failed Code: {reason}";
					if (reason == 0x06) createFailMsg = "CreateChar Failed: name already exists";
					if (reason == 0x09) createFailMsg = "CreateChar Failed: invalid name";
					if (reason == 0x15) createFailMsg = "CreateChar Failed: wrong status amount";

					GD.Print($"[PacketHandler] {createFailMsg}");
					OnLoginFailed?.Invoke(createFailMsg);
					EmitSignal(SignalName.LoginFailed, createFailMsg);
				}
				catch (Exception e)
				{
					GD.PrintErr($"[PacketHandler] S_CharCreateStatus parse failed: {e.Message}");
				}
				break;

			// 【JP協議對齊】登錄停留在角色列表
			// 所有角色的簡要信息（包括HP/MP）
			// S_OPCODE_CHARLIST = 184
			// 對齊 jp S_CharPacks.java 結構
			case 184: // S_OPCODE_CHARLIST (jp) - 單個或多個角色數據
				ParseCharList(reader);
				break;
					
			// 【JP協議對齊】jp 服務器進入遊戲後不再發送單獨的 S_CharacterInfo 封包
			// 角色信息已包含在 S_OwnCharPack (3) 和 S_OwnCharStatus (145) 中
			// case 5 已移除，不再使用

			// 【JP協議對齊】S_OPCODE_LOGINTOGAME = 131
			// jp S_LoginGame.java: writeC(131), writeC(0x03), writeC(0x15), ... 共 7 字節
			// 部分伺服器改為其他內容（如 4 字節 "oooo" + 3 字節 0），故依 Remaining 全部消耗，避免粘包
			case 131: // S_OPCODE_LOGINTOGAME (jp) - 進入世界成功前之 Cookie
				while (reader.Remaining > 0)
					reader.ReadByte();
				GD.Print("[RX] Opcode 131 (S_LoginGame)");
				break;
			
			// ------------------------------------------------------------
			// [GROUP] Protocol: PacketBox / CharReset / Weather / SpMr / Title
			// ------------------------------------------------------------
			// 【JP協議對齊】S_OPCODE_CHARRESET = 33 / S_OPCODE_PETCTRL = 33
			case 33:
				ParseOpcode33(reader);
				break;

			// 【JP協議對齊】S_OPCODE_PACKETBOX = 40
			case 40:
				ParsePacketBox(reader);
				break;

			// 【JP協議對齊】S_OPCODE_SPMR = 174
			case 174:
				ParseSpMr(reader);
				break;

			// 【JP協議對齊】S_OPCODE_WEATHER = 193
			case 193:
				ParseWeather(reader);
				break;

			// 【JP協議對齊】S_OPCODE_ITEMNAME = 195
			// 結構: writeC(195), writeD(itemId), writeS(viewName)
			case 195: // S_ItemName (jp)
			{
				int objectId = reader.ReadInt();
				string rawName = reader.ReadString();
				bool isEquipped = false;
				if (!string.IsNullOrEmpty(rawName))
				{
					// 必須在翻譯前判斷裝備狀態，因為 DescTable 會改寫文字
					isEquipped = rawName.Contains("($") || rawName.Contains("裝備") || rawName.Contains("手中");
				}
				
				string displayName = rawName;
				if (DescTable.Instance != null)
				{
					displayName = DescTable.Instance.ResolveName(rawName);
				}
				
				EmitSignal(SignalName.InventoryItemNameUpdated, objectId, displayName, isEquipped);
				GD.Print($"[RX] ItemName(195): ObjId={objectId} Name={displayName} Equipped={isEquipped}");
				break;
			}

			// 【JP協議對齊】S_OPCODE_CHARTITLE = 202
			case 202:
				ParseCharTitle(reader);
				break;

			// ----------------------------------------------------------------
			// 【JP協議對齊】Opcode 212: 創角成功回傳 (S_NewCharPacket)
			// 對齊 jp S_NewCharPacket.java 結構：
			// writeC(212), writeS(name), writeS(clan), writeC(type), writeC(sex),
			// writeH(lawful), writeH(maxHp), writeH(maxMp), writeC(ac), writeC(level),
			// writeC(str), writeC(dex), writeC(con), writeC(wis), writeC(cha), writeC(int),
			// writeC(isAdmin), writeD(birthday), writeC(code)
			// ----------------------------------------------------------------
			case 212: // S_OPCODE_NEWCHARPACK (jp)
				try
				{
					var c = new Client.Data.CharacterInfo();
					c.Name = reader.ReadString();
					c.ClanName = reader.ReadString();
					c.Type = reader.ReadByte();
					c.Sex = reader.ReadByte();
					c.Lawful = reader.ReadShort();
					c.Hp = reader.ReadShort();
					c.Mp = reader.ReadShort();
					c.Ac = reader.ReadByte();
					c.Level = reader.ReadByte();
					c.Str = reader.ReadByte();
					c.Dex = reader.ReadByte();
					c.Con = reader.ReadByte();
					c.Wis = reader.ReadByte();
					c.Cha = reader.ReadByte();
					c.Int = reader.ReadByte();
					reader.ReadByte(); // isAdmin
					reader.ReadInt();  // birthday
					reader.ReadByte(); // code

					GD.Print($"[RX] NewChar: {c.Name} (Type:{c.Type} Lvl:{c.Level})");
					EmitSignal(SignalName.CharacterInfoReceived, c);
				}
				catch (Exception e)
				{
					GD.PrintErr($"[PacketHandler] S_NewCharPacket parse failed: {e.Message}");
				}
				break;

			// 核心：進入世界成功 (刷出周圍對象/自己)

			// ------------------------------------------------------------
			// [GROUP] Enter World / Object Spawn / Character Status
			// ------------------------------------------------------------
			// 【JP協議對齊】S_OPCODE_CHARPACK = 3
			// 對齊 jp S_OwnCharPack.java 結構（與 182 的 S_ObjectAdd 結構完全不同）
			case 3: // S_OPCODE_CHARPACK (jp) - 角色對象封包
				ParseObjectAdd(reader);
				break;

			// 【JP協議對齊】進入遊戲後角色狀態更新
			// S_OPCODE_OWNCHARSTATUS = 145
			// 對齊 jp S_OwnCharStatus.java 結構
			case 145: // S_OPCODE_OWNCHARSTATUS (jp)
				ParseCharacterStat(reader);                 
				break;
				// ------------------------------------------------------------
				// [GROUP] HP / MP / HitRatio Updates
				// ------------------------------------------------------------

			// 【JP協議對齊】HP/MP 更新
			// 對齊 jp S_HPUpdate.java 和 S_MPUpdate.java 結構
			case 42: // S_OPCODE_HPUPDATE (jp) - writeC(42), writeH(currentHp), writeH(maxHp)
				ParseHPUpdate(reader);
				break;
			case 73: // S_OPCODE_MPUPDATE (jp) - writeC(73), writeH(currentMp), writeH(maxMp)
				ParseMPUpdate(reader);
				break;

				// 【JP協議對齊】Opcode 128: S_OPCODE_HPMETER (怪物血量比例)
			   case 128: // S_OPCODE_HPMETER
					int targetObjId = reader.ReadInt();
					int hpRatio = reader.ReadByte(); // 0-100
					
					// [核心修復] 不再直接訪問 _entities，改為發送信號
					EmitSignal(SignalName.ObjectHitRatio, targetObjId, hpRatio);
					break;
					
			// 【JP協議對齊】交易添加物品 - 對齊 jp S_TradeAddItem.java
			// 結構: writeC(86), writeC(type), writeH(gfxId), writeS(name), writeC(status), writeC(0)
			case 86: // S_OPCODE_TRADEADDITEM (jp)
			{
				int tradeType = reader.ReadByte(); // 0:上段 1:下段
				int gfxId = reader.ReadUShort();
				string itemName = reader.ReadString();
				int status = reader.ReadByte();
				reader.ReadByte(); // padding 0
				GD.Print($"[RX] TradeAddItem (86): Type={tradeType} Gfx={gfxId} Name={itemName}");
				// TODO: 實現交易物品解析和 UI 更新
				break;
			}

				// 【JP協議對齊】S_ObjectLawful (正义值更新)
				// 伺服器: S_OPCODE_LAWFUL = 140
				case 140:
					ParseObjectLawful(reader);
					break;

					// 【JP協議對齊】Opcode 123 - S_War
					// 結構: writeC(123), writeC(type), writeS(clan1), writeS(clan2)
					case 123: // S_OPCODE_WAR (jp)
						ParseWar(reader);
						break;

				// ------------------------------------------------------------
				// [GROUP] Movement / Heading / Delete / Action / Effects
				// ------------------------------------------------------------

			// 【JP協議對齊】移動同步
			// S_OPCODE_MOVEOBJECT = 122
			// 對齊 jp S_MoveCharPacket.java 結構
			case 122: // S_OPCODE_MOVEOBJECT (jp)
				ParseObjectMoving(reader);
				break;
				// 【核心对齐】 Opcode 28: S_ObjectHeading (原地转身)
				// 【JP協議對齊】S_RemoveObject (對象消失) - 對齊 jp S_RemoveObject.java
				// 結構: writeC(185), writeD(obj.getId())
				case 185: // S_OPCODE_REMOVE_OBJECT (jp)
				{
					// 【修复】增加花括号作用域，避免变量 delId 冲突
					int delId = reader.ReadInt();
					// [診斷] 確認刪除封包時序；raw 為封包內 4 字節 objectId（可與伺服器日誌對照，確認是否為伺服器發送）
					GD.Print($"[RX] ObjectDeleted objId={delId} (0x{delId:X8})");
					// [FIX] 移除对象时清理缓存，防止内存泄漏
					_lastHeadingByObjectId.Remove(delId);
					_lastMoveByObjectId.Remove(delId);

					EmitSignal(SignalName.ObjectDeleted, delId);
					break;
				}

				// 【JP協議對齊】對象朝向 - 對齊 jp S_ChangeHeading.java
				// 結構: writeC(199), writeD(id), writeC(heading)
				case 199: // S_OPCODE_CHANGEHEADING (jp)
				{
					// [FIX] 升级 Case 199 的逻辑以防止抖动
					int hObjId = reader.ReadInt();
					int heading = reader.ReadByte();

					// [FIX] 去重：同一朝向重复包不落地，避免 SetHeading recv 刷屏/重复应用
					if (_lastHeadingByObjectId.TryGetValue(hObjId, out var lastH) && lastH == heading)
						break;

					_lastHeadingByObjectId[hObjId] = heading;

					// [FIX] 同步移动缓存的 Heading，避免“移动包携带 heading + 额外 heading 包”两次触发
					if (_lastMoveByObjectId.TryGetValue(hObjId, out var lastMv))
						_lastMoveByObjectId[hObjId] = (lastMv.X, lastMv.Y, heading);

					EmitSignal(SignalName.ObjectHeadingChanged, hObjId, heading); 
					break;
				}

				// ------------------------------------------------------------
				// [GROUP] Inventory (Add / Equip)
				// ------------------------------------------------------------

				// 【JP協議對齊】添加/更新單個物品 (拾取成功後伺服器送 S_AddItem)
				// 對齊 jp S_AddItem.java 結構
				case 63: // S_OPCODE_ADDITEM (jp)
					GD.Print("[RX] Opcode 63 (S_AddItem)");
					ParseInventoryAdd(reader);
					break;
				// 装备状态改变
				case 24: 
					ParseInventoryEquipped(reader);
					break;
				
				// Opcode 23: S_InventoryDelete (删除物品)
				case 23:
				{
					// 【修复】增加花括号作用域，避免变量 delId 冲突
					int delId = reader.ReadInt(); // 读取物品 ObjectId
					EmitSignal(SignalName.InventoryItemDeleted, delId);
					break;
				}
				
				// 【JP協議對齊】S_OPCODE_DELETEINVENTORYITEM = 148
				case 148:
					ParseDeleteInventoryItem(reader);
					break;

				// Opcode 111: S_InventoryStatus (物品狀態更新，含鑑定後名稱/數量/鑑定位；拾取堆疊時伺服器送此包而非 22)
				case 111:
					GD.Print("[RX] Opcode 111 (S_InventoryStatus)");
					ParseInventoryStatus(reader);
					break;
				
				// 【JP協議對齊】S_OPCODE_ITEMSTATUS / S_OPCODE_ITEMAMOUNT = 127
				// 結構: writeC(127), writeD(id), writeS(viewName), writeD(count), writeC(statusLen), [status bytes]
				case 127:
					ParseItemStatusAmount(reader);
					break;

				// ------------------------------------------------------------
				// [GROUP] Skills (Add / Delete / BuyList)
				// ------------------------------------------------------------

			// 【JP協議對齊】技能系統
			// 對齊 jp S_AddSkill.java 和 S_DelSkill.java 結構
			case 48: // S_OPCODE_ADDSKILL (jp) - writeC(48), writeC(header), [28 bytes levels], writeC(0)*5
				ParseSkillAdd(reader);
				break;
			case 18: // S_OPCODE_DELSKILL (jp) - writeC(18), writeC(header), [28 bytes levels], writeD(0), writeD(0)
				ParseSkillDelete(reader);
				break;
				case 78: // S_SkillBuyList (注意：data参数) // 技能商店
					ParseSkillBuyList(reader); // 統一改名為 Parse 開頭，並傳入 reader
					break;
				//skill end


				// ------------------------------------------------------------
				// [GROUP] Sound
				// ------------------------------------------------------------
			// 【JP協議對齊】音效 - 對齊 jp S_Sound.java
			// 結構: writeC(84), writeC(0), writeH(sound)
			case 84: // S_OPCODE_SOUND (jp)
			{
				// 數據結構: [Op:1][Repeat:1][SoundId:2]
					// 日誌數據: 4A 00 91 00 ...
					reader.ReadByte(); // 跳過 Padding (00)
					int soundId = reader.ReadUShort(); // 讀取 SoundID (91 00 -> 145)
					
					// GD.Print($"[Sound] Server requested sound: {soundId}");
					EmitSignal(SignalName.ServerSoundReceived, soundId);
					break;
				}


				// 【JP協議對齊】S_OPCODE_DOACTIONGFX = 218
				// 結構: writeC(218), writeD(objectId), writeC(actionId)
				case 218:
				{
					int actObjId = reader.ReadInt();
					int actId = reader.ReadByte();
					// [診斷] 確認是否收到死亡動作 (actionId=8)、以及任意 ObjectAction 時序
					GD.Print($"[RX] ObjectAction objId={actObjId} actionId={actId}");
					EmitSignal(SignalName.ObjectAction, actObjId, actId);
					break;
				}
					
				

				
			// 【JP協議對齊】Opcode 142 - 物理/遠程攻擊 (S_AttackPacket)
			// 對齊 jp S_AttackPacket.java 結構：
			// writeC(142), writeC(type), writeD(pc.getId()), writeD(objid), writeH(damage), writeC(heading), writeH(0), writeH(0), writeC(0)
			case 142: // S_OPCODE_ATTACKPACKET (jp)
				ParseObjectAttack(reader);
				break;


			// 【JP協議對齊】Opcode 232 - 魔法攻擊/AOE (S_SkillSoundGFX)
			// 對齊 jp S_SkillSoundGFX.java 結構
			case 232: // S_OPCODE_SKILLSOUNDGFX (jp)
				ParseObjectAttackMagic(reader);
				break;

			// 【JP協議對齊】Opcode 16 - 範圍/遠程技能 (S_RangeSkill)
			// jp S_RangeSkill: writeC(16), writeC(actionId), writeD(chaId), writeH(x), writeH(y), writeC(heading), writeD(seq), writeH(spellgfx), writeC(type), writeH(0), writeH(n), [writeD(tid), writeC(0x20)]*n
			case 16:
				try
				{
					int actId = reader.ReadByte();
					int chaId = reader.ReadInt();
					int rx = reader.ReadUShort();
					int ry = reader.ReadUShort();
					reader.ReadByte(); // heading
					reader.ReadInt(); // seq
					int spellGfx = reader.ReadUShort();
					reader.ReadByte(); // type
					reader.ReadUShort(); // 0
					int nTarget = reader.ReadUShort();
					for (int i = 0; i < nTarget && reader.Remaining >= 5; i++) { reader.ReadInt(); reader.ReadByte(); }
					EmitSignal(SignalName.EffectAtLocation, spellGfx, rx, ry);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 16 parse: {e.Message}"); }
				break;

			// 【JP協議對齊】Opcode 110 - 精準目標 (S_TrueTarget)
			// jp: writeC(110), writeD(targetId), writeD(objectId), writeS(message)
			case 110:
				try
				{
					reader.ReadInt(); reader.ReadInt();
					string msg = reader.ReadString();
					EmitSignal(SignalName.SystemMessage, msg);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 110 parse: {e.Message}"); }
				break;

			// 【JP協議對齊】Opcode 155 - Yes/No 選項 (S_MessageYN)
			// jp: writeC(155), writeH(0), writeD(yesNoCount), writeH(type), writeS(msg1) [writeS(msg2)] [writeS(msg3)]
			case 155:
				try
				{
					reader.ReadUShort();
					int yesNoCount = reader.ReadInt();
					int yntype = reader.ReadUShort();
					string m1 = reader.Remaining > 0 ? reader.ReadString() : "";
					string m2 = reader.Remaining > 0 ? reader.ReadString() : "";
					string m3 = reader.Remaining > 0 ? reader.ReadString() : "";
					EmitSignal(SignalName.YesNoRequestReceived, yntype, yesNoCount, m1, m2, m3);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 155 parse: {e.Message}"); }
				break;

			// 【JP協議對齊】Opcode 192 - 血盟推薦 (S_PledgeRecommendation)，結構多變，僅消耗避免粘包
			case 192:
				while (reader.Remaining > 0) try { reader.ReadByte(); } catch { break; }
				break;

			// 【JP協議對齊】Opcode 255 - 密語 (S_ChatPacket type 16)
			// jp: writeC(255), writeS(senderName), writeS(chat)
			case 255:
				try
				{
					string from = reader.ReadString();
					string chat = reader.ReadString();
					EmitSignal(SignalName.ObjectChat, 0, $"[密]{from}: {chat}", 16);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 255 parse: {e.Message}"); }
				break;

				// --------------------------------------------------------------------
				// [GROUP] Ground Effects / Effect Location
				// --------------------------------------------------------------------
				// 【JP協議對齊】S_OPCODE_EFFECTLOCATION = 112（jp 實際發送）
				// 對齊 jp S_EffectLocation.java: writeC(112), writeH(x), writeH(y), writeH(gfxId), writeC(0)
				case 112:
				{
					int fxX = reader.ReadUShort();
					int fxY = reader.ReadUShort();
					int fxGfx = reader.ReadUShort();
					reader.ReadByte();
					EmitSignal(SignalName.EffectAtLocation, fxGfx, fxX, fxY);
					break;
				}
				// 保留 83 相容（非 jp 或舊版）
				case 83:
				{
					int fxX = reader.ReadUShort();
					int fxY = reader.ReadUShort();
					int fxGfx = reader.ReadUShort();
					reader.ReadByte();
					EmitSignal(SignalName.EffectAtLocation, fxGfx, fxX, fxY);
					break;
				}

				// 【JP協議對齊】S_OPCODE_TELEPORT = 4 — 傳送動畫（jp S_Teleport: writeC(4), writeC(0), writeC(0x40), writeD(pcId)）
				case 4:
					try
					{
						reader.ReadByte();
						reader.ReadByte();
						int teleportObjId = reader.ReadInt();
						EmitSignal(SignalName.TeleportReceived, teleportObjId);
					}
					catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 4 parse: {e.Message}"); }
					break;

				// 【JP協議對齊】地圖切換 - 對齊 jp S_MapID.java
				// 結構: writeC(150), writeH(mapid), writeC(isUnderwater), writeC(0), writeH(0), writeC(0), writeD(0)
				case 150: // S_OPCODE_MAPID (jp)
					int mapId = reader.ReadUShort(); // writeH(mapid)
					int isUnderwater = reader.ReadByte(); // writeC(isUnderwater ? 1 : 0)
					reader.ReadByte(); // writeC(0)
					reader.ReadUShort(); // writeH(0)
					reader.ReadByte(); // writeC(0)
					reader.ReadInt(); // writeD(0)
					
					GD.Print($"[Map] Server Switch Map -> ID: {mapId} (Underwater: {isUnderwater})");
					EmitSignal(SignalName.MapChanged, mapId);
					break;


				// npc and. shop

				// ------------------------------------------------------------
				// [GROUP] NPC / HTML / Shop
				// ------------------------------------------------------------

			// 【JP協議對齊】NPC 對話/HTML - 對齊 jp S_ShowHtml.java
			// 結構: writeC(119), writeD(npcId), writeS(html)
			case 119: // S_OPCODE_SHOWHTML (jp)
				HandleShowHtmlOrPetPanel(data);
				break;
				// 【JP協議對齊】jp 商店：170=購買列表(NPC賣給玩家)、254=販賣列表(玩家賣給NPC)
				case 170: // S_OPCODE_SHOWSHOPSELLLIST (jp S_ShopBuyList)
					HandleShopBuyList(data);
					break;
				case 254: // S_OPCODE_SHOWSHOPBUYLIST (jp S_ShopSellList)
					HandleShopSellList(data);
					break;
				// 【JP協議對齊】S_OPCODE_IDENTIFYDESC = 43 — 鑑定描述（writeC(43), writeH(descId), 其餘為 type 依賴；客戶端讀 descId 後消耗剩餘並刷新物品）
				case 43:
					try
					{
						int descId = reader.ReadUShort();
						while (reader.Remaining > 0) reader.ReadByte();
						EmitSignal(SignalName.IdentifyDescReceived, descId, "");
					}
					catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 43 parse: {e.Message}"); }
					break;
				case 44: // 保留相容
					HandleShopSellList(data);
					break;

				// 接收到服务器消息，变身活动类似公告

				// ------------------------------------------------------------
				// [GROUP] Chat & System Messages
				// ------------------------------------------------------------

			// 【JP協議對齊】全局聊天/系統消息 - 對齊 jp S_SystemMessage.java
			// 結構: writeC(10), writeC(0x09), writeS(msg)
			case 10: // S_OPCODE_GLOBALCHAT (jp)
				HandleGlobalChat(data);
				break;



		// 【JP協議對齊】系統消息 - 對齊 jp S_ServerMessage.java
		// 結構: writeC(14), writeH(type), writeC(args.length), [writeS(args)...]
		case 14: // S_OPCODE_SERVERMSG (jp)
			ParseServerMessage(reader); 
			break;

			// 【JP協議對齊】普通聊天 - 對齊 jp S_ChatPacket.java
			// 結構: writeC(76), writeC(type), writeD(objectId), writeS(text), writeH(x), writeH(y)
			case 76: // S_OPCODE_NORMALCHAT (jp)
				ParsePacket19(reader); 
				break;
				// 物品消失/怪物死亡
				// S_ObjectChatting (Opcode 45) - 普通/大喊
				// 【新增】普通聊天
				case 45: ParsePacket45(reader); break; 
				
				// UI_S_GlobalChat (公告)
				case 221:
					reader.ReadByte(); // type (usually 3)
					string globalMsg = reader.ReadString();
					EmitSignal(SignalName.SystemMessage, "[公告] " + globalMsg);
					break;



				// ------------------------------------------------------------
				// [GROUP] Inventory List (Login-time full list)
				// ------------------------------------------------------------

				// 背包列表 (登录时)
			// 【JP協議對齊】背包列表 - 對齊 jp S_InvList.java
			// 結構: writeC(180), writeC(count), [每個物品: writeD(id), writeH(magicCatalystType), writeC(type), writeC(chargeCount), writeH(gfxId), writeC(status), writeD(count), writeC(identified), writeS(name), writeC(statusLen), [status bytes], writeC(10), writeD(0), writeD(0), writeH(0)]
			case 180: // S_OPCODE_INVLIST (jp)
				ParseInventoryList(reader);
				break;

				// buff

				// S_BuffSpeed (Haste/加速)
				// S_BuffSpeed (Brave/勇敢)

				// ------------------------------------------------------------
				// [GROUP] Buffs (Haste/Brave/Aqua/Shield/Blind)
				// ------------------------------------------------------------

			// 【JP協議對齊】技能加速 - 對齊 jp S_SkillHaste.java
			// 結構: writeC(149), writeD(i), writeC(j), writeH(k)
			case 149: // S_OPCODE_SKILLHASTE (jp)
					HandleBuffSpeed(data);
					break;



			// 【JP協議對齊】勇敢藥水 - 對齊 jp S_SkillBrave.java
			// 結構: writeC(200), writeD(i), writeC(j), writeH(k)
			case 200: // S_OPCODE_SKILLBRAVE (jp)
				HandleBuffSpeed(data);
				break;
			
			// 注意：S_BuffAqua 在 jp 服務器中可能使用其他 opcode，或已整合到其他封包
			// 暫時移除 case 119，因為 119 已被 S_OPCODE_SHOWHTML 使用
			// TODO: 確認 jp 服務器中水下呼吸 Buff 的 opcode
			// 【JP協議對齊】護盾 - 對齊 jp S_SkillIconShield.java
			// 結構: writeC(69), writeH(time), writeC(type), writeD(0)
			case 69: // S_OPCODE_SKILLICONSHIELD (jp)
				HandleBuffShield(data);
				break;
			// 【JP協議對齊】致盲 - 對齊 jp S_CurseBlind.java
			// 結構: writeC(238), writeH(type) - type 0:OFF 1:自分以外見えない 2:周りのキャラクターが見える
			case 238: // S_OPCODE_CURSEBLIND (jp)
				HandleBuffBlind(data);
				break;

			// 【JP協議對齊】中毒狀態 - 對齊 jp S_Poison.java
			// 結構: writeC(93), writeD(objId), writeC(type1), writeC(type2)
			// type: 0=通常, 1=緑色, 2=灰色
			case 93: // S_OPCODE_POISON (jp)
			{
				int poisonObjId = reader.ReadInt();
				int type1 = reader.ReadByte();
				int type2 = reader.ReadByte();
				bool isPoison = (type1 != 0 || type2 != 0);
				EmitSignal(SignalName.ObjectPoisonReceived, poisonObjId, isPoison);
				GD.Print($"[Buff] 中毒狀態 (93): ObjId={poisonObjId} type1={type1} type2={type2}");
				break;
			}

				// 【核心对齐】 Opcode 17: S_ObjectRestore (復活)
				// Java: writeC(17), writeD(target.getObjectId()), writeC(target.getGfxMode()), writeD(cha==null?target.getObjectId():cha.getObjectId()), writeH(target.getGfx())
				case 17:
				{
					int targetId = reader.ReadInt();
					int restoreGfxMode = reader.ReadByte(); // 【修復】重命名避免與 case 29 中的 gfxMode 衝突
					int reviverId = reader.ReadInt();
					int gfx = reader.ReadUShort();
					GD.Print($"[RX] ObjectRestore objId={targetId} gfxMode={restoreGfxMode} reviverId={reviverId} gfx={gfx}");
					EmitSignal(SignalName.ObjectRestore, targetId, restoreGfxMode, reviverId, gfx);
					break;
				}
				
				// 【JP協議對齊】Opcode 227: S_Resurrection
				// Java: writeC(227), writeD(targetId), writeC(type), writeD(useId), writeD(classId)
				case 227:
				{
					try
					{
						int targetId = reader.ReadInt();
						int type = reader.ReadByte();
						int useId = reader.ReadInt();
						int classId = reader.ReadInt();
						GD.Print($"[RX] Resurrection: target={targetId} type={type} useId={useId} classId={classId}");
						EmitSignal(SignalName.SystemMessage, $"Resurrection received (type={type})");
					}
					catch (Exception e)
					{
						GD.PrintErr($"[RX] Resurrection parse failed: {e.Message}");
					}
					break;
				}

				// 【核心对齐】 Opcode 55: S_ObjectEffect (对象特效)
				// Java: writeD(objId), writeH(effectId)
				case 55: EmitSignal(SignalName.ObjectEffect, reader.ReadInt(), reader.ReadUShort()); 
				break;

			// 【JP協議對齊】隱身術 - 對齊 jp S_OPCODE_INVIS = 57
			// 結構: writeC(57), writeD(id), writeH(ck)
			case 57: // S_OPCODE_INVIS (jp)
			{
				int invisObjId = reader.ReadInt();
					int ck = reader.ReadUShort(); // writeH(ck ? 1 : 0)
					bool invis = (ck != 0);
					EmitSignal(SignalName.ObjectInvisReceived, invisObjId, invis);
					break;
				}

				// 在 switch (opcode) 中添加：
			// 【JP協議對齊】變身 - 對齊 jp S_OPCODE_POLY = 164
			// 結構: writeC(164), writeD(objId), writeH(polyId)
			case 164: // S_OPCODE_POLY (jp)
				ParseObjectPoly(reader);
				break;

				
				// 【JP協議對齊】S_OPCODE_ABILITY = 116（jp 實際發送）
				case 116:
					ParseObjectAbility(reader);
					break;
				case 38: // 保留相容
					ParseObjectAbility(reader);
					break;




				// 在 HandlePacket 的 switch(opcode) 中添加：

				// 【JP協議對齊】遊戲時間 - 對齊 jp S_GameTime.java
			// 結構: writeC(194), writeD(time)
			case 194: // S_OPCODE_GAMETIME (jp)
					{
						int worldTimeSeconds = reader.ReadInt();
						_lastWorldTimeSeconds = worldTimeSeconds;
						EmitSignal(SignalName.GameTimeReceived, worldTimeSeconds);
						break;
					}

		// 【JP協議對齊】倉庫列表 - 對齊 jp S_ShowRetrieveList.java
		// 結構: writeC(250), writeD(npcId), writeH(count), [items...]
		case 250: // S_OPCODE_SHOWRETRIEVELIST (jp)
			ParseWarehouseOrPetWarehouse(reader);
			break;

		// 【JP協議對齊】交易封包 - 對齊 jp S_Trade.java
		// 結構: writeC(77), writeS(name)
		case 77: // S_OPCODE_TRADE (jp)
		{
			string tradePartnerName = reader.ReadString();
			GD.Print($"[RX] Trade (77): Partner={tradePartnerName}");
			// TODO: 實現交易窗口 UI
			break;
		}

		// 【JP協議對齊】交易狀態 - 對齊 jp S_TradeStatus.java
		// 結構: writeC(239), writeC(type) - 0:完成 1:取消
		case 239: // S_OPCODE_TRADESTATUS (jp)
		{
			int tradeStatus = reader.ReadByte();
			GD.Print($"[RX] TradeStatus (239): Status={tradeStatus}");
			// TODO: 實現交易狀態處理
			break;
		}

		// 【jp 對齊】角色視覺/武器更新由 S_OPCODE_CHARVISUALUPDATE (113) 處理；29 在 jp 為 S_OPCODE_CLAN，見下方 case 29。

				// Opcode 27: S_ObjectLight
				// Java: writeC(27), writeD(objId), writeC(light)
				case 27:
						int lightObjId = reader.ReadInt();
						int lightVal = reader.ReadByte();
						// GD.Print($"[RX] Light (27): Obj {lightObjId} -> {lightVal}");
						EmitSignal(SignalName.ObjectLightChanged, lightObjId, lightVal);
						break;

				// 【JP協議對齊】角色視覺更新 - 對齊 jp S_CharVisualUpdate.java
				// 結構: writeC(113), writeD(id), writeC(currentWeapon)
				case 113: // S_OPCODE_CHARVISUALUPDATE (jp)
					int visualObjId = reader.ReadInt();
					int visualGfxMode = reader.ReadByte();
					// jp 版本只有 3 個字段，不需要讀取 padding
					// GD.Print($"[RX] Mode (113): Obj {visualObjId} -> {visualGfxMode}");
					EmitSignal(SignalName.ObjectVisualModeChanged, visualObjId, visualGfxMode);
					break;

				// --------------------------------------------------------------------
				// [新增] 环境与锁定 - 对应 S_Attribute / S_ObjectLock
				// --------------------------------------------------------------------

					// Opcode 34: S_Attribute (通常用于地牢迷雾/门状态)
				// Java: writeC(34), writeH(x), writeH(y), writeC(heading?), writeC(move?)
				case 34:
					reader.ReadUShort(); // X
					reader.ReadUShort(); // Y
					reader.ReadByte();   // Heading Flag
					reader.ReadByte();   // Move Flag
					break;

				// Opcode 37: S_ObjectLock (防外挂检测/客户端锁)
				// Java: writeC(37), writeC(9), ... 固定字节序列
				case 37:
					// S_ObjectLock 构造函数写入了 7 个字节 (除了 Opcode)
					for (int i = 0; i < 7; i++) reader.ReadByte();
					break;



				// --------------------------------------------------------------------
				// 宠物/召唤状态
				// --------------------------------------------------------------------

				case 79: // S_OPCODE_SUMMON_OWN_CHANGE
					ParseSummonStatus(reader);
					break;

				// 【jp 對齊】經驗值由 S_OPCODE_EXP (121) 處理；81 在 jp 為 S_OPCODE_CHANGENAME，見下方 case 81。
				
				// 【JP協議對齊】S_OPCODE_EXP = 121
				// 結構: writeC(121), writeC(level), writeD(exp)
				case 121: // S_OPCODE_EXP (jp)
				{
					int level = reader.ReadByte();
					long expValue = reader.ReadInt();
					UpdateMyExp(expValue);
					UpdateMyLevel(level);
					GD.Print($"[Stats] Exp (121): Lv={level} Exp={expValue}");
					break;
				}

				// 【JP協議對齊】S_OPCODE_PINKNAME = 252（jp 實際發送）
				case 252:
					ParsePinkName(reader);
					break;
				case 106: // 保留相容
					ParsePinkName(reader);
					break;

			// --------------------------------------------------------------------
			// 【JP協議對齊】屬性提升特效 - 對齊 jp S_Strup.java 和 S_Dexup.java
			// --------------------------------------------------------------------

			// 【JP協議對齊】力量提升 - 對齊 jp S_Strup.java
			// 結構: writeC(120), writeH(time), writeC(str), writeC(weight), writeC(type), writeD(0)
			case 120: // S_OPCODE_STRUP (jp)
			{
				reader.ReadUShort(); // time
				int strVal = reader.ReadByte();
				int weight = reader.ReadByte(); // weight240
				int type = reader.ReadByte();
				reader.ReadInt();    // padding 0
				GD.Print($"[RX] Str Up (120): {strVal} Weight={weight} Type={type}");
				// 可在此處發送系統消息或特效信號
				break;
			}

			// 【JP協議對齊】敏捷提升 - 對齊 jp S_Dexup.java
			// 結構: writeC(28), writeH(time), writeC(dex), writeC(type), writeD(0)
			case 28: // S_OPCODE_DEXUP (jp)
			{
				reader.ReadUShort(); // time
				int dexVal = reader.ReadByte();
				reader.ReadByte();   // type
				reader.ReadInt();    // padding 0
				GD.Print($"[RX] Dex Up (28): {dexVal}");
				break;
			}

			// 【JP協議對齊】角色防禦和屬性防禦更新 - 對齊 jp S_OwnCharAttrDef.java
			// 結構: writeC(15), writeC(ac), writeC(fire), writeC(water), writeC(wind), writeC(earth)
			case 15: // S_OPCODE_OWNCHARATTRDEF (jp)
			{
				int ac = reader.ReadByte();
				int fire = reader.ReadByte();
				int water = reader.ReadByte();
				int wind = reader.ReadByte();
				int earth = reader.ReadByte();
				GD.Print($"[RX] AttrDef (15): AC={ac} Fire={fire} Water={water} Wind={wind} Earth={earth}");
				// 可在此處發送信號更新角色面板
				break;
			}

				// Opcode 71: S_OPCODE_CASTLEMASTER
				// 服务器源码: S_WorldStatPacket(int type, int objID)
				//   writeC(71), writeC(type), writeD(objID)
				// 说明：你的日志中该包常见长度为 8（含 opcode）。服务器实现是 1+1+4=6 字节，
				//       但仍可能存在尾部 padding/变体。这里坚持“按服务器字段读取 + 吞掉尾部剩余字节”策略，
				//       保证不影响后续粘包拆包与状态机。
					case 71:
					ParseWorldStatPacket71(reader, data);
					break;


			// 【JP協議對齊】公告視窗 - 對齊 jp S_CommonNews.java
			// 結構: writeC(30), writeS(message)
			case 30: // S_OPCODE_COMMONNEWS (jp)
			{
				string announcement = reader.ReadString();
				GD.Print($"[RX] Common News (30): {announcement}");
				break;
			}

			// 【JP】S_OPCODE_CLAN = 29 — 血盟更新（jp 部分使用 119+html；29 僅消耗避免粘包）
			case 29:
				try { while (reader.Remaining > 0) reader.ReadByte(); } catch { }
				break;

			// 【JP】S_OPCODE_LIGHT = 53 — 物件亮度（jp S_Light: writeC(53), writeD(objid), writeC(type)）
			case 53:
				try
				{
					int objId53 = reader.ReadInt();
					int type53 = reader.ReadByte();
					while (reader.Remaining > 0) reader.ReadByte();
					EmitSignal(SignalName.ObjectLightChanged, objId53, type53);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 53 (S_Light) parse: {e.Message}"); }
				break;

			// 【JP】S_OPCODE_CHANGENAME = 81 — 物件名稱變更（writeC(81), writeD(objectId), writeS(name)）
			case 81:
				try
				{
					int nameObjId = reader.ReadInt();
					string newName = reader.ReadString();
					EmitSignal(SignalName.ObjectNameChanged, nameObjId, newName ?? "");
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 81 parse: {e.Message}"); }
				break;

			// 【JP】S_OPCODE_INPUTAMOUNT = 253 — 數量輸入（jp S_HowManyMake: writeD(objId), writeD(0), writeD(0), writeD(max), writeH(0), writeS("request"), writeS(htmlId)）
			case 253:
				try
				{
					int amountObjId = reader.ReadInt();
					reader.ReadInt();
					reader.ReadInt();
					int maxAmount = reader.ReadInt();
					reader.ReadUShort();
					string reqStr = reader.ReadString();
					string htmlId = reader.ReadString() ?? "";
					EmitSignal(SignalName.InputAmountRequested, amountObjId, maxAmount, htmlId);
				}
				catch (Exception e) { GD.PrintErr($"[PacketHandler] Op 253 parse: {e.Message}"); }
				break;

			default:
				
				// 开启未解析的数据包
				// GD.Print($"[RX] Unhandled Opcode: {opcode}");

				// 开启未解析的数据包
				// [调试日志] 记录所有未能识别或未被当前逻辑拦截的包
				string hexIn = BitConverter.ToString(data).Replace("-", " ");
				GD.PrintRich($"[DEBUG-NET] >>> [收到未知包] Op:{opcode} | Len:{data.Length} | Data: [color=yellow]{hexIn}[/color]");
					break;
			}
		}

		// ====================================================================
		// [SECTION END] Packet Dispatch Entry
		// ====================================================================
			
		// ====================================================================
		// 登陆-注册-创建账户阶段的显示，非常重要，不可修改
		// ====================================================================
		// ====================================================================
		// [SECTION] Login: Character List Packet
		// ====================================================================
		// ====================================================================
		// [核心修复] ParseCharList - 对应 Opcode 4
		// 修复说明：
		// 1. 你的日志显示 Opcode 4 被发送了多次（每次一个角色）。
		// 2. 因此这里【不能循环】，必须只读一个角色。
		// 3. 读完后触发 OnLoginCharacterItem (Event) 通知 Boot 累加。
		// ====================================================================

			
		private void ParseCharList(PacketReader reader)
		{
			// 【JP協議對齊】對齊 jp S_CharPacks.java 結構
			// 1. 在此包中，服務器不發送數量字節，直接發數據！
			// 2. 我們使用在 Opcode 126 中緩存的 _cachedCharCount 作為循環次數。
			// 3. jp 版本比 182 多了 accessLevel 和 birthday 字段
			
			try 
			{
				var c = new Client.Data.CharacterInfo();
				
				// 嚴格按照 jp S_CharPacks.java 的順序讀取
				// writeC(184), writeS(name), writeS(clanName), writeC(type), writeC(sex),
				// writeH(lawful), writeH(hp), writeH(mp), writeC(ac), writeC(lv),
				// writeC(str), writeC(dex), writeC(con), writeC(wis), writeC(cha), writeC(intel),
				// writeC(accessLevel), writeD(birthday), writeC(code)
				c.Name = reader.ReadString();      // writeS
				c.ClanName = reader.ReadString();  // writeS
				c.Type = reader.ReadByte();        // writeC
				c.Sex = reader.ReadByte();         // writeC
				c.Lawful = reader.ReadShort();     // writeH
				c.Hp = reader.ReadShort();         // writeH
				c.Mp = reader.ReadShort();         // writeH
				c.Ac = reader.ReadByte();          // writeC
				c.Level = reader.ReadByte();       // writeC
				c.Str = reader.ReadByte();         // writeC
				c.Dex = reader.ReadByte();         // writeC
				c.Con = reader.ReadByte();         // writeC
				c.Wis = reader.ReadByte();         // writeC
				c.Cha = reader.ReadByte();         // writeC
				c.Int = reader.ReadByte();         // writeC
				
				// 【JP協議新增字段】
				int accessLevel = reader.ReadByte(); // writeC(accessLevel) - 管理員等級
				int birthday = reader.ReadInt();     // writeD(birthday) - 生日
				int code = reader.ReadByte();        // writeC(code) - 驗證碼 (3.53c)
				c.AccessLevel = accessLevel;
				
				GD.Print($"[RX] CharItem: {c.Name} (Type:{c.Type} Lvl:{c.Level} AccessLevel:{accessLevel} Birthday:{birthday})");
				
				// 【關鍵步驟】
				// 解析完這一個角色後，立刻通知 Boot 把它放入列表。
				// Boot.cs 會負責收集所有角色，不需要在這裡 EmitList。
				OnLoginCharacterItem?.Invoke(c);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[PacketHandler] 解析單個角色包失敗: {ex.Message}");
			}
		}


		// ====================================================================
		// [SECTION END] Login: Character List Packet
		// ====================================================================
		// ====================================================================
		// [SECTION] Character: Login-time Info & Runtime Stat Updates
		// ====================================================================

		private void ParseCharacterInfo(PacketReader reader, int opcode)
		{
			// Opcode 5: 登录后初次发送的角色信息
			CharacterInfo info = new CharacterInfo();
			info.Name = reader.ReadString();
			info.ClanName = reader.ReadString();
			info.Type = reader.ReadByte();
			info.Sex = reader.ReadByte();
			info.Lawful = reader.ReadUShort(); 
			info.Hp = reader.ReadUShort();     
			info.Mp = reader.ReadUShort();     
			info.Ac = reader.ReadByte();
			info.Level = reader.ReadByte();
			info.Str = reader.ReadByte();
			info.Dex = reader.ReadByte();
			info.Con = reader.ReadByte();
			info.Wis = reader.ReadByte();
			info.Cha = reader.ReadByte();
			info.Int = reader.ReadByte(); 

			GD.PrintRich($"[b][color=yellow][RX] CharInfo: {info.Name} Str:{info.Str} Dex:{info.Dex}[/color][/b]");
			
			// 发送完整信息，以便 GameWorld 更新 Boot 数据
			EmitSignal(SignalName.CharacterInfoReceived, info);
		}

		private void ParseCharacterStat(PacketReader reader)
		{
			// 【JP協議對齊】Opcode 145: 角色屬性/狀態變化
			// 對齊 jp S_OwnCharStatus.java 結構：
			// writeC(145), writeD(pc.getId()), writeC(level), writeD(exp),
			// writeC(str), writeC(int), writeC(wis), writeC(dex), writeC(con), writeC(cha),
			// writeH(currentHp), writeH(maxHp), writeH(currentMp), writeH(maxMp),
			// writeC(ac), writeD(time), writeC(food), writeC(weight), writeH(lawful),
			// writeC(fire), writeC(water), writeC(wind), writeC(earth), writeD(monsterKill)
			int objectId = reader.ReadInt(); // writeD(pc.getId()) - jp 版本多了這個字段
			
			CharacterInfo info = new CharacterInfo(); // 臨時容器
			
			info.Level = reader.ReadByte();
			info.Exp = reader.ReadInt();
			info.Str = reader.ReadByte();
			info.Int = reader.ReadByte(); 
			info.Wis = reader.ReadByte();
			info.Dex = reader.ReadByte();
			info.Con = reader.ReadByte();
			info.Cha = reader.ReadByte();
			
			int currentHp = reader.ReadUShort();
			int maxHp = reader.ReadUShort();
			int currentMp = reader.ReadUShort();
			int maxMp = reader.ReadUShort();
			
			info.CurrentHP = currentHp;
			info.MaxHP = maxHp;
			info.CurrentMP = currentMp;
			info.MaxMP = maxMp;
			
			int rawAc = reader.ReadByte();
			info.Ac = 266 - rawAc; // 天堂AC計算公式

			int worldTimeSeconds = reader.ReadInt(); // writeD(time)
			_lastWorldTimeSeconds = worldTimeSeconds;
			EmitSignal(SignalName.GameTimeReceived, worldTimeSeconds);

			reader.ReadByte();   // writeC(pc.getFood())
			reader.ReadByte();   // writeC(weight) - jp 使用 getWeight240()
			reader.ReadUShort(); // writeH(pc.getLawful())
			reader.ReadByte();   // writeC(pc.getFire())
			reader.ReadByte();   // writeC(pc.getWater())
			reader.ReadByte();   // writeC(pc.getWind())
			reader.ReadByte();   // writeC(pc.getEarth())
			
			// 【JP協議新增字段】
			int monsterKill = reader.ReadInt(); // writeD(pc.getMonsterKill()) - 3.53C 怪物討伐數

			GD.Print($"[RX] Stats: HP{currentHp}/{maxHp} Str:{info.Str} Dex:{info.Dex} AC:{info.Ac} MonsterKill:{monsterKill}");

			// 1. 發送基礎 HP/MP 信號 (兼容舊邏輯)
			EmitSignal(SignalName.HPUpdated, currentHp, maxHp);
			EmitSignal(SignalName.MPUpdated, currentMp, maxMp);

			// 2. 【關鍵】發送完整屬性更新信號 (解決 C 面板數據 0 的問題)
			EmitSignal(SignalName.CharacterStatsUpdated, info);
		}

		// ------------------------------------------------------------
		// [GROUP] Protocol Parsers (JP aligned)
		// ------------------------------------------------------------
		private void ParseOpcode33(PacketReader reader)
		{
			try
			{
				int sub = reader.ReadByte();
				// 0x0c = S_PetCtrlMenu
				if (sub == 0x0c)
				{
					ParsePetCtrlMenu(reader);
					return;
				}

				// 0x01/0x02/0x03 = S_CharReset variations
				if (sub == 0x01)
				{
					int baseStr = reader.ReadUShort();
					int baseDex = reader.ReadUShort();
					int ac = reader.ReadByte();
					int maxLv = reader.ReadByte();
					GD.Print($"[RX] CharReset(1): BaseStr={baseStr} BaseDex={baseDex} AC={ac} MaxLv={maxLv}");
					return;
				}
				if (sub == 0x02)
				{
					int lv = reader.ReadByte();
					int maxLv = reader.ReadByte();
					int hp = reader.ReadUShort();
					int mp = reader.ReadUShort();
					int ac = reader.ReadUShort();
					int str = reader.ReadByte();
					int intel = reader.ReadByte();
					int wis = reader.ReadByte();
					int dex = reader.ReadByte();
					int con = reader.ReadByte();
					int cha = reader.ReadByte();
					GD.Print($"[RX] CharReset(2): Lv={lv}/{maxLv} HP={hp} MP={mp} AC={ac} Str={str} Int={intel} Wis={wis} Dex={dex} Con={con} Cha={cha}");
					return;
				}
				if (sub == 0x03)
				{
					int point = reader.ReadByte();
					GD.Print($"[RX] CharReset(3): Point={point}");
					return;
				}
				
				// 0x42 = S_EquipmentWindow (裝備狀態更新)
				// 結構: writeC(33), writeC(0x42), writeD(itemObjId), writeC(index), writeC(isEq)
				if (sub == 0x42)
				{
					int objectId = reader.ReadInt();
					int index = reader.ReadByte();
					bool isEquipped = reader.ReadByte() != 0;
					GD.Print($"[RX] EquipWindow(0x42): ObjId={objectId} Index={index} Equipped={isEquipped}");
					EmitSignal(SignalName.ItemEquipStatusChanged, objectId, isEquipped);
					return;
				}

				GD.Print($"[RX] Opcode33 Unknown SubCode: {sub} (Remaining={reader.Remaining})");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParseOpcode33 failed: {e.Message}");
			}
		}

		private void ParsePetCtrlMenu(PacketReader reader)
		{
			try
			{
				int count = reader.ReadUShort();
				int flag = reader.ReadInt();
				int npcId = reader.ReadInt();

				if (reader.Remaining >= 8)
				{
					int mapId = reader.ReadUShort();
					reader.ReadUShort(); // unknown
					int x = reader.ReadUShort();
					int y = reader.ReadUShort();
					string name = reader.ReadString();
					GD.Print($"[RX] PetCtrl(Open): Count={count} Flag={flag} NpcId={npcId} Map={mapId} X={x} Y={y} Name={name}");
				}
				else
				{
					GD.Print($"[RX] PetCtrl(Close): Count={count} Flag={flag} NpcId={npcId}");
				}
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParsePetCtrlMenu failed: {e.Message}");
			}
		}

		private void ParsePacketBox(PacketReader reader)
		{
			try
			{
				int sub = reader.ReadByte();

				switch (sub)
				{
					case 15: // MSG_ELF
					{
						int elfValue = reader.ReadByte();
						reader.ReadByte();
						GD.Print($"[PacketBox] 精靈狀態更新: {elfValue}");
						break;
					}
					case 10: // WEIGHT
					case 11: // FOOD
					{
						int value = reader.ReadByte();
						GD.Print($"[RX] PacketBox({sub}) Value={value}");
						break;
					}
					case 34: // ICON_BLUEPOTION
					case 35: // ICON_POLYMORPH
					case 36: // ICON_CHATBAN
					case 40: // ICON_I2H
					case 153: // MAP_TIMER
					{
						int time = reader.ReadUShort();
						GD.Print($"[RX] PacketBox({sub}) Time={time}");
						break;
					}
					case 46: // LOGINS_OK
					{
						if (reader.Remaining >= 16)
						{
							reader.ReadInt();
							reader.ReadInt();
							reader.ReadInt();
							reader.ReadUShort();
							reader.ReadUShort();
							GD.Print("[PacketBox] 登錄環境數據初始化 A");
						}
						else
						{
							byte[] cookieData = reader.ReadBytes(reader.Remaining);
							GD.Print($"[PacketBox] 登錄環境數據初始化 B (Len:{cookieData.Length})");
						}
						break;
					}
					case 52: // COOK_WINDOW
					{
						reader.ReadByte();
						reader.ReadByte();
						reader.ReadByte();
						reader.ReadByte();
						reader.ReadByte();
						int level = reader.ReadByte();
						GD.Print($"[RX] PacketBox(COOK_WINDOW) Level={level}");
						break;
					}
					case 82: // BLESS_OF_AIN
					{
						int value = reader.ReadInt();
						GD.Print($"[RX] PacketBox(BLESS_OF_AIN) Value={value}");
						break;
					}
					case 88: // DODGE_RATE_PLUS
					{
						int value = reader.ReadByte();
						reader.ReadByte();
						GD.Print($"[RX] PacketBox(DODGE_RATE_PLUS) Value={value}");
						break;
					}
					case 101: // DODGE_RATE_MINUS
					{
						int value = reader.ReadByte();
						GD.Print($"[RX] PacketBox(DODGE_RATE_MINUS) Value={value}");
						break;
					}
					default:
						GD.Print($"[RX] PacketBox SubCode={sub} (Remaining={reader.Remaining})");
						break;
				}
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParsePacketBox failed: {e.Message}");
			}
		}

		private void ParseWar(PacketReader reader)
		{
			try
			{
				int type = reader.ReadByte();
				string clan1 = reader.ReadString();
				string clan2 = reader.ReadString();

				GD.Print($"[RX] War: type={type} clan1={clan1} clan2={clan2}");
				EmitSignal(SignalName.SystemMessage, $"War: type={type} {clan1} {clan2}");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParseWar failed: {e.Message}");
			}
		}

		private void ParseSpMr(PacketReader reader)
		{
			try
			{
				int sp = reader.ReadByte();
				int mr = reader.ReadByte();
				GD.Print($"[RX] SpMr: SP+={sp} MR+={mr}");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParseSpMr failed: {e.Message}");
			}
		}

		private void ParseWeather(PacketReader reader)
		{
			try
			{
				int weather = reader.ReadByte();
				GD.Print($"[RX] Weather: {weather}");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParseWeather failed: {e.Message}");
			}
		}

		private void ParseCharTitle(PacketReader reader)
		{
			try
			{
				int objId = reader.ReadInt();
				string title = reader.ReadString();
				GD.Print($"[RX] CharTitle: ObjId={objId} Title={title}");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParseCharTitle failed: {e.Message}");
			}
		}

		private void ParseDeleteInventoryItem(PacketReader reader)
		{
			try
			{
				int objId = reader.ReadInt();
				EmitSignal(SignalName.InventoryItemDeleted, objId);
				GD.Print($"[RX] DeleteInventoryItem: ObjId={objId}");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[PacketHandler] ParseDeleteInventoryItem failed: {e.Message}");
			}
		}

		// ====================================================================
		// [SECTION END] Character: Login-time Info & Runtime Stat Updates
		// ====================================================================

		// ====================================================================
		// [SECTION] Combat: Physical & Magic Attack Packets
		// ====================================================================

		// ====================================================================
		// 【JP協議對齊】物理攻擊解析 - 對齊 jp S_AttackPacket.java
		// 結構: writeC(142), writeC(type), writeD(pc.getId()), writeD(objid), 
		//       writeH(damage), writeC(heading), writeH(0), writeH(0), writeC(0)
		// ====================================================================
		private void ParseObjectAttack(PacketReader reader)
		{
			try
			{
				// 【JP協議對齊】對齊 jp S_AttackPacket.java 結構
				int actionId = reader.ReadByte();     // [1 byte] type (動作類型)
				int attackerId = reader.ReadInt();    // [4 bytes] 攻擊者實體 ID (pc.getId())
				int targetId = reader.ReadInt();      // [4 bytes] 目標實體 ID (objid)
				int damage = reader.ReadUShort();     // [2 bytes] 傷害數值 (writeH，不是 writeC)
				int heading = reader.ReadByte();      // [1 byte] 攻擊者的朝向
				reader.ReadUShort();                  // writeH(0x0000) - target x
				reader.ReadUShort();                  // writeH(0x0000) - target y
				reader.ReadByte();                     // writeC(0x00) - 0x00:none 0x04:Claw 0x08:CounterMirror

				// jp 版本的攻擊封包結構更簡單，不包含遠程攻擊的額外字段
				// 遠程攻擊應該由其他封包處理（如 S_UseArrowSkill）
				
				GD.Print($"[RX] Attack: Attacker:{attackerId} Target:{targetId} Action:{actionId} Damage:{damage} Heading:{heading}");
				
				// 發送攻擊信號
				_lastOp35WasMagic = false; // jp 版本中，142 是純物理攻擊
				EmitSignal(SignalName.ObjectAttacked, attackerId, targetId, actionId, damage);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Protocol Error] Opcode 142 對齊崩潰，Error: {e.Message}");
			}
		}

		// ====================================================================
		// [核心修复] ParseObjectAttackMagic (Opcode 57) - 支持坐标
		// ====================================================================
		private void ParseObjectAttackMagic(PacketReader reader)
		{
			try
			{
				int gfxId = 0;
				// 【JP協議對齊】S_SkillSound (短封包) 結構:
				// writeD(objid), writeH(gfxid), writeH(0), writeD(0)
				// 長度通常為 12 bytes（不含 opcode）。用於藥水/變身等特效播放。
				if (reader.Remaining <= 12)
				{
					int objectId = reader.ReadInt();
					gfxId = reader.ReadUShort();
					if (reader.Remaining >= 2) reader.ReadUShort();
					if (reader.Remaining >= 4) reader.ReadInt();
					GD.Print($"[RX] SkillSoundGfx objId={objectId} gfxId={gfxId}");
					if (objectId > 0 && gfxId > 0)
						EmitSignal(SignalName.ObjectEffect, objectId, gfxId);
					return;
				}

				// 1. 基礎動作與施法者
				// Java: writeC(action), writeD(attacker)
				int actionId = reader.ReadByte(); // [1 byte] 施法動作
				int attackerId = reader.ReadInt(); // [4 bytes] 施法者 ID

				// 2. 坐標與方向 (地面魔法關鍵)
				// Java: writeH(x), writeH(y), writeC(heading), writeD(etc), writeH(gfx)
				// 【座標同步修復】Op57 包中的 x, y 是攻擊者的座標（cha.getX(), cha.getY()），這是服務器確認的玩家座標！
				int attackerX = reader.ReadUShort();        // 对应 cha.getX()攻擊者 X 座標（服務器確認）
				int attackerY = reader.ReadUShort();        // 对应 cha.getY()攻擊者 Y 座標（服務器確認）
				int heading = reader.ReadByte();    // 对应 cha.getHeading()施法者朝向
				int etcId = reader.ReadInt();       // 对应 Config.getObjectID_ETC()
				gfxId = reader.ReadUShort();    // 对应 gfx (魔法效果 ID)

				// 3. 类型与填充
				// Java: writeC(0) 或 writeC(8), writeH(0)
				int type = reader.ReadByte(); // [1 byte] 0:單體, 8:範圍(AOE)
				reader.ReadUShort(); // Padding

				// 4. [核心] 读取目标数量
				// Java: writeH(list.size())
				int targetCount = reader.ReadUShort(); // 受影響的目標數量

				// 【座標同步修復】如果攻擊者是玩家自己，更新服務器確認的座標
				// 注意：這裡需要訪問 GameWorld 的 _serverConfirmedPlayerX/Y，但 PacketHandler 無法直接訪問
				// 所以通過信號傳遞，讓 GameWorld 處理
				// 暫時先發送信號，GameWorld 會在 OnObjectMagicAttacked 中處理

				// [魔法日誌] Op57 完整參數：用於定位動畫、座標、方向、範圍
				string typeStr = type == 0 ? "單體" : (type == 8 ? "AOE/群體" : $"type={type}");
				GD.Print($"[Magic][Packet57] Attacker:{attackerId} AttackerGrid:({attackerX},{attackerY}) GfxId:{gfxId} ActionId:{actionId} Heading:{heading} Range:{typeStr} TargetCount:{targetCount} EtcId:{etcId}");

				// 5. 遍历受击者列表
				// Java: for (L1Object o : list) { writeD(id), writeC(dmg) }
				for (int i = 0; i < targetCount; i++)
				{
					int targetId = reader.ReadInt(); // [4 bytes] 受擊者 ID
					int damage = reader.ReadByte();  // [1 byte] 分攤傷害數值
					// 【座標同步修復】Op57 包中的 x, y 是攻擊者座標，不是目標座標
					// 目標座標需要從目標實體獲取，或者使用攻擊者座標（對於地面魔法）
					// 這裡傳遞攻擊者座標，GameWorld 會根據需要處理
					GD.Print($"[Magic][Packet57] Target[{i}] Id:{targetId} Damage:{damage} -> Emit ObjectMagicAttacked(attacker:{attackerId}, target:{targetId}, gfx:{gfxId}, dmg:{damage}, attackerGrid:({attackerX},{attackerY}))");
					// 发送信号：带上攻擊者座標（用於座標同步）和目標ID（用於定位目標實體）
					// GameWorld 會根據 targetId 是否 > 0 來決定是在目標身上播還是在攻擊者座標播
					EmitSignal(SignalName.ObjectMagicAttacked, attackerId, targetId, gfxId, damage, attackerX, attackerY);
				}

				// 6. 處理無目標地面魔法 (如: 地裂、火牆的初始位置) (如 Buff 或 纯地面魔法 targetCount == 0)
				// 此时也需要触发一次信号，以便在 (attackerX, attackerY) 处播放动画或让施法者做动作
				if (targetCount == 0)
				{
					GD.Print($"[Magic][Packet57] 無目標地面魔法 -> Emit ObjectMagicAttacked(attacker:{attackerId}, target:0, gfx:{gfxId}, attackerGrid:({attackerX},{attackerY}))");
					// 传 targetId=0，让 GameWorld 使用攻擊者座標播放
					EmitSignal(SignalName.ObjectMagicAttacked, attackerId, 0, gfxId, 0, attackerX, attackerY);
				}
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Packet] Magic Parse Error: {e.Message}");
			}
		}


		// ====================================================================
		// [SECTION] Chat: Overhead / Complex / Global / System Messages
		// ====================================================================

		private void ParsePacket45(PacketReader reader)
		{
			int type = reader.ReadByte(); // 0=Normal, 2=Shout
			int objectId = reader.ReadInt();
			string text = reader.ReadString();
			
			// 服务器发来的通常是 "Name: ChatContent"
			// 我们需要拆分吗？通常 UI 会直接显示，或者在这里处理
			EmitSignal(SignalName.ObjectChat, objectId, text, type);
		}

		// S_ObjectChatting (Opcode 19) - 复杂聊天
		private void ParsePacket19(PacketReader reader)
		{
			int type = reader.ReadByte();
			int objectId = 0;
			string text = "";

			// 根据 S_ObjectChatting.java 的结构
			if (type == 0 || type == 2) // 0=Normal(with coords), 2=Shout?
			{
				objectId = reader.ReadInt();
				text = reader.ReadString();
				// Opcode 19 type 0 还带有 x, y，但我们通常只关心文字显示在头顶
				// reader.ReadUShort(); // x
				// reader.ReadUShort(); // y
			}
			// 其他类型 (Whisper, Party, etc) 暂时略过或根据需要添加
			
			if (objectId != 0 && !string.IsNullOrEmpty(text))
			{
				EmitSignal(SignalName.ObjectChat, objectId, text, type);
			}
		}
			


		// ====================================================================
		// [SECTION] 进入世界后的显示 World: Object Spawn / Movement / HP-MP Updates
		// ====================================================================

		private void ParseObjectAdd(PacketReader reader)
		{
			// 【JP協議對齊】對齊 jp S_OwnCharPack.java 結構
			// writeC(3), writeH(x), writeH(y), writeD(id), writeH(gfx), writeC(gfxMode),
			// writeC(heading), writeC(light), writeC(speed), writeD(exp), writeH(lawful),
			// writeS(name), writeS(title), writeC(status), writeD(clanId), writeS(clanName),
			// writeS(null), writeC(clanRank), writeC(hpRatio), writeC(thirdSpeed), writeC(0), writeC(0), writeH(0xFF), writeH(0xFF)
			var obj = new WorldObject();

			obj.X = reader.ReadUShort();           // writeH(pc.getX())
			obj.Y = reader.ReadUShort();           // writeH(pc.getY())
			obj.ObjectId = reader.ReadInt();       // writeD(pc.getId())
			obj.GfxId = reader.ReadUShort();       // writeH(pc.getTempCharGfx())
			obj.GfxMode = reader.ReadByte();       // writeC(pc.getCurrentWeapon())
			obj.Heading = reader.ReadByte();       // writeC(pc.getHeading())
			obj.Light = reader.ReadByte();         // writeC(pc.getOwnLightSize())
			obj.Speed = reader.ReadByte();         // writeC(pc.getMoveSpeed())
			obj.Exp = reader.ReadInt();            // writeD(pc.getExp())
			obj.Lawful = reader.ReadShort();       // writeH(pc.getLawful())
			obj.Name = reader.ReadString();        // writeS(pc.getName())
			obj.Title = reader.ReadString();       // writeS(pc.getTitle())
			obj.Status = reader.ReadByte();        // writeC(status)
			obj.ClanId = reader.ReadInt();         // writeD(pc.getClanId())
			obj.ClanName = reader.ReadString();    // writeS(pc.getClanName())
			reader.ReadString();                    // writeS(null) - ペッホチング？
			
			int clanRank = reader.ReadByte();      // writeC(clanRank > 0 ? clanRank << 4 : 0xb0)
			obj.HpRatio = reader.ReadByte();       // writeC(hpRatio) - 隊伍中顯示 HP 百分比，否則 0xFF
			int thirdSpeed = reader.ReadByte();    // writeC(0x08 if STATUS_THIRD_SPEED else 0)
			reader.ReadByte();                     // writeC(0) - 海底波浪模糊程度
			reader.ReadByte();                     // writeC(0) - 物件的等級
			reader.ReadUShort();                   // writeH(0xFF)
			reader.ReadUShort();                   // writeH(0xFF)

			// 【關鍵診斷】確保這行代碼存在！
			// 【調試】這是解決色塊問題的關鍵日誌！
			GD.Print($"[RX] Spawn Obj: name {obj.Name} (obj ID:{obj.ObjectId}) GfxId:{obj.GfxId} Lawful: {obj.Lawful}  X:{obj.X} Y:{obj.Y}");
			EmitSignal(SignalName.ObjectSpawned, obj);
		}

		private void ParseObjectMoving(PacketReader reader)
		{
			// 【JP協議對齊】S_MoveCharPacket: [opcode=122][D objId][H x][H y][C heading][C 129][D 0]
			// 對齊 jp S_MoveCharPacket.java 結構
			try 
			{
				int objectId = reader.ReadInt(); // writeD(cha.getId())
				int x = reader.ReadUShort(); // 必須是 ReadUShort 對齊伺服器 writeH
				int y = reader.ReadUShort();
				int heading = reader.ReadByte();
				reader.ReadByte(); // writeC(129) - jp 版本多了這個字段
				reader.ReadInt();  // writeD(0) - jp 版本多了這個字段

				// 【座標同步診斷】記錄所有移動包，特別是玩家自己的
				Vector2 serverGrid = new Vector2(x, y);
				Vector2 clientPixel = new Vector2(x * 32, y * 32);
				
				if (_entities.TryGetValue(objectId, out var entity))
				{
					// 【座標同步診斷】正確計算客戶端座標（考慮 CurrentMapOrigin）
					// entity.Position 是相對 CurrentMapOrigin 的像素座標
					// entity.MapX/MapY 是絕對座標
					int clientMapX = entity.MapX;
					int clientMapY = entity.MapY;
					GD.Print($"[Pos-Sync] ObjID:{objectId} | Server_Grid:({x},{y}) | Client_Grid:({clientMapX},{clientMapY}) | Client_Pos:{entity.Position} | Offset:({x-clientMapX},{y-clientMapY})");
				}
				else
				{
					GD.Print($"[Pos-Sync] ObjID:{objectId} | Server_Grid:({x},{y}) | Entity_NOT_FOUND");
				}

				if (_lastMoveByObjectId.TryGetValue(objectId, out var last) && last.X == x && last.Y == y && last.Heading == heading)
					return;

				_lastMoveByObjectId[objectId] = (x, y, heading);
				_lastHeadingByObjectId[objectId] = heading;
				EmitSignal(SignalName.ObjectMoved, objectId, x, y, heading);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Pos-Error] ParseObjectMoving failed: {e.Message}");
			}
		}


		private void ParseHPUpdate(PacketReader reader)
		{
			int current = reader.ReadUShort();
			int max = reader.ReadUShort();
			GD.Print($"[Packet] HP Update: {current}/{max}");
			EmitSignal(SignalName.HPUpdated, current, max);
		}

		private void ParseMPUpdate(PacketReader reader)
		{
			int current = reader.ReadUShort();
			int max = reader.ReadUShort();
			EmitSignal(SignalName.MPUpdated, current, max);
		}
		// ====================================================================
		// [SECTION END] World: Object Spawn / Movement / HP-MP Updates
		// ====================================================================






		// 2. 添加解析方法 (严格对齐 C_ChatGlobal.java)
		private void HandleGlobalChat(byte[] data)
		{
			var r = new PacketReader(data);
			r.ReadByte(); // 跳过 Opcode 15
			
			int type = r.ReadByte();      // readC()
			string text = r.ReadString(); // readS()
			
			// 打印服务器消息，这是我们调试的关键！
			GD.Print($"[Server Message] Type:{type} Msg: {text}");
			
			// 如果你有 HUD，建议显示出来：
		EmitSignal(SignalName.SystemMessage, text);

		}


		// S_ServerMessage (Opcode 16)
		// --- 系统消息解析 (使用 string.txt) ---
		private void ParseServerMessage(PacketReader reader)
		{
			try
			{
				int msgId = reader.ReadUShort();
				int count = reader.ReadByte(); // 参数数量
				
				string msgTemplate = "";
				// 从 StringTable 查找模板
				if (StringTable.Instance != null)
				{
					msgTemplate = StringTable.Instance.GetText(msgId);
				}

				// 如果没找到模板，就显示 ID
				if (string.IsNullOrEmpty(msgTemplate) || msgTemplate.StartsWith("$"))
				{
					msgTemplate = $"SysMsg: {msgId}";
				}

				// 讀取所有參數
				var args = new System.Collections.Generic.List<string>();
				for (int i = 0; i < count; i++)
					args.Add(reader.ReadString());

				// 依序替換 %0, %1, %2...（與 L1J/string.txt 對齊；模板如 "\f1%0%s 主動固定在你的手上！" 僅一個參數時只替換 %0，未替換的 %s 由 HUD 清除）
				for (int i = 0; i < args.Count; i++)
				{
					string arg = args[i];
					string pctN = "%" + i.ToString();
					msgTemplate = msgTemplate.Replace(pctN, arg);
				}

				// 移除伺服器字型/顏色碼 \fX（如 \f1, \fR），避免在 ChatBox 顯示為亂碼
				msgTemplate = StripLineageFormatCodes(msgTemplate);

				GD.Print($"[System] MsgID:{msgId} -> {msgTemplate}");
				
				// 【修復】當收到系統消息時，通知 GameWorld 清除 UseItem 超時檢測（表示服務器有回應）
				var world = GetNodeOrNull<Client.Game.GameWorld>("/root/Boot/World");
				if (world != null)
				{
					world.OnSystemMessageReceived();
				}
				
				// 【修復】確保系統消息發送到 HUD 顯示
				EmitSignal(SignalName.SystemMessage, msgTemplate);
			}
			catch (Exception e) { GD.PrintErr($"[Packet] Msg Error: {e.Message}"); }
		}

		/// <summary>移除 L1 協議中的 \fX 字型/顏色碼，避免在聊天框顯示為字面 \f1、%0 等。</summary>
		private static string StripLineageFormatCodes(string text)
		{
			if (string.IsNullOrEmpty(text)) return text;
			// \f 後接一字符（如 \f1, \fR, \fU）
			return System.Text.RegularExpressions.Regex.Replace(text, @"\\f.", "");
		}

		// ====================================================================
		// [SECTION END] Chat: Overhead / Complex / Global / System Messages
		// ====================================================================


		// ====================================================================
		// [SECTION] Login: Character List Packet
		// ====================================================================

		// ====================================================================
		// [SECTION] Inventory: Item Decode / List / Add / Equip State
		// ====================================================================

		// ========================================================================
		// 物品解析核心逻辑
		// ========================================================================

		// 通用物品读取器 (适配 Weapon, Armor, Etc 的公共结构)
		// 结构: ID(4) -> Type/Action(2) -> Gfx(2) -> Bless(1) -> Count(4) -> Ident(1) -> Name(S)
		private Client.Data.InventoryItem ParseCommonItemData(PacketReader reader)
		{
			var item = new Client.Data.InventoryItem();
			item.ObjectId = reader.ReadInt();
			
			// 跳过 Type/Action (2字节)，对齐到 Gfx
			// reader.ReadUShort(); 

			// 修复：读取它赋值给 Type！
			item.Type = reader.ReadUShort();
			
			item.GfxId = reader.ReadUShort();
			item.Bless = reader.ReadByte();
			item.Count = reader.ReadInt();
			
			int isIdentified = reader.ReadByte();
			
			// 读取原始名称 (可能是 "$93" 或 "皮夹克")
			string rawName = reader.ReadString();


 	// 【偵探日誌】這是解決裝備無反應的關鍵！
	GD.Print($"[Protocol-Check] ItemID:{item.ObjectId} RawName Received: '{rawName}'");

			// 【核心修复】立即调用 DescTable 进行翻译
			// 这样 InventorySlot 在显示时拿到的就是 "银箭" 而不是 "$93"
			if (DescTable.Instance != null)
			{
				item.Name = DescTable.Instance.ResolveName(rawName);
			}
			else
			{
				item.Name = rawName;
			}
			
			// 【埋点日志】检查物品类型读取结果
			// 重点：检查弓箭（Bow）的 Type 是否为 4，剑是否为 1
			GD.Print($"[ItemParse] ID:{item.ObjectId} Name:{item.Name} -> Type(Action): {item.Type} Gfx:{item.GfxId}");

			// 解析装备状态
			if (rawName.Contains("($"))
			{
				item.IsEquipped = true;
			}
			else
			{
				item.IsEquipped = false;
			}

			item.Ident = (isIdentified != 0) ? 1 : 0;
			if (isIdentified != 0 && reader.Remaining >= 2)
			{
				item.DetailInfo = ParseInventoryStatusExtended(reader) ?? "";
			}

			return item;
		}

		// 【JP協議對齊】背包列表解析 - 對齊 jp S_InvList.java
		// 結構: writeC(180), writeC(count), [每個物品: writeD(id), writeH(magicCatalystType), writeC(type), writeC(chargeCount), writeH(gfxId), writeC(status), writeD(count), writeC(identified), writeS(name), writeC(statusLen), [status bytes], writeC(10), writeD(0), writeD(0), writeH(0)]
		private void ParseInventoryList(PacketReader reader)
		{
			try 
			{
				int count = reader.ReadByte(); // writeC(items.size())
				var list = new Godot.Collections.Array<InventoryItem>();

				for (int i = 0; i < count; i++)
				{
					// 【JP協議對齊】對齊 jp S_InvList.java 結構
					int objId = reader.ReadInt(); // writeD(item.getId())
					reader.ReadUShort(); // writeH(magicCatalystType) - jp 新增字段
					int type = reader.ReadByte(); // writeC(type) - UseType
					int chargeCount = reader.ReadByte(); // writeC(chargeCount)
					int gfxId = reader.ReadUShort(); // writeH(gfxId)
					int status = reader.ReadByte(); // writeC(status)
					int countVal = reader.ReadInt(); // writeD(count)
					int identified = reader.ReadByte(); // writeC(identified)
					string name = reader.ReadString(); // writeS(name)
					
					// 【JP協議對齊】重新組裝 item - 對齊 jp S_InvList.java
					var item = new InventoryItem();
					item.ObjectId = objId;
					item.Type = type;
					item.GfxId = gfxId;
					item.Bless = status;
					item.Count = countVal;
					item.Ident = (identified != 0) ? 1 : 0;
					
					// 解析名稱
					if (DescTable.Instance != null)
					{
						item.Name = DescTable.Instance.ResolveName(name);
					}
					else
					{
						item.Name = name;
					}
					
					// 解析裝備狀態
					item.IsEquipped = name.Contains("($");
					
					// 解析狀態字節
					int statusLen = reader.ReadByte(); // writeC(status.length) 或 writeC(0)
					if (statusLen > 0 && reader.Remaining >= statusLen)
					{
						item.DetailInfo = ParseInventoryStatusExtended(reader);
					}
					
					// jp 版本結尾字段
					reader.ReadByte(); // writeC(10)
					reader.ReadInt();  // writeD(0)
					reader.ReadInt();  // writeD(0)
					reader.ReadUShort(); // writeH(0)
					
					GD.Print($"[ItemList] {item.Name} | Type:{type} | Gfx:{gfxId} | Status:{status} | Count:{countVal}");
					
					list.Add(item);
				}
				EmitSignal(SignalName.InventoryListReceived, list);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Packet] List Error: {e.Message}");
			}
		}


		// 物品添加
		// 【JP協議對齊】對齊 jp S_AddItem.java 結構
		// writeC(63), writeD(id), writeH(magicCatalystType), writeC(type), writeC(chargeCount),
		// writeH(gfxId), writeC(status), writeD(count), writeC(identified), writeS(name),
		// writeC(statusLen), [status bytes], writeC(10), writeD(0), writeD(0), writeH(0)
		private void ParseInventoryAdd(PacketReader reader)
		{
			try
			{
				var item = new Client.Data.InventoryItem();
				item.ObjectId = reader.ReadInt(); // writeD(item.getId())
				reader.ReadUShort(); // writeH(magicCatalystType) - jp 新增字段
				item.Type = reader.ReadByte(); // writeC(type) - UseType
				int chargeCount = reader.ReadByte(); // writeC(chargeCount)
				item.GfxId = reader.ReadUShort(); // writeH(gfxId)
				item.Bless = reader.ReadByte(); // writeC(status)
				item.Count = reader.ReadInt(); // writeD(count)
				int isIdentified = reader.ReadByte(); // writeC(identified)
				item.Ident = (isIdentified != 0) ? 1 : 0;
				
				string rawName = reader.ReadString(); // writeS(name)
				if (DescTable.Instance != null)
				{
					item.Name = DescTable.Instance.ResolveName(rawName);
				}
				else
				{
					item.Name = rawName;
				}
				
				// 解析狀態字節
				int statusLen = reader.ReadByte(); // writeC(status.length) 或 writeC(0)
				if (statusLen > 0 && reader.Remaining >= statusLen)
				{
					item.DetailInfo = ParseInventoryStatusExtended(reader);
				}
				
				// jp 版本結尾字段
				reader.ReadByte(); // writeC(10)
				reader.ReadInt();  // writeD(0)
				reader.ReadInt();  // writeD(0)
				reader.ReadUShort(); // writeH(0)
				
				GD.Print($"[Inventory] Added/Update: {item.Name} (ID:{item.ObjectId}) Type:{item.Type} Gfx:{item.GfxId} Count:{item.Count}");
				EmitSignal(SignalName.InventoryItemAdded, item);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Packet] Add Error: {e.Message}");
			}
		}

		// 
		private void ParseInventoryEquipped(PacketReader reader)
		{
			// Opcode 24
			try
				{
					int objectId = reader.ReadInt();
					string rawName = reader.ReadString(); // 读取原始名字 (例如 "$33 ($9)")

					// 【調試】打印原始名稱，用於診斷
					GD.Print($"[Packet] Equip RawName: '{rawName}' (ObjectId:{objectId})");

					// 【核心修复】必须先判断原始字符串中的状态！
					// 服務器發送的格式：Type1=1 武器裝備時會加上 " ($9)"，Type1=2 防具會加上 " ($117)"
					// 必須在翻譯之前判斷，因為 DescTable 會把 ($9) 翻譯成 (揮舞)
					bool isEquipped = rawName.Contains("($") || rawName.Contains("(正在使用)");
					
					// 【調試】檢查各種可能的裝備標記
					bool hasDollar9 = rawName.Contains("($9)");
					bool hasDollar117 = rawName.Contains("($117)");
					bool hasDollar10 = rawName.Contains("($10)");
					GD.Print($"[Packet] Equip Check: has($)={rawName.Contains("($")}, has($9)={hasDollar9}, has($117)={hasDollar117}, has($10)={hasDollar10}, Result={isEquipped}");

					// 之后再翻译名字用于显示 (如果需要)
					string displayName = rawName;
					if (DescTable.Instance != null) displayName = DescTable.Instance.ResolveName(rawName);

					// 发送正确的状态
					GD.Print($"[Packet] Equip Status: {displayName} (ObjectId:{objectId}) -> Equipped:{isEquipped}");
					EmitSignal(SignalName.ItemEquipStatusChanged, objectId, isEquipped);
				}
				catch (Exception e) 
				{ 
					GD.PrintErr($"[Packet] ParseInventoryEquipped Error: {e.Message}");
				}
		}

		// Opcode 111: S_InventoryStatus (對齊 S_InventoryStatus.java: writeC(111), writeD(invID), writeS(name), writeD(count), writeC(0)|weapon/armor...)
		private void ParseInventoryStatus(PacketReader reader)
		{
			try
			{
				int objectId = reader.ReadInt();
				string name = reader.ReadString();
				int count = reader.ReadInt();
				int status = reader.Remaining > 0 ? reader.ReadByte() : 0;
				string detailInfo = "";
				if (status != 0 && reader.Remaining >= 2)
					detailInfo = ParseInventoryStatusExtended(reader);
				else
					while (reader.Remaining > 0) reader.ReadByte();
				EmitSignal(SignalName.InventoryItemUpdated, objectId, name, count, status, detailInfo ?? "");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Protocol Error] Opcode 111 ParseInventoryStatus: {e.Message}");
			}
		}
		
		// Opcode 127: S_ItemStatus / S_ItemAmount (jp 3.0)
		// 結構: writeC(127), writeD(id), writeS(viewName), writeD(count), writeC(statusLen), [status bytes]
		private void ParseItemStatusAmount(PacketReader reader)
		{
			try
			{
				int objectId = reader.ReadInt();
				string name = reader.ReadString();
				int count = reader.ReadInt();
				int statusLen = reader.Remaining > 0 ? reader.ReadByte() : 0;
				string detailInfo = "";
				
				if (statusLen > 0 && reader.Remaining >= statusLen)
				{
					byte[] statusBytes = reader.ReadBytes(statusLen);
					var statusReader = new PacketReader(statusBytes);
					detailInfo = ParseInventoryStatusExtended(statusReader);
				}
				else
				{
					while (reader.Remaining > 0) reader.ReadByte();
				}
				
				int statusFlag = statusLen > 0 ? 1 : 0;
				EmitSignal(SignalName.InventoryItemUpdated, objectId, name, count, statusFlag, detailInfo ?? "");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Protocol Error] Opcode 127 ParseItemStatusAmount: {e.Message}");
			}
		}

		// 解析 Opcode 111 鑑定後的擴充資料：武器(攻擊/命中/傷害/職業/屬性)、防具(AC/職業/屬性)、一般(材質/重量)。對齊 S_Inventory.weapon/armor/etc。
		private static string ParseInventoryStatusExtended(PacketReader r)
		{
			var lines = new List<string>();
			try
			{
				int b0 = r.ReadByte();
				int b1 = r.Remaining > 0 ? r.ReadByte() : 0;
				if (b1 == 1)
				{
					int dmgS = r.Remaining > 0 ? r.ReadByte() : 0;
					int dmgL = r.Remaining > 0 ? r.ReadByte() : 0;
					int material = r.Remaining > 0 ? r.ReadByte() : 0;
					int weight = r.Remaining >= 4 ? r.ReadInt() : 0;
					lines.Add($"攻擊 {dmgS}/{dmgL}");
					string matStr = MaterialToStr(material);
					if (!string.IsNullOrEmpty(matStr)) lines.Add($"材質 {matStr}");
					if (weight > 0) lines.Add($"重量 {weight}");
					while (r.Remaining >= 1)
					{
						int tag = r.ReadByte();
						if (tag == 2 && r.Remaining >= 1) { int en = r.ReadByte(); if (en != 0) lines.Add($"強化 +{en}"); }
						else if (tag == 3 && r.Remaining >= 1) { int dur = r.ReadByte(); lines.Add($"耐久 {dur}"); }
						else if (tag == 4) { }
						else if (tag == 5 && r.Remaining >= 1) { int hit = r.ReadByte(); lines.Add($"命中 +{hit}"); }
						else if (tag == 6 && r.Remaining >= 1) { int dmg = r.ReadByte(); lines.Add($"傷害 +{dmg}"); }
						else if (tag == 7 && r.Remaining >= 1) { int mask = r.ReadByte(); lines.Add("職業: " + ClassMaskToStr(mask)); }
						else if (tag == 8 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"力量 +{v}"); }
						else if (tag == 9 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"敏捷 +{v}"); }
						else if (tag == 10 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"體質 +{v}"); }
						else if (tag == 11 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"智慧 +{v}"); }
						else if (tag == 12 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"智力 +{v}"); }
						else if (tag == 13 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"魅力 +{v}"); }
						else if (tag == 14 && r.Remaining >= 2) { int v = r.ReadUShort(); lines.Add($"HP +{v}"); }
						else if (tag == 15 && r.Remaining >= 2) { int v = r.ReadUShort(); lines.Add($"魔防 +{v}"); }
						else if (tag == 16) { }
						else if (tag == 17 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"魔攻 +{v}"); }
						else if (tag == 18) { lines.Add("加速"); }
						else break;
					}
				}
				else if (b1 == 19)
				{
					int ac = r.Remaining > 0 ? r.ReadByte() : 0;
					int material = r.Remaining > 0 ? r.ReadByte() : 0;
					int weight = r.Remaining >= 4 ? r.ReadInt() : 0;
					lines.Add($"防禦 {ac}");
					string matStr = MaterialToStr(material);
					if (!string.IsNullOrEmpty(matStr)) lines.Add($"材質 {matStr}");
					if (weight > 0) lines.Add($"重量 {weight}");
					while (r.Remaining >= 1)
					{
						int tag = r.ReadByte();
						if (tag == 2 && r.Remaining >= 1) { int en = r.ReadByte(); if (en != 0) lines.Add($"強化 +{en}"); }
						else if (tag == 7 && r.Remaining >= 1) { int mask = r.ReadByte(); lines.Add("職業: " + ClassMaskToStr(mask)); }
						else if (tag == 8 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"力量 +{v}"); }
						else if (tag == 9 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"敏捷 +{v}"); }
						else if (tag == 10 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"體質 +{v}"); }
						else if (tag == 11 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"智慧 +{v}"); }
						else if (tag == 12 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"智力 +{v}"); }
						else if (tag == 13 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"魅力 +{v}"); }
						else if (tag == 14 && r.Remaining >= 2) { int v = r.ReadUShort(); lines.Add($"HP +{v}"); }
						else if (tag == 15 && r.Remaining >= 2) { int v = r.ReadUShort(); lines.Add($"魔防 +{v}"); }
						else if (tag == 17 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"魔攻 +{v}"); }
						else if (tag == 18) { lines.Add("加速"); }
						else if (tag == 26 && r.Remaining >= 2) { int v = r.ReadUShort(); lines.Add($"等級限制 {v}"); }
						else if (tag == 27 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"火屬性 +{v}"); }
						else if (tag == 28 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"水屬性 +{v}"); }
						else if (tag == 29 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"風屬性 +{v}"); }
						else if (tag == 30 && r.Remaining >= 1) { int v = r.ReadByte(); lines.Add($"地屬性 +{v}"); }
						else { while (r.Remaining > 0) r.ReadByte(); break; }
					}
					while (r.Remaining > 0) r.ReadByte();
				}
				else if (b0 == 6 && b1 == 23)
				{
					int material = r.Remaining > 0 ? r.ReadByte() : 0;
					int weight = r.Remaining >= 2 ? r.ReadUShort() : 0;
					string matStr = MaterialToStr(material);
					if (!string.IsNullOrEmpty(matStr)) lines.Add($"材質 {matStr}");
					if (weight > 0) lines.Add($"重量 {weight}");
					while (r.Remaining > 0) r.ReadByte();
				}
				else
					while (r.Remaining > 0) r.ReadByte();
			}
			catch { while (r.Remaining > 0) r.ReadByte(); }
			return lines.Count > 0 ? string.Join("\n", lines) : "";
		}

		private static string ClassMaskToStr(int mask)
		{
			var s = new List<string>();
			if ((mask & 1) != 0) s.Add("王族");
			if ((mask & 2) != 0) s.Add("騎士");
			if ((mask & 4) != 0) s.Add("妖精");
			if ((mask & 8) != 0) s.Add("法師");
			return s.Count > 0 ? string.Join(" ", s) : "無";
		}

		// 對齊 Item.java MATERIAL_* (0=一般 11=鐵 12=鋼 14=銀 15=金 等)
		private static string MaterialToStr(int material)
		{
			switch (material)
			{
				case 0: return "";
				case 1: return "液體";
				case 2: return "米索莉";
				case 3: return "植物";
				case 4: return "動物";
				case 5: return "紙";
				case 6: return "絲綢";
				case 7: return "皮革";
				case 8: return "木頭";
				case 9: return "骨頭";
				case 10: return "龍";
				case 11: return "鐵";
				case 12: return "鋼";
				case 13: return "奧里哈魯根";
				case 14: return "銀";
				case 15: return "金";
				case 16: return "白金";
				case 17: return "米索莉";
				case 18: return "黑色米索莉";
				case 19: return "玻璃";
				case 20: return "寶石";
				case 21: return "礦石";
				case 22: return "奧里哈魯根";
				default: return material > 0 ? $"材質{material}" : "";
			}
		}
		
	
		// ====================================================================
		// [SECTION END] Inventory: Item Decode / List / Add / Equip State
		// ====================================================================

		// skill 
		// --- 修复版：使用 data.Length 控制循环，不依赖 PacketReader 内部属性 ---

		// ====================================================================
		// [SECTION] Skills: Bitmask Updates & Buy List
		// ====================================================================

		// --------------------------------------------------------------------
		// Opcode 30: S_SkillAdd (學習/添加技能)
		// --------------------------------------------------------------------
		private void ParseSkillAdd(PacketReader reader)
		{
			try
			{
				int type = reader.ReadByte(); // [1 byte] 10: 基礎魔法, 32: 複合魔法
				
				// 服務端會根據 type 發送不同長度的位圖 (Masks)
				List<int> masks = new List<int>();
				while (reader.Remaining > 0)
				{
					masks.Add(reader.ReadByte()); // 循環讀取 8-bit 的技能掩碼
				}
				
				GD.Print($"[Skill] 添加技能包 Type:{type} Count:{masks.Count}");
				EmitSignal(SignalName.SkillAdded, type, masks.ToArray());
			}
			catch (Exception e) { GD.PrintErr($"Op30 解析錯誤: {e.Message}"); }
		}

		// --------------------------------------------------------------------
		// Opcode 31: S_SkillDelete (刪除技能)
		// --------------------------------------------------------------------
		private void ParseSkillDelete(PacketReader reader)
		{
			try
			{
				reader.ReadByte(); // [1 byte] 固定為 32 (Type)
				List<int> masks = new List<int>();
				while (reader.Remaining > 0)
				{
					masks.Add(reader.ReadByte());
				}
				GD.Print($"[Skill] 技能重置/刪除，剩餘掩碼數: {masks.Count}");
				EmitSignal(SignalName.SkillDeleted, masks.ToArray());
			}
			catch (Exception e) { GD.PrintErr($"Op31 解析錯誤: {e.Message}"); }
		}

		// --------------------------------------------------------------------
		// Opcode 78: S_SkillBuyList (打開技能商人的購買列表)
		// --------------------------------------------------------------------
		// [核心修復] 嚴格對齊 S_SkillBuyList.java
		// --------------------------------------------------------------------
		private void ParseSkillBuyList(PacketReader reader)
		{
			try
			{
				reader.ReadInt();               // [4 bytes] 讀取服務端的 writeD(100)
				int count = reader.ReadShort();     // [2 bytes] 讀取服務端的 writeH(size)
				
				// 1.82 版本中，如果是 NPC 打開，後面會跟一個 NPC ID
				// 根據 C_SkillBuy.java，通常玩家是跟 NPC 對話觸發
				int npcId = 0; 
				if (reader.Remaining >= 4) {
					npcId = reader.ReadInt();   // [4 bytes] 讀取 NPC ObjectId
				}

				List<int> skillIds = new List<int>();
				for (int i = 0; i < count; i++)
				{
					if (reader.Remaining >= 4)
						skillIds.Add(reader.ReadInt()); // [4 bytes] 循環讀取 writeD(id)
				}
				
				GD.Print($"[SkillBuy] 成功解析商店技能列表. NPC:{npcId} 數量:{count}");
				EmitSignal(SignalName.SkillBuyListReceived, npcId, skillIds.ToArray());
			}
			catch (Exception e) { GD.PrintErr($"Op78 解析失敗: {e.Message}"); }
		}
		// skill end

		// ====================================================================
		// [SECTION END] Skills: Bitmask Updates & Buy List
		// ====================================================================


		// npc shop start
		// 对应 S_ShowHtml.java
		// 2. 解析方法 (使用 Godot 集合)

		// ====================================================================
		// [SECTION] NPC Shop: HTML & Buy/Sell Lists
		// ====================================================================

	/// <summary>
	/// 處理 Opcode 42：可能是 S_ShowHtml 或 S_ObjectPet (寵物/召喚物面板)
	/// 區分方法：讀取第二個字符串，如果是 "anicom" 或 "moncom" 則是寵物/召喚物面板
	/// </summary>
	private void HandleShowHtmlOrPetPanel(byte[] data)
	{
		var r = new PacketReader(data);
		r.ReadByte(); // Op 42
		
		int objectId = r.ReadInt();
		string firstString = r.ReadString();
		
		// 檢查是否是寵物/召喚物面板
		if (firstString == "anicom" || firstString == "moncom")
		{
			// 這是寵物/召喚物面板
			ParsePetPanel(r, objectId, firstString);
		}
		else
		{
			// 這是 HTML 對話
			// 回退讀取位置，重新解析為 HTML
			r = new PacketReader(data);
			r.ReadByte(); // Op 42
			int npcId = r.ReadInt();
			string htmlId = r.ReadString();
			r.ReadString(); // skip null
			
			int count = r.ReadShort();
			List<string> args = new List<string>();
			for(int i=0; i<count; i++)
			{
				args.Add(r.ReadString());
			}
			
			EmitSignal(SignalName.ShowHtmlReceived, npcId, htmlId, args.ToArray());
		}
	}
	
	/// <summary>
	/// 解析寵物/召喚物面板 (S_ObjectPet Opcode 42)
	/// 【修復】改為使用 TalkWindow 顯示，將寵物數據轉換為 HTML 參數
	/// 對齊服務器 S_ObjectPet.java: writeC(42), writeD(petId), writeS("anicom"/"moncom"), writeC(0), writeH(10/9), writeS(...)
	/// </summary>
	private void ParsePetPanel(PacketReader reader, int petId, string panelType)
	{
		try
		{
			reader.ReadByte(); // 跳過填充的 0
			int fieldCount = reader.ReadUShort(); // 10 (寵物) 或 9 (召喚物)
			
			// 讀取所有字段
			string status = reader.ReadString(); // 狀態描述 (#0)
			string currentHp = reader.ReadString(); // (#1)
			string totalHp = reader.ReadString(); // (#2)
			string currentMp = reader.ReadString(); // (#3)
			string totalMp = reader.ReadString(); // (#4)
			string level = reader.ReadString(); // (#5)
			string name = reader.ReadString(); // (#6)
			
			// 構建 HTML 參數數組（對應 <var src="#0"> 到 <var src="#9">）
			var args = new List<string>();
			args.Add(status);      // #0: 狀態
			args.Add(currentHp);   // #1: 當前 HP
			args.Add(totalHp);     // #2: 最大 HP
			args.Add(currentMp);   // #3: 當前 MP
			args.Add(totalMp);     // #4: 最大 MP
			args.Add(level);       // #5: 等級
			args.Add(name);        // #6: 名稱
			
			if (panelType == "anicom")
			{
				// 寵物額外字段
				string foodStatus = reader.ReadString(); // (#7)
				string expPercentage = reader.ReadString(); // (#8)
				string lawful = reader.ReadString(); // (#9)
				args.Add(foodStatus);    // #7: 食物狀態
				args.Add(expPercentage);  // #8: 經驗百分比
				args.Add(lawful);        // #9: 正義值
			}
			else
			{
				// 召喚物額外字段
				string unknown1 = reader.ReadString(); // "0"
				string unknown2 = reader.ReadString(); // "792"
				args.Add(unknown1);      // #7: 未知1（對應 Alignment）
				args.Add("");            // #8: 空（召喚物沒有經驗）
				args.Add(unknown2);      // #9: 未知2（對應 Alignment）
			}
			
			// 【修復】改為發送 ShowHtmlReceived 信號，使用 TalkWindow 顯示
			EmitSignal(SignalName.ShowHtmlReceived, petId, panelType, args.ToArray());
			GD.Print($"[Pet] 收到寵物面板（轉為 TalkWindow）: PetId={petId} Type={panelType} Args={args.Count}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Pet] 解析寵物面板失敗: {e.Message}");
		}
	}

		private void HandleShopBuyList(byte[] data)
		{
			var r = new PacketReader(data);
			r.ReadByte(); // Op 43
			int npcId = r.ReadInt();
			int count = r.ReadShort();
			
			// 【核心修复】直接使用 Godot.Collections.Array 和 Dictionary
			Godot.Collections.Array items = new Godot.Collections.Array();
			
			for(int i=0; i<count; i++)
			{
				var item = new Godot.Collections.Dictionary();
				item["order_id"] = r.ReadInt(); // uid
				item["gfx"] = r.ReadShort();
				item["price"] = r.ReadInt();
				item["name"] = r.ReadString();
				
				// 跳过后续未使用的字段
				r.ReadByte(); r.ReadByte(); r.ReadByte(); r.ReadInt();
				
				items.Add(item);
			}
			
			// 现在类型匹配了，可以直接发送
			EmitSignal(SignalName.ShopBuyOpen, npcId, items); 
		}

		private void HandleShopSellList(byte[] data)
		{
			// 【修復】解析 S_ShopSellList (Opcode 44)
			// 結構：writeC(44), writeD(npcId), writeH(count), writeB(ByteArrayOutputStream)
			// ByteArrayOutputStream 包含：每個物品的 InvID (4字節) + Price (4字節)
			var r = new PacketReader(data); 
			r.ReadByte(); // 讀取 Opcode (44)
			
			int npcId = r.ReadInt();
			int count = r.ReadUShort();
			
			// 解析物品列表（從 ByteArrayOutputStream）
			var items = new Godot.Collections.Array();
			for (int i = 0; i < count; i++)
			{
				int invId = r.ReadInt(); // InvID (ObjectId)
				int price = r.ReadInt();  // Price
				
				// 從本地背包獲取物品信息
				var world = GetNodeOrNull<GameWorld>("/root/Boot/World");
				if (world != null)
				{
					var item = world.GetInventoryItem(invId);
					if (item != null && !item.IsEquipped)
					{
						var itemData = new Godot.Collections.Dictionary();
						itemData["order_id"] = invId; // 出售時使用 ObjectId
						itemData["gfx"] = item.GfxId;
						itemData["price"] = price;
						itemData["name"] = item.Name;
						itemData["count"] = item.Count;
						items.Add(itemData);
					}
				}
			}
			
			// 發送信號打開商店（出售模式），包含物品列表
			EmitSignal(SignalName.ShopSellOpen, npcId, items);
		}
		// npc shop end



		// ====================================================================
		// [SECTION END] NPC Shop: HTML & Buy/Sell Lists
		// ====================================================================


			//buff
			// 对应 S_BuffSpeed.java
			// 结构: writeC(41/98), writeD(objId), writeC(speed), writeH(time)

			// ====================================================================
			// [SECTION] Buffs: Speed/Aqua/Shield/Blind Status
			// ====================================================================

			private void HandleBuffSpeed(byte[] data)
			{
				var r = new PacketReader(data);
				int opcode = r.ReadByte(); // 41 or 98
				
				// S_SkillHaste (41) 和 S_SkillBrave (98) 结构通常都是:
				// writeD(objId), writeC(mode), writeH(time)
				
				int entityId = r.ReadInt();
				int mode = r.ReadByte();
				int time = r.ReadUShort(); // 注意：服务器是 writeH，这里读 UShort 或 Short 均可
				
				// 发送信号给 UI 或 GameEntity
				EmitSignal(SignalName.BuffSpeedReceived, entityId, opcode, mode, time);
			}

			// 对应 S_BuffAqua.java
			// 结构: writeC(119), writeD(objId), writeH(time)
			private void HandleBuffAqua(byte[] data)
			{
				var r = new PacketReader(data);
				r.ReadByte(); // 119
				
				int entityId = r.ReadInt();
				int time = r.ReadShort();
				
				EmitSignal(SignalName.BuffAquaReceived, entityId, time);
			}

		// 【JP協議對齊】護盾 - 對齊 jp S_SkillIconShield.java
		// 結構: writeC(69), writeH(time), writeC(type), writeD(0)
		// 注意：這個包沒有 ObjId，通常只發給當前玩家更新狀態欄
		private void HandleBuffShield(byte[] data)
		{
			var r = new PacketReader(data);
			r.ReadByte(); // 69
			
			int time = r.ReadUShort(); // writeH(time)
			int type = r.ReadByte(); // writeC(type)
			r.ReadInt(); // writeD(0) - jp 新增字段
			
			EmitSignal(SignalName.BuffShieldReceived, time, type);
		}

		// 【JP協議對齊】致盲 - 對齊 jp S_CurseBlind.java
		// 結構: writeC(238), writeH(type) - type 0:OFF 1:自分以外見えない 2:周りのキャラクターが見える
		private void HandleBuffBlind(byte[] data)
		{
			var r = new PacketReader(data);
			r.ReadByte(); // 238
			
			int blindType = r.ReadUShort(); // writeH(type)
			
			EmitSignal(SignalName.BuffBlindReceived, blindType);
		}

			//buff end

			// ====================================================================
			// [SECTION END] Buffs: Speed/Aqua/Shield/Blind Status
			// ====================================================================


			// 变身 对应 S_ObjectPoly.java / S_ChangeShape.java
			// 结构 (旧): writeC(39), writeD(objId), writeH(gfx), writeC(mode), writeC(255), writeC(255)
			// 结构 (jp): writeC(164), writeD(objId), writeH(polyId), writeH(weaponTakeoff ? 0 : 29)
			private void ParseObjectPoly(PacketReader reader)
			{
				int objectId = reader.ReadInt();
				int gfxId = reader.ReadUShort();
				int gfxMode = -1;
				
				// jp 版本：剩餘 2 bytes (weaponTakeoff flag)
				if (reader.Remaining == 2)
				{
					reader.ReadUShort(); // weaponTakeoff (0 or 29)
				}
				else if (reader.Remaining >= 3)
				{
					// 舊版：mode + 255 + 255
					gfxMode = reader.ReadByte();
					reader.ReadByte();
					reader.ReadByte();
				}

				GD.Print($"[RX] Poly (39): Obj {objectId} -> Gfx {gfxId} Mode {gfxMode}");
				
				// 我们复用 ObjectVisualUpdated 信号，或者你可以定义一个新的
				// 这里假设你之前定义了 ObjectVisualUpdated (int id, int gfx, int action, int heading)
				// 如果没有 heading，我们就传 -1 表示不改变
				EmitSignal(SignalName.ObjectVisualUpdated, objectId, gfxId, gfxMode, -1);
			}

			// 正义值更新 对应 S_ObjectLawful.java
			// 结构: writeC(89), writeD(objId), writeD(lawful)
			private void ParseObjectLawful(PacketReader reader)
			{
				int objectId = reader.ReadInt();
				int lawful = reader.ReadInt(); // 注意：服务器这里用的是 writeD (4字节)

				// GD.Print($"[RX] Lawful (89): Obj {objectId} -> {lawful}");
				
				// 发送信号通知 GameEntity 更新颜色
				EmitSignal(SignalName.ObjectLawfulChanged, objectId, lawful);
			}


			/// <summary>
			/// 處理 Opcode 49：可能是 S_WarehouseItemList (普通倉庫) 或 S_ObjectPet (寵物倉庫列表)
			/// 區分方法：讀取 option，如果是 12 則是寵物倉庫
			/// </summary>
			private void ParseWarehouseOrPetWarehouse(PacketReader reader)
			{
				try
				{
					int npcId = reader.ReadInt();
					int itemCount = reader.ReadUShort();
					int option = reader.ReadByte();
					
					// option=12 表示寵物倉庫 (對齊服務器 S_ObjectPet.java 第一個構造函數)
					if (option == 12)
					{
						ParsePetWarehouseList(reader, npcId, itemCount);
					}
					else
					{
						// 普通倉庫
						ParseWarehouseList(reader, npcId, itemCount, option);
					}
				}
				catch (Exception e)
				{
					GD.PrintErr($"[Packet] Warehouse/PetWarehouse Error: {e.Message}");
				}
			}
			
			/// <summary>
			/// 解析寵物倉庫列表 (S_ObjectPet Opcode 49, option=12)
			/// 對齊服務器 S_ObjectPet.java 第一個構造函數
			/// </summary>
			private void ParsePetWarehouseList(PacketReader reader, int npcId, int itemCount)
			{
				var items = new Godot.Collections.Array();
				for (int i = 0; i < itemCount; i++)
				{
					var item = new Godot.Collections.Dictionary();
					item["invId"] = reader.ReadInt(); // 項圈的 inv_id
					item["type"] = reader.ReadByte();
					item["gfxid"] = reader.ReadUShort();
					item["bless"] = reader.ReadByte();
					item["count"] = reader.ReadInt();
					item["isDefinite"] = reader.ReadByte();
					item["name"] = reader.ReadString(); // 格式: "項圈 [Lv.X 寵物名]"
					items.Add(item);
				}
				reader.ReadInt(); // 消耗尾部 4 字節 (值 80，表示領取費用)
				
				EmitSignal(SignalName.PetWarehouseListReceived, npcId, items);
				GD.Print($"[Pet] 收到寵物倉庫列表: NpcId={npcId} Count={itemCount}");
			}
			
			// warehouse
			// [解析逻辑：仓库物品列表] 严格对齐 S_WarehouseItemList.java
			private void ParseWarehouseList(PacketReader reader, int npcId, int itemCount, int option)
			{
				var items = new Godot.Collections.Array();
				for (int i = 0; i < itemCount; i++)
				{
					var item = new Godot.Collections.Dictionary();
					item["uid"] = reader.ReadInt();
					item["type"] = reader.ReadByte();
					item["gfxid"] = reader.ReadUShort();
					item["bless"] = reader.ReadByte();
					item["count"] = reader.ReadInt();
					item["isid"] = reader.ReadByte();
					item["name"] = reader.ReadString();
					items.Add(item);
				}
				reader.ReadInt(); // 消耗尾部 4 字节 (值 30)

				// ✅ 【修復】打開倉庫窗口而不是對話窗口
				var context = new WindowContext { 
					NpcId = npcId, 
					Type = option, 
					ExtraData = items 
				};
				UIManager.Instance.Open(WindowID.WareHouse, context); 
			}



			// ====================================================================
			// [PARSER] Opcode 71: S_WorldStatPacket(type, objID)
			// 服务器源码 (S_WorldStatPacket.java):
			//   writeC(71);
			//   writeC(type);
			//   writeD(objID);
			// ====================================================================
			// 设计原则：
			// 1) 字段读取严格按服务器写入顺序。
			// 2) 若服务端存在尾部 padding/变体（你日志 Len=8 的情况），这里“吞掉剩余字节”以保证缓冲区对齐。
			// 3) 不引入新状态、不改动既有成功逻辑，仅提供可控日志，方便你继续对照服务器行为。
			private void ParseWorldStatPacket71(PacketReader reader, byte[] rawPacket)
			{
				// 结构： [71][type][objId(D)]
				int type = 0;
				int objId = 0;
				try
				{
					// 基于服务器写入顺序：type (C)
					type = reader.ReadByte();
					// objId (D)
					objId = reader.ReadInt();
				}
				catch (Exception ex)
				{
					// 只在该包解析失败时输出明确日志，避免污染其他模块。
					GD.PrintErr($"[RX][71] Parse failed: {ex.Message} (Len:{(rawPacket?.Length ?? 0)})");
					return;
				}

				// 吞掉尾部剩余字节（若存在）。
				// 说明：PacketReader 是基于 rawPacket 的读取器。这里不猜字段含义，只确保对齐。
				try
				{
					// rawPacket[0] 是 opcode，reader 已经 ReadByte() 过 opcode。
					// 已读 = 1(opcode) + 1(type) + 4(objId) = 6
					int expectedReadBytes = 6;
					int remain = (rawPacket != null) ? (rawPacket.Length - expectedReadBytes) : 0;
					if (remain > 0)
					{
						for (int i = 0; i < remain; i++) reader.ReadByte();
					}
				}
				catch
				{
					// 如果尾部不足/已读到末尾，说明包体正好符合 6 字节，无需额外处理。
				}

				// 解析结果日志：不推断用途，仅记录字段。
				GD.Print($"[RX][71] WorldStatPacket: type={type} objId={objId}");
			}



				// 宠物/召唤状态 具体解析方法
				private void ParseSummonStatus(PacketReader reader)
				{
					int petObjId = reader.ReadInt();
					int status = reader.ReadByte(); // 0:休息, 1:攻擊, 2:跟隨...
					bool isMine = reader.ReadByte() == 1; // 判定是否是自己的寵物

					GD.Print($"[Pet] ID:{petObjId} 狀態變更:{status} 是否歸屬玩家:{isMine}");
					
					// 如果是自己的寵物，通知 HUD 顯示/更新寵物小血條
					if (isMine) {
				// [FIX] Error CS0117: 现在 PetStatusChanged 已在顶部定义
					EmitSignal(SignalName.PetStatusChanged, petObjId, status);
					}
				}

				// 補全特殊能力狀態 (Opcode 38)
				// 實現「傳送戒指」或「變身戒指」的 UI 高亮至關重要
				private void ParseObjectAbility(PacketReader reader)
				{
					int type = reader.ReadByte(); // 1: 傳送, 2: 變身...
					bool isActive = reader.ReadByte() == 1;
					
					GD.Print($"[Ability] Type:{type} State:{isActive}");
					// 觸發信號更新 UI 面板
					EmitSignal(SignalName.ItemEquipStatusChanged, type, isActive); 
				}

				//補全 PVP 紫名系統 (Opcode 106)
				// 【服務器對齊】對齊服務器 S_PinkName.java: writeC(106), writeD(objId), writeH(time)
				private void ParsePinkName(PacketReader reader)
				{
					int objId = reader.ReadInt();
					int duration = reader.ReadUShort(); // 紫名持續時間（秒）
					
					GD.Print($"[PVP] Obj {objId} is Pink for {duration}s");
					// 【修復】發送專用信號，而不是使用 ObjectVisualUpdated
					EmitSignal(SignalName.ObjectPinkNameReceived, objId, duration);
				}

				// 更新等級
				private void UpdateMyLevel(int level)
				{
					var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
					if (boot?.MyCharInfo != null)
					{
						boot.MyCharInfo.Level = level;
						EmitSignal(SignalName.CharacterStatsUpdated, boot.MyCharInfo);
					}
				}
				
				// 更新經驗值
				// 輔助方法
				private void UpdateMyExp(long exp)
				{
					var boot = GetNodeOrNull<Client.Boot>("/root/Boot");
					if (boot?.MyCharInfo != null)
					{
						boot.MyCharInfo.Exp = (int)exp;
						EmitSignal(SignalName.CharacterStatsUpdated, boot.MyCharInfo);
					}
				}

				// 更新魔攻(SP)與魔抗(MR)
				private void UpdateMySpMr(int sp, int mr)
				{
					// SP/MR 通常需要顯示在 C 窗口
					GD.Print($"[Stats] SP: {sp}, MR: {mr}");
					// 發送信號給 UI 刷新
				}

	}// PacketHandler 类在这里结束！不要把扩展类写在这个括号里面

			// ------------------------------------------------------------
			// ✅ 修复：StringExtensions 必须是一个顶级静态类 (Top-level static class)
			// 它不能嵌套在 PacketHandler 里面
			// ------------------------------------------------------------

			// ============================================================================
			// [SECTION] Helpers: StringExtensions (Top-level static helper)
			// 说明：必须位于顶层(不嵌套在 PacketHandler 内)，避免编译/运行期错误。
			// ============================================================================

			public static class StringExtensions
			{
				public static string ReplaceFirst(this string text, string search, string replace)
				{
					if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search)) return text;
					
					int pos = text.IndexOf(search);
					if (pos < 0) return text;
					return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
				}
			}	

// ============================================================================
// [SECTION END] Helpers: StringExtensions
// ============================================================================

} // Namespace 在这里结束
