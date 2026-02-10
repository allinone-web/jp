# 校準流程：自動計算 Shadow/Clothes 微調

讓遊戲匯出每朝向的 (dx,dy,w,h)，再用 Python 依「腳底對齊」算出最優微調並輸出 C# 程式碼。

## 1. 遊戲內匯出

1. 進入遊戲，用**玩家角色**（有 Body / Shadow / Clothes）。
2. **旋轉角色**至朝向 **0**（北），按 **F8**。
3. 再旋轉至 **1, 2, 3, 4, 5, 6, 7**，各按一次 **F8**。
4. 匯出檔寫在 **user://calibration_anchor.json**（Godot 使用者目錄）。
   - macOS: `~/Library/Application Support/Godot/app_userdata/<project_name>/calibration_anchor.json`
   - 或遊戲內日誌會印出路徑。
5. 將 **calibration_anchor.json** 複製到專案目錄（例如 `Assets/` 或專案根目錄），方便給 Python 讀。

## 2. 執行 Python 腳本

```bash
# 若檔在專案根目錄
python3 Assets/scripts/calibration_to_tweak.py calibration_anchor.json

# 若檔在 Assets/
python3 Assets/scripts/calibration_to_tweak.py Assets/calibration_anchor.json
```

腳本會輸出一段 **C# 的 GetShadowClothesHeadingTweak**（含 `switch (Heading)`）。

## 3. 貼回程式碼

1. 開啟 `Client/Game/GameEntity.Visuals.cs`。
2. 找到 **GetShadowClothesHeadingTweak** 方法。
3. 用腳本輸出的 **switch** 區塊替換掉原本的 `switch (Heading) { ... }`（保留方法簽名與 `shadowTweak`/`clothesTweak` 的宣告與 `switch` 外層括號）。

## 4. 可選：用圖片再微調

若你有一批**截圖**（例如每個朝向一張，標好 Body/Shadow/Clothes 的腳底位置），可以：

- 手動量出每張圖裡「腳底」的像素差（Shadow、Clothes 相對 Body）。
- 把這些差值換算成與目前公式的差（tweak），再微調腳本輸出的數值或直接改 C# 裡的 `shadowTweak`/`clothesTweak`。

目前流程以「遊戲內 F8 匯出 + 腳本計算」為主；圖片可作為事後微調的依據。
