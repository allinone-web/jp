// ==================================================================================
// [FILE] Skins/CustomFantasy/CustomCharacterProvider.cs
// [NAMESPACE] Skins.CustomFantasy
// Visuals.cs（渲染執行）
// GameEntity.cs（狀態控制）、
// ListSprLoader.cs（數據解析）和 
// CustomCharacterProvider.cs（資源分發）
// [修復] 注入 PngInfoLoader 的偏移數據
// jan 27.11;57pm.
// ==================================================================================

using Godot;
using System.Collections.Generic;
using Client.Utility;
using Core.Interfaces;
using System.Threading.Tasks;

namespace Skins.CustomFantasy
{
	public class CustomCharacterProvider : ICharacterProvider
	{
		// 核心：從 sprites-138-new2.pak（單一 .pak 或舊 .idx+.pak）讀取，含 pak 內 sprite_offsets-138_update.txt
		private Sprite138PakLoader _pakLoader = new();
		private Dictionary<string, SpriteFrames> _framesCache = new();
		private readonly object _syncLock = new object();
		private HashSet<string> _loadingKeys = new();
		private static readonly HashSet<int> _loggedMagicNoAction = new HashSet<int>();
		private static readonly object _magicNoActionLogLock = new object();

		public CustomCharacterProvider() {
			LoadPak("res://Assets/sprites-138-new2");
		}

		/// <summary>指定 PAK 基底路徑建構（供 PakBrowser 切換 PAK 用），例如 res://Assets/sprites-138-new2</summary>
		public CustomCharacterProvider(string baseResOrPath) {
			LoadPak(baseResOrPath);
		}

		private void LoadPak(string baseResOrPath) {
			_pakLoader.Load(baseResOrPath);
		}

		// 獲取主體或單一圖層 (兼容舊調用)
		public SpriteFrames GetBodyFrames(int gfxId, int actionId, int heading) {
			// 主體自己既是目標也是參考
			return BuildLayer(gfxId, gfxId, actionId, heading);
		}

		// 【新架構】支持組件層借用主體骨架 (如陰影、服裝)
		public SpriteFrames GetBodyFrames(int gfxId, int referenceGfxId, int actionId, int heading) {
			return BuildLayer(gfxId, referenceGfxId, actionId, heading);
		}

		// 獲取武器圖層 (核心邏輯)
		/// <summary>
		/// 【服務器對齊】對齊服務器武器系統
		/// 服務器使用 106.weapon 字段定義武器 GfxId，客戶端從 list.spr 讀取
		/// 武器類型映射（對齊服務器 ItemsTable.java）：
		/// - 1:劍 (Sword), 2:斧 (Axe), 3:弓 (Bow), 4:矛 (Spear), 5:杖 (Wand)
		/// </summary>
		public SpriteFrames GetWeaponFrames(int bodyGfxId, int actionId, int heading, int weaponType) {
			var def = ListSprLoader.Get(bodyGfxId);
			// 【服務器對齊】武器類型映射：1:劍, 2:斧, 3:弓, 4:矛, 5:杖
			// 對齊服務器 ItemsTable.java 的武器類型定義
			if (def == null || weaponType < 1 || weaponType > 5) return null;
			// 【服務器對齊】讀取 list.spr 106.weapon 字段 (數組索引從0開始，所以 -1)
			// 服務器使用 106.weapon 定義武器 GfxId，客戶端從 SprDefinition.WeaponGfxs 讀取
			int weaponGfxId = def.WeaponGfxs[weaponType - 1]; 
			
			// 如果該角色沒有定義對應武器的圖檔，返回 null
			if (weaponGfxId <= 0) return null;

			// GD.Print($"[Provider] Gfx:{bodyGfxId} 匹配武器 Gfx:{weaponGfxId}, 類型:{weaponType}");
			return BuildLayer(weaponGfxId, bodyGfxId, actionId, heading);
		}

		public SpriteFrames GetEffectFrames(int effectId) => BuildLayer(effectId, effectId, 0, 0);
		
		public Vector2 GetOffset(int gfxId, int action) => Vector2.Zero;

		// --------------------------------------------------------------------
		// 核心構建邏輯 (整合版)
		// --------------------------------------------------------------------
		
		// ====================================================================
		// [輔助方法]
		// ====================================================================

		/// <summary>
		/// 構建圖層動畫。服裝/陰影/武器幀順序唯一規則（與 batch_merge_offsets.py 一致）：僅用 refDef 動作序列，
		/// 檔名後綴 -{fileAct}-{frameIdx} 與主體一致，僅前綴 SpriteId 不同（734/736/242 等）。
		/// </summary>
		/// <param name="gfxId">目標資源ID (圖片來源 SpriteId，如服裝 242)</param>
		/// <param name="refGfxId">參考資源ID (動作定義來源，主體如 240)</param>
		/// <param name="actionId">動作編號</param>
		/// <param name="heading">朝向</param>
		private SpriteFrames BuildLayer(int gfxId, int refGfxId, int actionId, int heading) {
			var targetDef = ListSprLoader.Get(gfxId);    // 圖片來源
			var refDef = ListSprLoader.Get(refGfxId);    // 動作定義來源（服裝/陰影/武器僅用此）

			// 1b. 【102.type(9) 物品】屬性分類為物品時，直接讀第一幀 (SpriteId-0-000.png) 顯示，例如 #7 adena、#13 cloak。不需其他複雜規則。
			if (targetDef != null && targetDef.Type == 9)
			{
				int loadId = targetDef.SpriteId;
				var tex = _pakLoader.GetTexture(loadId, 0, 0, out int dx, out int dy);
				if (tex != null)
				{
					if (targetDef.Attr == 8)
					{
						var processed = BlackToTransparentProcessor.ProcessTexture(tex);
						if (processed != null) tex = processed;
					}
					if (PngInfoLoader.TryGetFrame(loadId, 0, 0, out var info)) { dx = info.Dx; dy = info.Dy; }
					tex.SetMeta("spr_anchor_x", dx);
					tex.SetMeta("spr_anchor_y", dy);
					var sfStatic = new SpriteFrames();
					sfStatic.RemoveAnimation("default");
					sfStatic.AddAnimation("0");
					sfStatic.SetAnimationLoop("0", true);
					sfStatic.SetAnimationSpeed("0", 1.0f);
					sfStatic.AddFrame("0", tex, 1.0f);
					return sfStatic;
				}
				return null;
			}

			// 動畫播放是否為單幀／多幀、是否 8 方向：唯一依據為「有方向／無方向」，即 list.spr 動作括號內第一數字（1=有方向，0=無方向）。
			// 例如 0.fly(1 3,...) 中 1=有方向，須用 ListSprLoader.GetFileOffset(heading, seq.DirectionFlag) 取對應檔。不依 102.type 判斷。

			// 如果連參考對象都沒有，無法構建
			if (refDef == null) return null;

			// 2. 【新架構核心】確定動作序列（待機規則：先 ActionId=3，再 Name 含 breath/idle）
			// 【服務器對齊】對齊 ActionCodes.java 的動作常量定義
			// [104.attr(8) 魔法] 不需武器映射，僅一組動畫：優先 ActionId=0 (ACTION_Walk)，無則 ActionId=3 (ACTION_Idle)
			SprActionSequence seq;
			bool useBreathFallbackFirstFrameOnly = false;
			if (targetDef != null && targetDef.Attr == 8) {
				// 【服務器對齊】魔法優先使用 ActionId=0 (ACTION_Walk)，無則 ActionId=3 (ACTION_Idle)
				seq = ListSprLoader.GetActionSequence(targetDef, 0) ?? ListSprLoader.GetActionSequence(targetDef, 3);
				// 105.clothes: 服裝層無自身動作定義時，複用主身體(refDef)動作定義，圖仍從 targetDef.SpriteId 載入
				if (seq == null)
					seq = ListSprLoader.GetActionSequence(refDef, actionId)
						?? ListSprLoader.GetActionSequence(refDef, 0)
						?? ListSprLoader.GetActionSequence(refDef, 3);
				if (seq == null)
				{
					lock (_magicNoActionLogLock)
					{
						if (!_loggedMagicNoAction.Contains(gfxId))
						{
							_loggedMagicNoAction.Add(gfxId);
							GD.Print($"[Magic][Provider] GfxId:{gfxId} Attr=8 魔法 無 Action0/Action3 且 refDef 也無對應動作 返回 null (本 gfxId 僅記錄一次)");
						}
					}
					return null;
				}
			} else {
				// 【服務器對齊】服裝/陰影：list.spr 不定義動作序列，與主體整體設計、分層輸出，必須且僅能用主體(refDef)動作序列。主體自身時 refDef==targetDef。
				// 對齊 ActionCodes.java：動作 ID 映射到語義關鍵字（walk/attack/damage/idle/death）
				seq = ListSprLoader.GetActionSequence(refDef, actionId);
				if (seq == null && IsBreathAction(actionId)) {
					// 【服務器對齊】待機動作回退規則：對齊 ActionCodes.java 的動作常量
					// 3→0, 7→4, 14→11, 23→20, 27→24, 43→40
					int walkAction = GetWalkActionForBreath(actionId);
					if (walkAction >= 0) {
						seq = ListSprLoader.GetActionSequence(refDef, walkAction);
						if (seq != null) useBreathFallbackFirstFrameOnly = true;
					}
				}
				if (seq == null) return null;
			}

			string seqNameLower = (seq.Name ?? "").Trim().ToLowerInvariant();
			bool isIdleSequence = seqNameLower.Contains("breath") || seqNameLower.Contains("idle");

			// 3. 初始化 SpriteFrames（幀播放規則：循環則繼續播，非循環則播完停在第一幀）
			var sf = new SpriteFrames();
			sf.RemoveAnimation("default");
			// 【統一規則】動畫名稱：DirectionFlag=0（無方向）使用 "0"，DirectionFlag=1（有方向）使用 heading.ToString()
			// 此規則適用於所有動畫（魔法、弓箭、角色等），不允許特殊處理
			string animName = (seq.DirectionFlag == 0) ? "0" : heading.ToString();
			sf.AddAnimation(animName);
			bool loop = isIdleSequence || IsLoopingAction(actionId);
			sf.SetAnimationLoop(animName, loop); 

			// 4. 方向偏移與幀順序（唯一規則：seq 來自 refDef，與 batch_merge 檔名後綴一致）
			int offset = ListSprLoader.GetFileOffset(heading, seq.DirectionFlag);

			// 播放速率基準
			// 由於我們已經在 sf.AddFrame 中設置了正確的 RealDuration (DurationUnit * 40ms)
			// Godot 的 AnimatedSprite2D 默認 FPS 是 5，我們需要確保它按 RealDuration 播放。
			// 在 Godot 4 中，SpriteFrames 的每幀 duration 是相對於動畫 FPS 的倍數。
			// 為了讓 RealDuration (秒) 直接生效，我們將動畫 FPS 設為 1.0。
			// 攻擊速度的 幀速度，和整體速度，此公式和代碼 正確無誤。不准修改。
			sf.SetAnimationSpeed(animName, 1.0);

			// 【不允許修改】動畫幀播放規則：第一幀必須是 FrameIdx=1 的那一幀；依 FrameIdx 升序循環。若找不到 FrameIdx=1 則不播放。
			var sortedFrames = new List<SprFrame>(seq.Frames);
			sortedFrames.Sort((a, b) => a.FrameIdx.CompareTo(b.FrameIdx));
			int startIdx = -1;
			for (int i = 0; i < sortedFrames.Count; i++)
			{
				if (sortedFrames[i].FrameIdx == SprPlaybackRule.MinPlaybackFrameIdx) { startIdx = i; break; }
			}
			if (startIdx < 0) return null;
			// 播放順序：從 startIdx 到末尾，再接 0 到 startIdx-1（例：FrameIdx 0,1,2,3,4 → 播 1,2,3,4,0 循環）
			var order = new List<SprFrame>();
			for (int k = startIdx; k < sortedFrames.Count; k++) order.Add(sortedFrames[k]);
			for (int k = 0; k < startIdx; k++) order.Add(sortedFrames[k]);
			// [Magic 日誌] 首幀檔與幀數依「播放順序」（第一幀為 FrameIdx=1 的那一幀），與實際動畫一致
			if (targetDef != null && targetDef.Attr == 8 && order.Count > 0)
			{
				var f0 = order[0];
				string firstFrameFile = $"{targetDef.SpriteId}-{f0.ActionId + offset}-{f0.FrameIdx:D3}.png";
				GD.Print($"[Magic][Provider] GfxId:{gfxId} Attr=8 SpriteId:{targetDef.SpriteId} ActionId:{(seq.ActionId)} Name:{seq.Name} DirFlag:{seq.DirectionFlag} Heading:{heading} FileOffset:{offset} 首幀檔:{firstFrameFile} 幀數:{order.Count}");
			}
			for (int i = 0; i < order.Count; i++) {
				if (useBreathFallbackFirstFrameOnly && i > 0) break;
				var f = order[i];
				int fileAct = f.ActionId + offset;  // 與主體同 fileAct，batch_merge 同後綴
				int dx, dy;
				int loadId = targetDef != null ? targetDef.SpriteId : gfxId;  // 圖片來源前綴（242/240/736 等）
				var tex = _pakLoader.GetTexture(loadId, fileAct, f.FrameIdx, out dx, out dy);
				if (tex != null) {
					// 僅 104.attr(8) 魔法效果圖像套用黑色→透明
					if (targetDef != null && targetDef.Attr == 8)
					{
						var processed = BlackToTransparentProcessor.ProcessTexture(tex);
						if (processed != null) tex = processed;
					}
					if (PngInfoLoader.TryGetFrame(loadId, fileAct, f.FrameIdx, out var info)) { dx = info.Dx; dy = info.Dy; }
					tex.SetMeta("spr_anchor_x", dx);
					tex.SetMeta("spr_anchor_y", dy);
					tex.SetMeta("spr_file_name", $"{loadId}-{fileAct}-{f.FrameIdx:D3}.png");
					if (f.IsKeyFrame) tex.SetMeta("key", true);
					if (f.IsStepTick) tex.SetMeta("step", true);
					if (f.SoundIds != null && f.SoundIds.Count > 0) {
						var arr = new Godot.Collections.Array<int>();
						foreach (var s in f.SoundIds) arr.Add(s);
						tex.SetMeta("spr_sound_ids", arr);
					}
					if (f.EffectIds != null && f.EffectIds.Count > 0) {
						var arr = new Godot.Collections.Array<int>();
						foreach (var e in f.EffectIds) arr.Add(e);
						tex.SetMeta("effects", arr);
					}
					bool isMagicOnly = targetDef != null && targetDef.Attr == 8 && (!targetDef.ParsedMetadataIds.Contains(102) || targetDef.Type != 0);
					float durationForFrame = isMagicOnly ? f.RealDuration * 1.0f : f.RealDuration;
					sf.AddFrame(animName, tex, durationForFrame);
				}
			}
			return sf.GetFrameCount(animName) > 0 ? sf : null;
		}

		// ====================================================================
		// [核心修復] 補全缺失的輔助方法
		// ====================================================================

		/// <summary>
		/// 【服務器對齊】判斷是否為待機動作
		/// 對齊 ActionCodes.java：ACTION_Idle = 3, ACTION_SwordIdle = 7, ACTION_AxeIdle = 14, 
		/// ACTION_BowIdle = 23, ACTION_SpearIdle = 27, ACTION_StaffIdle = 43
		/// </summary>
		private bool IsBreathAction(int action) {
			return action == 3 || action == 7 || action == 14 || action == 23 || action == 27 || action == 43;
		}

		/// <summary>
		/// 【服務器對齊】待機動作對應的攻擊動作
		/// 對齊 ActionCodes.java：
		/// - ACTION_Idle (3) → ACTION_Attack (1)
		/// - ACTION_SwordIdle (7) → ACTION_SwordAttack (5)
		/// - ACTION_AxeIdle (14) → ACTION_AxeAttack (12)
		/// - ACTION_BowIdle (23) → ACTION_BowAttack (21)
		/// - ACTION_SpearIdle (27) → ACTION_SpearAttack (25)
		/// - ACTION_StaffIdle (43) → ACTION_StaffAttack (41)
		/// </summary>
		private int GetAttackActionForBreath(int breath) {
			switch(breath) {
				case 3: return 1;   // ACTION_Idle → ACTION_Attack
				case 7: return 5;   // ACTION_SwordIdle → ACTION_SwordAttack
				case 14: return 12; // ACTION_AxeIdle → ACTION_AxeAttack
				case 23: return 21; // ACTION_BowIdle → ACTION_BowAttack
				case 27: return 25; // ACTION_SpearIdle → ACTION_SpearAttack
				case 43: return 41; // ACTION_StaffIdle → ACTION_StaffAttack
				default: return 1;
			}
		}

		/// <summary>
		/// 【服務器對齊】待機/呼吸動作缺圖時，用對應「行走」動作的第一幀作為 fallback
		/// 對齊 ActionCodes.java：
		/// - ACTION_Idle (3) → ACTION_Walk (0)
		/// - ACTION_SwordIdle (7) → ACTION_SwordWalk (4)
		/// - ACTION_AxeIdle (14) → ACTION_AxeWalk (11)
		/// - ACTION_BowIdle (23) → ACTION_BowWalk (20)
		/// - ACTION_SpearIdle (27) → ACTION_SpearWalk (24)
		/// - ACTION_StaffIdle (43) → ACTION_StaffWalk (40)
		/// </summary>
		private int GetWalkActionForBreath(int breathActionId) {
			switch (breathActionId) {
				case 3:  return 0;  // ACTION_Idle → ACTION_Walk
				case 7:  return 4;  // ACTION_SwordIdle → ACTION_SwordWalk
				case 14: return 11; // ACTION_AxeIdle → ACTION_AxeWalk
				case 23: return 20; // ACTION_BowIdle → ACTION_BowWalk
				case 27: return 24; // ACTION_SpearIdle → ACTION_SpearWalk
				case 43: return 40; // ACTION_StaffIdle → ACTION_StaffWalk
				default: return -1;
			}
		}

		/// <summary>
		/// 【服務器對齊】判斷動作是否應循環播放
		/// 對齊 ActionCodes.java：循環動作包括 walk 和 idle
		/// - ACTION_Walk (0), ACTION_SwordWalk (4), ACTION_AxeWalk (11), ACTION_BowWalk (20), ACTION_SpearWalk (24), ACTION_StaffWalk (40)
		/// - ACTION_Idle (3), ACTION_SwordIdle (7), ACTION_AxeIdle (14), ACTION_BowIdle (23), ACTION_SpearIdle (27), ACTION_StaffIdle (43)
		/// 注意：死亡動作（ACTION_Die = 8）不應循環，應播放完整後停留在最後一幀
		/// </summary>
		private bool IsLoopingAction(int actionId)
		{
			// 【服務器對齊】對齊 ActionCodes.java 的動作常量
			// 循環動作：walk 和 idle（對齊服務器動作分類）
			return actionId == 0 || actionId == 3 ||   // ACTION_Walk, ACTION_Idle (空手)
				   actionId == 4 || actionId == 7 ||   // ACTION_SwordWalk, ACTION_SwordIdle (劍)
				   actionId == 11 || actionId == 14 || // ACTION_AxeWalk, ACTION_AxeIdle (斧)
				   actionId == 20 || actionId == 23 || // ACTION_BowWalk, ACTION_BowIdle (弓)
				   actionId == 24 || actionId == 27 || // ACTION_SpearWalk, ACTION_SpearIdle (矛)
				   actionId == 40 || actionId == 43;   // ACTION_StaffWalk, ACTION_StaffIdle (杖)
		}
	}
}
