# 攻擊系統穩定性分析報告

## 問題描述

根據 `test.txt` 日誌分析，遊戲存在兩個核心故障：

1. **攻擊系統不穩定**：近戰攻擊經常完全沒反應（damage=0），但遠程和魔法非常穩定
2. **怪物不追擊**：近戰怪物不追擊玩家，但弓箭手怪物可以追擊成功

## 日誌對比分析

### 前段日誌（攻擊失效階段）

**時間範圍**：日誌開頭至約第8400行

**關鍵特徵**：

1. **所有攻擊都是 damage=0**
   ```
   [Combat-Packet] Received Op35 attacker=10002 target=6265409 action=1 damage=0
   [Combat-Fix] Monster 10002 too far (dist=1 > 2) or damage=0 <= 0, skipping attack animation.
   ```

2. **座標更新間隔嚴重超時**
   ```
   [Pos-Update] TimeSinceLastUpdate=1283ms (預期: 640ms)
   [Pos-Update-Error] Position update interval too long! Expected: 640ms Actual: 1283ms
   [Pos-Update-Error] 這說明玩家座標沒有及時傳給服務器，服務器可能使用舊座標計算怪物攻擊和追擊
   ```

3. **移動封包間隔超時**
   ```
   [Move-Packet-Error] Move packet interval too long! Expected: 600ms Actual: 7706ms
   [Move-Packet-Error] 這說明玩家座標沒有及時傳給服務器，服務器可能使用舊座標計算怪物攻擊
   ```

4. **沒有 Server-Audit 日誌**
   - 前段完全沒有 `[Server-Audit]` 日誌
   - 說明服務器**沒有發送攻擊包給玩家**，或者服務器認為玩家不在攻擊範圍內

5. **玩家位置固定**
   - 玩家位置長時間停留在 `(32661,33135)`
   - 怪物位置在 `(32662,33136)` 附近，距離僅1格

6. **怪物移動異常**
   - 怪物收到 `S_ObjectMoving` 但都是**原地踏步**（From 和 To 相同）
   ```
   [Move-Diag] Monster 6265409 received move packet:
     From: (32662,33136) To: (32662,33136)  // 原地踏步
     Distance to player: 1 cells
   ```

### 後段日誌（攻擊正常階段）

**時間範圍**：約第8400行至結尾

**關鍵特徵**：

1. **攻擊有傷害**
   ```
   [Combat-Packet] Received Op35 attacker=10002 target=6264445 action=1 damage=16
   [Combat-Packet] Received Op35 attacker=10002 target=6263645 action=1 damage=32
   [Combat-Packet] Received Op35 attacker=10002 target=6264453 action=1 damage=25
   ```

2. **座標更新正常**
   ```
   [Pos-Update] TimeSinceLastUpdate=844ms (接近預期)
   [Move-Packet] TimeSinceLastPacket=461ms (預期: 450ms)  // 正常範圍
   ```

3. **有 Server-Audit 日誌**
   ```
   [Server-Audit] Server thinks I am at: (32672,33143) | My Current: (32672,33143)
   [Server-Audit] Op35 RangeAttack: Server thinks player at (32672,33140) from attacker 6264447
   ```
   - 說明服務器**知道玩家位置**，並能正確計算攻擊

4. **玩家位置正常移動**
   - 玩家位置從 `(32671,33141)` → `(32672,33142)` → `(32672,33143)` 正常移動
   - 移動封包間隔在 450-650ms 正常範圍內

5. **弓箭手怪物追擊成功**
   ```
   [Pos-Sync] Op35 RangeAttack: Updating entity 6264447 position: server-confirmed (32671,33138)
   [Server-Audit] Op35 RangeAttack: Server thinks player at (32668,33146) from attacker 6264447
   [Combat-Packet] Received Op35 attacker=6264447 target=10002 action=21 damage=0
   ```
   - 弓箭手（6264447）可以追擊並攻擊玩家
   - 攻擊範圍為 12 格（`range=12`）

## 根本原因分析

### 1. 座標同步失效（核心問題）

**前段問題**：
- 座標更新間隔經常超過 1000ms（正常應為 640ms）
- 移動封包間隔有時超過 7000ms（正常應為 600ms）
- **服務器不知道玩家最新位置**

**後段正常**：
- 座標更新間隔在 640-850ms 範圍內
- 移動封包間隔在 450-650ms 範圍內
- **服務器知道玩家位置**

**影響**：
- 服務器使用**舊座標**計算攻擊距離
- 服務器認為玩家不在攻擊範圍內 → 返回 `damage=0`
- 服務器AI認為玩家不在追擊範圍內 → 怪物不追擊

### 2. 攻擊距離判定差異

**近戰攻擊（range=1-2格）**：
- 攻擊範圍極小（1-2格）
- 座標偏差 1 格就會導致攻擊失敗
- **對座標同步要求極高**

**遠程攻擊（弓箭手 range=12格）**：
- 攻擊範圍大（12格）
- 座標偏差 1-2 格仍能命中
- **對座標同步要求較低**

**魔法攻擊**：
- 攻擊範圍通常 ≥ 6 格
- 對座標同步要求中等

**結論**：
- **近戰攻擊對座標同步最敏感**，座標偏差會導致攻擊失效
- **遠程和魔法攻擊對座標同步較不敏感**，即使座標略有偏差也能成功

### 3. 怪物AI追擊機制

**近戰怪物（range=1-2格）**：
- 掃描範圍通常為 2-5 格
- 如果服務器認為玩家不在掃描範圍內，AI不會啟動追擊
- **依賴服務器知道玩家最新位置**

**弓箭手怪物（range=12格）**：
- 掃描範圍通常為 12-15 格
- 即使座標略有偏差，仍能掃描到玩家
- **對座標同步要求較低**

**結論**：
- **弓箭手怪物有更大的掃描範圍**，即使座標略有偏差也能追擊
- **近戰怪物掃描範圍小**，座標偏差會導致AI不啟動追擊

## 為什麼會有這種區別？

### 前段（攻擊失效）的原因

1. **座標同步失效**
   - 客戶端發送的 `C_MoveChar` 封包可能被服務器丟棄（速度檢查失敗）
   - 或者封包丟失/延遲
   - 服務器使用**舊座標**計算攻擊

2. **服務器AI狀態**
   - 服務器認為玩家不在攻擊範圍內（使用舊座標）
   - 返回 `damage=0`（MISS 或超出範圍）
   - 怪物AI不啟動追擊（認為玩家不在掃描範圍內）

3. **近戰攻擊的脆弱性**
   - 攻擊範圍只有 1-2 格
   - 座標偏差 1 格就會導致攻擊失敗
   - **對座標同步要求極高**

### 後段（攻擊正常）的原因

1. **座標同步正常**
   - 客戶端發送的 `C_MoveChar` 封包正常到達服務器
   - 座標更新間隔在正常範圍內
   - 服務器知道玩家最新位置

2. **服務器AI正常**
   - 服務器使用**最新座標**計算攻擊
   - 攻擊判定成功 → 返回實際傷害
   - 怪物AI正常啟動追擊

3. **弓箭手怪物的優勢**
   - 攻擊範圍大（12格），即使座標略有偏差也能命中
   - 掃描範圍大（12-15格），更容易發現玩家

## 總結

### 核心問題

**座標同步失效**導致服務器使用舊座標計算攻擊和AI，進而導致：

1. **近戰攻擊失效**：攻擊範圍小（1-2格），座標偏差會導致攻擊失敗
2. **近戰怪物不追擊**：掃描範圍小，座標偏差會導致AI不啟動追擊
3. **遠程和魔法穩定**：攻擊範圍大，對座標同步要求較低

### 關鍵差異

| 項目 | 前段（失效） | 後段（正常） |
|------|-------------|-------------|
| **座標更新間隔** | 1283ms, 1270ms, 22040ms（超時） | 640-850ms（正常） |
| **移動封包間隔** | 7706ms, 9580ms, 31755ms（超時） | 450-650ms（正常） |
| **Server-Audit** | ❌ 沒有 | ✅ 有 |
| **攻擊傷害** | 全部 damage=0 | damage=16, 21, 25, 32 |
| **怪物追擊** | ❌ 不追擊 | ✅ 弓箭手追擊成功 |

### 解決方向

1. **確保座標同步穩定**
   - 檢查 `C_MoveChar` 封包是否被服務器丟棄（速度檢查）
   - 確保移動封包間隔符合服務器要求
   - 添加重試機制，確保座標更新成功

2. **優化近戰攻擊**
   - 在攻擊前強制發送座標更新
   - 確保服務器知道玩家最新位置後再發送攻擊封包

3. **改善怪物AI喚醒**
   - 定期發送座標更新，確保服務器AI知道玩家位置
   - 在移動時及時發送座標更新，避免座標偏差

## 附錄：關鍵日誌片段

### 前段（攻擊失效）

```
[Combat-Packet] Received Op35 attacker=10002 target=6265409 action=1 damage=0
[Pos-Update-Error] Position update interval too long! Expected: 640ms Actual: 1283ms
[Move-Packet-Error] Move packet interval too long! Expected: 600ms Actual: 7706ms
```

### 後段（攻擊正常）

```
[Combat-Packet] Received Op35 attacker=10002 target=6264445 action=1 damage=16
[Server-Audit] Server thinks I am at: (32672,33143) | My Current: (32672,33143)
[Move-Packet] TimeSinceLastPacket=461ms (expected: 450ms)  // 正常
```
