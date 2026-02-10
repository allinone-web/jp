# 182 vs JP 魔法 ID 映射差異分析

## 核心問題

**182 版每級 5 個魔法（ID 連續 +5），JP 版每級 8 個魔法（ID 連續 +8）。**

- 182: `id = (level-1) × 5 + skill_no + 1`，總共 50 個（ID 1-50）
- JP:  `id = (level-1) × 8 + skill_no + 1`，總共 80 個（ID 1-80，基礎法師）

**Level 1 前 5 個完全相同，從第 6 個開始全面錯位！**

## 完整對照表

```
JP_ID | JP 魔法名           | 182_ID | 182 魔法名          | 匹配？
------|---------------------|--------|--------------------|---------
  ===== Level 1 (JP: ID 1-8, 182: ID 1-5) =====
  1   | Heal 治癒術          |   1    | 初級治癒術          | ✅ 相同
  2   | Light 日光術          |   2    | 日光術              | ✅ 相同
  3   | Shield 保護罩         |   3    | 保護罩              | ✅ 相同
  4   | Energy Bolt 光箭      |   4    | 光箭                | ✅ 相同
  5   | Teleport 指定傳送     |   5    | 指定傳送            | ✅ 相同
  6   | Ice Dagger 冰箭       |  ---   | (182 無此魔法)      | ❌ JP 新增
  7   | Wind Cutter 風刃      |  ---   | (182 無此魔法)      | ❌ JP 新增
  8   | Holy Weapon 神聖武器   |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 2 (JP: ID 9-16, 182: ID 6-10) =====
  9   | Cure Poison 解毒      |   6    | 解毒術              | ⚠️ ID 不同！JP=9, 182=6
 10   | Chill Touch 寒冷戰慄   |   7    | 寒冷戰慄            | ⚠️ ID 不同！JP=10, 182=7
 11   | Curse Poison 毒咒     |   8    | 毒咒                | ⚠️ ID 不同！JP=11, 182=8
 12   | Enchant Weapon 擬魔武器|   9    | 擬似魔法武器         | ⚠️ ID 不同！JP=12, 182=9
 13   | Detection 無所遁形     |  10    | 無所遁形術           | ⚠️ ID 不同！JP=13, 182=10
 14   | Decrease Weight       |  ---   | (182 無此魔法)      | ❌ JP 新增
 15   | Fire Arrow 火箭       |  ---   | (182 無此魔法)      | ❌ JP 新增
 16   | Stalac 落石           |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 3 (JP: ID 17-24, 182: ID 11-15) =====
 17   | Lightning 極光雷電     |  11    | 極光雷電            | ⚠️ JP=17, 182=11
 18   | Turn Undead 起死回生   |  12    | 起死回生術           | ⚠️ JP=18, 182=12
 19   | Extra Heal 中級治癒    |  13    | 中級治癒術           | ⚠️ JP=19, 182=13
 20   | Curse Blind 闇盲咒     |  14    | 闇盲咒術            | ⚠️ JP=20, 182=14
 21   | Blessed Armor 鎧甲護持 |  15    | 鎧甲護持            | ⚠️ JP=21, 182=15
 22   | Frozen Cloud 冰錐     |  ---   | (182 無此魔法)      | ❌ JP 新增
 23   | Weak Elemental        |  ---   | (182 無此魔法)      | ❌ JP 新增
 24   | (none 預留)           |  ---   | (182 無此魔法)      | ❌ JP 預留

  ===== Level 4 (JP: ID 25-32, 182: ID 16-20) =====
 25   | Fire Ball 火球        |  16    | 燃燒的火球           | ⚠️ JP=25, 182=16
 26   | P.E. DEX 通暢氣脈     |  17    | 通暢氣脈術           | ⚠️ JP=26, 182=17
 27   | Weapon Break 壞物術   |  18    | 壞物術              | ⚠️ JP=27, 182=18
 28   | Vampiric Touch 吸血    |  19    | 吸血鬼之吻           | ⚠️ JP=28, 182=19
 29   | Slow 緩速術           |  20    | 緩速術              | ⚠️ JP=29, 182=20
 30   | Earth Jail 地獄        |  ---   | (182 無此魔法)      | ❌ JP 新增
 31   | Counter Magic         |  ---   | (182 無此魔法)      | ❌ JP 新增
 32   | Meditation            |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 5 (JP: ID 33-40, 182: ID 21-25) =====
 33   | Curse Paralyze 石化   |  21    | 木乃伊的詛咒         | ⚠️ JP=33, 182=21
 34   | Call Lightning 極道落雷|  22    | 極道落雷            | ⚠️ JP=34, 182=22
 35   | Greater Heal 高級治癒  |  23    | 高級治癒術           | ⚠️ JP=35, 182=23
 36   | Taming Monster 迷魅   |  24    | 迷魅術              | ⚠️ JP=36, 182=24
 37   | Remove Curse 聖潔之光  |  25    | 聖潔之光            | ⚠️ JP=37, 182=25
 38   | Cone of Cold          |  ---   | (182 無此魔法)      | ❌ JP 新增
 39   | Mana Drain            |  ---   | (182 無此魔法)      | ❌ JP 新增
 40   | Darkness              |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 6 (JP: ID 41-48, 182: ID 26-30) =====
 41   | Create Zombie 造屍    |  26    | 造屍術              | ⚠️ JP=41, 182=26
 42   | P.E. STR 體魄強健     |  27    | 體魄強健術           | ⚠️ JP=42, 182=27
 43   | Haste 加速術          |  28    | 加速術              | ⚠️ JP=43, 182=28
 44   | Cancellation 相消     |  29    | 魔法相消術           | ⚠️ JP=44, 182=29
 45   | Eruption 地裂術       |  30    | 地裂術              | ⚠️ JP=45, 182=30
 46   | Sun Burst             |  ---   | (182 無此魔法)      | ❌ JP 新增
 47   | Weakness              |  ---   | (182 無此魔法)      | ❌ JP 新增
 48   | Bless Weapon          |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 7 (JP: ID 49-56, 182: ID 31-35) =====
 49   | Heal All 體力回復      |  31    | 體力回復術           | ⚠️ JP=49, 182=31
 50   | Ice Lance 冰矛圍籬    |  32    | 冰矛圍籬            | ⚠️ JP=50, 182=32
 51   | Summon Monster 召喚   |  33    | 召喚術              | ⚠️ JP=51, 182=33
 52   | Holy Walk 神聖的圓     |  34    | 神聖的圓            | ⚠️ JP=52, 182=34
 53   | Tornado 龍捲風        |  35    | 龍捲風              | ⚠️ JP=53, 182=35
 54   | Greater Haste         |  ---   | (182 無此魔法)      | ❌ JP 新增
 55   | Berserkers            |  ---   | (182 無此魔法)      | ❌ JP 新增
 56   | Disease               |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 8 (JP: ID 57-64, 182: ID 36-40) =====
 57   | Full Heal 全部治癒     |  36    | 全部治癒術           | ⚠️ JP=57, 182=36
 58   | Fire Wall 火牢        |  37    | 火牢                | ⚠️ JP=58, 182=37
 59   | Blizzard 冰雪暴       |  38    | 冰雪暴              | ⚠️ JP=59, 182=38
 60   | Invisibility 隱身     |  39    | 隱身術              | ⚠️ JP=60, 182=39
 61   | Resurrection 返生     |  40    | 返生術              | ⚠️ JP=61, 182=40
 62   | Earthquake            |  ---   | (182 無此魔法)      | ❌ JP 新增
 63   | Life Stream           |  ---   | (182 無此魔法)      | ❌ JP 新增
 64   | Silence               |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 9 (JP: ID 65-72, 182: ID 41-45) =====
 65   | Lightning Storm 力場  |  41    | 力場                | ⚠️ JP=65, 182=41
 66   | Fog of Sleeping 沉睡  |  42    | 沉睡之霧            | ⚠️ JP=66, 182=42
 67   | Shape Change 變形     |  43    | 變形術              | ⚠️ JP=67, 182=43
 68   | Immune to Harm 聖結界 |  44    | 聖結界              | ⚠️ JP=68, 182=44
 69   | Mass Teleport 集體傳送|  45    | 集體傳送術           | ⚠️ JP=69, 182=45
 70   | Fire Storm            |  ---   | (182 無此魔法)      | ❌ JP 新增
 71   | Decay Potion          |  ---   | (182 無此魔法)      | ❌ JP 新增
 72   | Counter Detection     |  ---   | (182 無此魔法)      | ❌ JP 新增

  ===== Level 10 (JP: ID 73-80, 182: ID 46-50) =====
 73   | Create M. Weapon 創魔 |  46    | 創造魔法武器         | ⚠️ JP=73, 182=46
 74   | Meteor Strike 流星雨  |  47    | 流星雨              | ⚠️ JP=74, 182=47
 75   | G. Resurrection 反射  |  48    | 反射之池            | ⚠️ JP=75, 182=48
 76   | Mass Slow 瞬間停止    |  49    | 瞬間停止            | ⚠️ JP=76, 182=49
 77   | Disintegrate 究極光裂 |  50    | 究極光裂術           | ⚠️ JP=77, 182=50
 78   | Absolute Barrier      |  ---   | (182 無此魔法)      | ❌ JP 新增
 79   | Advanced Spirits      |  ---   | (182 無此魔法)      | ❌ JP 新增
 80   | Freezing Blizzard     |  ---   | (182 無此魔法)      | ❌ JP 新增
```

## ID 偏移規律

```
Level  | 182 起始ID | JP 起始ID | 偏移量 (JP - 182)
-------|-----------|----------|------------------
  1    |     1     |     1    |    0  ← 前5個完全一致！
  2    |     6     |     9    |   +3
  3    |    11     |    17    |   +6
  4    |    16     |    25    |   +9
  5    |    21     |    33    |  +12
  6    |    26     |    41    |  +15
  7    |    31     |    49    |  +18
  8    |    36     |    57    |  +21
  9    |    41     |    65    |  +24
 10    |    46     |    73    |  +27
```

**偏移公式**: `JP_ID = 182_ID + (level - 1) × 3`

## 匯入 182 數據到 JP 會發生什麼？

假設你把 182 的 ID 1-50 直接匯入 JP 的 skills 表：

```
你匯入 182 ID=6 (解毒術 Lv2)    → 覆蓋了 JP ID=6 (冰箭 Ice Dagger Lv1)
你匯入 182 ID=11 (極光雷電 Lv3) → 覆蓋了 JP ID=11 (毒咒 Curse Poison Lv2)
你匯入 182 ID=16 (火球 Lv4)     → 覆蓋了 JP ID=16 (落石 Stalac Lv2)
你匯入 182 ID=21 (石化 Lv5)     → 覆蓋了 JP ID=21 (鎧甲護持 Blessed Armor Lv3)
你匯入 182 ID=30 (地裂 Lv6)     → 覆蓋了 JP ID=30 (地獄 Earth Jail Lv4)
你匯入 182 ID=47 (流星雨 Lv10)  → 覆蓋了 JP ID=47 (弱化 Weakness Lv6)
你匯入 182 ID=50 (究極光裂 Lv10)→ 覆蓋了 JP ID=50 (冰矛圍籬 Ice Lance Lv7)
```

**結果**：高等級的 182 數據覆蓋了低等級的 JP 魔法位置，整個魔法書全亂了。

## 修復方案

**最簡單的方案：直接使用 JP 原始的 skills.csv 資料**

```bash
# 清空 skills 表
mysql -u root -p7777 l1jdb -e "TRUNCATE TABLE skills;"

# 重新匯入 JP 原始 CSV
mysqlimport -u root -p7777 --local --fields-terminated-by=',' \
  --lines-terminated-by='\n' --ignore-lines=1 l1jdb \
  /Users/airtan/Documents/GitHub/jp/db/csv/ja/skills.csv
```

JP 原始數據已包含 182 的全部 50 個魔法（只是 ID 不同），加上 30 個新魔法和精靈/騎士/龍騎/幻術等系列。

## 客戶端 (game2) 注意事項

如果客戶端之前按 182 的 ID 寫死了魔法映射，需要同步修改為 JP 的 ID 映射。
關鍵：客戶端發送 C_UseSkill 封包時帶的是 `skill_number` (0-7) + `skill_level` (1-10)，
伺服器根據這兩個值查表找到對應魔法，所以**客戶端不需要知道 skills 表的主鍵 ID**。

真正影響客戶端的是 `skill_id` 欄位（bitmask），用於：
- S_AddSkill 封包：告訴客戶端哪些魔法已學會
- 每級別用 8 位 bitmask (1,2,4,8,16,32,64,128)
- 182 每級 5 位 (1,2,4,8,16)，JP 每級 8 位 (1,2,4,8,16,32,64,128)
