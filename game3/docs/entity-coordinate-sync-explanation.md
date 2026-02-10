# 實體座標同步說明：為什麼要「重新從 _entities 獲取目標實體」

## 問題背景

### 當前代碼的問題

**當前代碼** (`Client/Game/GameWorld.Combat.cs:408-409`):
```csharp
var target = _currentTask.Target;  // 從任務中獲取目標引用
int tx = target.MapX;              // 使用目標的座標
int ty = target.MapY;
```

**問題**：
1. `_currentTask.Target` 是一個 `GameEntity` 對象的**引用**
2. 雖然這個引用指向的對象會更新座標，但可能存在以下情況：
   - 目標已經被刪除，但 `_currentTask.Target` 還保留著舊的引用
   - 目標已經超出視野範圍，`_entities` 中已經沒有這個目標了
   - 目標移動了，但 `_currentTask.Target` 的座標可能還沒有更新

## 什麼是 `_entities`？

### `_entities` 字典的作用

`_entities` 是一個字典，存儲所有**當前存在的實體**：

```csharp
private Dictionary<int, GameEntity> _entities;  // ObjectId -> GameEntity
```

**更新機制**：
1. **實體出現時** (`OnObjectSpawned`): 添加到 `_entities`
2. **實體移動時** (`OnObjectMoved`): 更新 `_entities[objectId].SetMapPosition(x, y, heading)`
3. **實體刪除時** (`OnObjectDeleted`): 從 `_entities` 中移除

**關鍵點**：
- `_entities` 中的實體座標是**最新的**（通過 `OnObjectMoved` 更新）
- 如果實體不在 `_entities` 中，說明它已經被刪除或超出視野範圍

## 「重新獲取」的含義

### 當前做法（有問題）

```csharp
// 第395行：從任務中獲取目標
var target = _currentTask.Target;

// 第408-409行：直接使用目標的座標
int tx = target.MapX;
int ty = target.MapY;

// 第419行：發送攻擊包
PerformAttackOnce(target.ObjectId, tx, ty);
```

**問題**：
- 如果目標已經被刪除，`target` 可能指向一個已經不存在的實體
- 如果目標移動了，`target.MapX` 和 `target.MapY` 可能不是最新的

### 修復方案（重新獲取）

```csharp
// 第395行：從任務中獲取目標ID
var target = _currentTask.Target;
int targetId = target.ObjectId;

// 【修復】重新從 _entities 獲取目標實體，確保使用最新的座標
if (!_entities.TryGetValue(targetId, out var freshTarget))
{
    // 目標不在 _entities 中，說明已經被刪除或超出視野範圍
    GD.Print($"[Combat-Error] Target {targetId} not in _entities, canceling attack");
    FinishCurrentTask();
    return;
}

// 使用最新的目標座標
int tx = freshTarget.MapX;  // 從 _entities 中獲取的最新座標
int ty = freshTarget.MapY;

// 發送攻擊包
PerformAttackOnce(targetId, tx, ty);
```

## 為什麼這樣做？

### 1. 確保目標存在

```csharp
if (!_entities.TryGetValue(targetId, out var freshTarget))
{
    // 目標已經被刪除或超出視野範圍，不發送攻擊包
    return;
}
```

**好處**：
- 如果目標不在 `_entities` 中，說明它已經被刪除或超出視野範圍
- 不發送攻擊包，避免服務器返回 `damage=0`（miss）

### 2. 確保使用最新座標

```csharp
// _entities 中的實體座標會通過 OnObjectMoved 更新
_entities[objectId].SetMapPosition(x, y, heading);  // 在 OnObjectMoved 中更新

// 所以從 _entities 獲取的座標是最新的
int tx = freshTarget.MapX;  // 最新的座標
int ty = freshTarget.MapY;
```

**好處**：
- `_entities` 中的實體座標會通過 `OnObjectMoved` 實時更新
- 確保發送攻擊包時使用的是**服務器最新確認的目標座標**

### 3. 避免使用過時的引用

**問題場景**：
1. 玩家選擇目標A（ObjectId=100）
2. 目標A移動了，`_entities[100]` 更新了座標
3. 但 `_currentTask.Target` 可能還保留著舊的引用
4. 如果直接使用 `_currentTask.Target.MapX`，可能使用的是舊座標

**修復後**：
1. 從 `_entities` 重新獲取目標實體
2. 確保使用的是最新的座標

## 完整修復代碼示例

### 修復前（有問題）

```csharp
private void ExecuteAttackAction(CombatProfile profile, int dist)
{
    var target = _currentTask.Target;  // 直接使用任務中的目標引用
    
    int tx = target.MapX;              // 可能使用過時的座標
    int ty = target.MapY;
    
    PerformAttackOnce(target.ObjectId, tx, ty);
}
```

### 修復後（正確）

```csharp
private void ExecuteAttackAction(CombatProfile profile, int dist)
{
    var target = _currentTask.Target;
    int targetId = target.ObjectId;
    
    // 【修復】重新從 _entities 獲取目標實體，確保使用最新的座標
    if (!_entities.TryGetValue(targetId, out var freshTarget))
    {
        // 目標不在 _entities 中，說明已經被刪除或超出視野範圍
        GD.Print($"[Combat-Error] Target {targetId} not in _entities, canceling attack");
        FinishCurrentTask();
        return;
    }
    
    // 使用最新的目標座標（從 _entities 中獲取）
    int tx = freshTarget.MapX;
    int ty = freshTarget.MapY;
    
    if (profile.UsePredictShot) (tx, ty) = PredictRangedTarget(freshTarget);
    
    PerformAttackOnce(targetId, tx, ty);
}
```

## 總結

**「重新從 `_entities` 獲取目標實體」的意思是**：

1. **不要直接使用** `_currentTask.Target` 的座標
2. **而是從 `_entities` 字典中重新查找**目標實體：`_entities.TryGetValue(targetId, out var freshTarget)`
3. **確保**：
   - 目標還在 `_entities` 中（沒有被刪除）
   - 使用最新的座標（`_entities` 中的實體會通過 `OnObjectMoved` 更新座標）

**這樣做的好處**：
- 避免發送攻擊包給已經不存在的目標
- 確保使用服務器最新確認的目標座標
- 減少攻擊miss的情況
