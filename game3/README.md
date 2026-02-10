# Lineage 1 (1999) Reconstruction Project: Technical Documentation

## 0. 專案關聯與規範（優先級第一，必須遵守）

- **game2 + jp = 一套完整的 服務器 + 客戶端**
- **協議標準**：**/jp**（Lineage JP 開源服務器）為**唯一標準（Source of Truth）**
- **客戶端對齊**：**/game2** 必須與 **/jp** 對齊；封包、opcode、變數命名、位元對齊均以 jp 為依據
- **/linserver182**：**已廢棄，不再使用**；後續開發**不得**與 linserver182 對齊

所有客戶端開發與協議變更，以 **/jp** 目錄下的服務器代碼為準。

---

## 1. Project Overview

This project is a rigorous reverse-engineering and reconstruction of the classic MMORPG *Lineage* (© 1999 NCSOFT). The singular goal is to faithfully recreate the original game's client-server architecture and network protocol, mirroring the 1.82c era.

-   **Client Engine**: Godot 4.3 (C#/.NET)
-   **Server**: /jp（Lineage JP 開源服務器）— Absolute Source of Truth；/linserver182 已廢棄

## 2. Development Philosophy

All work on this project is governed by a strict mandate to ensure authentic reconstruction.

-   **Zero Modernization**: The goal is **reconstruction, not reinvention**. We do not add modern features, quality-of-life improvements, or new interpretations of game mechanics. All logic, from rendering quirks to combat formulas, must strive to replicate the original experience.
-   **Protocol First**: The **/jp** server is the **unquestionable source of truth**. All client-side development is driven by and must remain in lockstep with **jp**'s network protocol. The client adapts to jp, never the other way around. (**/linserver182** is deprecated and must not be used for alignment.)

## 3. Server Architecture (`/server`)

The server is a stateful Java application that serves as the complete authority for the game world.

### 3.1. Core Systems & Logic

-   **Session Management (`server/net`, `server/mina`)**: Utilizes Apache MINA for low-level TCP session handling. `LineageProtocolHandler` manages initial connections and IP filtering, while `LineagePacketDecoder` is responsible for decrypting and framing raw byte streams into discrete packet objects.
-   **Game Logic (`server/world`)**: The `world` package contains the majority of the game's mechanics.
    -   **`WorldInstance`**: A singleton that acts as the main container for the game world, holding lists of all active players, NPCs, and monsters.
    -   **`PcInstance`**: The server-side representation of a player character. This massive class holds all player-specific data (stats, inventory, skills, location) and contains methods for core actions like `Attack`, `toMove`, `toTeleport`, and `toDead`.
    -   **AI and Spawns**: `server/world/ai` and `server/database/MonsterSpawnTable.java` manage NPC/monster behavior and their placement in the world.
-   **Data Persistence (`server/database`)**:
    -   **`DatabaseConnection`**: Manages the connection to a SQL database.
    -   **`CharacterTable`**: Handles the serialization and deserialization of player data to and from the `characters` table. It manages the entire character lifecycle, including creation, loading, saving, and deletion. The SQL queries within this class define the schema for player data.
    -   Other tables (`ItemsTable`, `SkillTable`, etc.) manage their respective game objects.

### 3.2. Server Network Protocol

-   **Opcode Definition (`server/Opcodes.java`)**: This class is the master dictionary for the server's protocol. It contains static integer constants for all client-to-server (`C_OPCODE_*`) and server-to-client (`S_OPCODE_*`) packets. It also contains a static map that associates incoming client opcodes with their corresponding Java handler classes (e.g., `C_Attack`, `C_Moving`).
-   **Packet Structure**: Packets are defined as individual Java classes in `server/network/client` (for C-packets) and `server/network/server` (for S-packets).

### 3.3. 服務器需要修改的清單（後續統一修改用）

以下為客戶端已支援、但伺服器尚未發送對應封包或邏輯的項目。待後續在伺服器端統一修改。

| 項目 | 檔案 | 說明 | 客戶端現狀 |
|------|------|------|------------|
| **日光術（Skill 2）魔法動畫** | `server/world/instance/skill/function/Light.java` | 目前只發送 `S_ObjectAction(19)` 與 `S_ObjectEffect(145)`，未發送 `S_ObjectAttackMagic`，客戶端收不到 Op 57，無法播放 Gfx 2510 特效。 | 已處理 Op 57 且 `targetCount == 0`：在 `(x,y)` 落點播放特效。 |
| **燃燒的火球（Skill 16）魔法動畫** | `server/world/instance/skill/function/Lightning.java` | `toMagic(int id)` 僅在 `o = this.operator.getObject(id) != null` 時才發送 `S_ObjectAttackMagic`。若目標不在 operator 的 object 列表中（或 id 無效），則不發送 Op 57，客戶端無法播放 Gfx 171 特效。 | 已處理 Op 57（含 targetCount≥0、落點 x,y）；需伺服器在施法成功時**必定**發送一次 S_ObjectAttackMagic（落點可用目標或施法者座標）。 |
| **修改方式** | `Light.java` → `toMagic(int id)` | 在 `SendPacket(new S_ObjectEffect(145), true)` 之後、`BuffTimerInstance` 之前，新增：<br/>`List<L1Object> emptyList = new ArrayList<>();`<br/>`this.operator.SendPacket(new S_ObjectAttackMagic(this.operator, emptyList, MagicAction2, true, getSkill().getCastGfx(), this.operator.getX(), this.operator.getY()), true);`<br/>並補齊 import：`java.util.ArrayList`、`java.util.List`、`net.network.server.S_ObjectAttackMagic`。 | — |
| **修改方式** | `Lightning.java` → `toMagic(int id)` | 在 `HpMpCheck() && ConsumeCount()` 通過後，若 `getObject(id)` 為 null，仍發送一次 S_ObjectAttackMagic（emptyList、施法者座標），或確保有目標時一律發送（list 可為空），讓客戶端能播放 Gfx 171。 | — |

（後續若有其他「客戶端已就緒、伺服器待補」的項目，可依上表格式續增。）

## 4. Client Architecture (`/Client`)

The client is a Godot 4.3 C# project designed to be a presentation and input layer, reacting to state updates sent by the authoritative server.

### 4.1. File & Logic Structure

-   **Project Configuration (`lingamev2.csproj`)**: Defines the project as a .NET Godot application.
-   **Network Layer (`Client/Network`)**:
    -   **`GodotTcpSession.cs`**: Manages the low-level TCP connection to the server.
    -   **`LineageCryptor.cs`**: Implements the byte-level XOR encryption/decryption required to communicate with the server.
    -   **`PacketHandler.cs`**: The core of the network layer. It receives decrypted byte arrays, uses a `switch` statement on the first byte (the opcode) to identify the packet, and delegates to a specific parsing function (e.g., `ParseObjectAdd`). After parsing, it emits Godot signals to notify the rest of the application of the game event. This decouples the network logic from the game logic.
    -   **Packet Definitions (`C_*.cs`)**: A series of classes that construct the byte arrays for client-to-server packets, mirroring the server's expected structure.
-   **Game Logic Layer (`Client/Game`)**:
    -   **`GameWorld.cs`**: The central controller for the main gameplay scene. It is implemented as a `partial class` to organize its many responsibilities (Combat, Movement, UI, etc.). It listens for signals from `PacketHandler` (e.g., `ObjectSpawned`, `ObjectMoved`) and updates the game state accordingly. It maintains a dictionary of all active `GameEntity` instances.
    -   **`GameEntity.cs`**: The client-side representation of any object in the world (player, NPC, monster). As a `Node2D`, it has a position and is responsible for its own visual representation, likely by managing child `AnimatedSprite2D` nodes for its body, equipment, and effects.
-   **UI Layer (`Client/UI`)**:
    -   **`UIManager.cs`**: A manager for opening, closing, and handling different UI windows.
    -   **UI Scripts (`Client/UI/Scripts`)**: Each UI scene (e.g., `HUD.cs`, `InvWindow.cs`, `CharacterSelect.cs`) has a corresponding script that handles its logic. These scripts typically connect to signals from `PacketHandler` or `GameWorld` to update their display (e.g., `HUD.cs` listens for `HPUpdated` to refresh the health bar).

## 5. Protocol Alignment Status

**重要**：客戶端已從 `linserver182` 協議遷移到 `jp` (lineage-jp) 服務器協議。完整的遷移文檔請參見：[JP 服務器協議對齊文檔](docs/jp-protocol-migration.md)

該文檔包含：
- 所有封包 opcode 映射（登錄、世界對象、UI、技能、Buff、角色等）
- 封包結構詳細說明（對齊服務器實現）
- 加密/解密邏輯變更（從 LineageCryptor 遷移到 JpCipher）
- 修改文件清單和測試檢查清單

---

### 5.0. 協議遷移摘要

| 系統 | 主要變更 | 狀態 |
|------|---------|------|
| **登錄流程** | 10 個封包 opcode 更新 | ✅ 完成 |
| **世界對象** | 12 個封包 opcode 更新 | ✅ 完成 |
| **UI 系統** | 5 個封包 opcode 更新 | ✅ 完成 |
| **技能系統** | 3 個封包 opcode 更新 | ✅ 完成 |
| **Buff 系統** | 6 個封包 opcode 更新 | ✅ 完成 |
| **角色系統** | 3 個封包 opcode 更新 | ✅ 完成 |
| **其他系統** | 6 個封包 opcode 更新 | ✅ 完成 |
| **加密/解密** | LineageCryptor → JpCipher | ✅ 完成 |

詳細對齊記錄請參見：[docs/jp-protocol-migration.md](docs/jp-protocol-migration.md)

---

The following tables detail the mapping between server opcodes and client handlers, which is the core of the reverse-engineering effort.

**注意**：以下表格為舊版 `linserver182` 協議的參考，實際運行使用 `jp` 協議。請以 [JP 協議對齊文檔](docs/jp-protocol-migration.md) 為準。

### 5.1. Client-to-Server (C-Packets)

The client sends these packets to perform actions.

| Action | Client Packet Class | Server Opcode | Server Handler Class |
| :--- | :--- | :--- | :--- |
| Login | `C_LoginPacket.cs` | `1` | `C_Logins.java` |
| Enter World | `C_EnterWorldPacket.cs` | `5` | `C_LineageWorldJoin.java` |
| Move Character | `C_MoveCharPacket.cs` | `10` | `C_Moving.java` |
| Pickup Item | `C_ItemPickupPacket.cs` | `11` | `C_ItemPickup.java` |
| Attack | `C_AttackPacket.cs` | `23` | `C_Attack.java` |
| Magic/Skill | `C_MagicPacket.cs` | `20` | `C_Magic.java` |
| Talk to NPC | `C_NpcPacket.cs` | `41` | `C_NpcTalk.java` |
| Create Character| `C_CreateCharPacket.cs` | `112` | `C_CharacterCreate.java` |

### 5.2. Server-to-Client (S-Packets)

The server sends these packets to update the client's state. Handled in `Client/Network/PacketHandler.cs`.

| Event | Server Opcode | Client Handler Logic | Emitted Signal |
| :--- | :--- | :--- | :--- |
| Character/Object Spawn | `11` | `ParseObjectAdd` | `ObjectSpawned` |
| Object Move | `18` | `ParseObjectMoving` | `ObjectMoved` |
| Object Deleted | `21` | (Direct parse) | `ObjectDeleted` |
| Object Action/Attack | `32`/`35` | `ParseObjectAttack` | `ObjectAttacked`/`ObjectRangeAttacked` |
| Magic Attack | `57` | `ParseObjectAttackMagic`| `ObjectMagicAttacked` |
| HP Update | `13` | `ParseHPUpdate` | `HPUpdated` |
| MP Update | `77` | `ParseMPUpdate` | `MPUpdated` |
| Full Inventory | `65` | `ParseInventoryList` | `InventoryListReceived`|
| Add Inventory Item | `22` | `ParseInventoryAdd` | `InventoryItemAdded` |
| Delete Inventory Item| `23` | (Direct parse) | `InventoryItemDeleted` |
| System Message | `16` | `ParseServerMessage` | `SystemMessage` |
| Object Polymorph | `39` | `ParseObjectPoly` | `ObjectVisualUpdated` |
| Map Change/Teleport | `40` | (Direct parse) | `MapChanged` |
| Show HTML Dialog | `42` | `HandleShowHtml` | `ShowHtmlReceived` |
| Open Shop (Buy) | `43` | `HandleShopBuyList` | `ShopBuyOpen` |
| Open Shop (Sell) | `44` | `HandleShopSellList`| `ShopSellOpen` |
| Skill Update (Add) | `30` | `ParseSkillAdd` | `SkillAdded` |
| Haste/Brave Buff | `41`/`98` | `HandleBuffSpeed` | `BuffSpeedReceived` |




架構分離
GameWorld：控制動作邏輯（何時攻擊/移動/待機）、動作間隔（cooldown）、與服務器同步。
GameEntity：控制播放畫面（動畫播放速度、待機姿勢選擇、視覺表現）。

## 6. 核心渲染與動畫規則 (Core Rendering & Animation Rules)

為了確保客戶端視覺表現與 1999 年原版邏輯及伺服器數據完全對齊，所有開發必須遵循以下約束性規則：

### 6.0. 魔法畫面表現（禁止擅自修改）

-   **規則**：魔法測試畫面表現已通過，**禁止在未經特別許可下修改任何魔法動畫／視覺表現相關功能**。若需修改魔法動畫表現的設計或實作，須**事先請求許可**後再進行。
-   **適用範圍**：含但不限於魔法特效播放、方向性、連貫(109.effect)、飛行段、落點播放、104.attr(8)／102.type 與魔法慢速等邏輯。

### 6.1. 動畫播放速率 (Animation Speed)
-   **唯一基準**：動畫播放速度嚴格由 `list.spr` 定義的 `DurationUnit * 40ms` 決定。
-   **禁止硬編碼**：嚴禁在 `GameEntity` 或任何表現層代碼中寫死動畫播放時間或額外的 `Tween` 延遲。
-   **加速控制**：除了「綠水/勇水」等合法加速藥水（縮放係數通常為 0.75）外，不允許任何地方對動畫進行二次加速。
-   **公式權威**：`攻擊速度的 幀速度，和整體速度，此公式和代碼 正確無誤。不准修改。`

### 6.1.1. 動畫幀播放順序 (Frame Playback Rule) — **重點（不允許修改）**

-   **動畫幀播放規則**：**從 FrameIdx=1 開始、依 FrameIdx 順序**。即：以 FrameIdx=1 作為第一幀開始播放，然後按 `FrameIdx` 數值升序播放，**然後循環**。
-   **無論 FrameIdx=1 前面是 0 還是 8**（或任何其他值），**必須從 FrameIdx=1 開始播放**，跳過 1 之前的所有幀，然後循環。確保**全局**都按照此規則。
-   **魔法、角色、陰影**：全部走此規則。`CustomCharacterProvider.BuildLayer`（魔法／角色／陰影等建幀）、`GameEntity.Audio`（幀音效對應）均依「從 FrameIdx=1 開始、依 FrameIdx 順序」實作；禁止改為從 0 或「在 1 之前」的幀起播。
-   **定義位置**：`Client/Utility/ListSprLoader.cs` 中 `SprPlaybackRule.MinPlaybackFrameIdx`（預設為 1；若要以 0 或自訂數字為第一幀，可手動修改該常數，見 §6.1.1.1）。
-   **list.spr 範例**：`0.fire(0 5,0.0:2 0.1:2<127 0.2:2 0.3:2 0.4:2)` 中，括號內 token 解析後得到 FrameIdx=0,1,2,3,4；排序後為 0,1,2,3,4。實際播放時**從 FrameIdx=1 作為第一幀**，故跳過 0，播放順序為 **1 → 2 → 3 → 4 → 循環 → 1 → …**。若 1 前面是 8，同樣跳過 8，從 1 開始。

**備注（重要關鍵信息）**：本規則的表述是「**從 FrameIdx=1 開始、依 FrameIdx 順序**」。第一幀必須是 FrameIdx=1；排序後在 1 之前的幀（例如 0、8 等）皆跳過，不參與播放與循環。此規則已驗證正確。

#### 6.1.1.1. 第一幀常數：可手動修改（自訂 0 或其它數字為第一幀）

- **定義位置（唯一）**：`Client/Utility/ListSprLoader.cs`，類別 `SprPlaybackRule`，常數 **`MinPlaybackFrameIdx`**（目前為 `1`）。
- **修改方式**：若希望改為「以 FrameIdx=0 為第一幀」或自訂某數字為第一幀，僅需在該檔案中修改此常數值即可（例如改為 `0` 即從 0 開始播；改為 `2` 即從 FrameIdx=2 的那一幀開始）。建幀與音效邏輯會依此常數全局生效（CustomCharacterProvider、GameEntity.Audio 等皆引用 `SprPlaybackRule.MinPlaybackFrameIdx`）。

#### 6.1.1.2. 動畫幀播放與音效經驗總結（FrameIdx=1 + 魔法音效不截斷）

- **問題**：魔法動畫若從 FrameIdx=0 起播，首幀為 0.0:2，音效定義在 0.1:2<127，會變成「第二幀」才播，且動畫 0.4s 結束時 SkillEffect 被 QueueFree，音效節點一併銷毀，導致音效被截斷。
- **對策一（第一幀）**：全局改為「**第一幀必須是 FrameIdx=1 的那一幀**」；排序後找到 **FrameIdx == MinPlaybackFrameIdx** 的幀作為起點，若找不到則不播放。實作處：`Skins/CustomFantasy/CustomCharacterProvider.cs`（BuildLayer）、`Client/Game/GameEntity.Audio.cs`（OnFrameChanged）。如此 0.fire(0.0:2 0.1:2<127 …) 的播放順序為 1→2→3→4→0→1…，音效<127 在第一幀播放。
- **對策二（魔法音效不截斷）**：魔法音效改為使用**掛在場景根**的臨時 `AudioStreamPlayer2D` 播放，播畢再 `QueueFree` 該臨時節點；不依賴 SkillEffect 自身的子節點。實作處：`Client/Game/SkillEffect.cs` 的 `PlaySound(int soundId)`。這樣動畫 0.4s 結束、SkillEffect 被銷毀時，音效仍由根節點下的臨時播放器繼續播完，不被截斷。
- **音效來源**：動畫幀音效僅依 **SprFrame.SoundIds**（list.spr 中 `<` 與 `[` 皆解析為音效）；實體由 GameEntity.Audio 依 `order[frameIdx].SoundIds` 播放，魔法由 SkillEffect 依紋理 meta `spr_sound_ids`（建幀時自 SoundIds 寫入）播放。

#### 6.1.1.3. 魔法音效失效修復（Magic Sound Fix）

**問題描述**：大部分魔法音效突然無法播放，只有少數魔法（如 `gfx=243`）音效正常。

**根本原因**：
在 `Client/Game/SkillEffect.cs` 的 `OnFrameChanged` 方法中，對齊邏輯完成後有一個 `return` 語句，導致後續的音效播放代碼被跳過。

**為什麼會導致音效失效**：
1. 大部分魔法都有 `alignEntity`（跟隨目標或對齊目標），因此會進入對齊邏輯分支
2. 當 `bodyTex` 存在且包含 `spr_anchor_x`/`spr_anchor_y` 元數據時，會執行對齊計算
3. 原本的 `return` 會直接結束方法，導致音效播放代碼（讀取 `spr_sound_ids` 並播放）永遠不會執行
4. 只有少數魔法（如 `gfx=243`）可能沒有 `alignEntity` 或 `bodyTex` 缺少元數據，因此不會進入此分支，音效播放代碼得以執行

**修復方案**：
移除對齊邏輯中的 `return` 語句，讓對齊邏輯執行完畢後，繼續執行後續的音效播放代碼。這樣可以確保：
- 對齊邏輯正常執行（設置 `_sprite.Offset`）
- 音效播放代碼在對齊之後執行（讀取 `spr_sound_ids` 並播放音效）
- 所有魔法都能正常播放音效

**修復位置**：`Client/Game/SkillEffect.cs` 第 372 行（原 `return;` 已移除，改為註釋說明）

**相關代碼**：
- 對齊邏輯：第 345-388 行
- 音效播放邏輯：第 390-407 行

#### 6.1.1.4. Godot 專案中新增 SFX 音效巴士（Godot 4.x，英文選單）

**為什麼要增加 SFX 巴士？有什麼區別？**

- **程式預期**：客戶端程式將攻擊／魔法等音效指定到名為 **"SFX"** 的巴士（`SkillEffect.cs` 的 `PlaySound`、`GameEntity.Audio.cs` 的 `_soundPlayer.Bus = "SFX"`）。若專案中沒有建立名為 "SFX" 的巴士，Godot 會退回到 **Master** 巴士播放，音效仍會響，但無法單獨控制「音效」音量。
- **有 SFX 巴士的差別**：
  - **獨立音量**：可在選項中單獨調整「音效」與「音樂／主音量」，不互相影響。
  - **單獨靜音**：可只關閉音效而保留音樂，或反過來。
  - **混音／效果**：可對 SFX 巴士單獨加效果（如壓限、混響）或送不同輸出。
- **不新增會怎樣**：音效會走 Master，遊戲仍可正常播音；只是無法在 Godot 的 Audio 面板裡單獨調「SFX」這一軌，若未來要做設定介面（例如「音效音量」滑桿），就需要有 SFX 巴士。

**新增步驟（Godot 4.x，英文介面）：**

音效巴士在編輯器**底部面板**的 **Audio** 標籤，不是在 Project Settings 裡。

1. 開啟專案後，看編輯器**最下方**（與 Output、Debugger 同一排）。
2. 點選 **Audio** 標籤，打開 Audio 面板（若沒看到，可試 **Editor → Layouts** 或視窗底部其他標籤）。
3. 面板內預設會顯示 **Master** 巴士。點右上或面板內的 **+**（**Add Bus**）或右鍵 Master → **Add Bus Child**，新增一個巴士。
4. 將新巴士命名為 **SFX**（雙擊名稱可改；若用其它名稱，須將程式內 `Bus = "SFX"` 改為該名稱）。
5. 新巴士預設會送給 Master；可依需要調整音量滑桿、**Solo**、**Mute**。儲存專案後，客戶端播放音效時會使用此巴士。

**若仍找不到**：確認底部是否有 **Audio** 標籤；或選單 **Editor → Editor Layout** 切回預設版面，再在底部找 Audio。

### 6.2. 邏輯與視覺解耦 (Logic-Visual Decoupling)
-   **GameEntity (視覺)**：純粹的動畫播放器。只負責按幀速率「畫」出動作，不控制動作的總時長或發生頻率。
-   **GameWorld (邏輯)**：權威指揮官。結合 `SprDataTable.cs`（讀取伺服器 `sprite_frame`，依**當前角色 GfxId** 查表）來精確控制移動間隔、攻擊冷卻與施法頻率；不同變身外觀（gfxId）即有不同速度與冷卻。

### 6.3. 魔法特效規則 (Skill Effect Rules)

#### 6.3.1 動畫「單幀／多幀、有方向／無方向」唯一依據（禁止額外規則）
-   **唯一依據**：**有方向 (DirectionFlag=1) 與 無方向 (DirectionFlag=0)**。動畫播放是否走單幀路徑、是否使用 8 方向檔，**僅**依 list.spr 動作定義中的方向旗標，**不得**用 102.type 或其他欄位判斷。
-   **list.spr 約定**：例如 `0.fly(1 3,...)` 中括號內第一個數字 **1 = 有方向**（8 方向），0 = 無方向。此約定已明確，不需再增加 102.type(9) 等額外規則。
-   **禁止**：不要額外加規則；在「有方向／無方向」已可判斷的情況下，不要增加其他判斷規則，避免把簡單功能做成複雜臃腫。

#### 6.3.2 錯誤原點（曾發生的錯誤修改，供及時發現與糾正）
-   **錯誤**：曾以 **102.type(9)** 作為「是否走單幀路徑」的判斷依據。專案**從未**聲明 102.type(9) 的規則與用途用於動畫路徑。
-   **正確**：動畫單幀／多幀、8 方向與否的**唯一**依據為 list.spr 動作的 **有方向(1)／無方向(0)**（即 `DirectionFlag`），與 102.type 無關。102.type 在 list.spr 中大量存在（如掉落物、物品等），用途未在客戶端動畫邏輯中定義，**不參與**動畫播放路徑判斷。

#### 6.3.3 已驗證成功的魔法規則與關鍵點
-   **Gfx 167 光箭 與 弓箭同一格式**：Op35 + (sx,sy)(tx,ty)，與 S_ObjectAttack 弓箭同封包；單體飛行魔法與物理遠程共用 `ObjectRangeAttacked` → 起點→終點飛行。**此判斷成功，不可修改。**
-   **有方向魔法 (DirFlag=1)**：AOE 時依**每個目標**位置計算 heading（施法者→該目標），使用與角色／怪物相同的 `ListSprLoader.GetFileOffset(heading, seq.DirectionFlag)` 映射，不重複寫臃腫代碼。
-   **無方向魔法 (DirFlag=0)**：固定動畫名 `"0"`（對應 list.spr 的 0.fire 等），不依 heading 切換動畫名。
-   **104.attr(8)**：魔法定義；動畫取得優先 ActionId=0，無則 ActionId=3；不依武器／walk／attack 語義。

#### 6.3.4 其他魔法特效邏輯
-   **連環播放 (109.effect)**：嚴格執行 `list.spr` 中的 109 號定義。當一個魔法動畫結束後，必須能在目標座標觸發下一個定義的動畫（如 109 結束後播 219）。
-   **方向性 (fly/fire)**：依據 `DirectionFlag` 判定。有方向的魔法（Flag=1）必須計算攻擊者與目標間的 8 方向偏移，並調用通用的 8 方向動畫邏輯。

#### 6.3.5 經用戶確認的修正（曾錯誤、已改正，請勿回退）
-   **102.type(9) 誤用**：曾以 102.type(9) 作為動畫單幀／多幀判斷，**錯誤**。正確為僅依 list.spr 動作的 **有方向(1)／無方向(0)**，與 102.type 無關。已刪除 `BuildType9ItemLayer` 等相關邏輯。
-   **Gfx 167 光箭／飛行魔法**：單體飛行魔法（Op35 + magicFlag=6）改為發送 `ObjectRangeAttacked`，與弓箭同一格式；由 `OnRangeAttackReceived` 統一處理「起點→終點」飛行。**已確認正確。**
-   **AOE 魔法方向**：Gfx 170 等有方向 AOE，改為依**每個目標**單獨計算 heading（施法者→該目標），不再用單一落點方向。**已確認正確。**
-   **無方向魔法動畫名**：104.attr(8) 且 DirFlag=0 時，動畫名固定為 `"0"`，與 list.spr 一致。
-   **日誌節流**：GfxId:129 輪詢時僅首次打 TryLoad 日誌，避免刷屏；GfxId:242 等「無 Action0/Action3」每 gfxId 只打一次。

#### 6.3.6 8 方向檔序與 Gfx167／弓箭／Gfx170 對照（僅供查閱與除錯）
-   **客戶端邏輯朝向 (heading)**：與 `GameEntity.GetHeadingTo` / `GameWorld.GetHeading` 一致。0=上、1=右上、2=右、3=右下、4=下、5=左下、6=左、7=左上。
-   **檔名對應**：有方向魔法 (DirFlag=1) 時，`ListSprLoader.GetFileOffset(heading, 1)` 回傳 0–7，與 PAK 內檔名第二段對應：`SpriteId-{fileOffset}-{FrameIdx}.png`（例如 167-0-003.png、167-1-003.png … 167-7-003.png）。
-   **GetFileOffset 映射表**（`ListSprLoader.cs`）：heading 7→0、0→1、1→2、2→3、3→4、4→5、5→6、6→7。即 PAK 檔序假設為：0=左上、1=上、2=右上、3=右、4=右下、5=下、6=左下、7=左。
-   **Gfx 167 光箭／弓箭**：與角色／怪物共用同一 8 方向映射；飛行魔法由 `OnRangeAttackReceived` 依「起點→終點」計算 heading，再呼叫 `GetBodyFrames(gfxId, 0, heading)`，Provider 依 `GetFileOffset(heading, seq.DirectionFlag)` 載入對應幀。若畫面上箭矢／光箭「方向一致」與預期不符，需核對 PAK 內 167／66 等資源的檔序是否與 L1 原版一致，程式端不另做映射。
-   **Gfx 170 AOE 有方向魔法**：依**每個目標**單獨計算「施法者→該目標」的 heading，再 SpawnEffect；動畫載入同樣經 `GetBodyFrames` → `GetFileOffset`，與角色動畫同一套規則。
-   **朝向日誌**：`[Magic][Range]` 與 `[Magic][Op57]` 會輸出「角色朝向」「魔法朝向(8方向)」「目標實體朝向」等，供比對視覺與邏輯是否一致。

#### 6.3.7 動畫播放速度控制權責（角色 vs 魔法，禁止混淆）

-   **綠水、勇水、加速魔法**：可疊加速度，但**僅針對角色的動畫與邏輯**，**不控制魔法特效動畫**。
    -   **影響範圍**：移動間隔（`GameWorld.Movement.cs`：`_moveInterval` 在 `_myPlayer.AnimationSpeed > 1.0f` 時 ×0.75）、攻擊冷卻（`GameWorld.Combat.cs`：`currentCooldown` 在 `_myPlayer.AnimationSpeed > 1.0f` 時 ×0.75）。與伺服器 CheckSpeed 等邏輯對齊。
    -   **不影響**：角色身上 AnimatedSprite2D 的 `SpeedScale` 在 `GameEntity.Visuals.cs` 中固定為 `1.0f`（由 RealDuration 控制幀長）；**魔法特效**（SkillEffect）的播放速度與時長**完全不讀取** AnimationSpeed 或任何加速 Buff。
-   **魔法動畫播放時長：唯一控制處（重要：須排除 102.type(0)）【不允許修改或刪除此設定】**  
    全專案**僅有一處**單獨控制「魔法類型」動畫播放時長；且**僅當「純魔法」時**才套用 2 倍慢速，**102.type(0) 不套用**：
    -   **檔案**：`Skins/CustomFantasy/CustomCharacterProvider.cs`
    -   **方法**：`BuildLayer`
    -   **條件**：`isMagicOnly = (targetDef != null && targetDef.Attr == 8 && targetDef.Type != 0)`。即 **104.attr(8) 且 102.type ≠ 0** 才視為「純魔法」、每幀時長 ×2；**104.attr(8) 但 102.type(0)**（如 #242 死騎光劍、陰影等與角色主體同步的附屬）**不**套用慢速，與角色同速。
    -   **設計理由**：list.spr 中多數魔法幀長偏短；2 倍僅影響獨立魔法特效。102.type(0) 表示與角色主體同步（陰影、光劍等），必須與角色播放速度一致，不可因 104.attr(8) 而慢速。
-   **魔法特效不參與加速**  
    -   **檔案**：`Client/Game/SkillEffect.cs`  
    -   **位置**：`TryLoadAndPlay` 內固定 `_sprite.SpeedScale = 1.0f`，註解為「魔法播放速度必须固定为原始速率，不受任何加速影响」。  
    -   魔法特效的總時長由 Provider 建好的 SpriteFrames 每幀 duration 與上述唯一倍率決定，SkillEffect 不再做任何速度縮放。
-   **禁止**：不得在其它檔案（例如 SkillEffect、PacketHandler、GameWorld）中對「魔法動畫」做額外的播放速度或時長縮放；不得讓綠水/勇水/加速 Buff 影響魔法特效的播放速度或時長。

#### 6.3.7. 攻擊邏輯（Z／魔法按鈕、距離與面向）

按 **Z** 或按**魔法按鈕**後的行為（對應 `Client/Game/GameWorld.Combat.cs`、`GameWorld.Skill.cs`）：

1. **先判斷攻擊／施法距離**：若已在射程內，則**馬上開始攻擊／施法**（不先走路）。
2. **若不在射程內**：走到目標身邊，進入攻擊範圍後**馬上開始攻擊**。
3. **若目標移動**：跟隨移動，並繼續攻擊。
4. **攻擊時**：及時調整朝向，面向被攻擊的目標。

實作要點：

- **狀態機**：`ExecuteAttackTask` 在 **Idle** 時依 `dist <= range` 決定直接進入 **Executing**（當幀嘗試攻擊）或進入 **Chasing**（追擊）；法師／弓箭手不再「一律先走到身邊」。
- **攻擊距離**：唯一來源為 `GetAttackRange()`（§6.3.7.2）；禁止其他處再寫死攻擊距離。
- **法師 Z 鍵**：法師職業（class=3，對齊 C_CreateCharPacket.ClassType.Wizard）按 Z = 自動魔法攻擊（skill 4 光箭 Gfx167）；其他角色按 Z = 自動物理攻擊。判定依 Boot.MyCharInfo.Type，不用 GfxId。
- **面向**：`ExecuteAttackAction` 內以 `GetHeading` + `_myPlayer.SetHeading(heading)` 使角色面向目標後再發包與播攻擊動畫。

#### 6.3.7.1. 攻擊範圍（SkillListData.Range）與攻擊距離（list.spr 102.type）— 區別與取得方式

兩者不可混淆，且各有唯一資料來源：

| 概念 | 含義 | 資料來源 | 取得方式 | 用途 |
|------|------|----------|----------|------|
| **攻擊範圍** | 群體魔法「主目標周圍 N 格」內皆為被攻擊目標 | **SkillListData.Range**（對齊 server skill_list.range） | `SkillListData.Get(skillId)?.Range`；例：skill 16 的 range=2 表示主目標周圍 2 格內都是被攻擊目標 | 群體魔法（燃燒的火球、龍捲風等）判定哪些角色頭上要播動畫；伺服器 AOE 計算 |
| **攻擊距離** | 角色「能從多遠格數發動攻擊／魔法」 | **list.spr 102.type(8) 或 102.type(9)** 括號內數字；無則 fallback | `ListSprLoader.Get(_myPlayer.GfxId)?.Type`；當 Type 為 8 或 9 時作為攻擊距離（格數）；否則 `GetAttackRangeFallback()`（法師 734→6、弓→6、槍→2、近戰→1） | 戰鬥狀態機「是否在射程內」、尋路與追擊；**唯一標準**，禁止其他處再寫死攻擊距離 |

- **實作位置**：攻擊距離唯一取得處為 `Client/Game/GameWorld.Combat.cs` 的 `GetAttackRange()`；攻擊範圍用於 `Client/Game/GameWorld.Skill.cs` 的 UseMagic 群體魔法目標收集（`aoeRange = entry?.Range ?? 0`，`isGroupMagic = aoeRange >= 2`）。

#### 6.3.7.2. 攻擊核心原則（客戶端表現／封包結算／冷卻）

1. **客戶端負責表現**
    - **UseMagic**：先 `SetAction(ACT_SPELL_DIR)` → 依 cast_gfx 播放落點／飛行特效 → 最後才 `_netSession.Send(C_MagicPacket.Make(...))`。表現先於發包。
    - **攻擊**：先通過 `SpeedManager.CanPerformAction(ActionType.Attack, ...)` 再發包並播放攻擊動畫；傷害仍只由封包結算。
2. **封包負責傷害結算（權威判斷，不依 actionId）**
    - **Op35 物理**：`ParseObjectAttack` → `ObjectAttacked` → `OnObjectAttacked` → PrepareAttack → 攻擊關鍵幀 → `HandleEntityAttackHit`。
    - **Op35 單體魔法**：`ParseObjectAttack` 內當 **magicFlag==6**（封包權威）時另發 **ObjectMagicHit(attackerId, targetId, damage)** → `OnObjectMagicHit` → 立即 `HandleEntityAttackHit`。**不依 actionId**，變身怪物默認魔法攻擊（如 #931 狗）亦正確結算飄字與僵硬。
    - **Op57**：僅綁定 `OnMagicVisualsReceived`；該方法兩條出口均在 `targetId > 0` 時呼叫 `HandleEntityAttackHit(targetId, damage)`。魔法傷害只來自封包。
3. **冷卻控制節奏**
    - **魔法**：`UseMagic` 開頭 `SpeedManager.CanPerformAction(ActionType.Magic, ...)` 未過冷卻則不播、不發包。
    - **攻擊**：`PerformAttackOnce` 內 `SpeedManager.CanPerformAction(ActionType.Attack, ...)` 未過冷卻則不發包、不播；自動攻擊另由 `_attackCooldownTimer` 與 SprDataTable 間隔約束。

#### 6.3.8. 先播放後結算與飛行魔法規則（客戶端魔法流程）

以下為客戶端施放魔法時的**統一邏輯**，對應檔案：`Client/Game/GameWorld.Skill.cs`（UseMagic）、`Client/Game/GameWorld.SkillEffect.cs`（SpawnEffect、RecordSelfMagicCast、TryConsumeSelfMagicCast、OnRangeAttackReceived）、`Client/Data/SkillListData.cs`、`Client/Network/PacketHandler.cs`（Op35/Op57）。

-   **資料來源**：技能 cast_gfx、range、type 由 **SkillListData** 從本地 **Assets/Data/skill_list.csv**（對齊 `server/skill_list.sql`）載入，供 UseMagic 取 cast_gfx 與後續擴充用。
-   **UseMagic 流程（先播放後結算）**：
    1.  檢查 HasLearnedSkill(skillId)；無則提示並 return。
    2.  解析目標：依 **skill_list.csv 的 type** 統一選取目標，見 §6.3.8.0。
    3.  **魔法冷卻**：`SpeedManager.CanPerformAction(ActionType.Magic, _myPlayer.GfxId, ACT_SPELL_DIR)` 未過冷卻則不播放、不發包，return（§6.3.9）。
    4.  施法動作：`_myPlayer.SetAction(ACT_SPELL_DIR)`。
    5.  **先播放**：依 SkillListData.Get(skillId) 取 castGfx。若 castGfx > 0 且**非飛行魔法**，則 `RecordSelfMagicCast(castGfx)` 與 `SpawnEffect(castGfx, endPos, heading, followTarget)` 在**落點/目標處**播放；若為**飛行魔法**（list.spr Action0=fly），則 `RecordSelfMagicCast` + `RecordSelfFlyingCast`，在**起點**建立 SkillEffect、Tween 至**終點**，傳入 followTarget + chainCallback（`useFollowForPosition: false`），播畢在目標處觸發 109.effect 連貫。
    6.  **發送封包**：`C_MagicPacket.Make(skillId, targetId, targetX, targetY)`；技能 5/45 傳送時僅 targetX 寫入封包（對齊 C_Magic readH），等伺服器 Op57（落點/多目標）或 Op35（單體飛行）回傳。
-   **飛行魔法判定**：依 **list.spr Action0 名稱含 "fly"**（`ListSprLoader.IsAction0Fly(castGfx)`）判定，與 167（光箭）、171（燃燒的火球）等統一邏輯；客戶端一律在 UseMagic 先播飛行段＋連貫，不依賴 Op35。
-   **Op35（ObjectRangeAttacked）**：若攻擊者為己方且 `TryConsumeSelfFlyingCast(gfxId)` 為 true（2.5 秒內已在 UseMagic 播過該飛行段），則**跳過**重複播放；否則在起點建立 SkillEffect、Tween 至終點，傳入 followTarget + chainCallback，播畢觸發 109.effect 連貫。
-   **Op57（ObjectMagicAttacked）**：若攻擊者為己方且 `TryConsumeSelfMagicCast(attackerId, gfxId)` 為 true，則**不重複播主段**；若有 109.effect 連貫（如 171→218），則在**該目標位置**播連貫段（AOE 時每個目標各播一次），再 return；否則僅 return。他方正常 SpawnEffect 主段。
-   **連貫魔法**：SkillEffect 生命結束僅由 `OnAnimationFinished` 處理（`_sprite.AnimationFinished` 或安全 Timer 皆呼叫此方法，`_animationFinishedHandled` 防重複），保證第一段播完必觸發 109.effect 下一段，不因意外中斷。
-   **小結**：非飛行魔法 = 先播落點 + 送封包 → Op57 己方去重、他方正常 SpawnEffect；飛行魔法 = 先播起點→終點＋連貫 + 送封包 → Op35 己方去重、Op57 己方僅播連貫段於各目標。

#### 6.3.8.0. 魔法目標選取規則（依 skill_list type）

客戶端 **UseMagic** 的目標**唯一**依本地 **skill_list.csv** 的 **type** 欄位決定；與伺服器 `skill_list` 表對齊。實作位置：`Client/Game/GameWorld.Skill.cs`（UseMagic 開頭）。

| type 值 | 行為 | 說明 |
|--------|------|------|
| **none** / **item** | 一律目標 = 自己 | 不檢查 targetId，不使用傳入的 targetId 或當前任務／攻擊目標；一律 `targetId = _myPlayer.ObjectId`，即便選了別人也只對自己施法。例：日光術(2)、指定傳送(5)、集體傳送(45)。**例外**：type 為 **item** 時，技能 9（神聖武器）、15（鎧甲護持）、46（創造魔法武器）改為傳入當前裝備武器／盔甲的 **InvID**，詳見 docs/magic-system-dev.md §3.1–3.3。 |
| **buff** | 人工選中或自己 | 若尚無目標（targetId==0）才用「人工選中」：GetCurrentTaskTarget() ?? GetCurrentAttackTarget()；若仍無則選自己。例：中級治癒(1)、神聖武器(6)、壞物術(18)、解毒(23)。 |
| **attack** 及其餘 | 必須有目標 | 若尚無目標才用「人工選中或 Z 攻擊目標」：GetCurrentAttackTarget() ?? GetCurrentTaskTarget()；若仍無則提示「請先點選目標或按 Z 選怪」並 **return**，不發包。例：光箭(4)、燃燒的火球(16)、極道落雷(22)。 |

-   **魔法目標驗證**：取得目標後會驗證是否為**有效魔法目標**（`IsValidMagicTarget`）：排除地面物品（無血條）、死亡（HpRatio≤0）、己方召喚/寵物（attack 時）；僅 **102.type(5)/(10)** 有血量（`ShouldShowHealthBar()`）之怪物/NPC/玩家可為魔法目標。若目標無效：**attack** 提示並 return；**buff** 改為對自己施法。避免「點選地面物品後再放魔法」導致特效在物品頭上播放。
-   **資料來源**：`Client/Data/SkillListData.cs` 從 **Client/Data/skill_list.csv**（或 `res://Data/skill_list.csv`）載入，欄位 `skill_id, cast_gfx, range, type`；與伺服器 **skill_list** 表一致。
-   **注意**：若某技能（如壞物術 18）需**敵方目標**且不希望無目標時對自己施放，應在 **skill_list.csv** 中將該技能的 **type** 設為 **attack**，而非 buff。

#### 6.3.8.1. 魔法／攻擊傷害與封包對應（伺服器為準）

以下對齊 **server/network/server/S_ObjectAttackMagic.java**、**server/world/instance/skill/function/EnergyBolt.java** 等，明確傷害由何封包攜帶、客戶端如何結算。

-   **伺服器**：
    -   **單體魔法**（如光箭 EnergyBolt）：伺服器先 `o.setCurrentHp(o.getCurrentHp() - dmg)`，再 `SendPacket(new S_ObjectAttackMagic(cha, o, action, dmg, gfx))`。該封包實作為 **writeC(35)**，即 **Op35**（與物理遠程共用格式），封包內含 **dmg**（writeC(dmg)）。
    -   **多目標魔法**（AOE）：伺服器對每個目標扣血後，發送 **writeC(57)** 即 **Op57**，封包內對每個目標 **writeD(objectId), writeC(o.getDmg())**，故 **傷害在 Op57 內、每目標一筆**。
    -   **Op104 (S_ObjectHitratio)**：伺服器在**物理攻擊**等路徑若 `isHpBar()` 會送 Op104 更新血條比例；魔法傷害**不**在技能檔內另送 Op104，客戶端依 Op35/Op57 內之 damage 做飄字與受擊表現。
-   **客戶端**：
    -   **Op35**：物理攻擊經 ObjectAttacked → PrepareAttack → 關鍵幀 → HandleEntityAttackHit。單體魔法（光箭、變身怪物魔法等）由封包 **magicFlag==6** 觸發 **ObjectMagicHit** → OnObjectMagicHit → 立即 HandleEntityAttackHit（權威判斷，不依 actionId）。
    -   **Op57**：目前僅綁定 **OnMagicVisualsReceived**（視覺＋去重）。**傷害結算**須在同一處理內完成：每次收到 Op57 一筆（每目標一次）時，在處理完視覺後呼叫 **HandleEntityAttackHit(targetId, damage)**（targetId > 0），以與 Op35 共用同一套飄字／受擊邏輯，不重複開發。

#### 6.3.9. 播放間隔／冷卻規則（攻擊與魔法頻率）

以下為客戶端**攻擊與魔法**的統一約束，對應資料：`Client/Data/SprDataTable.cs`（walk / attack / spell interval）、`Client/Game/SpeedManager.cs`（CanPerformAction）、`Client/Game/GameWorld.Combat.cs`（攻擊冷卻）、`Client/Game/GameWorld.Skill.cs`（UseMagic 魔法冷卻）。

-   **依 gfx 判斷（單一依據）**：
    -   攻擊頻率、步行速度、攻擊冷卻、魔法冷卻的**唯一依據**為客戶端以 **gfxId** 查表；全遊戲皆依**當前角色／變身外觀的 GfxId** 決定可觸發下一次動作的時機。
    -   **攻擊**：`SprDataTable.GetInterval(ActionType.Attack, _myPlayer.GfxId, actionId)`、`SpeedManager.CanPerformAction(ActionType.Attack, _myPlayer.GfxId, actionId)`（`GameWorld.Combat.cs`）。
    -   **步行**：移動間隔由 `SprDataTable.GetInterval(ActionType.Move, gfxId, 0)` 取得（`SpeedManager` / `GameWorld.Movement.cs`）。
    -   **魔法**：`SpeedManager.CanPerformAction(ActionType.Magic, _myPlayer.GfxId, ACT_SPELL_DIR)`、`SprDataTable.GetInterval(ActionType.Magic, _myPlayer.GfxId, …)`（`GameWorld.Skill.cs`、法師 Z 鍵光箭）。
    -   **資料來源**：間隔值來自 **SprDataTable**，其內容對應服務端 **sprite_frame** 表導出（key = gfxId + actionId）；list.spr 透過 ListSprLoader 解析後，每個 GfxId 亦有幀級 **RealDuration**（sprite_frame 級別）。故不同變身外觀（不同 gfxId）會有不同的攻擊速度、冷卻時間與步行間隔。

-   **原則**：
    1.  **本地客戶端只管播放**：動畫由客戶端依本地資料播放；封包只負責結算傷害等。
    2.  **封包控制結算**：伺服器回傳（Op35/Op57）只用於結算；若該次施法／攻擊動畫已播過，則只結算、不重播。
    3.  **播放次數 = 冷卻允許的觸發次數**：無論用戶按多少次按鈕（或自動攻擊觸發多少次），**必須等冷卻期結束後，才響應下一次「播放動作＋發送封包」**；實際播放與發包次數由**攻擊／施法頻率**決定。

-   **間隔資料來源**：**SprDataTable**（`Client/Data/SprDataTable.cs`）記錄各角色 **GfxId** 的動作間隔（毫秒）：
    -   **Move (walk)**：actionId 0, 4, 11, 20, 24, 40 等 → 移動間隔（每格步進）。
    -   **Attack**：actionId 1, 5, 12, 21, 25, 41 等 → 攻擊間隔（兩次普攻之間最少間隔）。
    -   **Spell (魔法)**：actionId 18, 19 → 施法間隔（兩次魔法之間最少間隔）。
    取得方式：`SprDataTable.GetInterval(ActionType.Attack, gfxId, actionId)` / `GetInterval(ActionType.Magic, gfxId, actionId)`；綠水／勇水等 Buff 在 **SpeedManager** 內對間隔做倍率（如 0.75）。

-   **攻擊（含自動攻擊）**：
    -   手動攻擊（按鍵／點擊）與自動攻擊皆經 **PerformAttackOnce**；其內呼叫 **SpeedManager.CanPerformAction(ActionType.Attack, gfxId, actionId)**，未過冷卻則不發包、不播攻擊動畫。
    -   自動攻擊另由 **GameWorld.Combat** 的 **HandleExecutingState** 以 **SprDataTable** 攻擊間隔與 **\_attackCooldownTimer** 約束，冷卻內不進入下一次 **ExecuteAttackAction**。
    -   故攻擊播放次數與發包次數 = 冷卻允許的次數（由 SprDataTable 攻擊頻率約束）。

-   **魔法**：
    -   **UseMagic** 入口處呼叫 **SpeedManager.CanPerformAction(ActionType.Magic, _myPlayer.GfxId, ACT_SPELL_DIR(18))**；未過冷卻則**不播放動畫、不發送 C_MagicPacket**，直接 return。
    -   過冷卻後才：施法動作 → 先播放（落點或飛行＋連貫）→ 發送 C_MagicPacket；伺服器應答 Op35/Op57 只結算傷害，己方不重播動畫（見 §6.3.8）。
    -   故魔法播放次數與發包次數 = 冷卻允許的次數（由 SprDataTable 施法間隔約束）。

-   **小結**：動畫播放次數由「按鈕／自動觸發次數」經**冷卻過濾**後決定；冷卻資料來自客戶端 **SprDataTable** 的 walk / attack / spell 間隔，與 **SpeedManager** 一致。

### 6.3.10. 魔法／技能相關未完善功能

以下為客戶端與伺服器魔法／技能對齊後，仍待補齊的項目（供後續統一修改用）。

| 項目 | 說明 | 備註 |
|------|------|------|
| **精靈魔法 (skill_id 129+)** | 客戶端目前僅支援 PC 魔法 skill_id 1–50；精靈魔法的等級對應與「是否已學」邏輯不同，施法封包與學習狀態檢查尚未實作。 | 對齊 `server/world/instance/skill/PcSkill.java`、S_SkillAdd 的 elf1/elf2/elf3 等。 |
| **技能購買窗口 (Op 78)** | 客戶端已解析 S_SkillBuyList (Op 78)，並在 `OnSkillBuyListReceived` 留接口；尚未實作技能購買的 UI 介面。 | `Client/Game/GameWorld.Skill.cs` → OnSkillBuyListReceived。 |
| **Buff 圖標／AC 顯示、Op 29** | Buff 圖標與 AC 顯示、Op 29 Buff 圖標倒數等 UI／封包處理仍有 TODO 或未完善。 | 客戶端搜尋 TODO / Buff 相關。 |
| **壞物術 (Skill 18) type** | 若希望壞物術**必須選取敵方目標**（無目標時不施放），應在 **skill_list.csv** 中將技能 18 的 **type** 改為 **attack**；目前為 buff 會導致無目標時對自己施放。 | 見 §6.3.8.0。 |
| **伺服器 Op 57 必發** | 日光術(2)、燃燒的火球(16) 等，伺服器須在施法成功時**必定**發送 S_ObjectAttackMagic (Op 57)，即使無目標或目標無效，客戶端才能正確播放落點／特效。 | 見 §3.3 服務器需要修改的清單。 |

### 6.4. 魔法面板與 S_SkillAdd 對應（技能「已學習」顯示）

-   **伺服器協議**：Opcode 30 (S_SkillAdd) 發送 `type` 後為一串 **byte**，對應 `PcSkill.sendList()`：依 **skill_level (1–10)** 累加每個魔法的 **Id**（1,2,4,8,16）到 `lv[skill_level-1]`，封包順序為 lv1..lv10、6 個 0、elf1/elf2/elf3 等（見 `server/network/server/S_SkillAdd.java`、`server/world/instance/skill/PcSkill.java`）。
-   **資料來源**：`server/database/skill_list`（及 `skill_list.sql`）定義每個 **skill_id** 的 **skill_level** 與 **id**；PC 魔法 1–50 為 skill_level 1–10、每級 5 格，id 依序為 1,2,4,8,16（位元對應）。
-   **曾發生的錯誤**：客戶端原先以 `levelIdx = (skillId - 1) / 8`、`bitIdx = (skillId - 1) % 8` 解讀掩碼，與伺服器「每級 5 格」不一致，導致僅 1 級前幾格對應、2 級起與資料庫不符。
-   **正確對應（已修正）**：與 `skill_list` 表一致，**每級 5 格**：
    -   `levelIdx = (skillId - 1) / 5`（skill_id 1–5→level1，6–10→level2，…，46–50→level10）
    -   `bitIdx = (skillId - 1) % 5`
    -   已學習判定：`(masks[levelIdx] & (1 << bitIdx)) != 0`
-   **實作位置**：`Client/Game/GameWorld.Skill.cs`（`HasLearnedSkill`）、`Client/UI/Scripts/SkillWindow.cs`（`CheckLearned`）。兩處必須使用相同公式，與資料庫／封包一致。

### 6.5. 待機姿勢定義 (Stand/Idle Pose) — 見第 8 章

待機圖像與播完後行為已統一為 **§8 待機規則與幀播放規則**，不再使用「walk 第一幀／攻擊第一幀」作為替身。

### 6.6. 死亡與索敵規則 (Death & Targeting)
-   **權威判定**：嚴格遵循伺服器 `ActionCodes.java` 定義的 `ACT_DEATH (8)`。
-   **屍體過濾**：自動找怪（Z鍵）邏輯必須過濾掉動作碼為 8 的實體。屍體會保留 60 秒直到伺服器發送 `S_ObjectRemove` (Opcode 21)，在此期間不可被再次選中為攻擊目標。
-   **死亡缺圖 Teleport-Fallback 規則（169 特效）**：
    -   實作位置：`Client/Game/GameWorld.Entities.cs` → `OnObjectAction(int objectId, int actionId)`。
    -   當伺服器下發 `actionId == 8`（死亡）時，客戶端會透過 `ListSprLoader.Get(entity.GfxId)` 取得對應的 `SprDefinition`：
        -   若 `def == null`（例如 GfxId=841 在 `list.spr` 中完全不存在），或
        -   `def.Actions` 不包含 `ActionId = 8`（例如 GfxId=790 沒有定義 `8.Death` 動作），
        -   則視為「此角色缺少死亡動畫」。
    -   在上述情況下，不再嘗試播放死亡幀，而是：
        1.  在該實體當前座標播放 `gfxId = 169` 的「指定傳送」特效（`SpawnEffect(169, entity.GlobalPosition, entity.Heading, entity)`），營造怪物被傳送飛走的效果。
        2.  輸出橘色診斷日誌：`[Death-Fallback] GfxId=XXX 無 8.Death 動畫，改用 169 傳送特效並提前刪除 ObjId=YYY`。
        3.  立即呼叫 `OnObjectDeleted(objectId)`，提早將實體從場景與 `_entities` 字典中移除，避免畫面上留下「沒有屍體圖片」的卡死對象。
    -   若 `list.spr` 中有合法的 `8.Death` 定義，則**一律保持伺服器原意**：直接呼叫 `entity.SetAction(8)` 播放正常死亡動畫，不觸發上述 Teleport-Fallback。

### 6.7. 職業專屬規則 (Class Specifics)
-   **法師 (734)**：默認具備遠程魔法攻擊特性，攻擊距離參考弓箭手（默認 6 格），支持預判射擊與邊走邊打邏輯。

### 6.8. 視覺診斷日誌（Visual Debug Logs，禁止移除）

為了在不改動伺服器資料的前提下精確偵測「資源缺失／方向缺失／替身退回」等問題，客戶端在 `GameEntity.Visuals.cs` 中實作了三種關鍵日誌。這三條日誌屬於**核心診斷機制，不得刪除或關閉**，後續修改必須保留原字串與顏色標記：

-   **`[Visual-Missing] list.spr 無定義`**  
    -   觸發位置：`UpdateAppearance` 內，`ListSprLoader.Get(gfxId)` 回傳 `null` 時。  
    -   含義：`list.spr` 中完全沒有這個 `GfxId` 的定義（例如 GfxId=841），屬於「資料缺檔 / 未配置此編號」。  
    -   對應日誌範例：  
        `"[Visual-Missing] list.spr 無定義 ObjId=... GfxId=841 action=3 head=0 name=... -> TryAsyncRetry"`。

-   **`[Visual-Missing] 動畫檔取得為空`**  
    -   觸發位置：`UpdateAppearance` 內，`_skinBridge.Character.GetBodyFrames(gfxId, finalAction, head)` 回傳 `null` 時。  
    -   含義：`GfxId` 在 `list.spr` 中有定義，但該 `action/head` 組合沒有對應的實際幀（例如 GfxId=790 沒有提供 `8.Death` 的圖片），屬於「動作／方向缺圖」。  
    -   對應日誌範例：  
        `"[Visual-Missing] 動畫檔取得為空 ObjId=... GfxId=790 action=8 head=6 name=$511 -> TryAsyncRetry"`。

-   **`[Visual-Fallback] 缺方向動畫`**  
    -   觸發位置：`ApplyFramesToLayer` 內，`SpriteFrames` 存在但 `frames.HasAnimation(anim)` 為 `false` 時。  
    -   含義：Spr 檔中存在此動作，但缺少當前方向（`head`）的動畫，系統會退回播放 `"0"` 動畫作為兜底替身，同時輸出紅字以協助定位資料問題。  
    -   對應日誌範例：  
        `"[Visual-Fallback] 缺方向動畫 ObjId=... name=... GfxId=... action=... head=... layer=... -> fallback to \"0\""`。

> **說明**：  
> - 若出現 **`[Visual-Missing] list.spr 無定義`**：代表 `list.spr` 裡根本沒有這個 `GfxId`，應先補齊資料或調整怪物配置。  
> - 若出現 **`[Visual-Missing] 動畫檔取得為空`**：代表 `GfxId` 有定義，但某個 `action/head` 缺少幀（常見為未配置 `8.Death`），此時會觸發前述「169 指定傳送特效 + 提前刪除」的死亡 fallback。  
> - 若出現 **`[Visual-Fallback] 缺方向動畫`**：代表動作存在但方向缺失，畫面會顯示 `"0"` 方向的替身圖像，方便在調整 Spr 資源時對照錯誤方向。

### 6.9. Visual-Missing 重試上限（避免刷屏，日誌保留）

-   **原因**：當某實體缺圖時會觸發 `TryAsyncRetry()`，約 0.2 秒後再次 `RefreshVisual()`，若仍缺圖則再次打日誌並重試，形成迴圈導致日誌刷屏；**並非每幀讀檔**。
-   **規則**：同一實體對「list.spr 無定義」或「動畫檔取得為空」的 **重試次數上限為 5**（`GameEntity._visualMissingRetryCount`、`VisualMissingRetryMax = 5`）。
-   **行為**：前 5 次照常輸出日誌並呼叫 `TryAsyncRetry()`；第 6 次起不再呼叫 `TryAsyncRetry()`、不再重複輸出同一條日誌，迴圈停止。一旦該次成功取得 `bodyFrames`，計數歸零，之後若再缺檔仍可再重試 5 次。
-   **日誌**：三條 Visual-Missing / Visual-Fallback 日誌**不得刪除**，僅以重試上限減少刷屏與無意義 IO。

### 6.10. 視野範圍與延後加載（只載入角色附近實體圖像）

-   **目的**：只對「與玩家同屏或附近」的實體載入圖像，其餘延後加載，減少客戶端壓力與無謂的缺圖日誌。
-   **常數**：`GameWorld.Entities.cs` 內 `VIEW_RANGE_CELLS = 24`（與玩家格距 dx、dy 皆 ≤ 24 才視為在視野內）。
-   **生成時**：`OnObjectSpawned` 時若非自己且 `IsEntityInViewRange(data.X, data.Y)` 為 false，則 `entity.Init(..., loadVisuals: false)`，不載入圖像、節點 `Visible = false`，並輸出 `[Visual-Defer] ObjId=... GfxId=... Map=(x,y) 距離玩家過遠 (格距 dx=... dy=...) 延後加載圖像`。
-   **進入範圍後**：每幀 `_Process` 呼叫 `UpdateDeferredVisuals()`，對所有 `IsVisualsDeferred` 的實體檢查是否已進入 24 格內，若是則 `EnsureVisualsLoaded()`（顯示並載入圖像）。
-   **延後期間**：`RefreshVisual()` 內若 `_visualsDeferred` 則直接 return，不觸發載圖與日誌。

### 6.11. list.spr 參數定義（104.attr、102.type 等，可陸續補齊）

以下為 list.spr 中常見元數據的**定義與客戶端使用方式**，用於**規範播放動畫與取文件名的規則**。根據 list.spr 內容，常見特別參數主要有六種：**100**（條目頭/編號）、**101**、**102**、**104**、**105**、**109**（106、110 等可陸續補齊）。

#### 權威定義總覽（程式唯一依據，避免重複判斷與代碼臃腫）

| 參數 | 定義 | 程式判斷方式 | 說明 |
|------|------|--------------|------|
| **102.type(9)** | **地面物品**（可拾取） | `ListSprLoader.Get(entity.GfxId)?.Type == 9` | **僅** 102.type(9) 屬於地面物品，才可被拾取；作為拾取邏輯的**第一優先與唯一依據**。程式不另寫複雜條件（如血條、Lawful 等）來定義「什麼是地面物品」。實作：`GameWorld.Input.cs` → `IsGroundItem(e)`；`GameWorld.Combat.cs` → `ExecutePickupTask` 發送拾取前亦依此驗證。 |
| **104.attr(8)** | 魔法定義（獨立魔法特效） | `ListSprLoader.Get(gfxId)?.Attr == 8` | 動畫取得優先 ActionId=0 再 3；與 102.type 搭配：僅當 104.attr(8) 且 102.type≠0 時套用魔法慢速。 |
| **102.type(5)/(10)** | 有血條（怪物/NPC/玩家） | `Type == 5 \|\| Type == 10`（即 `ShouldShowHealthBar()`） | 攻擊目標、魔法目標等僅選擇有血條實體；地面物品(9) 不可選為攻擊/魔法目標。 |

-   **原則**：上述定義為**權威**；客戶端邏輯**僅**依 list.spr 之 102.type、104.attr 等欄位判斷，不在此表以外重複定義「地面物品」「魔法」「有血條」等語義，避免代碼繁複與混亂。

#### 取檔與播放規則總覽

-   **文件名規則**：`{SpriteId}-{ActionId}-{FrameIdx:D3}.png`。例如 `66-0-002.png` 表示 SpriteId=66、ActionId=0、FrameIdx=2；`69-0-000.png` 表示單幀物品。
-   **SprFrame 播放順序（全局）**：動畫播放時**按 FrameIdx 升序**（0 → 1 → 2 …），與 list.spr 括號內 token 的**書寫順序**無關。例如 `0.fly(1 3, 0.2:1 0.0:1 0.1:1)` 解析後，播放順序為 FrameIdx 0、1、2（即 0.0:1、0.1:1、0.2:1），而非解析順序 0.2、0.0、0.1。實作：`ListSprLoader` 保留解析順序；`CustomCharacterProvider.BuildLayer` 遍歷前按 `FrameIdx` 排序後再加入 `SpriteFrames`。
-   **SprFrame 結構**（`Client/Utility/ListSprLoader.cs`）：`ActionId`（文件動作前綴，如 24.0:4 中的 24）、`FrameIdx`（spr 內幀索引，如 24.3:4 中的 3）、`DurationUnit`（時間單位，如 24.0:4 中的 4），以及 `RealDuration`、`IsKeyFrame`、`IsStepTick`、`RedirectId`、`SoundIds`、`EffectIds` 等。

#### 102.type(9)：地面物品（靜態顯示與拾取唯一依據）

-   **權威定義**：**102.type(9) 屬於地面物品**。客戶端拾取判斷**僅**依此：`ListSprLoader.Get(GfxId)?.Type == 9`，不另加血條、Lawful 等條件。
-   **含義**：**靜態物品**，直接在地面顯示的掉落物／物品（如箭矢、蛋、卷軸、金幣等）。同一 Gfx 可能被復用：**作為靜態物品**時只顯示「第一幀」；**作為動畫**時按幀播放。
-   **取檔規則**：
    1.  **優先讀取 SprDefinition 第一幀**：若有 `SprActionSequence`（例如弓箭有 `0.fly(1 3, 0.2:1 0.0:1 0.1:1)`），則取**該動作括號內第一個 token 對應的幀**作為靜態顯示幀。例如 #66 箭矢：`0.fly(1 3, 0.2:1 0.0:1 0.1:1)`，靜態物品取第一 token `0.2:1` → 檔名 `66-0-002.png`。
    2.  若無多幀動作（如 #69 蛋僅 `102.type(9)`），則直接讀該 Gfx 的**第一幀**，例如 `69-0-000.png`。
-   **小結**：**102.type(9) = 靜態時只使用「動畫第一幀」**——即 list.spr 括號內**第一個 token** 對應的幀（`seq.Frames[0]`，解析順序）；作為動畫時則按 **FrameIdx 升序**正常播放。範例：`#66	8	arrow` 與 `0.fly(1 3, 0.2:1 0.0:1 0.1:1)`、`102.type(9)` → 靜態顯示 `66-0-002.png`；`#69	1	egg`、`102.type(9)` → 直接讀 `69-0-000.png`。

#### 104.attr (魔法定義／屬性)

| 值 | 含義 | 客戶端使用 |
|----|------|-------------|
| 8 | 魔法特效（獨立在場景中播放的魔法） | 動畫取得優先 ActionId=0 再 ActionId=3；**與 102.type 搭配**：僅當 **104.attr(8) 且 102.type≠0** 時，才對該 Gfx 套用「魔法慢速」（每幀 ×2）。104.attr(8) 但 102.type(0)（如 #242 死騎光劍）與角色主體同步，不套用慢速。 |
| 0 / 未寫 | 非魔法定義 | 不走魔法專用邏輯。 |

-   **重要**：104.attr(8) **不等於**「一定套用魔法慢速」。須同時滿足 **102.type ≠ 0** 才視為「純魔法」並套用慢速；102.type(0) 表示與角色主體同步（陰影、光劍等），必須與角色同速。

#### 102.type (類型／與主體關係)

| 值 | 含義 | 客戶端使用 |
|----|------|-------------|
| 0 | 與角色主體同步（陰影、光劍、附屬物等） | 若同時為 104.attr(8)（如 #242），**不**套用魔法慢速，與角色播放速度一致。動畫單幀／8 方向仍僅依動作的 DirectionFlag，不依 102.type。 |
| 9 | **地面物品**（掉落物、金幣、卷軸、箭矢等，可拾取） | **唯一**拾取判斷依據；**不**作為動畫單幀／多幀判斷；單幀／8 方向僅依 DirectionFlag。見 §6.11 權威定義總覽。 |
| 8 | 多處（箭矢、魔法等） | 待補。 |
| 10 | 大量（怪物等） | 待補。 |
| 其他 | 陸續收錄 | 可依調適結果補表。 |

-   **核對建議**：若需釐清 102.type 的正式用途，請對照伺服器或原始資料；客戶端僅在「魔法慢速」條件中使用 **Type != 0**，其餘動畫路徑不依 102.type。

#### 109.effect（連環／連續特效）

-   **用途**：用於**連續多次播放**同一序列中的多個魔法／特效。例如第一個動畫（如 Gfx 171）播畢後，在**相同位置**（或跟隨目標頭上）自動播放下一個動畫（如 Gfx 218）。
-   **語法**：`109.effect(a b)`。其中 `a` 為條目鍵（可為 0、5 等），`b` 為下一個要播放的 **GfxId**。一個 Gfx 可有多條 109 條目（如 `109.effect(0 218)`、`109.effect(5 219)`）；客戶端取 **EffectChain 中第一個 value > 0** 的條目作為下一段 GfxId。
-   **客戶端行為**：
    -   **魔法**：由 `S_ObjectAttackMagic`（Op 57）等觸發的 `SpawnEffect` 創建之 `SkillEffect`，在動畫生命週期結束時（`OnAnimationFinished`）檢查該 Gfx 的 `EffectChain`；若有下一段 GfxId，則呼叫連環回調，在**當前位置或跟隨目標位置**再 `SpawnEffect(nextGfxId, ...)`，形成鏈式播放。
    -   **角色／幀特效**：list.spr 幀元數據中的 **effects**（]effectId 列表）在 `GameEntity.Visuals` 的 `ProcessFrameMetadata` 中觸發 `GameWorld.SpawnEffect(eid, ...)`；這些由**角色幀**觸發的特效同樣帶有跟隨目標與連環回調，因此 **109.effect 不僅適用於魔法，只要該 Gfx 在 list.spr 中定義了 109.effect，都會在播畢後自動播放下一段**。
-   **小結**：**只要有 109.effect 定義，不論是魔法還是角色相關特效，客戶端都會在該段動畫播畢後，在相同／跟隨目標位置播放下一個 GfxId，實現連環播放。**

#### 其他 list.spr 參數（可陸續擴充）

-   **100**：條目頭（`#GfxId` 行，如 `#66	8	arrow`，定義該條 GfxId、SpriteId、名稱）。
-   **101**：陰影 GfxId（該角色/物品使用的陰影圖編號）。
-   **105**：服裝層 GfxId 列表。
-   **106**：武器層 GfxId（劍/斧/弓/矛/杖）。
-   **110**：framerate（僅供參考，客戶端動畫時長以 DurationUnit×40ms 為準）。

### 6.12. list.spr 102.type 一覽（僅供查閱，不參與動畫單幀判斷）

-   **說明**：**客戶端動畫單幀／8 方向僅依動作的 DirectionFlag**；102.type 僅在「魔法慢速」條件（Attr==8 且 Type!=0）中使用。
-   **一覽表**（與 §6.11 對照，可陸續補齊）：

| 102.type 值 | 在 list.spr 中出現 | 備註 |
|-------------|--------------------|------|
| 0 | 大量（建築、裝飾、**#242 死騎光劍**等） | 與主體同步；104.attr(8)+102.type(0) 不套用魔法慢速。 |
| 8 | 箭矢、魔法等 | 待補。 |
| 9 | **地面物品**（掉落物、金幣、卷軸等，可拾取） | **唯一**拾取判斷依據；**不**作為動畫單幀判斷。見 §6.11 權威定義。 |
| 10 | 怪物等 | 待補。 |
| 其他 | 見 §6.11 | 陸續收錄。 |

## 7. 服務器移動協議與座標同步機制 (Server Movement Protocol & Coordinate Sync)

### 7.0. 服務器移動協議分析報告（權威結論）

**重要結論**：服務器**沒有**640ms心跳機制。服務器只檢查移動速度（間隔時間），不要求定期心跳。

#### 7.0.1. C_MoveChar (Opcode 10) 處理流程

**服務器處理**（`server/network/client/C_Moving.java`）：
```java
// 客戶端發送：writeH(x), writeH(y), writeC(h)
// 服務器接收後直接調用：
pc.toMove(this.x, this.y, this.h);
```

**服務器移動驗證**（`server/world/instance/PcInstance.java`）：
```java
public void toMove(int x, int y, int h)
{
    // 1. 速度檢查（如果啟用）
    if (Config.CHECK_SPEED_TYPE == 2)
        getCheckSped().checkInterval(CheckSpeed.ACT_TYPE.MOVE);
    else
        this.move.check();
    
    // 2. 距離驗證（只能移動1格）
    if (getDistance(x, y, getMap(), 1)) {
        // 3. 根據朝向調整座標
        switch (h) { ... }
        
        // 4. 執行移動
        super.toMove(x, y, h);
    }
}
```

#### 7.0.2. 速度檢查邏輯（CheckSpeed.java）

**關鍵代碼**：
```java
// CheckSpeed.getRightInterval() - 移動時
case MOVE:
    interval = SprTable.getInstance().getAttackSpeed(
        this._pc.getGfx(), 
        this._pc.getGfxMode() + 1  // 注意：gfxMode + 1
    );
```

**重點**：
- 移動速度檢查使用 `getAttackSpeed(gfx, gfxMode + 1)`，**不是** `getMoveSpeed`
- 間隔時間來自數據庫 `sprite_frame` 表的 `frame` 字段
- 如果間隔時間 < `rightInterval`，會被判定為加速
- 連續10次加速會被懲罰（傳送回原點、凍結或踢下線）

#### 7.0.3. 座標確定方式

服務器通過以下方式確定玩家座標：

**A. 直接使用客戶端座標**
```java
// L1Object.toMove()
setX(x);  // 直接設置客戶端發送的座標
setY(y);
```

**B. 距離驗證**
```java
// 只能移動1格（歐幾里得距離）
if (getDistance(x, y, getMap(), 1)) {
    // 允許移動
}
```

**C. 安全座標（TempX/TempY）**
```java
// 當玩家移動超過12格時，更新安全座標
if (!getDistance(getTempX(), getTempY(), getMap(), 12)) {
    setTempX(x);
    setTempY(y);
    updateWorld();  // 更新世界地圖
}
```

**D. 座標同步給其他玩家**
```java
// L1Object.updateObject()
// 當其他玩家在14格範圍內時，發送移動封包
if (getDistance(o.getX(), o.getY(), o.getMap(), 14)) {
    if (containsObject(o)) {
        if ((o instanceof PcInstance))
            o.SendPacket(new S_ObjectMoving(this));  // Opcode 18
    }
}
```

#### 7.0.4. 總結對照表

| 項目 | 服務器邏輯 |
|------|-----------|
| **心跳機制** | ❌ 沒有640ms心跳要求 |
| **速度檢查** | ✅ 檢查移動間隔時間（使用 `getAttackSpeed(gfx, gfxMode + 1)`） |
| **座標來源** | ✅ 直接使用客戶端發送的座標（但驗證距離≤1格） |
| **座標同步** | ✅ 通過 `S_ObjectMoving` (Opcode 18) 發送給其他玩家 |
| **安全座標** | ✅ 維護 `tempX/tempY` 作為安全座標（用於加速檢測後的傳送） |

#### 7.0.5. 客戶端對齊要求

1. **不要強制每640ms發送心跳**：只在移動時發送 `C_MoveChar` 封包
2. **移動間隔時間**：應使用 `SprDataTable.GetInterval(ActionType.Attack, gfxId, gfxMode + 1)`（注意：是 `Attack` 不是 `Move`）
3. **距離限制**：確保每次移動只移動1格
4. **座標同步**：服務器會自動同步座標給其他玩家，無需額外處理

**重要**：服務器沒有640ms心跳機制，只檢查移動速度。客戶端應根據實際移動速度發送移動封包，而不是定期心跳。

## 8. 角色移動系統 (Character Movement System)

### 8.1 職責與模組邊界
-   **`GameWorld.Movement.cs`（邏輯層）**：
    -   負責 `StartWalking` / `StopWalking` / `UpdateMovementLogic` / `StepTowardsTarget`。
    -   定時呼叫 `SprDataTable.GetInterval(ActionType.Move, gfxId, 0)` 取得**每步移動冷卻時間（毫秒）**，並轉為秒：`_moveInterval = baseInterval / 1000f`。
    -   僅在 `_moveTimer >= _moveInterval` 時推進一步（呼叫 `_myPlayer.SetMapPosition(nextX, nextY, heading)`），確保與伺服器 `CheckSpeed.java` 對齊。
    -   若 `_myPlayer.AnimationSpeed > 1.0f`（如綠水/勇水），僅在此處套用 `*_0.75` 的合法加速縮放。

-   **`GameEntity.Movement.cs`（表現層）**：
    -   負責「單步」的平滑位移與受擊僵直，而**不決定總移動距離與節奏**。
    -   對外公開的關鍵介面：`SetMapPosition(int x, int y, int h = 0)`、`OnDamageStutter()`。

### 8.2 `SetMapPosition` 關鍵流程（更新後）

```text
public void SetMapPosition(int x, int y, int h = 0)
1) 基礎檢查：若 x < 0 或 y < 0，直接 return。
2) 若存在舊的 `_moveTween` 且仍有效，先 Kill 並置 null（防止殘留回調干擾新一步）。
3) 計算位移差量：
   dx = x - MapX
   dy = y - MapY
4) 更新邏輯座標：
   MapX = x
   MapY = y
5) 更新朝向：
   SetHeading(h)
6) 計算目標像素座標（對齊地圖原點，單格 32 像素，中心偏移 0.5 格）：
   origin = GameWorld.CurrentMapOrigin   // 通常為 (0,0)
   localX = (x - origin.X + 0.5f) * GRID_SIZE
   localY = (y - origin.Y + 0.5f) * GRID_SIZE
   targetPos = (localX, localY)
7) 計算距離與移動模式：
   dist = Position.DistanceTo(targetPos)
   isSmoothMove = dist > 0 && dist < TELEPORT_THRESHOLD
8) 依模式執行：
   - 若 isSmoothMove 為 true：
       SetAction(ACT_WALK)
       _moveTween = CreateTween()
       _moveTween.TweenProperty(this, \"position\", targetPos, BASE_MOVE_DURATION)
                 .SetTrans(Tween.TransitionType.Linear)
   - 若 isSmoothMove 為 false（傳送/長距離修正）：
       Position = targetPos
       若 !_isActionBusy，則 SetAction(ACT_BREATH)
```

### 8.3 移動相關常數（必須保持一致）

這些常數目前定義於 `GameEntity.Movement.cs`，與 1999 年移動體感對齊，**請勿任意修改**：

-   **`GRID_SIZE = 32`**  
    -   每個地圖格對應 32 像素，所有角色與技能落點都以此為單位。

-   **`TELEPORT_THRESHOLD = 256.0f`**  
    -   以像素為單位，約等於 8 格距離（`256 / 32 = 8`）。
    -   若 `dist >= TELEPORT_THRESHOLD`，本次視為「瞬移/位置校正」，直接設置 `Position = targetPos`，不做 Tween。

-   **`BASE_MOVE_DURATION = 0.6f`**  
    -   單格平滑移動耗時（秒），對應無加速狀態下的標準移動速度。
    -   配合 `SprDataTable.GetInterval(ActionType.Move, ...)` 與伺服器 `sprite_frame` 設定，共同決定整體節奏。

### 8.4 受擊僵直 (`OnDamageStutter`)

-   實作位置：`GameEntity.Movement.cs`。  
-   呼叫入口：`GameWorld.Combat.cs` 在實體實際受到傷害時呼叫 `target.OnDamageStutter()`。  
-   行為：
    -   若 `_moveTween` 存在且有效，立刻 `Kill()` 並設為 null。  
    -   不修改 `MapX/MapY`，僅讓當前平滑位移在「半路」中止，營造原版受擊停頓感。  
    -   僵直結束後，由 `GameWorld` 下一次移動心跳決定是否繼續尋路。

### 8.5 `*-main-branch.cs` 檔案說明

-   `Client/Game/GameEntity.Action-main-branch.cs`  
-   `Client/Game/GameEntity.Movement-main-branch.cs`  
-   `Client/Game/GameEntity.Visuals-main-branch.cs`  

這三個檔案為「架構演進參考實作」，**不參與編譯**：  
專案檔 `lingamev2.csproj` 中已加入：

```xml
<ItemGroup>
  <Compile Remove=\"**/*-main-branch.cs\" />
</ItemGroup>
```

實際運行版本一律以無 `-main-branch` 後綴的 `GameEntity.*.cs` 為準。  
若未來需要對比或回滾，請只在 Git 層面切換分支或還原，避免同時啟用兩套 partial 導致重複定義與行為衝突。


## 9. 待機規則與幀播放規則 (Idle & Frame Playback Rules)

以下為**已實作並強制遵守**的規則，對應檔案：`Client/Utility/ListSprLoader.cs`、`Skins/CustomFantasy/CustomCharacterProvider.cs`、`Client/Game/GameEntity.cs`、`Client/Game/GameEntity.Action.cs`。後續修改不得與本章衝突。

### 8.0. SprActionSequence 取得規則（全局唯一：ActionId + Name 語義關鍵字）

-   **玩家角色**：依武器有 6 套外觀（空手 + 5 種武器），對應不同 ActionId（如 0/4/11/20/24/40 為 walk），邏輯已存在不變。
-   **怪物／弓箭手／法師等**：部分無空手外觀（例如只有 `20.walk bow`、無 `0.walk`），伺服器不會傳 ActionId=20，客戶端請求 walk 時只會查 0，導致找不到。故**升級為雙重取得**：
    1.  先依 **ActionId** 查 `def.Actions[actionId]`。
    2.  若無，再依 **Name 語義關鍵字** 搜尋：在 `def.Actions` 中找第一個 `Name`（不分大小寫）包含對應關鍵字的序列。
-   **唯一入口**：`ListSprLoader.GetActionSequence(SprDefinition def, int actionId)`。全專案僅此一處定義「先 ActionId 再 Name」；呼叫端（如 `CustomCharacterProvider.BuildLayer`）僅呼叫 `GetActionSequence`，不得再寫重複的 ContainsKey／Name 比對。
-   **語義關鍵字對照（寫死、與 list.spr 對齊）**：

| actionId 範例 | 關鍵字 |
|---------------|--------|
| 0,4,11,20,24,40 | walk |
| 1,5,12,21,25,41 | attack |
| 2,6,13,22,26,42 | damage |
| 3,7,14,23,27,43 | breath, idle |
| 8 | death |
| 15,16,17,18,19 | get, throw, wand, spell |

-   範例：請求 actionId=0（walk）時，若無 `0.walk`，則依 Name 搜 `"walk"`，可命中 `20.walk bow`，弓箭手怪物即可正確顯示走路。

### 8.1. 待機規則（角色圖像來源）

-   **圖像來源**：待機圖像由 **ActionId** 與 **Name**（邏輯 ID + 動作名）共同決定，不再僅依 ActionId 或僅取數字 3。
-   **解析順序（唯一入口：`ListSprLoader.GetIdleSequence(SprDefinition def)`）**：
    1.  先查 **ActionId = 3**，有則回傳該序列。
    2.  若無，再依 **Name** 查：等於或包含 `"breath"` 或 `"idle"`（不分大小寫；例如 `"breath row"` 可）。
    3.  以上皆無則回傳 `null`，**呼叫端不做待機**（不替身、不取 walk／攻擊第一幀）。
-   **資料結構參考**：`SprActionSequence`（`ListSprLoader.cs`）含 `ActionId`（邏輯 ID，如 0=walk）、`Name`（動作名）。

### 8.2. SprFrame 幀動作播放規則（全局、不重複定義）

-   **定義**：每一幀動作如 `25.attack Spear(1 5,160.0:4 136.0:4 136.1:4 136.2:4[255 136.3:6!)`，括號內為幀列表。
-   **播放順序**：括號內幀依 **解析順序（自然先後）** 播放，**不**依 `160.0:4` 中間的數字排序。
-   **播完後行為**：
    -   **循環動作**：繼續循環播放。
    -   **非循環動作**：停留在**當前動作的第一幀**，停止不動（不回到其他動作）。
-   **適用範圍**：對**所有角色、所有動畫**生效；邏輯集中封裝，不在他處重複定義。

### 8.3. 統一待機規則（動作播完後的唯一行為）

每次動作播放完成後（`OnUnifiedAnimationFinished`）：

1.  依 **幀播放規則** 決定「播完後」表現。
2.  **若該角色有待機動畫**（`GetIdleSequence(def) != null`）：播放待機動作（`CurrentAction = idleSeq.ActionId + _visualBaseAction`，`RefreshVisual()`）。
3.  **若沒有待機動畫**：依上述 §8.2「非循環」規則，回到**第一幀並停止**（呼叫 `StopAllLayersOnFirstFrame()`，全局唯一封裝）。

### 8.4. 實作對應表

| 項目 | 檔案 | 說明 |
|------|------|------|
| **動作序列取得（全局唯一）** | `ListSprLoader.cs` | `GetActionSequence(def, actionId)`：先 ActionId，再 Name 語義關鍵字（walk/attack/damage/breath/idle/death/get/throw/wand/spell） |
| 請求幀時取序列 | `CustomCharacterProvider.cs` | `GetActionSequence(targetDef, actionId) ?? GetActionSequence(refDef, actionId)`，不重複寫 ContainsKey／Name 邏輯 |
| 待機解析 | `ListSprLoader.cs` | `GetIdleSequence(def)`：ActionId=3 → Name 含 breath/idle → 否則 null |
| 請求待機時取序列 | `CustomCharacterProvider.cs` | 待機類動作由 `GetActionSequence` 一併支援（Name 含 breath/idle 即為待機序列） |
| 循環與否 | `CustomCharacterProvider.cs` | `loop = isIdleSequence \|\| IsLoopingAction(actionId)` |
| 預設待機姿勢 | `GameEntity.cs` | `SetDefaultStandAction()`：有 idleSeq 才設 `CurrentAction` 並 `RefreshVisual()`，否則 return |
| 播完後行為 | `GameEntity.Action.cs` | `OnUnifiedAnimationFinished()`：有待機則播待機，否則 `StopAllLayersOnFirstFrame()` |
| 停第一幀（全局唯一） | `GameEntity.cs` | `StopAllLayersOnFirstFrame()`：主體／武器／陰影／服裝皆 Frame=0 且 Stop() |

### 8.4.1. 待機/呼吸缺圖 fallback（伺服器 action 3/7 驗證）

-   **伺服器行為（已驗證）**：
    -   **action=3**：伺服器可發送 `S_ObjectAction(cha, 3)`（如 `ItemMagicInstance` 對**玩家**發送）；怪物預設 `gfxMode=0`（`MonsterInstance`），故**一般不會**對青蛙等怪物發送 action=3，但協定上允許任意 actionId。
    -   **action=7**：玩家持劍時 `gfxMode=4`（武器 `getGfxmode()`）；伺服器**不會**把 gfxMode 設成 7。客戶端在呼叫 `SetAction(ACT_BREATH)` 時會算出 `finalSequenceId = 3 + _visualBaseAction`，持劍即 **7**，故 **action=7 來自客戶端**（待機＝呼吸＋劍），非錯誤的僵硬動畫 id。
-   **僵硬動畫**：受擊僵硬使用 **ACT_DAMAGE(2)**，持劍時為 **action=6**（`6.damage sword`），程式未誤用 action=7。
-   **客戶端 fallback**：若 list.spr 未定義待機/呼吸（如青蛙無 `3.breath`、玩家 GfxId 61 無 `7.breath`），`CustomCharacterProvider.BuildLayer` 會改為使用**對應行走動作的第一幀**：3→0、7→4、14→11、23→20、27→24、43→40，僅取一幀、循環顯示，避免 `[Visual-Missing]`。

### 8.5. 已廢止、禁止再用的邏輯

-   替身動畫、以**攻擊第一幀**作為待機、以 **walk 第一幀**作為待機之任何寫法，均已移除。
-   不得再新增「無待機時 fallback 到 Walk(0)／攻擊第一幀」等替身邏輯；無待機時僅能「不做待機」或「停第一幀」。
-   若需放寬待機（例如更多 Name 關鍵字），僅能在 `ListSprLoader.GetIdleSequence` 內擴充比對條件，不得在別處重複定義待機來源。

### 8.6. 102.type(9) 地面物品與 104.attr(8) 魔法識別規則

**權威定義**（與 §6.11 一致）：**102.type(9) = 地面物品**；**104.attr(8) = 魔法定義**。程式僅依 list.spr 此二參數判斷，不另寫複雜條件。

以下規則在 `Skins/CustomFantasy/CustomCharacterProvider.cs` 的 `BuildLayer` 中實作，怪物與玩家沒有這些屬性，故不影響角色／怪物邏輯。

-   **102.type(9) 地面物品**：
    -   當 `list.spr` 中該 GfxId 有 **102.type(9)** 時，判定為**地面物品**（可拾取；如 #22 scroll、金幣等）。拾取邏輯**唯一**依此判斷，見 §6.11。
    -   不依 ActionId／方向取動畫，改走 `BuildType9ItemLayer`：**僅讀一張圖**，檔名與正常動畫第一幀同一規則，即 **{SpriteId}-0-000.png**（action=0, frame=0），例如 #22 → `22-0-000.png`。
    -   產出一個只有動畫名 `"0"`、單幀的 `SpriteFrames`，無方向、無多段 ActionId。

-   **104.attr(8) 魔法**：
    -   當該 GfxId 有 **104.attr(8)** 時，判定為**魔法**。
    -   組檔時**不需武器映射**，僅用**一組動畫**：優先取 **ActionId=0** 的序列，若無則取 **ActionId=3**；只從 **targetDef** 取序列，不套 refDef（角色／武器）邏輯。
    -   其餘方向、幀、速度等仍照既有邏輯，僅動作來源固定為 0 或 3 這一組。

## 10. 攻擊系統 (Attack System)

以下為已實作並通過測試的攻擊規則與重構說明。對應檔案：`Client/Game/GameWorld.Combat.cs`、`GameWorld.Entities.cs`、`GameEntity.CombatFx.cs`、`GameEntity.cs`。詳細設計文件見 **`docs/ATTACK_SYSTEM_REFACTOR.md`**。

### 9.1. 攻擊規則（權威與流程）

-   **權威**：攻擊是否命中、傷害數值、MISS 判定均由**伺服器**決定；客戶端僅負責發送攻擊封包、播放動畫與飄字。
-   **流程**：
    1.  玩家按 Z 或選定目標後，戰鬥邏輯建立 **AutoTask(Attack, target)**，目標唯一來源為 **`_currentTask?.Target`**（見 §9.2）。
    2.  若目標超出距離，先 **Moving** 接近；進入範圍後進入 **Executing**，依 **冷卻** 與 **SpeedManager** 決定何時發送攻擊封包並播放攻擊動畫。
    3.  伺服器回傳 `ObjectAttacked`／`ObjectRangeAttacked` 後，客戶端依 **damage** 與 **關鍵幀** 觸發飄字、僵硬動畫與音效。
-   **MISS 與傷害飄字**：
    -   **MISS（damage ≤ 0）**：僅顯示 MISS 飄字，**不**播放受擊僵硬動畫（不呼叫 `SetAction(ACT_DAMAGE)`）。僵硬動畫僅在 **damage > 0** 時由 `GameWorld.Combat.HandleEntityAttackHit` 觸發。
-   **僵硬動畫**：受擊方使用 **ACT_DAMAGE(2)**，持劍時為 action=6（`6.damage sword`）；播放由 `list.spr` 與 `CustomCharacterProvider` 關鍵幀（`tex.SetMeta("key", true)`）驅動。
-   **音效**：攻擊命中／MISS 之音效依現有戰鬥特效流程播放，與飄字、僵硬同步。
-   **Z 鍵語義**：Z =「以當前視野**重新掃描**最佳目標並開始攻擊」。每次按 Z 都會執行掃描；有目標則**覆蓋**當前攻擊任務，無目標則**清除**攻擊任務，故可持續按 Z 切換下一隻怪。
-   **僵硬時是否可移動**：由 `GameWorld.AllowMoveWhenDamageStiffness`（預設 `true`）控制。預設為**允許**在受擊僵硬時移動；若設為 `false`，則與 `GameEntity.DamageStiffnessBlocksMovement` 連動，僵硬時禁止移動。

### 9.2. 攻擊系統重構與修改摘要

重構目標：**Z 一律可重選目標、攻擊只由冷卻與伺服器速率節流、單一目標來源**。具體修改如下：

| 項目 | 修改內容 |
|------|----------|
| **Z 鍵 ScanForAutoTarget** | **一律重掃**；有目標則覆蓋當前攻擊任務（必要時 Forced 入隊），無目標則清除攻擊任務。刪除「已有 Attack 任務就 return」的邏輯。 |
| **UpdateCombatLogic** | **移除**開頭 `if (_myPlayer.IsActionBusy) return;`，避免被連續攻擊時整段戰鬥迴圈不跑。 |
| **ExecuteAttackAction / PerformAttackOnce** | **移除**依賴 `IsActionBusy` 的阻擋；是否可攻擊僅由 **冷卻**（`_attackInProgress` / `_attackCooldownTimer`）與 **SpeedManager.CanPerformAction** 決定。 |
| **單一目標來源** | 全專案讀寫攻擊目標改為 **`_currentTask?.Target`**；為兼容舊模組（如 Inventory），`GameWorld._autoTarget` 欄位暫保留，僅在 `StopAutoActions()` 時清空為 `null`。 |
| **Arrived → Executing** | 進入 Arrived 時，**同一幀內**可立即呼叫 `HandleExecutingState`，使用進入時的距離／範圍，避免目標移動導致攻擊失敗。 |
| **訂閱實體事件** | `OnObjectSpawned` 時呼叫 `SubscribeEntityEvents(entity)`，`OnObjectDeleted` 時呼叫 `UnsubscribeEntityEvents(entity)`，確保攻擊關鍵幀命中時能觸發 `OnAttackKeyFrameHit` 與僵硬／飄字。 |

完整設計與問題整理見 **`docs/ATTACK_SYSTEM_REFACTOR.md`**。

### 9.3. 已驗證行為（測試通過）

以下項目已在實際遊玩中確認正常：

-   攻擊 **MISS** 時正確顯示 MISS 飄字，且不播放受擊僵硬動畫。
-   攻擊**命中**時正確顯示傷害飄字、播放受擊僵硬動畫與音效。
-   攻擊方播放攻擊動畫與音效正常。
-   **按 Z** 可持續切換目標，繼續攻擊下一隻怪物（Z 每次重掃、覆蓋或清除任務）。
-   被圍毆時，若 `AllowMoveWhenDamageStiffness = true`，可在僵硬期間移動。
-   **Z 會排除「血量=0」的怪物**：選怪時依 `HpRatio ≤ 0` 排除已死亡目標（伺服器 Opcode 104 或收到死亡動作時設 0）。**此功能測試通過，不可刪除或修改。**

### 9.4. Z 鍵／攻擊目標選擇（ScanForAutoTarget）排除規則

攻擊目標的選擇（按 Z 或魔法/攻擊時取得目標）須排除下列對象，**僅選擇 102.type(5)/(10) 有血量**之怪物/NPC/玩家。實作位置：`GameWorld.Combat.cs` → `ScanForAutoTarget`；代碼旁註明**不可以刪除修改**。

-   **排除自己**：`entity == _myPlayer` → continue。
-   **排除死亡目標**：依 `HpRatio ≤ 0`（伺服器 Opcode 104 / 死亡時 SetHpRatio(0)）。此功能測試通過，不可以刪除修改。
-   **排除地面物品等**：`!entity.ShouldShowHealthBar()` → continue；即**僅選擇 list.spr 102.type(5) 或 102.type(10)** 有血條的實體，地面物品（type 9 等）不可選為攻擊目標。此規則此文字備注不可以刪除。
-   **排除己方召喚/寵物**：`_mySummonObjectIds.Contains(entity.ObjectId)` → continue；不當成自動尋怪目標。
-   **距離**：格距 > 15 不選。

-   **資料來源**：`HpRatio` 由 (1) 伺服器 **Opcode 104** (S_ObjectHitratio) 更新，或 (2) 收到 **ObjectAction(8)** 時呼叫 `SetHpRatio(0)` 同步。`ShouldShowHealthBar()` 在 `GameEntity.cs` 中依 list.spr 102.type 為 5 或 10 判定。
-   **其他使用處**：`GameWorld.Search.cs`（GetSmartTarget）、`GameWorld.Input.cs`（GetClickedEntity）亦須排除死亡／無血條／己方寵物，與 ScanForAutoTarget 一致。

### 9.5. 怪物血條與伺服器血量（單一流程）

-   **伺服器**：有發送怪物血量／比例。
    -   **Opcode 104**（`server/network/server/S_ObjectHitratio.java`）：發送 `objectId` + `hpRatio`（0–100，或 255 表示不檢查）。公式 `(int)(currentHp/totalHp*100)`。
    -   **S_ObjectAdd（生成）**：封包內含 `HpRatio` 一欄，客戶端 `ParseObjectAdd` 讀入 `obj.HpRatio`。
-   **客戶端**：**僅此一條**怪物血條流程，無重複實作。
    -   **儲存**：`GameEntity._hpRatio`（對外唯讀 `HpRatio`），`Init` 時從 `data.HpRatio` 寫入，執行期由 **Opcode 104** 回調 `OnObjectHitRatio` → `entity.SetHpRatio(ratio)` 更新。
    -   **顯示**：同一 `SetHpRatio` 內更新 `_healthBar.Value` 與可見性（滿血隱藏）；血條 UI 在 `GameEntity.UI.cs` 的 `_healthBar`，僅此一處。
-   **死亡動畫不被覆蓋**：`HandleEntityAttackHit` 內若 `target.HpRatio <= 0` 則不呼叫 `SetAction(ACT_DAMAGE)`，避免「先收到 ObjectAction(8) 再收到命中」時死亡動畫被受擊僵硬覆蓋。

### 9.6. 為何會「先播死亡、再播命中」？（封包與關鍵幀時序）

**現象**：伺服器先送 `ObjectAction(objId, 8)`，客戶端已播死亡動畫；之後才觸發 `HandleEntityAttackHit`，把目標改成受擊僵硬，死亡動畫被蓋掉。

**原因（單一根本原因）**：

1. **伺服器**：送出兩類封包、(1) **S_ObjectAction(objId, 8)**＝「該實體死亡」，(2) **S_ObjectAttack(attackerId, targetId, damage)**＝「這次攻擊的結果」。兩包可能幾乎同時送出，順序不保證。
2. **客戶端**：
   - **ObjectAction(8)** 一收到就處理 → 立刻呼叫 `entity.SetAction(8)`，死亡動畫開始。
   - **ObjectAttacked** 一收到只做「記錄」：呼叫 `attacker.PrepareAttack(targetId, damage)`，把 (targetId, damage) 放進攻擊者的 **待處理列表**，**此時不呼叫** `HandleEntityAttackHit`。
   - **HandleEntityAttackHit** 只在「攻擊者動畫播到關鍵幀」時才被呼叫（`OnAnimationKeyFrame` → 從待處理列表取出並呼叫），目的是讓飄字與受擊僵硬**對齊武器揮擊的畫面**。
3. **時序結果**：死亡狀態在「收包時」就套用；命中效果在「攻擊者關鍵幀時」才套用。若關鍵幀晚於 ObjectAction(8)，就會出現「先死亡、後命中」的視覺覆蓋。

**結論**：不是伺服器邏輯錯誤，而是客戶端把「命中效果」刻意延後到攻擊動畫關鍵幀，與伺服器「立即宣告死亡」的時序不一致。修正方式：在 `HandleEntityAttackHit` 內若目標已死（`HpRatio <= 0`）則不套用受擊僵硬，見 §9.5。

### 9.7. 怪物從畫面消失後又出現在遠處（診斷）

**現象**：角色在攻擊一隻怪物時，該怪物（含圖像、頭頂名字）突然從畫面消失；數秒後又在 50–100px 外出現。

**可能原因**（需日誌佐證）：

1. **ObjectDeleted 後再 Spawn**：怪物被刪除又重生／傳送，日誌會出現 `[RX] ObjectDeleted objId=...` 與後續 `[RX] Spawn Obj:...`。
2. **ObjectMoved 座標異常**：收到 Opcode 18 時若 (x,y) 為 0 或錯誤值，會呼叫 `SetMapPosition(x,y)`，實體會瞬移到錯誤位置（例如畫面外）；之後再收到正確的移動包就會「又出現」在別處。日誌會出現 `[Pos-Sync] ObjID:... Server_Grid:(x,y)` 與 `[Pos-Teleport]`（見下方診斷）。
3. **延後載入**：僅對「剛生成且距離過遠」的實體設 `Visible=false`；已顯示過的實體不會因距離再被隱藏，故一般不是視野開關造成。

**診斷日誌**（已加於程式，便於下次重現時比對）：

- **`[Pos-Teleport]`**：`GameEntity.Movement.SetMapPosition` 在「瞬移」分支（距離 ≥ 256px）時輸出，含 ObjId、舊格座標、新格座標。若出現 `Server_Grid:(0,0)` 或與預期不符的座標，代表可能收到錯誤的移動封包。
- **`[Pos-Sync]`**：`PacketHandler.ParseObjectMoving` 收到 Opcode 18 時輸出，含 ObjID、Server_Grid、Client_Pos。
- **`[RX] ObjectDeleted` / `[RX] Spawn Obj`**：實體被刪除或生成時輸出。

您提供的日誌片段僅含 HitChain／魔法飛行，**沒有** ObjectDeleted、ObjectMoved、Spawn 或 Pos-Sync。若下次再出現「怪物消失又出現」，請保留該時間點前後的完整日誌（含 `[Pos-Teleport]`、`[Pos-Sync]`、`[RX] ObjectDeleted`、`[RX] Spawn Obj`），即可判斷是刪除重生還是移動座標異常。

### 9.8. ObjectDeleted 是否為伺服器發送？解析與時序說明

當出現「正在攻擊的怪物突然消失」且日誌為 `[RX] ObjectDeleted objId=6475120` 時，可依下列方式確認是否為伺服器刪除、以及客戶端是否解析正確。

**客戶端解析（已對齊伺服器）**  
- 封包格式：`S_ObjectRemove.java` 為 `writeC(21); writeD(o.getObjectId());`，即 1 字節 opcode + 4 字節 objectId。  
- 客戶端：`PacketHandler` 在 `case 21` 僅執行 `reader.ReadInt()` 讀取 objectId，與伺服器一致。日誌已改為輸出 `[RX] ObjectDeleted objId=X (0xXXXXXXXX)`，可與伺服器日誌對照 objectId 是否一致。

**伺服器何時發送 S_ObjectRemove（怪物）**  
- **屍體移除**：`NpcInstance.toDead(long time)` 中，怪物（非寵物）在 **死亡後 60 秒** 會呼叫 `toDelete()`，對所有曾將該怪物加入 objectList 的玩家發送 `S_ObjectRemove(this)`，再從世界移除。  
- **重生／傳送**：`reSpawn()` 會呼叫 `toTeleport(getHomeX(), getHomeY(), getHomeMap())`，而 `toTeleport()` 內會先呼叫 `toDelete()`，因此也會對上述玩家發送 `S_ObjectRemove`，再在同一 objectId 下於出生點重新 insert。  
- **召喚物**：召喚物死亡或解除時會 `removeMon` + `toDelete()`，同樣會發送 Remove。

**為何會出現「攻擊中怪物突然被刪」**  
1. **死亡後 60 秒才刪**：怪物早已死亡（例如先前被擊殺或伺服器判定死亡），客戶端可能已播過 ObjectAction(8) 或未注意到；玩家繼續點選／攻擊的是「屍體」。60 秒到時伺服器發送 Remove，畫面上該怪消失。  
2. **重生／傳送**：伺服器在該 objectId 上執行 `reSpawn()`／`toTeleport()` 時會先 toDelete，同一 objectId 會先從客戶端消失，再於新座標出現（若伺服器有再送 Add／周圍更新則會再顯示）。  
3. **封包順序**：Remove 比「命中關鍵幀」先到時，客戶端會先從 `_entities` 移除該 objId，之後攻擊動畫關鍵幀觸發 `HandleEntityAttackHit(targetId)` 時實體已不存在，故出現 `[HitChain] HandleEntityAttackHit target X NOT in _entities`，屬預期且不影響穩定性。

**結論**  
- **是伺服器刪除**：Remove 封包來自伺服器 `S_ObjectRemove`，客戶端僅依 opcode 21 + ReadInt() 正確解析 objectId。  
- 若需進一步確認，可在伺服器端對同一 objectId 打日誌（例如發送 S_ObjectRemove 時印出 objectId），與客戶端 `[RX] ObjectDeleted objId=X (0xXXXXXXXX)` 比對；兩者一致即可確認為同一封包、且客戶端未誤解析。

---

## 11. 裝備參數與物品提示 (Equipment Parameters & Item Tooltip)

裝備的詳細參數（攻擊力、防禦、職業限制、材質、重量等）由伺服器在**鑑定後**透過封包擴充資料下發，客戶端解析後顯示於 **ItemTooltip**。

### 10.1. 資料來源與協議對齊

- **伺服器**：`server/database/bean/Item.java` 定義物品欄位（`_dmgsmall`、`_dmglarge`、`ac`、`addHit`、`addDmg`、`_royal`/`_knight`/`_elf`/`_mage`、材質、重量、等級限制、四屬性等）。  
- **封包**：  
  - **Opcode 65**（S_InventoryList）：登入時背包列表；每筆已鑑定物品在 `name` 之後會寫入 weapon/armor/etc 擴充區塊。  
  - **Opcode 22**（S_InventoryAdd）：拾取／獲得物品；已鑑定時在 `name` 之後寫入擴充區塊。  
  - **Opcode 111**（S_InventoryStatus）：鑑定後或狀態更新；`writeC(111)`、`writeD(invID)`、`writeS(name)`、`writeD(count)`、`writeC(status)` 後，若 status≠0 則寫入 weapon/armor/etc 區塊（對齊 `S_Inventory.weapon()` / `armor()` / etc）。

### 10.2. 客戶端解析流程

- **PacketHandler.ParseInventoryStatus**（Opcode 111）：讀取 objectId、name、count、status 後，若 status≠0 且尚有位元組，呼叫 **ParseInventoryStatusExtended(reader)** 解析擴充資料，並 emit `InventoryItemUpdated(objectId, name, count, status, detailInfo)`。  
- **ParseInventoryList**（Opcode 65）：每筆物品讀完 name 後，若 `val4`（鑑定）≠0 且 `reader.Remaining >= 2`，呼叫 **ParseInventoryStatusExtended**，並設定 **item.Ident**、**item.DetailInfo**。  
- **ParseCommonItemData**（Opcode 22）：在回傳前若 `isIdentified != 0` 且尚有至少 2 位元組，呼叫 **ParseInventoryStatusExtended**，並設定 **item.Ident**、**item.DetailInfo**。

### 10.3. 擴充資料格式（ParseInventoryStatusExtended）

對齊 `S_Inventory.java` 的 weapon/armor/etc 寫入順序：

- **武器**（b1==1）：攻擊小/大、材質、重量；其後可選 tag：強化(2)、耐久(3)、單手(4)、命中(5)、傷害(6)、**職業(7)**、力敏體智魅(8–13)、HP/魔防(14–15)、吸魔(16)、魔攻(17)、加速(18)。  
- **防具**（b1==19）：AC、材質、重量；其後可選 tag：強化(2)、職業(7)、屬性加成、**等級限制(26)**、火/水/風/地(27–30) 等。  
- **一般**（b0==6, b1==23）：材質、重量(H)。  

解析時會消耗所有剩餘位元組，避免錯位；並呼叫 **MaterialToStr**、**ClassMaskToStr** 將材質 ID 與職業 mask 轉成中文（鐵/鋼/銀/金、王族/騎士/妖精/法師）。

### 10.4. 資料模型與 UI

- **InventoryItem**（`Client/Data/InventoryItem.cs`）：新增 **DetailInfo**（string），存放上述解析出的多行介紹文字。**Ident** 由各封包寫入（0=未鑑定，1=鑑定）。  
- **GameWorld.OnInventoryItemUpdated**：收到 Opcode 111 時更新對應物品的 Name、Count、Ident、**DetailInfo**，並 RefreshWindows。  
- **ItemTooltip**（`Client/UI/Scripts/ItemTooltip.cs`）：  
  - 名稱使用 **DescTable.ResolveName** 解析 `$數字` 與括號內 `($9)`、`($117)`。  
  - 顯示數量、類型、狀態（已裝備/未裝備；未鑑定時標註）。  
  - 若 **DetailInfo** 非空，在「── 屬性 ──」下顯示攻擊/防禦/材質/重量/職業/強化/命中傷害/等級限制/四屬性等；若已鑑定但無擴充資料則顯示「（無額外屬性資料）」。  
  - Bless=0 時整段文字為金色。

### 10.5. 鑑定卷軸流程（與裝備參數連動）

- 雙擊鑑定卷軸 → 進入鑑定模式（`_pendingIdentifyScrollId`），提示「請單擊要鑑定的裝備」。  
- 單擊目標裝備 → 發送 **UseIdentifyScroll(卷軸ID, 目標ID)**（Opcode 28 + 兩 ID）；伺服器鑑定後會發送 **Opcode 111**，客戶端更新該物品的 name/count/Ident/**DetailInfo**，tooltip 即可顯示完整參數。

---

## 12. 窗口 UI 與各窗口核心邏輯 (Window UI & Core Logic)

### 11.1. 架構總覽

- **UIManager**（`Client/UI/Core/UIManager.cs`）：單例，管理所有遊戲視窗。維護 **WindowID → .tscn 路徑** 的註冊表（`_windowPaths`）與 **WindowID → GameWindow 實例** 的緩存（`_windows`）。所有視窗掛在 **CanvasLayer (Layer=10)** 下，確保蓋住 HUD。  
  - **Open(id, context)**：取得或建立視窗後呼叫 **OnOpen(context)** 刷新資料，並設為可見、置頂。  
  - **Close(id)** / **Toggle(id)**：關閉或切換開關。  
  - **GetWindow(id)**：若緩存無則依路徑載入場景並實例化，根節點須為 **GameWindow** 子類，建立後預設關閉並加入緩存。  

- **GameWindow**（`Client/UI/Core/GameWindow.cs`）：所有浮動視窗的基類。  
  - 約定：子類場景需有 **CloseBtn**、可選 **TitleBar**（用於拖拽）。  
  - 負責：關閉按鈕、標題欄/整窗拖拽、點擊時 **BringToFront**。  
  - 生命週期：**OnOpen(context)** 供子類刷新資料；**Close()** 隱藏並觸發 OnWindowClosed。  

- **WindowID** / **WindowContext**（`Client/UI/Core/WindowDef.cs`）：視窗唯一 ID（Character、Inventory、Skill、Shop、Talk、WareHouse、Options、SkinSelect、AmountInput 等）；Context 可帶 NpcId、Type、**ExtraData**（任意資料，如變身卷軸 objectId、商店資料等）。

### 11.2. 各窗口核心邏輯

| 窗口 | 場景/腳本 | 核心邏輯 |
|------|------------|----------|
| **角色 (C)** | ChaWindow | 裝備槽 TextureRect1–12 與 Type 映射（頭盔/盔甲/盾/手套/鞋/斗篷/項鍊/腰帶/戒指/耳環等）；從 GameWorld 取得背包中已裝備物品填槽；點擊槽可穿戴/卸下；懸停顯示 **ItemTooltip**（含 DetailInfo）。 |
| **背包 (I/TAB)** | InvWindow | GridContainer + **InventorySlot** 動態生成；**RefreshInventory(items)** 由 GameWorld.RefreshWindows 驅動；單擊選中、雙擊使用；**鑑定卷軸**：雙擊卷軸進入模式，單擊目標裝備發送 UseIdentifyScroll；變身卷軸雙擊打開 SkinSelectWindow；其餘雙擊 UseItem；刪除按鈕呼叫 GameWorld.DeleteItem。 |
| **技能 (S)** | SkillWindow | TabContainer + **SkillSlot** 網格；**RefreshSkills(skillMasks)** 依 Opcode 30 技能掩碼點亮已學技能；技能名由 DescTable.GetSkillName 解析。 |
| **HUD** | HUD | 血條/魔條、**ChatBox**、**ChatInput**；訂閱 PacketHandler.**SystemMessage** 顯示系統訊息；ChatInput 送出時 **ChatSubmitted** 信號給 GameWorld 發送聊天，並本地 **AddChatMessage** 做 Local Echo；訊息會經 **SanitizeChatMessage** 清理 `\f.`、`%0` 等格式碼。 |
| **對話** | TalkWindow | **ContentLabel**（RichTextLabel）顯示 NPC 對話；**OnOpen(context)** 依 context 的 NpcId/ExtraData 載入 HTML（LoadAndShowHtml）；**meta_clicked** 解析連結動作（如 teleport、next）並發送 C_NpcPacket；支援多語言檔（-c/-h/-e 後綴）。 |
| **商店** | ShopWindow | **OnOpen(context)** 從 context.ExtraData 取 npc_id、items 陣列；**ShopList** ItemList 顯示名稱（DescTable/StringTable 解析 $ID）、價格；雙擊或 Buy 按鈕發送購買封包（依伺服器協議）。 |
| **變身選擇** | SkinSelectWindow | 雙擊變身卷軸時由 UIManager.Open(SkinSelect, **ExtraData = 卷軸 objectId**) 打開；**LoadAndShowHtml(SkinSelectWindow)** 載入多語言 HTML；**meta_clicked** 選擇變身後發送 **UsePolymorphScroll(scrollObjectId, polyName)**。 |
| **倉庫** | WareHouseWindow | 與伺服器 Opcode 49 倉庫列表對接；顯示存儲物品、存取邏輯（依現有實作）。 |
| **數量輸入** | AmountInputWindow | 彈窗輸入數量，用於購買/堆疊等需指定數量的操作。 |

### 11.3. 組件與共用邏輯

- **InventorySlot**（`Client/UI/Scenes/Components/InventorySlot.tscn` + Script）：顯示物品圖示、數量；**ItemClicked** / **ItemDoubleClicked** 傳遞 objectId；懸停時建立 **ItemTooltip** 並呼叫 **Setup(item)**，顯示名稱（ResolveName）、數量/類型/狀態、**DetailInfo**（裝備參數）。  
- **ItemTooltip**：見 §10.4；依 **item.Ident**、**item.DetailInfo**、**item.Bless** 顯示完整裝備介紹與未鑑定提示。  
- **BottomBar**：快捷欄（1–8）、輸入（Z/X/C 等）；**BottomBar.Input** 在 1–8 鍵處理前檢查焦點是否在 ChatInput/LineEdit，避免與聊天輸入衝突。  
- **Restart／設定／快捷欄依角色存讀**：遊戲內 Options → Restart 退回角色列表（不送 Op 15，保持連線）。選新角色登入後，**設定**（音效、血條、黑夜開關等）與 **快捷欄 8 格** 依 **角色名字** 在 `user://settings.cfg` 的 `[char:角色名]` 節區存讀；切換角色時會載入該角色專屬快捷欄並正確儲存。測試通過。

### 11.4. 資料流摘要

- **背包 / 角色 / 技能**：由 **GameWorld.RefreshWindows()** 統一驅動（收到 Opcode 65/22/23/24/111、裝備變化、技能更新後呼叫），從 GameWorld 取得當前背包列表、裝備狀態、技能掩碼，再呼叫各視窗的 Refresh 方法。  
- **對話 / 商店 / 變身**：由遊戲邏輯（NPC 對話、開商店、雙擊卷軸）呼叫 **UIManager.Open(id, context)**，context 帶 NpcId 或 ExtraData，視窗在 **OnOpen** 內依 context 載入 HTML 或列表並綁定發送封包邏輯。
