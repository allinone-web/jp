# PakBrowser / Clothes 幀同步與對齊 — 修改記錄

用於回撤：每次修改記錄「原始內容」與「修改後內容」，驗證未達效果時可依此還原。

---

## 修改 1 — CustomCharacterProvider 服裝/陰影幀順序與主體一致

**日期**: 2025-01-31  
**問題**: #240 Death Knight 等有 clothes 角色，1.attack 後續幀 clothes 載入錯誤圖片（尤其方向 6）；0.walk 第 3 幀、30.Alt attack 多幀 clothes 輕微偏離。第一幀均正常，異常發生在後續幀。  
**原因**: BuildLayer 時 `seq = GetActionSequence(targetDef, actionId) ?? GetActionSequence(refDef, actionId)`，若服裝(242)自身有 action 定義且幀數/順序與主體(240)不同，則 clothes 的「播放順序」與 body 不一致，body.Frame 與 clothes.Frame 對齊時會錯幀（錯圖）。  
**修復**: 當 refDef != targetDef（服裝/陰影層）時，**優先使用 refDef（主體）的動作序列**，再 fallback targetDef，確保幀順序與主體一致，再從 targetDef 載入對應 (fileAct, frameIdx) 的圖。

### 原始內容（可回撤用）

```csharp
			} else {
				// [SprActionSequence 取得規則] 全局唯一：ListSprLoader.GetActionSequence（先 ActionId，再 Name 語義關鍵字）
				seq = ListSprLoader.GetActionSequence(targetDef, actionId)
					?? ListSprLoader.GetActionSequence(refDef, actionId);
```

### 修改後內容

```csharp
			} else {
				// [SprActionSequence 取得規則] 服裝/陰影層：優先用 refDef（主體）序列，幀順序與主體一致，避免錯幀錯圖（#240 1.attack 等）
				seq = ListSprLoader.GetActionSequence(refDef, actionId)
					?? ListSprLoader.GetActionSequence(targetDef, actionId);
```

**檔案**: `Skins/CustomFantasy/CustomCharacterProvider.cs`  
**回撤**: 將上述兩行還原為「先 targetDef 後 refDef」即可。

---

## 修改 1b — Breath fallback 的 walk 序列也優先用 refDef

**同一問題/修復**：待機/呼吸 fallback 時，walk 序列也改為優先用 refDef，再 targetDef，與主序列邏輯一致。

### 原始內容（可回撤用）

```csharp
						var walkSeq = ListSprLoader.GetActionSequence(targetDef, walkAction)
							?? ListSprLoader.GetActionSequence(refDef, walkAction);
```

### 修改後內容

```csharp
						var walkSeq = ListSprLoader.GetActionSequence(refDef, walkAction)
							?? ListSprLoader.GetActionSequence(targetDef, walkAction);
```

**回撤**: 將 walkSeq 兩行還原為先 targetDef 後 refDef 即可。

---

## 修改 2 — 服裝/陰影僅用主體序列，無 fallback（確定版）

**日期**: 2025-01-31  
**結論**: list.spr 不為服裝/陰影定義動作序列；服裝/陰影與主體整體設計、分層輸出，動作完全相同，必須且僅能用主體(refDef)動作序列。不需 fallback、不需臃腫代碼。

### 原始內容（修改 1 後的內容，可回撤用）

```csharp
			} else {
				// [SprActionSequence 取得規則] 服裝/陰影層：優先用 refDef（主體）序列，幀順序與主體一致，避免錯幀錯圖（#240 1.attack 等）
				seq = ListSprLoader.GetActionSequence(refDef, actionId)
					?? ListSprLoader.GetActionSequence(targetDef, actionId);
				// [待機/呼吸缺圖 fallback] ...
				if (seq == null && IsBreathAction(actionId)) {
					int walkAction = GetWalkActionForBreath(actionId);
					if (walkAction >= 0) {
						var walkSeq = ListSprLoader.GetActionSequence(refDef, walkAction)
							?? ListSprLoader.GetActionSequence(targetDef, walkAction);
						if (walkSeq != null) {
							seq = walkSeq;
							useBreathFallbackFirstFrameOnly = true;
						}
					}
				}
				if (seq == null) return null;
			}
```

### 修改後內容（確定版）

```csharp
			} else {
				// 服裝/陰影：list.spr 不定義動作序列，與主體整體設計、分層輸出，必須且僅能用主體(refDef)動作序列。主體自身時 refDef==targetDef。
				seq = ListSprLoader.GetActionSequence(refDef, actionId);
				if (seq == null && IsBreathAction(actionId)) {
					int walkAction = GetWalkActionForBreath(actionId);
					if (walkAction >= 0) {
						seq = ListSprLoader.GetActionSequence(refDef, walkAction);
						if (seq != null) useBreathFallbackFirstFrameOnly = true;
					}
				}
				if (seq == null) return null;
			}
```

**回撤**: 還原為上述「原始內容」即可。

---

## 修改 3 — 唯一規則、無重複、對齊 batch_merge

**日期**: 2025-01-31  
**確認**: 服裝/陰影/武器幀順序僅此一處定義（BuildLayer 內僅用 refDef 序列）；與 batch_merge_offsets.py 一致（檔名後綴 -{fileAct}-{frameIdx} 相同，僅前綴 SpriteId 不同）。無其他臃腫或重複定義。

- BuildLayer 註解改為單一規則說明（batch_merge 同後綴）。
- loadId 改為 `targetDef != null ? targetDef.SpriteId : gfxId`（圖片來源前綴明確）。
- fileAct 註解標明「與主體同 fileAct，batch_merge 同後綴」。
