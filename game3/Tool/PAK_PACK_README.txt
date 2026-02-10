================================================================================
單一 .pak 打包/解包工具 (專案根目錄 Tool/)
================================================================================

【用途】
- 僅輸出一個 .pak 檔（無 .idx），支援長檔名、加密索引。
- 格式：[4B 索引區長度][加密索引][檔案資料]。索引內檔名變長（2B+UTF8）。
- 登入素材 (Img182)、角色圖 (png138) 等皆可用此工具打包並加密。

【加密/解密】
- 索引區使用 Client/Utility/L1PakTools（Map1～Map5.bin）加密/解密。
- 解密表從 res://Assets/assets_maps/ 載入。打包預設加密索引。

【產生登入素材 pak (Img182)】
1. 在 Godot 中執行場景：res://Tool/PakPackTool.tscn
2. 點擊「打包: img182 → Img182.pak（單一 .pak 加密）」
   或 指令列：godot --path . --headless --run-scene res://Tool/PakPackTool.tscn -- --pack-once
3. 完成後僅產生 Assets/Img182.pak（單一檔，無 .idx）
4. 遊戲從 Img182.pak 讀取（PakArchiveReader 支援單一 .pak 與舊 .idx+.pak）

【產生角色/怪物/魔法圖 pak (png138 → sprites-138-new2)】
1. 執行場景 res://Tool/PakPackTool.tscn
2. 點擊「png138 打包 → Assets/sprites-138-new2.pak」
   - 輸入：PNG 目錄、sprite_offsets-138_update.txt（路徑在 Tool/PakPackToolScene.cs）
   - 輸出：僅 Assets/sprites-138-new2.pak（含 PNG + sprite_offsets-138_update.txt）
3. 解包：點擊「解包: sprites-138-new2.pak → png138out」可還原為 PNG + txt

【sprite_offsets 檔名】
- 打包與讀取端統一使用：sprite_offsets-138_update.txt（帶副檔名避免出錯）

【AssetManager 圖片來源】
- 登入/選角/創角 的圖片由 AssetManager 從 res://Assets/Img182.pak 載入。

【角色/怪物/魔法圖來源】
- CustomCharacterProvider 從 res://Assets/sprites-138-new2.pak 載入 PNG，
  並從 pak 內 sprite_offsets-138_update.txt 載入偏移。

================================================================================
