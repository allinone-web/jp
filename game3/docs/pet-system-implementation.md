# 寵物系統實現報告

## 一、已實現功能

### 1. 抓寵物流程
- ✅ **攻擊怪物**：玩家可以攻擊狗/狼等可馴服的怪物
- ✅ **喂食邏輯**：實現了 `FeedMonster` 方法，支持使用物品（肉，item id=23 或 331）喂給怪物
- ✅ **自動移動**：如果距離太遠，會自動走向目標後再喂食
- ✅ **封包對齊**：使用 `C_GiveItemPacket` (Opcode 17) 對齊服務器 `C_GiveItem.java`

### 2. 項圈物品系統
- ✅ **項圈創建**：服務器成功抓寵後會自動創建項圈物品（item id=308）
- ✅ **項圈顯示**：項圈在背包中顯示為 "項圈 [Lv.X 寵物名]" 格式（服務器已格式化）
- ✅ **項圈存儲**：項圈存儲寵物信息（pet_id, pet_class_id, pet_level, pet_name 等）

### 3. 寵物倉庫系統
- ✅ **打開倉庫**：實現了 `OpenPetWarehouse` 方法，發送 `C_Shop` type=12 請求寵物列表
- ✅ **寵物列表**：解析服務器 `S_ObjectPet` (Opcode 49, option=12) 寵物倉庫列表
- ✅ **領取寵物**：實現了 `RetrievePet` 方法，發送 `C_ShopPacket.MakePetGet` 領取寵物
- ✅ **UI 窗口**：創建了 `PetWarehouseWindow.cs` 顯示可領取的寵物列表

### 4. 寵物面板系統
- ✅ **面板解析**：解析服務器 `S_ObjectPet` (Opcode 42, "anicom"/"moncom") 寵物面板數據
- ✅ **數據顯示**：顯示寵物血量、藍量、等級、名字、食物狀態、經驗百分比、正義值
- ✅ **UI 窗口**：創建了 `PetPanelWindow.cs` 顯示寵物信息（與法師召喚物面板相同）
- ✅ **點擊打開**：點擊自己的寵物時自動打開寵物面板

### 5. 寵物上線/下線邏輯
- ✅ **上線處理**：角色上線後，寵物會自動從寵物倉庫領取（需要玩家手動到寵物倉庫領取）
- ✅ **下線處理**：角色下線時，寵物會自動消失（服務器處理）
- ✅ **狀態管理**：使用 `_myPetObjectIds` HashSet 追蹤自己的寵物
- ✅ **狀態同步**：通過 `PetStatusChanged` 信號（Opcode 79）更新寵物狀態

### 6. 封包處理
- ✅ **C_GiveItem** (Opcode 17)：喂食封包
- ✅ **C_Shop** (Opcode 40, type=12)：寵物倉庫請求/領取
- ✅ **S_ObjectPet** (Opcode 49, option=12)：寵物倉庫列表
- ✅ **S_ObjectPet** (Opcode 42, "anicom"/"moncom")：寵物面板數據
- ✅ **S_SummonStatus** (Opcode 79)：寵物狀態變更

## 二、實現的文件

### 新增文件
1. **`Client/Network/C_GiveItemPacket.cs`** - 喂食封包
2. **`Client/Game/GameWorld.Pet.cs`** - 寵物系統核心邏輯
3. **`Client/UI/Scripts/PetWarehouseWindow.cs`** - 寵物倉庫窗口
4. **`Client/UI/Scripts/PetPanelWindow.cs`** - 寵物面板窗口

### 修改文件
1. **`Client/Network/PacketHandler.cs`**
   - 添加 `HandleShowHtmlOrPetPanel` 區分 HTML 和寵物面板
   - 添加 `ParseWarehouseOrPetWarehouse` 區分普通倉庫和寵物倉庫
   - 添加 `ParsePetWarehouseList` 解析寵物倉庫列表
   - 添加 `ParsePetPanel` 解析寵物面板數據
   - 添加寵物相關信號

2. **`Client/Network/C_ShopPacket.cs`**
   - 添加 `MakePetGet` 方法創建領取寵物封包

3. **`Client/UI/Core/WindowDef.cs`**
   - 添加 `PetWarehouse` 和 `PetPanel` 窗口 ID

4. **`Client/UI/Core/UIManager.cs`**
   - 註冊寵物窗口路徑（需要創建 .tscn 場景文件）

5. **`Client/Game/GameWorld.Bindings.cs`**
   - 添加 `BindPetSignals` 調用

6. **`Client/Game/GameWorld.Movement.cs`**
   - 在 `StopWalking` 中添加 `CheckFeedAfterMove` 調用

7. **`Client/Game/GameWorld.Input.cs`**
   - 添加寵物點擊處理（打開寵物面板）

8. **`Client/Game/GameWorld.Entities.cs`**
   - 在 `OnObjectDeleted` 中清理寵物 ID

9. **`Client/Game/GameWorld.UI.cs`**
   - 在 `ClearWorldState` 中清理寵物 ID

10. **`Client/Game/GameWorld.Npc.cs`**
    - 移除重複的 `OnPetStatusChanged` 定義

## 三、待完成的工作

### 1. UI 場景文件（.tscn）
需要創建以下場景文件：
- **`Client/UI/Scenes/Windows/PetWarehouseWindow.tscn`**
  - 包含 `ItemList` (命名為 "PetList")
  - 包含 `Button` (命名為 "RetrieveButton")
  
- **`Client/UI/Scenes/Windows/PetPanelWindow.tscn`**
  - 包含 `Label` (命名為 "NameLabel", "LevelLabel", "HPLabel", "MPLabel", "StatusLabel", "FoodLabel", "ExpLabel", "LawfulLabel")
  - 包含 `ProgressBar` (命名為 "HPBar", "MPBar")

### 2. 喂食 UI 交互
目前喂食需要通過代碼調用 `FeedMonster` 方法。可以考慮：
- 在背包中右鍵物品，選擇 "喂食" 選項
- 或拖動物品到怪物身上

### 3. 寵物命令系統
服務器支持寵物命令（攻擊、跟隨、防禦、停留、警戒等），目前客戶端只接收狀態更新，未實現命令發送。

## 四、使用說明

### 抓寵物流程
1. 找到可馴服的怪物（狗/狼等）
2. 攻擊怪物造成傷害
3. 使用物品（肉，item id=23 或 331）喂給怪物
4. 服務器判斷是否成功
5. 成功後，背包會出現項圈物品（item id=308）

### 領取寵物流程
1. 找到寵物倉庫 NPC
2. 與 NPC 對話，選擇寵物倉庫選項
3. 在寵物倉庫窗口中選擇要領取的寵物
4. 點擊 "領取" 按鈕（需要支付 80 金幣）
5. 寵物會出現在玩家身邊

### 查看寵物信息
1. 點擊自己的寵物
2. 自動打開寵物面板窗口
3. 顯示寵物血量、藍量、等級、名字、食物狀態等信息

## 五、技術細節

### 封包對齊
- **C_GiveItem** (Opcode 17)：`writeD(t_obj)`, `writeD(etc)`, `writeD(inv_id)`, `writeD(count)`
- **C_Shop PetGet** (Opcode 40, type=12)：`writeD(npcId)`, `writeC(12)`, `writeH(1)`, `writeD(collarInvId)`, `writeD(0)`
- **S_ObjectPet 倉庫列表** (Opcode 49, option=12)：`writeD(npcId)`, `writeH(count)`, `writeC(12)`, `writeD(invId)`, `writeC(type)`, `writeH(gfxid)`, `writeC(bless)`, `writeD(1)`, `writeC(isDefinite)`, `writeS(name)`, `writeD(80)`
- **S_ObjectPet 面板** (Opcode 42)：`writeD(petId)`, `writeS("anicom"/"moncom")`, `writeC(0)`, `writeH(10/9)`, `writeS(...)`

### 服務器邏輯對齊
- 抓寵物：`Wolf.toGiveMeItem` / `Collie.toGiveMeItem` 等檢查 `isTame(true)` 並調用 `SummonSystem.addPet`
- 寵物倉庫：`PetShopInstance.PetGet` 處理領取邏輯
- 寵物面板：`S_ObjectPet` 構造函數生成面板數據

## 六、注意事項

1. **項圈物品顯示**：項圈的名稱格式由服務器端格式化，客戶端直接顯示即可
2. **寵物上線**：角色上線後，寵物不會自動出現，需要玩家到寵物倉庫手動領取
3. **寵物下線**：角色下線時，寵物會自動消失（服務器處理），上線後需要重新領取
4. **寵物狀態**：寵物狀態通過 Opcode 79 同步，客戶端會自動更新 `_myPetObjectIds`

---

**實現日期**：2026-01-21
**對齊服務器版本**：L1J Server (Java)
