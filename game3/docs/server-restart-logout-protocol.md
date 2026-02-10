# 伺服器重登／退出協定說明

對齊 `server/` 的處理邏輯，供客戶端重登／Restart 行為參考。

## 進入世界 (Enter World)

- **客戶端發送**：Opcode **5** (`C_OPCODE_LOGINTOSERVER`) + 角色名 (readS)。
- **伺服器處理**：`C_LineageWorldJoin.java` → `CharacterTable.CharacterWorldJoin(lc, name)`。
- **條件**：
  - `lc.getSecurityVerification() >= 2`（通過登入驗證）。
  - `lc.getWorldJoinCount() <= 1`：**同一連線只允許一次進入世界**；若 `WorldJoinCount > 1` 會視為非法並 **封鎖 IP 並關閉連線**。
  - `WorldInstance.getPc(name) == null`：該角色名尚未在世界上（未重複登入）。
- **成功後**：建立 `PcInstance`、發送 `S_WorldJoin`、背包／技能／狀態等，並 `addPc` 到世界。

## 退出遊戲 (Quit)

- **客戶端發送**：Opcode **15** (`C_OPCODE_QUITGAME`)。
- **伺服器處理**：`C_QuitGame.java` → 解鎖血盟倉庫（若使用中）→ **`lc.close()`**。
- **結果**：**連線被關閉**，該帳號的 `PcInstance` 會由 `HpMpTimer` 等邏輯在偵測到 `!pc.getClient().isConnected()` 時呼叫 `removePc(pc)` 並清理。

## Restart（客戶端流程：退回選角、不送 Op 15）

- 客戶端 **Restart** 採用「保持連線、退回角色列表」流程（不採用伺服器「Op 15 徹底退出」）：
  1. **不發送 Opcode 15**：連線保持，伺服器端舊角色仍存在，由伺服器 `RemoveCodeCountTimer` 每秒將 `WorldJoinCount` 歸零。
  2. **清空世界狀態**：`GameWorld.ClearWorldState()` 清空實體、玩家鎖定、技能掩碼等，避免新角色看到舊角色技能／攝影機錯亂。
  3. **退回角色列表**：`Boot.ToCharacterSelectScene()` 載入選角場景；使用者選新角色後送 Op 5，以**新角色**進世界（資料為新角色）。
- **注意**：若在 Restart 後**短時間內**（約 1 秒內）立刻選角並送 Op 5，伺服器 `WorldJoinCount` 可能尚未被歸零，有機會觸發 `WorldJoinCount > 1` 而封鎖 IP。建議 Restart 後稍候約 1 秒再選角進入。
- 若需「徹底退出並回到登入畫面」，可另行使用 `Boot.Action_QuitGame()` + `Boot.ToLoginScene()`（例如選單「登出」）。

## 客戶端已做的重登相關修復

1. **技能列表混合**：在 `ClearWorldState()` 中清空 `_skillMasks`，並若技能視窗已開啟則刷新為空，避免先登法師、Restart 後選妖精仍顯示法師魔法。
2. **攝影機**：`ClearWorldState()` 已將 Camera2D reparent 回 GameWorld；`SetupPlayerCamera()` 在鎖定新玩家時會 `MakeCurrent()`，確保重登後鏡頭跟隨新角色。
