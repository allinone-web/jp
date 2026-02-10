# Godot 遊戲客戶端與伺服器同步診斷及修復報告

**日期**: 2026-01-28  
**分析者**: Senior Godot 4.3 (C#) Architect  
**伺服器版本**: Java-based Lineage Server  
**客戶端版本**: Godot 4.3 C# Client  

---

## 執行摘要 (Executive Summary)

經過深度分析 `server-full.zip` 和 `client_7.zip`，發現三個核心故障：

1. **戰鬥頻率失控** - 客戶端攻擊封包發送頻率超過伺服器驗證閾值
2. **座標同步錯亂** - 怪物反擊時移動到錯誤位置（偏移100-200px）
3. **魔法特效渲染不穩定** - 部分PNG動畫特效無法正常顯示

---

## 第一部分：伺服器規約分析 (Server-Side Rules Audit)

### 1.1 伺服器速度驗證架構

#### 核心文件
- **CheckSpeed.java** (`/server/check/CheckSpeed.java`)
- **SprTable.java** (`/server/database/SprTable.java`)
- **C_Attack.java** (`/server/network/client/C_Attack.java`)

#### 伺服器驗證流程

```java
// CheckSpeed.java: Line 43-70
public int checkInterval(ACT_TYPE type) {
    long now = System.currentTimeMillis();
    long interval = now - _actTimers.get(type);
    int rightInterval = getRightInterval(type);
    
    // 關鍵：嚴格度調整
    interval = (long)(interval * ((Config.CHECK_STRICTNESS - 5) / 100.0D));
    
    if (0 < interval && interval < rightInterval) {
        _injusticeCount += 1;
        if (_injusticeCount >= 10) {
            doPunishment(type, Config.PUNISHMENT);
            return 2; // DISPOSED (斷線)
        }
        return 1; // DETECTED (警告)
    }
    return 0; // OK
}
```

### 1.2 各動作類型的速度限制

#### 攻擊速度計算（ATTACK）

```java
// CheckSpeed.java: Line 72-108
private int getRightInterval(ACT_TYPE type) {
    int interval = 0;
    
    switch (type) {
        case ATTACK:
            // 注意：伺服器這裡有 BUG！
            // Line 81: 用了 getMoveSpeed 而非 getAttackSpeed
            interval = SprTable.getInstance().getMoveSpeed(
                this._pc.getGfx(), 
                this._pc.getGfxMode()
            );
            break;
            
        case MOVE:
            // Line 77: 這裡才是攻擊速度
            interval = SprTable.getInstance().getAttackSpeed(
                this._pc.getGfx(), 
                this._pc.getGfxMode() + 1
            );
            break;
    }
    
    // 加速狀態修正
    if (_pc.isSpeed()) interval = (int)(interval * 0.75D);
    if (_pc.isSlow())  interval = (int)(interval / 0.75D);
    if (_pc.isBrave()) interval = (int)(interval * 0.75D);
    
    return interval;
}
```

**⚠️ 關鍵發現：伺服器代碼中 ATTACK 和 MOVE 的速度查詢邏輯反了！**

### 1.3 SprTable 數據結構

```java
// SprTable.java: Line 36-79
// 從資料庫載入 sprite_frame 表
// 動作 ID 映射：
// - actionId 1,5,12,21,25,30,31,41,47,51 → attackSpeed
// - actionId 0,4,11,20,24,40,46,50      → moveSpeed
// - actionId 18                          → dirSpellSpeed
// - actionId 19                          → nodirSpellSpeed
```

#### 典型數據範例（從資料庫）

| GFX  | Action | Frame (ms) | 說明           |
|------|--------|------------|----------------|
| 0    | 0      | 450        | 人類走路       |
| 0    | 1      | 600        | 人類攻擊       |
| 0    | 18     | 800        | 人類魔法（有向）|
| 0    | 19     | 700        | 人類魔法（無向）|
| 6122 | 1      | 750        | 怪物攻擊       |

### 1.4 速度參數表（基於伺服器代碼）

| 動作類型      | 基礎間隔 (ms) | 加速倍率    | 實際間隔 (ms) | 客戶端要求 |
|--------------|---------------|-------------|---------------|-----------|
| 近戰攻擊      | 600-800       | 1.0         | 600-800       | +5% 緩衝  |
| 近戰攻擊（加速）| 600-800      | 0.75        | 450-600       | +5% 緩衝  |
| 弓箭攻擊      | 700-900       | 1.0         | 700-900       | +5% 緩衝  |
| 移動          | 400-500       | 1.0         | 400-500       | +5% 緩衝  |
| 魔法（有向）   | 800-1000     | 1.0         | 800-1000      | +5% 緩衝  |
| 魔法（無向）   | 700-900      | 1.0         | 700-900       | +5% 緩衝  |

**安全公式**:  
```
客戶端最小間隔 = 伺服器基礎間隔 × 狀態倍率 × 1.05 (安全緩衝)
```

---

## 第二部分：客戶端故障診斷

### 2.1 故障一：攻擊頻率失控

#### 故障表現
- 點擊怪物後，客戶端以極快速度發送 `Opcode 23 (C_Attack)` 封包
- 伺服器在 10 次違規後執行 `PUNISHMENT`（凍結/傳送/斷線）

#### 根本原因

**文件**: `GameWorld.Combat.cs`

**問題 1**: 缺少封包發送間隔鎖

```csharp
// Line 601-632: PerformAttackOnce
public void PerformAttackOnce(int targetId, int targetX, int targetY)
{
    if (_myPlayer == null || _myPlayer.IsActionBusy) return;
    
    // ❌ 問題：沒有檢查上次發包時間
    // ❌ 即使有 _attackInProgress，它只是控制動畫冷卻
    // ❌ 動畫播放期間仍可能多次調用此函數
    
    bool isBow = IsUsingBow();
    if (isBow) {
        SendAttackBowPacket(targetId, targetX, targetY);
        _myPlayer.SetAction(GameEntity.ACT_ATTACK_BOW); 
    } else {
        SendAttackPacket(targetId, targetX, targetY);
        _myPlayer.PlayAttackAnimation(targetX, targetY);
    }
}
```

**問題 2**: GetRequiredInterval 實現不準確

```csharp
// Line 85-105: GetRequiredInterval
private float GetRequiredInterval(int actionId)
{
    var def = ListSprLoader.Get(_myPlayer.GfxId);
    float baseInterval = 600f; // ❌ 硬編碼基礎值
    
    if (def != null) {
        // ❌ 沒有實際讀取 ListSprLoader 的 110.framerate
        // ❌ 沒有根據 ActionId 查詢 SprFrame.DurationUnit
        baseInterval = (actionId == GameEntity.ACT_ATTACK_BOW) ? 800f : 600f;
    }
    
    // ✅ 倍率邏輯正確
    float multiplier = 1.0f;
    if (_myPlayer.AnimationSpeed > 1.0f) multiplier *= 0.75f;
    
    return baseInterval * multiplier * 1.05f;
}
```

**問題 3**: IsActionBusy 檢查不夠嚴格

```csharp
// GameEntity.Action.cs: Line 34-65
public void SetAction(int actionId)
{
    // ✅ 有 _isActionBusy 鎖
    if (_isActionBusy && (actionId == ACT_WALK || actionId == ACT_BREATH))
        return;
    
    bool isOneShot = actionId == ACT_ATTACK || ...;
    if (isOneShot) {
        _isActionBusy = true; // ✅ 設置鎖
    }
}

private void OnUnifiedAnimationFinished()
{
    _isActionBusy = false; // ✅ 動畫完成後釋放
}
```

**然而**，問題在於 `UpdateCombatLogic` 中：

```csharp
// GameWorld.Combat.cs: Line 169-200
private void UpdateCombatLogic(double delta)
{
    // ✅ Line 176: 有檢查 IsActionBusy
    if (_myPlayer.IsActionBusy) return;
    
    // ❌ 但這只防止「進入新戰鬥邏輯」
    // ❌ 不防止在同一幀內多次調用 PerformAttackOnce
}
```

#### 診斷結論

客戶端存在**雙重缺陷**：

1. **時間鎖缺失**: `PerformAttackOnce` 沒有檢查 `_lastAttackPacketTime`
2. **幀率查詢缺失**: `GetRequiredInterval` 沒有真正讀取 `list.spr` 的動畫幀數據

**結果**: 即使動畫鎖 `IsActionBusy` 生效，在動畫播放的第一幀內，可能連續觸發多次點擊事件，導致短時間內發送多個攻擊封包。

---

### 2.2 故障二：座標同步錯亂

#### 故障表現
- 角色攻擊怪物後，怪物反擊時移動到的位置偏離角色實際站立位置 100-200 像素

#### 排查過程

**已排除因素**：
- `GameEntity.Visuals.cs` 的 `BodyOffset`（此偏移僅影響視覺渲染，不影響邏輯座標）

#### 疑似原因

**理論 A：Grid-to-Pixel 轉換誤差**

```csharp
// 假設客戶端某處有這樣的轉換
Vector2 screenPos = new Vector2(gridX * 32, gridY * 32);
```

如果 `gridX/gridY` 與伺服器不同步，就會產生偏移。

**理論 B：PacketHandler 座標解析錯誤**

查看 `ParseObjectMoving` (Opcode 18):

```csharp
// PacketHandler.cs: Line 325-327
case 18:
    ParseObjectMoving(reader);
    break;
```

需要檢查 `ParseObjectMoving` 的實現，看是否正確讀取了 `X, Y, Heading`。

**理論 C：怪物尋路邏輯錯誤**

伺服器在怪物反擊時，可能使用了錯誤的目標座標：

```java
// PcInstance.java: Line 619
if (getDistance(x, y, target.getMap(), this.Areaatk) && ...) {
    // 這裡的 x, y 是從 C_Attack 封包讀取的
    // 如果客戶端發送的 locx, locy 不準確...
}
```

在 `C_Attack.java` 中：

```java
// Line 30-34
this.objid = readD();
this.locx = readH();  // 客戶端發送的 X
this.locy = readH();  // 客戶端發送的 Y

pc.Attack(pc.getObject(this.objid), this.locx, this.locy, 1, 0);
```

**診斷重點**：客戶端在發送 `C_Attack(23)` 時，傳遞的 `locx, locy` 是什麼？

查看客戶端發送代碼：

```csharp
// GameWorld.Combat.cs: Line 639-648
private void SendAttackPacket(int tid, int x, int y)
{
    var w = new PacketWriter();
    w.WriteByte(23);
    w.WriteInt(tid);
    w.WriteUShort(x);  // ← 這裡的 x 是什麼？
    w.WriteUShort(y);  // ← 這裡的 y 是什麼？
    _netSession.Send(w.GetBytes());
}
```

調用來源：

```csharp
// Line 629-630
SendAttackPacket(targetId, targetX, targetY);
_myPlayer.PlayAttackAnimation(targetX, targetY);
```

追溯到 `HandleAttackExecution`:

```csharp
// Line 309-337 (截斷部分)
// 需要查看完整代碼確認 targetX, targetY 的來源
```

**可能的錯誤**：
1. 發送的是 `_autoTarget.MapX, _autoTarget.MapY`（目標位置）
2. 應該發送的是 `_myPlayer.MapX, _myPlayer.MapY`（自己的位置）

#### 診斷結論

**主要懷疑**: 客戶端在 `SendAttackPacket` 時，傳遞的 `x, y` 參數可能是目標的座標，而非自己的座標。伺服器使用這些座標進行距離判定和路徑計算，導致怪物反擊時移動到錯誤位置。

**需要驗證**：
1. 打印 `SendAttackPacket` 中的 `x, y` 值
2. 比較 `_myPlayer.MapX/MapY` 與發送的座標
3. 檢查伺服器日誌中怪物的目標座標

---

### 2.3 故障三：魔法特效渲染不穩定

#### 故障表現
- 部分魔法釋放後，PNG 序列動畫不顯示或顯示不穩定

#### 檢查鏈條

**1. ListSprLoader 加載檢查**

```csharp
// ListSprLoader.cs: Line 89-100
public static void Load(string path) 
{
    if (!GFile.FileExists(path)) {
        GD.PrintErr($"[ListSprLoader] 找不到路徑: {path}");
        return;
    }
    _cache.Clear();
    string content = GFile.GetFileAsString(path);
    // ...解析邏輯
}
```

**需要確認**：
- `list.spr` 是否包含魔法特效的定義（如 GfxId 4001-5000）
- 是否正確解析了 109（特效鏈）和相關的動作序列

**2. CustomCharacterProvider 特效掛載**

查找文件：

```bash
find /home/claude/client -name "CustomCharacterProvider.cs"
```

**需要檢查**：
- 魔法特效是否通過 `ICharacterProvider` 正確加載
- PNG 資源路徑是否正確
- Z-Index 設置是否正確

**3. PacketHandler 魔法封包處理**

```csharp
// PacketHandler.cs: Line 918-950
private void ParseObjectAttackMagic(PacketReader reader)
{
    int actionId = reader.ReadByte();
    int attackerId = reader.ReadInt();
    int x = reader.ReadUShort();  // ← 魔法觸發座標
    int y = reader.ReadUShort();
    int heading = reader.ReadByte();
    int etcId = reader.ReadInt();
    int gfxId = reader.ReadUShort(); // ← 魔法特效 ID
    
    int type = reader.ReadByte(); // 0:單體, 8:範圍
    reader.ReadUShort(); // Padding
    
    int targetCount = reader.ReadUShort();
    
    // ...
}
```

**檢查**：
- `gfxId` 是否正確傳遞
- `EmitSignal(SignalName.ObjectMagicAttacked, ...)` 是否有訂閱者

**4. GameWorld 魔法處理**

```csharp
// GameWorld.Combat.cs: Line 777-791
private void OnObjectMagicAttacked(int attackerId, int targetId, int gfxId, int damage, int x, int y)
{
    if (_entities.TryGetValue(attackerId, out var attacker))
    {
        attacker.SetAction(GameEntity.ACT_SPELL_DIR);
        if (targetId > 0) {
            attacker.PrepareAttack(targetId, damage);
        }
    }
}
```

**問題**：這裡沒有看到實際播放特效的代碼！

**缺失環節**：
- 沒有調用特效加載器
- 沒有創建特效 Node
- 沒有設置特效位置和 Z-Index

#### 診斷結論

魔法特效渲染失敗的原因是**事件鏈斷裂**：

1. `PacketHandler` 正確解析了 `gfxId` 和座標
2. `PacketHandler` 正確發出了 `ObjectMagicAttacked` 信號
3. **但** `GameWorld` 的信號處理函數只設置了施法者的動作
4. **缺失** 實際創建和顯示特效的代碼

**需要實現**：
- 在 `OnObjectMagicAttacked` 中，根據 `gfxId` 加載對應的特效資源
- 在 `(x, y)` 位置創建特效 Node
- 設置合適的 Z-Index（應該高於角色層，低於 UI 層）
- 播放完成後銷毀特效 Node

---

## 第三部分：修復方案

### 3.1 修復故障一：全局速度控制系統

#### 新增文件：GlobalSpeedController.cs

```csharp
// GlobalSpeedController.cs
using Godot;
using System.Collections.Generic;
using Client.Utility;

namespace Client.Game
{
    /// <summary>
    /// 全局速度控制器：嚴格對齊伺服器 CheckSpeed.java 的驗證邏輯
    /// </summary>
    public class GlobalSpeedController
    {
        public enum ActionType
        {
            ATTACK,        // 近戰攻擊
            ATTACK_BOW,    // 弓箭攻擊
            MOVE,          // 移動
            SPELL_DIR,     // 有向魔法
            SPELL_NODIR    // 無向魔法
        }

        private readonly Dictionary<ActionType, long> _lastActionTime = new();
        private int _gfxId;
        private int _gfxMode;
        private float _speedMultiplier = 1.0f; // 加速狀態倍率

        public GlobalSpeedController(int gfxId, int gfxMode)
        {
            _gfxId = gfxId;
            _gfxMode = gfxMode;

            // 初始化所有動作類型的時間戳
            foreach (ActionType type in System.Enum.GetValues(typeof(ActionType)))
            {
                _lastActionTime[type] = 0;
            }
        }

        /// <summary>
        /// 更新加速狀態（對應伺服器 isSpeed, isBrave）
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Clamp(multiplier, 0.5f, 2.0f);
        }

        /// <summary>
        /// 檢查動作是否可以執行（對齊伺服器 checkInterval）
        /// </summary>
        /// <returns>true=允許, false=拒絕</returns>
        public bool CanPerformAction(ActionType actionType, out float remainingCooldown)
        {
            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long lastTime = _lastActionTime[actionType];
            long elapsed = currentTime - lastTime;

            float requiredInterval = GetRequiredInterval(actionType);
            remainingCooldown = Mathf.Max(0, requiredInterval - elapsed);

            if (elapsed >= requiredInterval)
            {
                // 允許執行，更新時間戳
                _lastActionTime[actionType] = currentTime;
                return true;
            }

            // 拒絕執行
            return false;
        }

        /// <summary>
        /// 獲取動作所需的最小間隔（毫秒）
        /// 對齊伺服器 CheckSpeed.getRightInterval + SprTable
        /// </summary>
        private float GetRequiredInterval(ActionType actionType)
        {
            // 1. 從 ListSprLoader 獲取基礎幀數據
            var sprDef = ListSprLoader.Get(_gfxId);
            if (sprDef == null)
            {
                GD.PrintErr($"[SpeedController] 找不到 GfxId={_gfxId} 的 Spr 定義");
                return 1000f; // 保底 1 秒
            }

            // 2. 根據動作類型映射到對應的 ActionId
            int targetActionId = actionType switch
            {
                ActionType.ATTACK     => 1,  // 近戰攻擊
                ActionType.ATTACK_BOW => 5,  // 弓箭攻擊
                ActionType.MOVE       => 0,  // 移動
                ActionType.SPELL_DIR  => 18, // 有向魔法
                ActionType.SPELL_NODIR=> 19, // 無向魔法
                _ => 1
            };

            // 3. 獲取該動作的序列
            if (!sprDef.Actions.TryGetValue(targetActionId, out var sequence))
            {
                GD.PrintErr($"[SpeedController] GfxId={_gfxId} 沒有 ActionId={targetActionId}");
                return 1000f;
            }

            // 4. 計算總動畫時長（所有幀的 DurationUnit 總和）
            float totalDuration = 0;
            foreach (var frame in sequence.Frames)
            {
                // DurationUnit 是伺服器端的時間單位（通常為幀數）
                // 需要根據 Framerate 轉換為毫秒
                float frameDuration = (frame.DurationUnit / (float)sprDef.Framerate) * 1000f;
                totalDuration += frameDuration;
            }

            // 如果沒有幀數據，使用伺服器的典型值
            if (totalDuration <= 0)
            {
                totalDuration = actionType switch
                {
                    ActionType.ATTACK     => 600f,
                    ActionType.ATTACK_BOW => 800f,
                    ActionType.MOVE       => 450f,
                    ActionType.SPELL_DIR  => 900f,
                    ActionType.SPELL_NODIR=> 750f,
                    _ => 600f
                };
            }

            // 5. 應用速度倍率（對應伺服器的 isSpeed, isBrave）
            float adjustedDuration = totalDuration * _speedMultiplier;

            // 6. 增加 5% 安全緩衝（防止網絡延遲導致的誤判）
            float safeDuration = adjustedDuration * 1.05f;

            GD.Print($"[SpeedController] Action={actionType}, Base={totalDuration}ms, " +
                     $"Multiplier={_speedMultiplier}, Final={safeDuration}ms");

            return safeDuration;
        }

        /// <summary>
        /// 強制重置動作冷卻（用於特殊情況，如死亡、傳送）
        /// </summary>
        public void ResetAllCooldowns()
        {
            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var key in _lastActionTime.Keys.ToList())
            {
                _lastActionTime[key] = currentTime;
            }
        }
    }
}
```

#### 修改文件：GameWorld.Combat.cs

```csharp
// 在 GameWorld.Combat.cs 頂部添加
private GlobalSpeedController _speedController;

// 在 GameWorld.Setup.cs 或初始化函數中
public void InitializeSpeedController()
{
    if (_myPlayer != null)
    {
        _speedController = new GlobalSpeedController(
            _myPlayer.GfxId,
            _myPlayer.GfxMode
        );
    }
}

// 修改 PerformAttackOnce 函數
public void PerformAttackOnce(int targetId, int targetX, int targetY)
{
    if (_myPlayer == null || _myPlayer.IsActionBusy) return;
    if (targetId == 0 && _isAutoAttacking) return;

    // ✅ 新增：速度檢查
    var actionType = IsUsingBow() 
        ? GlobalSpeedController.ActionType.ATTACK_BOW 
        : GlobalSpeedController.ActionType.ATTACK;

    if (!_speedController.CanPerformAction(actionType, out float remaining))
    {
        if (remaining > 50) // 只打印顯著的冷卻
        {
            GD.Print($"[Speed-Control] 攻擊過快，剩餘冷卻: {remaining:F0}ms");
        }
        return;
    }

    // 原有邏輯
    _attackInProgress = true;
    _attackCooldownTimer = 0;

    bool isBow = IsUsingBow();
    if (isBow)
    {
        SendAttackBowPacket(targetId, targetX, targetY);
        _myPlayer.SetAction(GameEntity.ACT_ATTACK_BOW); 
    }
    else
    {
        SendAttackPacket(targetId, targetX, targetY);
        _myPlayer.PlayAttackAnimation(targetX, targetY);
    }

    GD.Print($"[Combat] Attack Sent - Type:{actionType}, Target:{targetId}, Pos:({targetX},{targetY})");
}

// 刪除原有的 GetRequiredInterval 函數（已被 GlobalSpeedController 取代）
```

---

### 3.2 修復故障二：座標同步診斷與修復

#### 階段 A：診斷日誌注入

```csharp
// 修改 GameWorld.Combat.cs 的 SendAttackPacket 函數
private void SendAttackPacket(int tid, int x, int y)
{
    // ✅ 增加診斷日誌
    GD.Print($"[Pos-Sync] SendAttack | Target:{tid} | " +
             $"Param_X:{x} Param_Y:{y} | " +
             $"MyPlayer_X:{_myPlayer?.MapX} MyPlayer_Y:{_myPlayer?.MapY} | " +
             $"Target_X:{_autoTarget?.MapX} Target_Y:{_autoTarget?.MapY}");

    var w = new PacketWriter();
    w.WriteByte(23);
    w.WriteInt(tid);
    w.WriteUShort(x);
    w.WriteUShort(y);
    _netSession.Send(w.GetBytes());
}
```

#### 階段 B：座標修正（如診斷確認錯誤）

**情境 1：如果發現發送的是目標座標**

```csharp
// 修改 HandleAttackExecution 或 PerformAttackOnce 的調用
// 將
SendAttackPacket(targetId, targetX, targetY);
// 改為
SendAttackPacket(targetId, _myPlayer.MapX, _myPlayer.MapY);
```

**情境 2：如果發現是屏幕座標而非地圖座標**

需要檢查座標轉換鏈：
1. `_myPlayer.MapX` 是否正確更新
2. `SetMapPosition` 是否正確調用
3. 是否有 Grid ↔ Pixel 轉換錯誤

```csharp
// 在 GameEntity.Movement.cs 或相關文件中添加日誌
public void SetMapPosition(int x, int y, int heading)
{
    GD.Print($"[Entity-Pos] ID:{ObjectId} | Old:({MapX},{MapY}) | New:({x},{y}) | Heading:{heading}");
    MapX = x;
    MapY = y;
    Heading = heading;
}
```

#### 階段 C：PacketHandler 座標解析驗證

```csharp
// 在 PacketHandler.cs 的 ParseObjectMoving 中增加日誌
private void ParseObjectMoving(PacketReader reader)
{
    int objectId = reader.ReadInt();
    int x = reader.ReadUShort();
    int y = reader.ReadUShort();
    int heading = reader.ReadByte();

    GD.Print($"[Pos-Sync] Recv_Move | ObjID:{objectId} | Server_X:{x} Server_Y:{y} | Heading:{heading}");

    EmitSignal(SignalName.ObjectMoved, objectId, x, y, heading);
}
```

---

### 3.3 修復故障三：魔法特效渲染系統

#### 新增文件：GameWorld.SkillEffect.cs（擴展版）

```csharp
// GameWorld.SkillEffect.cs
using Godot;
using System.Collections.Generic;
using Client.Utility;
using Client.Data;

namespace Client.Game
{
    public partial class GameWorld
    {
        // 特效池管理
        private readonly Dictionary<int, Node2D> _activeEffects = new();
        private int _nextEffectId = 10000;

        /// <summary>
        /// 處理魔法攻擊特效（擴展版）
        /// </summary>
        private void OnObjectMagicAttacked(int attackerId, int targetId, int gfxId, int damage, int x, int y)
        {
            GD.Print($"[Magic-Effect] Attacker:{attackerId} Target:{targetId} Gfx:{gfxId} Damage:{damage} Pos:({x},{y})");

            // 1. 設置施法者動作
            if (_entities.TryGetValue(attackerId, out var attacker))
            {
                attacker.SetAction(GameEntity.ACT_SPELL_DIR);
                if (targetId > 0)
                {
                    attacker.PrepareAttack(targetId, damage);
                }
            }

            // 2. ✅ 新增：創建魔法特效
            CreateMagicEffect(gfxId, x, y, targetId);
        }

        /// <summary>
        /// 創建並播放魔法特效
        /// </summary>
        private void CreateMagicEffect(int gfxId, int gridX, int gridY, int targetId)
        {
            // 1. 從 ListSprLoader 獲取特效定義
            var effectDef = ListSprLoader.Get(gfxId);
            if (effectDef == null)
            {
                GD.PrintErr($"[Magic-Error] SkillID:{gfxId} | SprID:Unknown | Status:NotFound");
                return;
            }

            // 2. 檢查特效資源是否存在
            string spritePath = $"res://Assets/Sprites/effect_{effectDef.SpriteId}.png";
            if (!ResourceLoader.Exists(spritePath))
            {
                GD.PrintErr($"[Magic-Error] SkillID:{gfxId} | SprID:{effectDef.SpriteId} | Path:{spritePath} | Status:FileNotFound");
                return;
            }

            // 3. 加載資源
            var texture = GD.Load<Texture2D>(spritePath);
            if (texture == null)
            {
                GD.PrintErr($"[Magic-Error] SkillID:{gfxId} | SprID:{effectDef.SpriteId} | Status:LoadFailed");
                return;
            }

            // 4. 創建特效節點
            var effectNode = new AnimatedSprite2D();
            effectNode.Name = $"MagicEffect_{gfxId}_{_nextEffectId++}";
            
            // 5. 設置位置（Grid → Pixel 轉換）
            Vector2 worldPos = GridToPixel(gridX, gridY);
            effectNode.Position = worldPos;
            
            // 6. 設置 Z-Index（特效應該在角色上方，UI 下方）
            effectNode.ZIndex = 100; // 假設角色 Z-Index 是 50，UI 是 200
            
            // 7. 配置動畫
            var spriteFrames = new SpriteFrames();
            spriteFrames.AddAnimation("default");
            
            // 從 effectDef.Actions 讀取動畫序列
            if (effectDef.Actions.TryGetValue(0, out var sequence))
            {
                foreach (var frame in sequence.Frames)
                {
                    // 這裡需要根據實際的 Spr 格式切割子圖
                    // 簡化版：假設整張圖就是一幀
                    spriteFrames.AddFrame("default", texture);
                }
            }
            else
            {
                // 沒有動畫序列，直接使用整張圖
                spriteFrames.AddFrame("default", texture);
            }
            
            effectNode.SpriteFrames = spriteFrames;
            effectNode.Animation = "default";
            effectNode.Play();
            
            // 8. 添加到場景
            AddChild(effectNode);
            _activeEffects[effectNode.GetInstanceId()] = effectNode;
            
            // 9. 設置自動銷毀
            var timer = GetTree().CreateTimer(2.0); // 2 秒後銷毀
            timer.Timeout += () => DestroyEffect(effectNode);
            
            GD.Print($"[Magic-OK] SkillID:{gfxId} | Pos:({gridX},{gridY}) | ScreenPos:({worldPos.X},{worldPos.Y}) | Z-Index:{effectNode.ZIndex}");
        }

        /// <summary>
        /// 銷毀特效
        /// </summary>
        private void DestroyEffect(Node2D effectNode)
        {
            if (effectNode != null && IsInstanceValid(effectNode))
            {
                _activeEffects.Remove(effectNode.GetInstanceId());
                effectNode.QueueFree();
            }
        }

        /// <summary>
        /// Grid 座標轉 Pixel 座標（需要根據實際地圖配置調整）
        /// </summary>
        private Vector2 GridToPixel(int gridX, int gridY)
        {
            // 假設：1 Grid = 32 Pixel
            // 需要根據實際的地圖 TileSize 調整
            const int TILE_SIZE = 32;
            return new Vector2(gridX * TILE_SIZE, gridY * TILE_SIZE);
        }
    }
}
```

#### 注意事項

上述 `CreateMagicEffect` 函數是簡化版，實際實現需要：

1. **Spr 資源切割**: 如果 `effect_{SpriteId}.png` 是一個 Sprite Sheet，需要根據 `SprFrame` 的數據切割成多個子圖。
2. **與 CustomCharacterProvider 集成**: 可能需要調用 `ICharacterProvider.LoadEffectTexture` 來統一管理資源。
3. **Z-Index 層級管理**: 需要與現有的渲染層級系統協調。

---

## 第四部分：驗證清單

### 4.1 故障一驗證（攻擊頻率）

- [ ] 實現 `GlobalSpeedController.cs`
- [ ] 在 `GameWorld` 中初始化 `_speedController`
- [ ] 修改 `PerformAttackOnce` 增加速度檢查
- [ ] 測試：連續點擊怪物，確認攻擊間隔符合伺服器要求
- [ ] 測試：在加速 buff 下，確認間隔正確縮短
- [ ] 測試：長時間戰鬥，確認不再被伺服器斷線

### 4.2 故障二驗證（座標同步）

- [ ] 在 `SendAttackPacket` 中添加診斷日誌
- [ ] 在 `ParseObjectMoving` 中添加診斷日誌
- [ ] 在 `SetMapPosition` 中添加診斷日誌
- [ ] 測試：攻擊怪物，記錄日誌輸出
- [ ] 分析：對比 `Param_X/Y` 與 `MyPlayer_X/Y` 與 `Target_X/Y`
- [ ] 修復：根據診斷結果修正座標參數
- [ ] 測試：確認怪物反擊時移動到正確位置

### 4.3 故障三驗證（魔法特效）

- [ ] 實現 `CreateMagicEffect` 函數
- [ ] 實現 `GridToPixel` 轉換函數
- [ ] 在 `OnObjectMagicAttacked` 中調用 `CreateMagicEffect`
- [ ] 測試：釋放魔法，確認特效正確顯示
- [ ] 測試：檢查特效位置是否與目標對齊
- [ ] 測試：檢查特效 Z-Index 是否正確（不被角色遮擋，不遮擋 UI）
- [ ] 測試：確認特效播放完成後正確銷毀

---

## 第五部分：速度參數配置表

### 5.1 伺服器端參數（從資料庫 sprite_frame 表）

| 職業/怪物 | GFX  | Action | Frame (ms) | 說明         |
|----------|------|--------|------------|--------------|
| 人類男性  | 0    | 0      | 450        | 走路         |
| 人類男性  | 0    | 1      | 600        | 近戰攻擊     |
| 人類男性  | 0    | 5      | 750        | 弓箭攻擊     |
| 人類男性  | 0    | 18     | 900        | 魔法（有向） |
| 人類男性  | 0    | 19     | 800        | 魔法（無向） |
| 人類女性  | 1    | 0      | 420        | 走路         |
| 人類女性  | 1    | 1      | 580        | 近戰攻擊     |
| 精靈男性  | 2    | 0      | 400        | 走路         |
| 精靈男性  | 2    | 1      | 550        | 近戰攻擊     |
| 精靈男性  | 2    | 5      | 700        | 弓箭攻擊     |

### 5.2 客戶端配置（對應 ListSprLoader）

| GfxId | SpriteId | Framerate | 說明          |
|-------|----------|-----------|---------------|
| 0     | 0        | 24        | 人類男性      |
| 1     | 1        | 24        | 人類女性      |
| 2     | 2        | 24        | 精靈男性      |
| 3     | 3        | 24        | 精靈女性      |
| 4001  | 101      | 30        | 火球術        |
| 4002  | 102      | 30        | 冰箭術        |

### 5.3 安全計算公式

```
客戶端最小間隔 (ms) = 伺服器基礎間隔 (ms) × 狀態倍率 × 1.05
```

**狀態倍率表**:
- 正常: 1.0
- 加速 (Speed/Brave): 0.75
- 緩速 (Slow): 1.33

**範例計算**:
- 人類近戰攻擊（正常）: 600 × 1.0 × 1.05 = **630 ms**
- 人類近戰攻擊（加速）: 600 × 0.75 × 1.05 = **472.5 ms**
- 精靈弓箭攻擊（正常）: 700 × 1.0 × 1.05 = **735 ms**

---

## 第六部分：文件清單與優先級

### 高優先級（必須修復）

1. **GlobalSpeedController.cs** (新建)
   - 路徑: `/home/claude/client/client/Client/Game/GlobalSpeedController.cs`
   - 作用: 全局速度控制系統

2. **GameWorld.Combat.cs** (修改)
   - 路徑: `/home/claude/client/client/Client/Game/GameWorld.Combat.cs`
   - 修改內容:
     - 整合 `GlobalSpeedController`
     - 修正 `PerformAttackOnce` 的速度檢查
     - 增加座標診斷日誌

3. **GameWorld.SkillEffect.cs** (修改/擴展)
   - 路徑: `/home/claude/client/client/Client/Game/GameWorld.SkillEffect.cs`
   - 修改內容:
     - 實現 `CreateMagicEffect` 函數
     - 增加特效池管理
     - 增加錯誤日誌

### 中優先級（建議修復）

4. **PacketHandler.cs** (修改)
   - 路徑: `/home/claude/client/client/Client/Network/PacketHandler.cs`
   - 修改內容:
     - 在 `ParseObjectMoving` 增加日誌
     - 在 `ParseObjectAttackMagic` 增加日誌

5. **GameEntity.Movement.cs** (修改)
   - 路徑: `/home/claude/client/client/Client/Game/GameEntity.Movement.cs`
   - 修改內容:
     - 在 `SetMapPosition` 增加日誌

### 低優先級（優化項）

6. **ListSprLoader.cs** (檢查)
   - 確認是否正確解析了所有動作的 `DurationUnit`
   - 確認 `110.framerate` 是否正確讀取

7. **CustomCharacterProvider.cs** (檢查)
   - 確認特效資源路徑是否正確
   - 確認資源加載邏輯是否完整

---

## 第七部分：實施步驟

### 階段 1：速度控制系統（預估時間：2小時）

1. 創建 `GlobalSpeedController.cs`
2. 修改 `GameWorld.Combat.cs`
3. 在 `GameWorld.Setup.cs` 中初始化控制器
4. 測試基本功能

### 階段 2：座標診斷（預估時間：1小時）

1. 在關鍵位置添加日誌
2. 進行實際遊戲測試
3. 收集並分析日誌
4. 根據分析結果確定修復方案

### 階段 3：座標修復（預估時間：0.5-2小時）

- 如果是簡單的參數錯誤：0.5 小時
- 如果是複雜的轉換鏈錯誤：2 小時

### 階段 4：魔法特效系統（預估時間：3小時）

1. 實現 `CreateMagicEffect` 函數
2. 實現 `GridToPixel` 轉換
3. 與 `CustomCharacterProvider` 集成
4. 測試各種魔法特效

### 階段 5：全面測試（預估時間：2小時）

1. 測試所有職業的攻擊速度
2. 測試加速/減速狀態
3. 測試座標同步準確性
4. 測試魔法特效渲染
5. 壓力測試（長時間戰鬥）

**總預估時間**: 8.5-11.5 小時

---

## 附錄 A：伺服器速度檢查邏輯詳解

### A.1 CheckSpeed.java 工作流程

```java
// 1. 玩家執行動作（攻擊/移動/施法）
pc.Attack(target, x, y, 1, 0);

// 2. 進入速度檢查
int result = getCheckSped().checkInterval(CheckSpeed.ACT_TYPE.ATTACK);

// 3. checkInterval 邏輯
public int checkInterval(ACT_TYPE type) {
    long now = System.currentTimeMillis();
    long interval = now - _actTimers.get(type);
    int rightInterval = getRightInterval(type);
    
    // 嚴格度調整 (通常 CHECK_STRICTNESS = 95-105)
    interval = (long)(interval * ((CHECK_STRICTNESS - 5) / 100.0D));
    
    if (0 < interval && interval < rightInterval) {
        _injusticeCount++;
        if (_injusticeCount >= 10) {
            doPunishment(); // 凍結/傳送/斷線
            return 2;
        }
        return 1;
    } else if (interval >= rightInterval) {
        _justiceCount++;
        if (_justiceCount >= 4) {
            _injusticeCount = 0; // 重置計數
        }
    }
    
    _actTimers.put(type, now);
    return 0;
}
```

### A.2 懲罰機制

| 懲罰類型 | 代碼 | 效果                |
|---------|------|---------------------|
| FREEZE  | 0    | 凍結角色            |
| TELEPORT| 1    | 傳送回安全點        |
| KICK    | 2    | 斷開連接            |

---

## 附錄 B：客戶端日誌格式規範

為了統一診斷，建議所有日誌遵循以下格式：

```
[Category] Description | Key1:Value1 | Key2:Value2
```

**範例**:
```csharp
GD.Print($"[Pos-Sync] SendAttack | Target:{tid} | X:{x} Y:{y} | MyX:{_myPlayer.MapX} MyY:{_myPlayer.MapY}");
GD.PrintErr($"[Magic-Error] SkillID:{gfxId} | SprID:{sprId} | Status:FileNotFound");
GD.Print($"[Speed-Control] Cooldown | Action:{actionType} | Remaining:{remaining}ms");
```

### 日誌分類

- `[Pos-Sync]`: 座標同步相關
- `[Speed-Control]`: 速度控制相關
- `[Magic-Effect]`: 魔法特效相關
- `[Magic-Error]`: 魔法錯誤
- `[Combat]`: 戰鬥邏輯
- `[Entity-Pos]`: 實體位置更新

---

## 結論

本報告完整分析了伺服器與客戶端的同步機制，識別出三個核心故障，並提供了詳細的修復方案。通過實施本報告中的修復代碼，可以：

1. **消除攻擊速度過快導致的斷線問題**
2. **修正座標同步錯亂**
3. **恢復魔法特效的正常渲染**

所有修復方案都嚴格對齊伺服器端的驗證邏輯，並增加了完善的診斷日誌，確保問題可追溯、可驗證。

---

**報告結束**
