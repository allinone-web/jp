// ==================================================================================
// [FILE] Client/Utility/ListSprLoader.cs
// [NAMESPACE] Client.Utility
// [DESCRIPTION]
//   负责解析 list.spr 文件

// Visuals.cs（渲染執行）
// GameEntity.cs（狀態控制）、
// ListSprLoader.cs（數據解析）和 
// CustomCharacterProvider.cs（資源分發）

//   負責解析 list.spr 文件，將服務端 GfxID 映射至物理 Spr 資源與動作序列。
//   支持元數據解析：101(陰影), 105(服裝), 106(武器), 110(幀率)。


//   1. 修正了負數 ActionId (-1) 無法解析的問題。
//   2. 增加了 TryParse 容錯，防止 malformed token 導致啟動崩潰。
//   3. 解決了 Godot.FileAccess 與 System.IO.FileAccess 的命名空間衝突。


// [修復版] 
// 1. 保持坐標對齊邏輯 (SyncLayerFrame 已包含 UpdateLayerOffset)。
// 2. 為 105.clothes 層增加 Shader，實現黑色透明+發光疊加效果。


// #167	4	energy bolt 0.fly(1 4,0.3:3 0.0:2<97 0.1:3 0.2:2)
//這是魔法光劍的音效， <97   這個是音效文件wav。
// < 表示音效  [ 也是音效。

// ==================================================================================

using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// 使用別名解決歧義
using GFile = Godot.FileAccess;

namespace Client.Utility
{
	/// <summary>
	/// 【全局約定，不允許修改】動畫幀播放規則：第一幀必須是 FrameIdx=0 的那一幀，然後按 FrameIdx 升序循環。
	/// 排序後找到 FrameIdx == MinPlaybackFrameIdx 的幀作為起點；若找不到則不播放。
	/// 對整個遊戲所有動畫（含魔法、攻擊、行走等）均生效，僅靜止圖片除外。見 README §6.1.1。
	/// 文檔寫是FrameIdx=1， 更新為FrameIdx=0 才是正確的。
	/// </summary>
	public static class SprPlaybackRule
	{
		/// <summary>注意：本段不可以刪除。不允許修改。第一幀必須為此值；排序後從 FrameIdx == MinPlaybackFrameIdx 的幀開始播放並循環，若無則停止。不允許修改。</summary>
		public const int MinPlaybackFrameIdx = 0;
	}

	public class SprFrame {
		public int ActionId;     // A: 文件動作前綴 (例如 24.0:4 中的 24)
		public int FrameIdx;     // B: spr內幀索引 (例如 24.3:4 中的 3)，播放順序按此值升序；第一幀為 FrameIdx=1，跳過 1 之前的幀
		public int DurationUnit; // C: 時間單位 (例如 24.0:4 中的 4)
		public float RealDuration;
		public bool IsKeyFrame;  // '!' 符號: 判定命中點；攻擊者播到此幀時觸發被攻擊者播放 2.damage 僵硬動畫（Visuals→OnAnimationKeyFrame→HandleEntityAttackHit）
		public bool IsStepTick;  // '>' 符號: 判定座標位移點
		public int RedirectId = -1; // 動作跳轉（若有其他符號定義）；'<' 與 '[' 均為幀音效，已解析進 SoundIds
		public List<int> SoundIds = new(); // '[' 與 '<' 符號: 幀音效（如 [86、<97）
		public List<int> EffectIds = new(); // ']' 符號: 幀內觸發特效，如 ]212 表示加載 gfxId=212（刀光等）
	}

	public class SprActionSequence
	{
		public int ActionId;     // 邏輯 ID (如 0 代表 walk)
		public string Name;      // 動作名
		public int DirectionCount; // 補全此屬性修復 Debug.cs 報錯
		public int DirectionFlag; // 1=有向(8方向), 0=無向
		public List<SprFrame> Frames = new();
	}

	public class SprDefinition
	{
		public int GfxId;    // 服務端邏輯 ID
		public int SpriteId; // 用於拼文件名的物理文件名 ID
		public string Name;
		public Dictionary<int, SprActionSequence> Actions = new();
		
		// Metadata (100+)
		public int ShadowId = -1;             // 101: 陰影編號
		public int Type = 0;                  // 102: 類型
		public int Attr = 0;                  // 104: 屬性
		public List<int> ClothesIds = new();  // 105: 關聯服裝
		public int[] WeaponGfxs = new int[5]; // 106: 0=劍, 1=斧, 2=弓, 3=矛, 4=杖
		public Dictionary<int, int> EffectChain = new(); // 109 鏈式特效 (結束後播下一個)
		public int Framerate = 24;            // 110: 基礎播放幀率
		/// <summary>list.spr 中該角色實際出現過的元數據 ID（101,102,104,105,106,109 等），用於篩選</summary>
		public HashSet<int> ParsedMetadataIds = new();
	}

	// =====================================================================
	// [SECTION] Loader Logic
	// =====================================================================

	public static class ListSprLoader 
	{
		private static readonly Dictionary<int, SprDefinition> _cache = new();

		/// <summary>
		/// 加載並解析 list.spr 配置文件
		/// </summary>
		public static void Load(string path) 
		{
			if (!GFile.FileExists(path)) {
				GD.PrintErr($"[ListSprLoader] 找不到路徑: {path}");
				return;
			}
			_cache.Clear();
			string content = GFile.GetFileAsString(path);
			
			using StringReader reader = new StringReader(content);
			string line;
			SprDefinition currentDef = null;

			while ((line = reader.ReadLine()) != null) 
			{
				line = line.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

				// 處理 ID 定義頭部 (#)
				if (line.StartsWith("#")) {
					currentDef = ParseHeader(line);
					if (currentDef != null) 
					{
						_cache[currentDef.GfxId] = currentDef;
						if (currentDef.GfxId == 167) GD.Print($"[ListSprLoader] Found Gfx 167 definition: {line}");
					}
					continue;
				}

				// 處理動作與元數據正文
				if (currentDef != null) ParseLineBody(currentDef, line);
			}
			GD.Print($"[ListSprLoader] 解析成功，共加載 {_cache.Count} 個資源定義");
		}

		private static SprDefinition ParseHeader(string line) 
		{
			var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2) return null;
			
			string idStr = parts[0].TrimStart('#');
			int gfxId = int.Parse(idStr);
			int spriteId = gfxId;

			// 處理等號邏輯，例如 #2356 56=1128
			if (parts[1].Contains("=")) {
				string rightSide = parts[1].Split('=')[1];
				spriteId = int.Parse(rightSide);
			}

			return new SprDefinition { 
				GfxId = gfxId, 
				SpriteId = spriteId, 
				Name = parts.Length > 2 ? parts[2] : "Unknown" 
			};
		}

		/// <summary>
		/// 【服務器對齊】8 方向標準映射表（對齊服務器方向系統）
		/// 服務器使用 8 方向系統（0-7），客戶端需要將 heading 映射到文件後綴偏移
		/// 對齊服務器 L1Object.getHeading() 和方向計算邏輯
		/// </summary>
		public static int GetFileOffset(int heading, int dirFlag) 
		{
			// 【服務器對齊】無向動作（dirFlag=0）永遠讀取基礎文件（無方向偏移）
			if (dirFlag == 0) return 0;
			// 【服務器對齊】8 方向映射（對齊服務器方向系統）
			// 服務器方向：0=正上, 1=右上, 2=正右, 3=右下, 4=正下, 5=左下, 6=正左, 7=左上
			return heading switch {
				7 => 0, // 左上 (基準，對齊服務器 heading=7)
				0 => 1, // 正上 (對齊服務器 heading=0)
				1 => 2, // 右上 (對齊服務器 heading=1)
				2 => 3, // 正右 (對齊服務器 heading=2)
				3 => 4, // 右下 (對齊服務器 heading=3)
				4 => 5, // 正下 (對齊服務器 heading=4)
				5 => 6, // 左下 (對齊服務器 heading=5)
				6 => 7, // 正左 (對齊服務器 heading=6)
				_ => 0
			};
		}

		private static void ParseLineBody(SprDefinition def, string line) {
			// 增強正則：支持動作名包含空格
			var matches = Regex.Matches(line, @"(\d+)\.([a-zA-Z0-9_\s]+)\(([^)]+)\)");
			foreach (Match m in matches) {
				int id = int.Parse(m.Groups[1].Value);
				string name = m.Groups[2].Value.Trim();
				string content = m.Groups[3].Value.Trim();

				if (id < 100) ParseAction(def, id, name, content);
				else ParseMetadata(def, id, content);
			}
		}

		private static void ParseAction(SprDefinition def, int id, string name, string content) {
			var parts = content.Split(',');
			if (parts.Length < 2) return;

			var header = parts[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (header.Length < 1) return;

			int dirFlag = 0;
			int.TryParse(header[0], out dirFlag);

			var seq = new SprActionSequence { ActionId = id, Name = name, DirectionFlag = dirFlag, DirectionCount = dirFlag };
			// 【不允許修改】動畫幀播放規則：從 FrameIdx=1 開始、依 FrameIdx 順序；跳過 1 之前的幀（如 0 或 8）；見 README §6.1.1
			var tokens = parts[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var t in tokens) {
				var frame = ParseFrameToken(t, def.Framerate);
				if (frame != null) seq.Frames.Add(frame);
			}
			def.Actions[id] = seq;
		}

		private static SprFrame ParseFrameToken(string token, int fps) {
			try {
				SprFrame f = new();
				string work = token;

				// 1. 剝離修飾符
				if (work.Contains("!")) { f.IsKeyFrame = true; work = work.Replace("!", ""); }
				if (work.Contains(">")) { f.IsStepTick = true; work = work.Replace(">", ""); }
				
				// 2. 解析 [音效 (支持多個)
				var sMatches = Regex.Matches(work, @"\[(\d+)");
				foreach (Match m in sMatches) {
					if (int.TryParse(m.Groups[1].Value, out int sid)) f.SoundIds.Add(sid);
					work = work.Replace(m.Value, "");
				}

				// 3. 解析 ]特效
				var eMatches = Regex.Matches(work, @"\](\d+)");
				foreach (Match m in eMatches) {
					if (int.TryParse(m.Groups[1].Value, out int eid)) f.EffectIds.Add(eid);
					work = work.Replace(m.Value, "");
				}

				// 4. 解析 <音效 (你的數據裡用 <97 表示音效 97)
				if (work.Contains("<")) {
					int idx = work.IndexOf("<");
					string sub = work.Substring(idx + 1);
					if (int.TryParse(sub, out var sid))
					{
						f.SoundIds.Add(sid);
					}
					work = work.Substring(0, idx);
				}

				// 5. 解析核心 A.B:C (支持 A 為負數)
				// 正則解釋：(?<A>-?\d+) 匹配可選負號的數字
				var mainMatch = Regex.Match(work, @"(?<A>-?\d+)\.(?<B>\d+):(?<C>\d+)");
				if (mainMatch.Success) {
					f.ActionId = int.Parse(mainMatch.Groups["A"].Value);
					f.FrameIdx = int.Parse(mainMatch.Groups["B"].Value);
					f.DurationUnit = int.Parse(mainMatch.Groups["C"].Value);
					
					// 換算時長:
					// - 基準單位: DurationUnit * 40ms (與原版 Lineage 完全一致)
					// - 110.framerate 只用來控制「全局加速/減速」，不再在這裡二次放大（避免整體過快）
					// [核心修復] 確保所有動作（包括 walk）嚴格遵循 DurationUnit * 40ms
					// 攻擊速度的 幀速度，和整體速度，此公式和代碼 正確無誤。不准修改。
					f.RealDuration = (f.DurationUnit * 40.0f) / 1000.0f;
					return f;
				}
			} catch {
				// 即使單個 token 壞了，也只是返回 null，不崩潰
			}
			return null;
		}

		/// <summary>
		/// 【服務器對齊】解析 list.spr 元數據（100+ ID）
		/// 對齊服務器 SprTable.java 和 ActionCodes.java 的動作分類
		/// </summary>
		private static void ParseMetadata(SprDefinition def, int id, string content) 
		{
			def.ParsedMetadataIds.Add(id);
			var vals = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			switch (id) {
				case 101: // 陰影編號
					def.ShadowId = int.Parse(content); 
					break;
				case 102: // 類型（對齊服務器 SprTable.java 的類型判斷）
					// 服務器使用 102.type 判斷實體類型（0=角色, 3=怪物, 9=物品等）
					def.Type = int.Parse(content); 
					break;
				case 104: // 104.attr(N)：屬性。N=8 表示魔法定義（CustomCharacterProvider 依 Attr==8 做去黑/動作選取）
					def.Attr = int.Parse(content);
					// if (def.Attr == 8) GD.Print($"[Magic][ListSpr] GfxId:{def.GfxId} 104.attr(8) 魔法定義 SpriteId:{def.SpriteId}");
					break;
				case 105: // Clothes（服裝關聯）
					foreach (var v in vals) def.ClothesIds.Add(int.Parse(v));
					if (def.ClothesIds.Count > 1 && def.ClothesIds[0] == def.ClothesIds.Count - 1) def.ClothesIds.RemoveAt(0);
					break;
				case 106: // Weapons（武器 GfxId 映射）
					// 【服務器對齊】對齊服務器武器系統
					// 服務器使用 106.weapon 定義武器 GfxId，客戶端從此讀取
					// 數組索引：0=劍, 1=斧, 2=弓, 3=矛, 4=杖（對齊 ItemsTable.java 武器類型）
					for (int i = 0; i < Math.Min(vals.Length, 5); i++) def.WeaponGfxs[i] = int.Parse(vals[i]);
					break;
				case 109: // Effect Chain (a b)（鏈式特效）
					if (vals.Length >= 2) def.EffectChain[int.Parse(vals[0])] = int.Parse(vals[1]);
					break;
				case 110: // Framerate（基礎播放幀率）
					def.Framerate = int.Parse(content); 
					break;
			}
		}

		public static List<int> GetAllGfxIds() => new List<int>(_cache.Keys);
		public static SprDefinition Get(int gfxId) => _cache.GetValueOrDefault(gfxId);

		// =====================================================================
		// [SprActionSequence 取得規則] 全局唯一：先 ActionId，再 Name 語義關鍵字
		// 玩家：依武器有 0/4/11/20/24/40(walk) 等 6 套外觀，已存在不變。
		// 怪物：部分無空手外觀（如弓箭手只有 20.walk bow），需依 Name 搜 walk/attack/damage/breath 等。
		// 關鍵字與 list.spr 對齊：walk, attack, damage, breath, idle, death, get, throw, wand, spell
		// =====================================================================

		/// <summary>
		/// 【服務器對齊】依請求的 actionId 回傳對應的語義關鍵字（用於 Name 搜尋）
		/// 參考服務器 ActionCodes.java 的動作常量定義：
		/// - ACTION_Walk = 0, ACTION_Attack = 1, ACTION_Damage = 2, ACTION_Idle = 3
		/// - ACTION_SwordWalk = 4, ACTION_SwordAttack = 5, ACTION_SwordDamage = 6, ACTION_SwordIdle = 7
		/// - ACTION_Die = 8
		/// - ACTION_AxeWalk = 11, ACTION_AxeAttack = 12, ACTION_AxeDamage = 13, ACTION_AxeIdle = 14
		/// - ACTION_Pickup = 15, ACTION_Throw = 16, ACTION_Wand = 17
		/// - ACTION_SkillAttack = 18, ACTION_SkillBuff = 19
		/// - ACTION_BowWalk = 20, ACTION_BowAttack = 21, ACTION_BowDamage = 22, ACTION_BowIdle = 23
		/// - ACTION_SpearWalk = 24, ACTION_SpearAttack = 25, ACTION_SpearDamage = 26, ACTION_SpearIdle = 27
		/// - ACTION_StaffWalk = 40, ACTION_StaffAttack = 41, ACTION_StaffDamage = 42, ACTION_StaffIdle = 43
		/// - ACTION_DaggerWalk = 46, ACTION_DaggerAttack = 47, ACTION_DaggerDamage = 48, ACTION_DaggerIdle = 49
		/// - ACTION_TwoHandSwordWalk = 50, ACTION_TwoHandSwordAttack = 51, ACTION_TwoHandSwordDamage = 52, ACTION_TwoHandSwordIdle = 53
		/// </summary>
		private static void GetSemanticKeywords(int actionId, List<string> outKeywords)
		{
			outKeywords.Clear();
			switch (actionId)
			{
				// 【服務器對齊】移動動作（對齊 ActionCodes.java ACTION_Walk/SwordWalk/BowWalk/SpearWalk/StaffWalk）
				case 0: case 4: case 20: case 24: case 40: 
					outKeywords.Add("walk"); 
					break;
				case 11: 
					// 【關鍵修復】11 可能是 walk（ACTION_AxeWalk = 11）或 death（ACTION_Die = 8 + 3 = 11），需要同時支持兩種映射
					outKeywords.Add("walk"); 
					outKeywords.Add("death"); 
					break;
				// 【服務器對齊】攻擊動作（對齊 ActionCodes.java ACTION_Attack/SwordAttack/BowAttack/SpearAttack/StaffAttack）
				case 1: case 5: case 12: case 21: case 25: case 41: 
					outKeywords.Add("attack"); 
					break;
				// 【服務器對齊】受擊動作（對齊 ActionCodes.java ACTION_Damage/SwordDamage/BowDamage/SpearDamage/StaffDamage）
				case 2: case 6: case 13: case 22: case 26: case 42: 
					outKeywords.Add("damage"); 
					break;
				// 【服務器對齊】待機動作（對齊 ActionCodes.java ACTION_Idle/SwordIdle/BowIdle/SpearIdle/StaffIdle）
				// 注意：ACTION_HideIdle = 14（隱身待機）與 ACTION_AxeIdle = 14 共用 ID
				case 3: case 7: case 14: case 23: case 27: case 28: case 31: case 43: 
					// 【關鍵修復】添加 28 和 31 的映射，支持 28.standby 動作（28 + 3 = 31）
					// 這些動作有8方向，應該通過語義關鍵字（standby/idle）找到對應的序列
					outKeywords.Add("breath"); 
					outKeywords.Add("idle"); 
					outKeywords.Add("standby");
					// 【服務器對齊】ACTION_HideIdle = 14 是隱身狀態的待機動作
					// 如果實體處於隱身狀態，應該使用 "hide" 關鍵字查找
					if (actionId == 14) outKeywords.Add("hide");
					break;
				// 【服務器對齊】死亡動作（對齊 ActionCodes.java ACTION_Die = 8）
				case 8: 
					outKeywords.Add("death"); 
					break;
				// 【服務器對齊】其他動作（對齊 ActionCodes.java）
				case 15: outKeywords.Add("get"); break;      // ACTION_Pickup = 15
				case 16: outKeywords.Add("throw"); break;   // ACTION_Throw = 16
				case 17: outKeywords.Add("wand"); break;   // ACTION_Wand = 17
				// 【服務器對齊】魔法動作（對齊 ActionCodes.java ACTION_SkillAttack = 18, ACTION_SkillBuff = 19）
				case 18: case 19: 
					outKeywords.Add("spell"); 
					break;
			}
		}

		/// <summary>
		/// 依語義關鍵字在 def.Actions 中搜尋第一個 Name 包含任一關鍵字的序列（不分大小寫）。
		/// </summary>
		private static SprActionSequence FindSequenceBySemanticName(SprDefinition def, int actionId)
		{
			if (def == null) return null;
			var keywords = new List<string>();
			GetSemanticKeywords(actionId, keywords);
			if (keywords.Count == 0) return null;
			foreach (var kv in def.Actions)
			{
				string nameLower = (kv.Value?.Name ?? "").Trim().ToLowerInvariant();
				foreach (var kw in keywords)
					if (nameLower.Contains(kw)) return kv.Value;
			}
			return null;
		}

		/// <summary>
		/// [唯一取得入口] 先依 ActionId 查，若無則依 Name 語義關鍵字查。全專案僅此一處設定此規則。
		/// </summary>
		public static SprActionSequence GetActionSequence(SprDefinition def, int actionId)
		{
			if (def == null) return null;
			if (def.Actions.TryGetValue(actionId, out var seq)) return seq;
			return FindSequenceBySemanticName(def, actionId);
		}

		/// <summary>
		/// 判斷指定 SprDefinition 是否定義了某個 ActionId（例如 8.Death）。
		/// 【死亡回退規則專用】請勿刪除或重複實作本方法。
		/// </summary>
		public static bool HasAction(SprDefinition def, int actionId)
		{
			if (def == null) return false;
			return def.Actions.ContainsKey(actionId);
		}

		/// <summary>
		/// 判斷 list.spr 中該 GfxId 的 Action0 是否為飛行（名稱含 "fly"）。
		/// 用於統一判定飛行魔法（光箭 167、燃燒的火球 171 等），客戶端先播飛行段再連貫落點。
		/// </summary>
		public static bool IsAction0Fly(int gfxId)
		{
			var def = Get(gfxId);
			if (def == null) return false;
			if (!def.Actions.TryGetValue(0, out var seq) || seq == null) return false;
			return (seq.Name ?? "").Trim().ToLowerInvariant().Contains("fly");
		}

		/// <summary>
		/// 判斷 list.spr 中該 GfxId 的 Action0 是否為有方向（DirectionFlag==1）。
		/// 用於區分「單向群體魔法」（如極光雷電 170：一條路上的怪物）與「全方向群體魔法」（如燃燒的火球 171：範圍內全部）。
		/// </summary>
		public static bool IsAction0Directional(int gfxId)
		{
			var def = Get(gfxId);
			if (def == null) return false;
			if (!def.Actions.TryGetValue(0, out var seq) || seq == null) return false;
			return seq.DirectionFlag == 1;
		}

		// =====================================================================
		// [待機規則] 唯一入口：依 ActionId=3 或 Name(breath/idle) 解析待機序列
		// 1. 先取 ActionId=3；若無則依 Name 查 "breath" / "idle" 或包含其一
		// 2. 若皆無則回傳 null，呼叫端不做待機
		// =====================================================================
		public static SprActionSequence GetIdleSequence(SprDefinition def)
		{
			if (def == null) return null;
			if (def.Actions.TryGetValue(3, out var seq3)) return seq3;
			foreach (var kv in def.Actions)
			{
				string name = (kv.Value?.Name ?? "").Trim().ToLowerInvariant();
				if (name.Contains("breath") || name.Contains("idle")) return kv.Value;
			}
			return null;
		}
	}
}
