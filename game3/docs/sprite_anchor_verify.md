# 如何確認 sprite_offsets 錨點在同一邏輯點（腳底）

全層統一錨點對齊時，主體、武器、Shadow、Clothes 的 **(dx, dy)** 應定義在同一邏輯點（例如都是腳底），對齊才會正確。

## 1. 約定

- **(dx, dy)** = 錨點在該紋理內的像素位置（左上角 0,0，y 向下為正）。
- 若錨點都在「腳底」：各層的 **dy** 會接近各自紋理的 **h**（底部），即 **dy/h ≈ 1**。
- 若錨點都在「紋理水平中心」：**dx ≈ w/2**。
- 各層紋理尺寸 (w, h) 不同，故 (dx, dy) 絕對值不必相同；比對時看「相對位置」（如 dy/h、dx - w/2）。

## 2. 方式一：執行時診斷（遊戲內）

在 `Client/Game/GameEntity.Visuals.cs` 中：

- 將 **`DEBUG_ANCHOR_ALIGN`** 設為 **`true`**（預設 `false`）。
- 執行遊戲，讓角色進入場景並播放動畫（例如 Walk）。
- 在 Godot 輸出視窗會看到前 3 個實體的首幀一行日誌，例如：

  ```
  [Anchor-Debug] ObjId=... GfxId=240 ... | Body dx=5 dy=-40 w=64 h=80 fromCenter=(-27,-80) dy/h=-0.50 | Shadow dx=1 dy=-3 w=48 h=20 fromCenter=(-23,-13) dy/h=-0.15 | Clothes[0] dx=33 dy=-30 ...
  ```

- **比對**：
  - **dy/h**：若主體、Shadow、Clothes 的 dy/h 都接近 **1**（或同一數值），表示錨點都在各自紋理的「底部」附近，即同一邏輯點（腳底）。
  - **fromCenter (dx-w/2, dy-h/2)**：可看錨點相對紋理中心；若三層都是「下方中央」，數值會有一致性（例如 dy - h/2 都為正且量級接近）。

確認完後請將 **`DEBUG_ANCHOR_ALIGN`** 改回 **`false`**，避免刷屏。

## 3. 方式二：離線腳本（比對 txt）

專案內腳本：

```bash
python3 Assets/scripts/check_sprite_anchor_align.py
# 或指定路徑與動作 ID
python3 Assets/scripts/check_sprite_anchor_align.py Assets/sprite_offsets-138_update.txt 10
```

會輸出同一動作（預設 10）下，主體 240、Shadow 241、Clothes 242 的 **FRAME 0 的 (dx, dy)**。  
腳本只讀 txt 的 (dx, dy)，不讀 (w, h)；要判斷「是否都在腳底」需搭配紋理高度 h，可再對照遊戲內 **DEBUG_ANCHOR_ALIGN** 的 dy/h。

## 4. 若發現錨點不在同一邏輯點

- 若 **dy/h** 差異大（例如主體 0.9、Shadow 0.2）：表示 sprite_offsets 裡主體錨點在腳底、Shadow 錨點在別處（如中央），需調整 **sprite_offsets** 或美術產出，使各層錨點定義一致（例如都設在腳底）。
- 遊戲端對齊公式已統一為「每層自己的 (dx, dy) 置於 node+BodyOffset」；數據一致後，無需再改程式。
