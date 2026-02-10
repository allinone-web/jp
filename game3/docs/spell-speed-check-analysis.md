# 服務器魔法速度檢查機制分析報告

## 結論確認

**✅ 結論正確：魔法間隔時間與動作總時長是不同的概念**

## 詳細分析

### 1. 服務器速度檢查機制

#### 1.1 CheckSpeed.java 核心邏輯

```java
public int checkInterval(ACT_TYPE type) {
    long now = System.currentTimeMillis();
    long interval = now - ((Long)this._actTimers.get(type)).longValue();
    int rightInterval = getRightInterval(type);
    
    // 檢查：如果間隔時間 < 期望間隔時間，則判定為加速
    if ((0L < interval) && (interval < rightInterval)) {
        this._injusticeCount += 1;
        // ... 懲罰邏輯
    }
    
    this._actTimers.put(type, Long.valueOf(now)); // 更新時間戳
    return result;
}
```

**關鍵點**：
- 服務器檢查的是**兩次動作之間的間隔時間**（interval）
- 不是動作的持續時間，而是**動作與動作之間的冷卻時間**

#### 1.2 getRightInterval() 方法

```java
private int getRightInterval(ACT_TYPE type) {
    switch (type) {
    case MOVE:
        interval = SprTable.getInstance().getAttackSpeed(this._pc.getGfx(), this._pc.getGfxMode() + 1);
        break;
    case ATTACK:
        interval = SprTable.getInstance().getMoveSpeed(this._pc.getGfx(), this._pc.getGfxMode());
        break;
    case SPELL_DIR:
        interval = SprTable.getInstance().getDirSpellSpeed(this._pc.getGfx(), 18);
        break;
    case SPELL_NODIR:
        interval = SprTable.getInstance().getNodirSpellSpeed(this._pc.getGfx(), 19);
        break;
    }
    return interval;
}
```

**關鍵點**：
- `SPELL_DIR` 和 `SPELL_NODIR` 固定使用 actionId=18 和 19
- 返回的值直接來自數據庫 `sprite_frame` 表的 `frame` 字段
- 這個值被用作**間隔時間**（毫秒），不是動作總時長

### 2. SprTable.java 數據載入

```java
public void loadSprAction() {
    // ...
    while (rs.next()) {
        int actid = rs.getInt("action");
        int speed = rs.getInt("frame");  // 直接將 frame 讀取為 speed
        
        switch (actid) {
        case 18:
            spr.dirSpellSpeed.put(Integer.valueOf(actid), Integer.valueOf(speed));
            break;
        case 19:
            spr.nodirSpellSpeed.put(Integer.valueOf(actid), Integer.valueOf(speed));
            break;
        // ...
        }
    }
}
```

**關鍵點**：
- 數據庫中的 `frame` 字段被直接讀取為 `speed`（速度/間隔時間）
- 這個值存儲在 `dirSpellSpeed` 和 `nodirSpellSpeed` Map 中
- 返回時直接返回這個值，**沒有進行任何轉換或計算**

### 3. 魔法使用時的檢查時機

#### 3.1 PcSkill.java - toMagic() 方法

```java
public void toMagic(int lv, int no, int id) {
    // ...
    m.toMagic(id);
    
    if (Config.CHECK_SPEED_TYPE != 2) break;
    if (m.getSkill().getType().equalsIgnoreCase("attack")) {
        this.pc.getCheckSped().checkInterval(CheckSpeed.ACT_TYPE.SPELL_DIR);
        break;
    }
    if (!m.getSkill().getType().equalsIgnoreCase("buff")) break;
    this.pc.getCheckSped().checkInterval(CheckSpeed.ACT_TYPE.SPELL_NODIR);
}
```

**關鍵點**：
- 速度檢查在**發送魔法封包時立即執行**
- 不是在動畫完成後，而是在**魔法封包發送時**
- 檢查的是**上一次魔法封包到這一次魔法封包之間的間隔時間**

### 4. 攻擊動作的對比

#### 4.1 PcInstance.java - Attack() 方法

```java
public synchronized void Attack(L1Object target, int locx, int locy, int type, int bowtype) {
    // ...
    if (Config.CHECK_SPEED_TYPE == 2) {
        getCheckSped().checkInterval(CheckSpeed.ACT_TYPE.ATTACK);
    } else {
        this.attack.check();
    }
    // ...
}
```

**關鍵點**：
- 攻擊速度檢查也在**發送攻擊封包時立即執行**
- 使用 `getMoveSpeed()` 獲取間隔時間（注意：這裡用的是 `getMoveSpeed`，可能是歷史遺留問題）
- 檢查的也是**兩次攻擊封包之間的間隔時間**

### 5. 數據庫 frame 值的實際含義

#### 5.1 從 SQL 文件分析

```sql
INSERT INTO `sprite_frame` (`name`, `gfx`, `action`, `action_name`, `frame`)
VALUES
    ('王子',0,18,'spell direction',880),
    ('王子',0,19,'spell no direction',800),
    ('王子',0,1,'attack',840),
    ('王子',0,0,'walk',640),
```

**觀察**：
- `frame` 值（880, 800, 840, 640）都是**毫秒級別的整數**
- 這些值與 `list.spr` 中動作的**總時長**（所有幀 DurationUnit * 40ms 的總和）**不一致**
- 例如：王子的 18.spell direction
  - `list.spr` 總時長：1600ms（160+200+560+160+520）
  - 數據庫 `frame` 值：880ms
  - **差異：720ms**

### 6. 結論

#### 6.1 魔法間隔時間的特殊性 ✅

1. **服務器使用的 frame 值是間隔時間，不是動作總時長**
   - 數據庫 `sprite_frame.frame` 字段存儲的是**兩次魔法動作之間的最小間隔時間**（毫秒）
   - 這個值用於速度檢查，防止玩家過快連續使用魔法

2. **list.spr 中的動作總時長是動畫播放時長**
   - `list.spr` 中計算的總時長是**動畫從開始到結束的總時間**
   - 這個值用於客戶端播放動畫，確保動畫流暢

3. **兩者不需要一致**
   - 間隔時間 < 動作總時長：允許在動畫完成前就發送下一個魔法（但會被服務器拒絕）
   - 間隔時間 > 動作總時長：必須等待動畫完成後才能發送下一個魔法
   - 間隔時間 = 動作總時長：理想情況，但實際中很少見

#### 6.2 為什麼魔法動作不一致是正常的

- **服務器設計**：魔法間隔時間是**遊戲平衡參數**，不是動畫時長
- **客戶端設計**：動畫總時長是**視覺效果參數**，確保動畫完整播放
- **兩者目的不同**：
  - 間隔時間：防止作弊、控制遊戲節奏
  - 動畫時長：提供視覺反饋、增強遊戲體驗

### 7. 對客戶端的啟示

1. **客戶端應該使用數據庫中的 frame 值作為攻擊/魔法間隔時間**
   - 不要使用 `list.spr` 計算的總時長作為間隔時間
   - 應該從 `SprDataTable.cs` 中讀取對應的間隔時間

2. **客戶端發送攻擊/魔法封包的時機**
   - 應該在**動畫播放時**發送封包（不是動畫完成後）
   - 但必須確保**兩次封包之間的間隔 >= 服務器期望的間隔時間**

3. **gfx=240 加速提示的可能原因**
   - 客戶端可能使用了錯誤的間隔時間計算
   - 或者客戶端在動畫完成前就發送了攻擊封包，導致間隔時間過短

## 建議

1. **檢查客戶端攻擊間隔計算邏輯**
   - 確認 `SprDataTable.GetInterval()` 是否正確使用 gfx=240 的數據
   - 確認是否使用了 `list.spr` 的總時長而不是數據庫的間隔時間

2. **添加日誌驗證**
   - 記錄客戶端計算的攻擊間隔時間
   - 記錄實際發送攻擊封包的時間戳
   - 對比服務器期望的間隔時間

3. **檢查動畫播放與封包發送的同步**
   - 確認攻擊封包是否在動畫開始時發送
   - 確認是否在動畫完成前就發送了下一輪攻擊封包
