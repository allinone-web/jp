using Godot;
using System;
using System.Collections.Generic;
using Client.Network;
using Client.Data;
using Client.UI;

namespace Client.Game
{
    public partial class GameWorld
    {
        // --- 魔法数据 ---
        // 存储 32 个类别的技能掩码 (Int32 可以存 32 位，服务器 byte 只有 8 位，但 Java 经常用 int 存 byte)
        // 对应 S_SkillAdd 中的 lv1, lv2... lv10, elf1...
        private int[] _skillMasks = new int[32];
        
        // 【魔法時位置更新】記錄上次發送位置更新的時間（用於避免過於頻繁）
        // 【關鍵設計】使用 640ms 作為更新間隔，與遊戲移動節奏同步
        private long _lastMagicPositionUpdateTime = 0;
        private const long MIN_MAGIC_POSITION_UPDATE_INTERVAL_MS = 640; // 最小發送間隔640ms，與遊戲移動節奏同步 
        
        // 【魔法冷卻日誌】避免刷屏：每隔一段時間輸出一次
        private long _lastMagicCooldownLogTime = 0;
        private int _lastMagicCooldownLogSkillId = 0;
        private const long MAGIC_COOLDOWN_LOG_INTERVAL_MS = 3000;
        // 【修復刷屏】「請先點選目標或按 Z 選怪」同一訊息最小間隔，避免每幀顯示
        private long _lastMagicTargetFailMessageTime = 0;
        private const long MAGIC_TARGET_FAIL_MESSAGE_INTERVAL_MS = 1500;

        // ========================================================================
        //   初始化与绑定 (Setup)
        // ========================================================================
        private void BindSkillSignals()
        {
            if (PacketHandlerRef != null)
            {
                // 绑定 S_SkillAdd (Opcode 30)
                PacketHandlerRef.SkillAdded += OnSkillAdded;
                
                // 绑定 S_SkillDelete (Opcode 31)
                PacketHandlerRef.SkillDeleted += OnSkillDeleted;
                
                // 绑定 S_SkillBuyList (Opcode 78) - 购买列表
                PacketHandlerRef.SkillBuyListReceived += OnSkillBuyListReceived;
            }
        }

        // ========================================================================
        //   网络回调 (Callbacks)
        // ========================================================================

        // 收到 S_SkillAdd (Opcode 30)
        // 服务器数据通常是: 30, type(10/32), [mask bytes...]
        // 这里假设 PacketHandler 已经解析成了 int 数组
        private void OnSkillAdded(int type, int[] masks)
        {
            // 更新本地掩码缓存
            // 你的 S_SkillAdd.java 显示有多种构造函数。
            // Type 10: 基础 10 级魔法
            // Type 32: 混合魔法 (包含精灵魔法等)
            
            for (int i = 0; i < masks.Length && i < _skillMasks.Length; i++)
            {
                // 如果是增量更新 (mode)，服务器可能只发一个非0值，其他为0
                // 这里我们做简单的位或运算 (OR) 来添加技能
                if (masks[i] != 0)
                {
                    _skillMasks[i] |= masks[i];
                }
            }

            GD.Print($"[Skill] Updated Skill Masks. Type: {type}");
            
            // 刷新 UI (如果有技能面板打开)
            RefreshWindows(); 
        }

        // 收到 S_SkillDelete (Opcode 31)
        private void OnSkillDeleted(int[] masks)
        {
            // 这是一个全量覆盖包，或者删除掩码包
            // S_SkillDelete.java 显示它发了 lv1..10, elf1..3 等一堆数据
            // 我们直接覆盖对应的索引
            
            for (int i = 0; i < masks.Length && i < _skillMasks.Length; i++)
            {
                _skillMasks[i] = masks[i];
            }
            
            GD.Print("[Skill] Skill Deleted/Reset.");
            RefreshWindows();
        }

        // 收到 S_SkillBuyList (Opcode 78)
// 打开 Client/Game/GameWorld.Skill.cs 找到对应方法修改

// 收到 S_SkillBuyList (Opcode 78)
private void OnSkillBuyListReceived(int npcId, int[] skillIds) // 【修复】这里改为 int[]
{
    GD.Print($"[Skill] Open Buy List. NPC: {npcId}, Count: {skillIds.Length}");
    
    // 打开技能购买窗口 (暂未实现窗口，先留接口)
    // UIManager.Instance.Open(WindowID.SkillBuy, new WindowContext { ExtraData = skillIds });
}

        // ========================================================================
        //   交互逻辑 (Interaction)
        // ========================================================================

        /// <summary>
        /// 释放魔法：先依本地 skill_list 播放動畫，再送封包等伺服器結算傷害。
        /// 飛行魔法（list.spr Action0=fly）不在此播落點，僅由 Op35 起點→終點播放。
        /// 光箭 Gfx167 魔法正常測試通過，功能正常，不要再修改。
        /// </summary>
        /// <param name="skillId">技能 ID</param>
        /// <param name="targetId">目标 ID (0=自己/无目标)；技能 5/45 傳送時為目標地點 id、技能 9/15/46 為裝備 InvID。</param>
        /// <param name="targetX">目標格 X（僅技能 5/45 傳送時會寫入封包，對齊 C_Magic readH()）</param>
        /// <param name="targetY">目標格 Y（目前封包不寫入，保留供日後擴充）</param>
        /// <summary>PC 魔法 ID 有效範圍（與 C_MagicPacket 一致，對齊 server skill_list 1–50）。精靈魔法 (129+ ) 尚未支援施法封包。</summary>
        public const int MinPcSkillId = 1;
        public const int MaxPcSkillId = 50;

        /// <summary>
        /// 使用魔法，返回是否成功發送
        /// </summary>
        public bool UseMagic(int skillId, int targetId = 0, int targetX = 0, int targetY = 0)
        {
            if (skillId < MinPcSkillId || skillId > MaxPcSkillId)
            {
                _hud?.AddSystemMessage($"魔法 ID {skillId} 不在支援範圍 ({MinPcSkillId}–{MaxPcSkillId})。");
                return false;
            }
            // 1. 检查是否学习了该技能
            if (!HasLearnedSkill(skillId))
            {
                _hud?.AddSystemMessage("你还没有学会这个魔法。");
                return false;
            }

            var entry = SkillListData.Get(skillId);
            string skillType = (entry?.Type ?? "").Trim().ToLowerInvariant();

            // 2. 【統一選怪】依 skill_list.csv 的 type：none/item=強制自己 / buff=人工選中或自己 / attack 及其餘=人工選中或 Z 攻擊目標
            const int SKILL_TELEPORT = 5;
            const int SKILL_MASS_TELEPORT = 69;
            if (skillType == "none" || skillType == "item")
            {
                // 技能 5/69 傳送：不覆寫 targetId/targetX；targetId=書籤 ID、targetX=地圖 ID，由呼叫端傳入
                if (skillId != SKILL_TELEPORT && skillId != SKILL_MASS_TELEPORT)
                {
                    // type "none" 或 "item"：一律對自己施法，不檢查 targetId，不使用傳入或任務/攻擊目標
                    targetId = _myPlayer != null ? _myPlayer.ObjectId : 0;
                }
                // 技能 9 擬似魔法武器（神聖武器）：伺服器 EnchantWeapon.toMagic(int id) 的 id 為「被施法角色之裝備武器 InvID」；
                // 因僅對自己施法，故以「自己當前裝備的武器」之 InvID 傳入；對齊 server getItemInvId(id)。
                if (skillId == 9)
                {
                    int weaponInvId = GetEquippedWeaponObjectId();
                    if (weaponInvId == 0)
                    {
                        _hud?.AddSystemMessage("你沒拿武器。");
                        return false;
                    }
                    targetId = weaponInvId;
                }
                // 技能 15 鎧甲護持：伺服器 BlessedArmor.toMagic(int id) 的 id 為「施法者背包內盔甲之 InvID」；對齊 getItemInvId(id)、getType()==16。
                if (skillId == 15)
                {
                    int armorInvId = GetEquippedArmorObjectId();
                    if (armorInvId == 0)
                    {
                        _hud?.AddSystemMessage("你沒穿盔甲。");
                        return false;
                    }
                    targetId = armorInvId;
                }
                // 技能 46 創造魔法武器：伺服器 CreateMagicalWeapon.toMagic(int id) 的 id 為「施法者背包內武器之 InvID」；對齊 getItemInvId(id)、ItemWeaponInstance。
                if (skillId == 46)
                {
                    int weaponInvId = GetEquippedWeaponObjectId();
                    if (weaponInvId == 0)
                    {
                        _hud?.AddSystemMessage("你沒拿武器。");
                        return false;
                    }
                    targetId = weaponInvId;
                }
            }
            // 技能 5/69：targetId=0、targetX=0 為隨機傳送；配戴傳送戒指後指定書籤時由 UI 傳入 (bookmarkId, mapId)
            if (skillType == "buff")
            {
                if (targetId == 0)
                {
                    var t = GetCurrentTaskTarget() ?? GetCurrentAttackTarget();
                    if (t != null) targetId = t.ObjectId;
                }
                if (targetId == 0 && _myPlayer != null) targetId = _myPlayer.ObjectId;
            }
            else
            {
                // 技能 5/69 傳送：targetId=0、targetX=0 為隨機傳送，不需目標，不進入下方檢查
                if (skillId != SKILL_TELEPORT && skillId != SKILL_MASS_TELEPORT)
                {
                    // attack 及其餘：僅用「攻擊目標」或「任務目標且為有效魔法目標」
                    if (targetId == 0)
                    {
                        var t = GetCurrentAttackTarget();
                        if (t == null) t = GetCurrentTaskTarget();
                        if (t != null && !IsValidMagicTarget(t, true)) t = null;
                        if (t != null) targetId = t.ObjectId;
                    }
                    if (targetId == 0 && skillType == "attack")
                    {
                        var t = GetCurrentTaskTarget();
                        if (t != null && IsValidMagicTarget(t, true)) targetId = t.ObjectId;
                    }
                    if (targetId == 0)
                    {
                        if ((long)Time.GetTicksMsec() - _lastMagicTargetFailMessageTime >= MAGIC_TARGET_FAIL_MESSAGE_INTERVAL_MS)
                        {
                            _lastMagicTargetFailMessageTime = (long)Time.GetTicksMsec();
                            _hud?.AddSystemMessage("請先點選目標或按 Z 選怪");
                        }
                        return false;
                    }
                }
            }

            // 2.1 【魔法目標驗證】排除地面物品/死亡/無血條/己方寵物；僅怪物/NPC/玩家可為魔法目標（技能 9/15/46 傳入裝備 InvID；技能 5/69 傳入書籤 ID，不驗證實體）
            if ((skillId != 9 && skillId != 15 && skillId != 46 && skillId != SKILL_TELEPORT && skillId != SKILL_MASS_TELEPORT) && targetId > 0 && _entities.TryGetValue(targetId, out var resolvedEnt) && !IsValidMagicTarget(resolvedEnt, skillType == "attack"))
            {
                if (skillType == "attack" || (skillType != "none" && skillType != "buff" && skillType != "item"))
                {
                    if ((long)Time.GetTicksMsec() - _lastMagicTargetFailMessageTime >= MAGIC_TARGET_FAIL_MESSAGE_INTERVAL_MS)
                    {
                        _lastMagicTargetFailMessageTime = (long)Time.GetTicksMsec();
                        _hud?.AddSystemMessage("請先點選目標或按 Z 選怪");
                    }
                    return false;
                }
                if (skillType == "buff")
                {
                    targetId = _myPlayer != null ? _myPlayer.ObjectId : 0;
                }
            }

            // 3. 【播放間隔】魔法冷卻：先檢查技能冷卻，再依 action_id 取得施法動作間隔
            if (SkillCooldownManager.IsOnCooldown(skillId))
            {
                return false;
            }
            int magicActionId = SkillDbData.GetActionId(skillId);
            if (magicActionId <= 0) magicActionId = GameEntity.ACT_SPELL_DIR;
            int reuseDelayMs = SkillDbData.GetReuseDelay(skillId);
            long animIntervalMs = 0;
            int effectiveCooldownMs = reuseDelayMs;
            if (_myPlayer != null)
            {
                animIntervalMs = SprDataTable.GetInterval(ActionType.Magic, _myPlayer.GfxId, magicActionId);
                if (animIntervalMs > effectiveCooldownMs)
                    effectiveCooldownMs = (int)animIntervalMs;
            }
            if (effectiveCooldownMs < 0) effectiveCooldownMs = 0;
            MaybeLogMagicCooldown(skillId, magicActionId, reuseDelayMs, animIntervalMs, effectiveCooldownMs);
            if (_myPlayer != null && !EnhancedSpeedManager.CanPerformAction(ActionType.Magic, _myPlayer.GfxId, magicActionId, out _))
            {
                return false;
            }
            
            // 【技能冷卻】記錄技能使用時間（用於 UI 顯示冷卻倒計時）
            SkillCooldownManager.RecordSkillUse(skillId, effectiveCooldownMs);

            int castGfx = entry?.CastGfx ?? 0;
            int aoeRange = entry?.Range ?? 0;
            bool isGroupMagic = aoeRange >= 2;

            // 5. 施法動作
            _myPlayer.SetAction(GameEntity.ACT_SPELL_DIR);

            // 6. 【先播放】依本地 skill_list 取得 cast_gfx，立即播放魔法動畫（飛行魔法除外）
            // 【座標同步修復】使用服務器確認的座標，如果尚未收到服務器確認則使用客戶端座標
            int serverCasterX = (_serverConfirmedPlayerX >= 0) ? _serverConfirmedPlayerX : _myPlayer.MapX;
            int serverCasterY = (_serverConfirmedPlayerY >= 0) ? _serverConfirmedPlayerY : _myPlayer.MapY;
            int clientCasterX = _myPlayer.MapX;
            int clientCasterY = _myPlayer.MapY;
            GD.Print($"[Magic-Diag] UseMagic skillId={skillId} targetId={targetId} serverCaster=({serverCasterX},{serverCasterY}) clientCaster=({clientCasterX},{clientCasterY})");
            
            Vector2 endPos = _myPlayer.GlobalPosition;
            int heading = _myPlayer.Heading;
            GameEntity followTarget = _myPlayer;
            int centerMapX = _myPlayer.MapX;
            int centerMapY = _myPlayer.MapY;

            int casterMapX = serverCasterX;  // 【座標同步修復】使用服務器確認的座標
            int casterMapY = serverCasterY;  // 【座標同步修復】使用服務器確認的座標
            // 【關鍵修復】目標座標：如果找到了目標實體，使用實體的 MapX/MapY；否則使用傳入的參數（用於傳送技能）
            int finalTargetX = targetX;
            int finalTargetY = targetY;
            if (skillId != SKILL_TELEPORT && skillId != SKILL_MASS_TELEPORT && targetId > 0 && _entities.TryGetValue(targetId, out var targetEnt))
            {
                endPos = targetEnt.GlobalPosition;
                followTarget = targetEnt;
                centerMapX = targetEnt.MapX;
                centerMapY = targetEnt.MapY;
                // 【關鍵修復】使用目標實體的當前座標，而不是參數中的座標
                finalTargetX = targetEnt.MapX;
                finalTargetY = targetEnt.MapY;
                heading = GetHeading(casterMapX, casterMapY, finalTargetX, finalTargetY, _myPlayer.Heading);
            }
            if (skillId == SKILL_TELEPORT || skillId == SKILL_MASS_TELEPORT)
            {
                finalTargetX = targetX;
                finalTargetY = targetY;
            }

            // 群體魔法：區分「單向群體」（list.spr Action0 DirFlag=1，如極光雷電 170）與「全方向群體」（如燃燒的火球 171）
            // 單向：僅施法者「同一方向、範圍內」的目標；全方向：主目標周圍 aoeRange 格內全部。
            bool isDirectionalAoe = castGfx > 0 && Client.Utility.ListSprLoader.IsAction0Directional(castGfx);
            List<GameEntity> targetsInRange = null;
            if (isGroupMagic && castGfx > 0)
            {
                targetsInRange = new List<GameEntity>();
                if (isDirectionalAoe)
                {
                    // 單向群體：以施法者為原點，僅加入「與主目標同方向、距離<=aoeRange」的實體（一條路上的怪物）
                    foreach (var kv in _entities)
                    {
                        var e = kv.Value;
                        if (e == null || e == _myPlayer) continue;
                        // 排除地面物品（102.type(9)）和死亡角色（正確的死亡判斷是 _currentRawAction == ACT_DEATH）
                        if (!IsValidMagicTarget(e, true)) continue;
                        int d = GetGridDistance(casterMapX, casterMapY, e.MapX, e.MapY);
                        if (d > aoeRange) continue;
                        int dirToE = GetHeading(casterMapX, casterMapY, e.MapX, e.MapY, heading);
                        if (dirToE != heading) continue;
                        targetsInRange.Add(e);
                    }
                    if (followTarget != null && followTarget != _myPlayer && IsValidMagicTarget(followTarget, true) && !targetsInRange.Contains(followTarget))
                        targetsInRange.Add(followTarget);
                    GD.Print($"[Magic] 單向群體魔法 Skill:{skillId} Range:{aoeRange} 施法者:({casterMapX},{casterMapY}) Heading:{heading} 同向目標數:{targetsInRange.Count}");
                }
                else
                {
                    // 全方向群體：主目標周圍 aoeRange 格內全部
                    foreach (var kv in _entities)
                    {
                        var e = kv.Value;
                        if (e == null || e == _myPlayer) continue;
                        // 排除地面物品（102.type(9)）和死亡角色（正確的死亡判斷是 _currentRawAction == ACT_DEATH）
                        if (!IsValidMagicTarget(e, true)) continue;
                        int d = GetGridDistance(centerMapX, centerMapY, e.MapX, e.MapY);
                        if (d <= aoeRange)
                            targetsInRange.Add(e);
                    }
                    if (followTarget != null && followTarget != _myPlayer && IsValidMagicTarget(followTarget, true) && !targetsInRange.Contains(followTarget))
                        targetsInRange.Add(followTarget);
                    GD.Print($"[Magic] 群體魔法 Skill:{skillId} Range:{aoeRange} 中心:({centerMapX},{centerMapY}) 範圍內目標數:{targetsInRange.Count}");
                }
            }

            // 飛行魔法（list.spr Action0=fly）：客戶端在起點播放飛行段，Tween 到終點，播畢觸發 109.effect 連貫。與 167/171 統一邏輯。
            bool isFlyingMagic = (castGfx > 0 && Client.Utility.ListSprLoader.IsAction0Fly(castGfx));
            if (castGfx > 0 && !isFlyingMagic)
            {
                RecordSelfMagicCast(castGfx);
                if (isGroupMagic && targetsInRange != null && targetsInRange.Count > 0)
                {
                    foreach (var t in targetsInRange)
                    {
                        // 單向群體：所有目標用同一法術方向；全方向群體：依中心到各目標算朝向
                        int th = isDirectionalAoe ? heading : (t != followTarget ? GetHeading(centerMapX, centerMapY, t.MapX, t.MapY, heading) : heading);
                        SpawnEffect(castGfx, t.GlobalPosition, th, t);
                    }
                    GD.Print($"[Magic] 群體魔法 先播放 Skill:{skillId} Gfx:{castGfx} 單向:{isDirectionalAoe} 共{targetsInRange.Count}個");
                }
                else
                {
                    SpawnEffect(castGfx, endPos, heading, followTarget);
                    GD.Print($"[Magic] 先播放 Skill:{skillId} Gfx:{castGfx} Target:{targetId} Pos:{endPos} Heading:{heading}");
                }
            }
            else if (castGfx > 0 && isFlyingMagic)
            {
                RecordSelfMagicCast(castGfx);
                RecordSelfFlyingCast(castGfx);
                Vector2 startPos = _myPlayer.GlobalPosition;
                var effect = new SkillEffect();
                if (_effectLayer == null) InitEffectSystem();
                _effectLayer.AddChild(effect);
                effect.GlobalPosition = startPos;
                
                // 【修復】檢查是否為群體魔法且已手動播連貫段，如果是則不傳入 chainCallback 避免重複播放
                bool hasManualChain = false;
                if (isGroupMagic && targetsInRange != null && targetsInRange.Count > 0)
                {
                    var gfxDef = Client.Utility.ListSprLoader.Get(castGfx);
                    if (gfxDef != null && gfxDef.EffectChain.Count > 0)
                    {
                        int chainGfx = 0;
                        foreach (var kv in gfxDef.EffectChain)
                            if (kv.Value > 0) { chainGfx = kv.Value; break; }
                        if (chainGfx > 0)
                        {
                            hasManualChain = true;
                            foreach (var t in targetsInRange)
                            {
                                int th = isDirectionalAoe ? heading : (t != followTarget ? GetHeading(centerMapX, centerMapY, t.MapX, t.MapY, heading) : heading);
                                SpawnEffect(chainGfx, t.GlobalPosition, th, t);
                            }
                            GD.Print($"[Magic] 飛行群體魔法 每個目標頭上播連貫段 Skill:{skillId} Gfx:{castGfx}->Chain:{chainGfx} 單向:{isDirectionalAoe} 共{targetsInRange.Count}個");
                        }
                    }
                }
                
                // 如果已手動播連貫段，不傳入 chainCallback 避免飛行段播畢後重複觸發
                effect.Init(castGfx, heading, _skinBridge, _audioProvider, followTarget, hasManualChain ? null : OnChainEffectTriggered, false);
                float dist = startPos.DistanceTo(endPos);
                float speed = 600.0f;
                float duration = Mathf.Max(0.1f, dist / speed);
                var tween = CreateTween();
                Vector2 localEnd = _effectLayer.ToLocal(endPos);
                tween.TweenProperty(effect, "position", localEnd, duration);
                GD.Print($"[Magic] 飛行魔法 先播放 Skill:{skillId} Gfx:{castGfx} 起點→終點 Target:{targetId} 群體目標數:{(targetsInRange?.Count ?? 0)} Heading:{heading} HasManualChain:{hasManualChain}");
            }

            // 7. 【魔法時位置更新】在發送魔法包前發送位置更新包，確保服務器知道玩家位置
            // 【修復】如果正在移動，跳過位置更新（移動包已經包含位置信息）
            // 只有在沒有移動且位置差距很大時，才發送位置更新包
            if (_myPlayer != null && !_isAutoWalking)
            {
                long currentTime = (long)Time.GetTicksMsec();
                int clientX = _myPlayer.MapX;
                int clientY = _myPlayer.MapY;
                int playerHeading = _myPlayer.Heading;
                
                // 檢查位置差距
                int serverX = (_serverConfirmedPlayerX >= 0) ? _serverConfirmedPlayerX : clientX;
                int serverY = (_serverConfirmedPlayerY >= 0) ? _serverConfirmedPlayerY : clientY;
                int diffX = Math.Abs(serverX - clientX);
                int diffY = Math.Abs(serverY - clientY);
                int diff = Math.Max(diffX, diffY);
                
                // 【關鍵修復】如果位置差距很大（>2格），強制發送位置更新包，不等待間隔
                // 注意：服務器只接受距離當前位置 <= 1格的位置更新
                // 如果差距 > 1格，服務器會拒絕位置更新，導致召喚怪物出現在錯誤位置
                bool forceUpdate = diff > 2;
                bool canUpdate = (currentTime - _lastMagicPositionUpdateTime >= MIN_MAGIC_POSITION_UPDATE_INTERVAL_MS);
                
                if (forceUpdate || canUpdate)
                {
                    if (forceUpdate)
                    {
                        GD.Print($"[Pos-Update-Force] Sending FORCED position update (BeforeMagic, diff={diff} > 2): Client:({clientX},{clientY}) Server:({serverX},{serverY}) heading={playerHeading}");
                        GD.Print($"[Pos-Update-Warn] WARNING: Position gap is large (diff={diff}), server may reject position update. Summoned monsters may appear at wrong location!");
                    }
                    else
                    {
                        GD.Print($"[Pos-Update] Sending position update (BeforeMagic): Client:({clientX},{clientY}) heading={playerHeading}");
                    }
                    _netSession.Send(C_MoveCharPacket.Make(clientX, clientY, playerHeading));
                    _lastMagicPositionUpdateTime = currentTime;
                }
            }
            else if (_myPlayer != null && _isAutoWalking)
            {
                GD.Print($"[Pos-Update-Skip] Skipping position update (BeforeMagic): player is moving, move packet already contains position");
            }
            
            // 8. 發送封包，等伺服器 Op57 結算傷害（或 Op35 飛行魔法）。
            // 群體魔法（如技能 16 燃燒的火球）：伺服器 Lightning.toMagic(id) 只接受「一個主目標」id，
            // 依該目標位置算範圍內所有目標、傷害後回傳「一個」Op57（多筆 targetId+damage）；故只發一包 C_Magic(skillId, targetId)。
            // 若對每個目標各發一包會觸發多次 toMagic、可能觸發冷卻導致僅第一次有 Op57，且與協議不符。
            // 技能 5/45 傳送時 targetX 會寫入封包（readH）；targetY 不寫入，對齊 C_Magic.java。
            // 【關鍵修復】使用計算出的最終目標座標（finalTargetX, finalTargetY），而不是參數中的座標
            // 這樣可以確保發送的座標是目標實體的當前座標，而不是過時的參數座標
            // 【座標同步診斷】記錄發送魔法封包時的座標信息
            int serverPlayerX = (_serverConfirmedPlayerX >= 0) ? _serverConfirmedPlayerX : _myPlayer.MapX;
            int serverPlayerY = (_serverConfirmedPlayerY >= 0) ? _serverConfirmedPlayerY : _myPlayer.MapY;
            GD.Print($"[Magic-Packet] Sending C_Magic(20) -> SkillId:{skillId} TargetID:{targetId} TargetGrid:({finalTargetX},{finalTargetY}) ServerPlayerGrid:({serverPlayerX},{serverPlayerY}) ClientPlayerGrid:({_myPlayer.MapX},{_myPlayer.MapY})");
            _netSession.Send(C_MagicPacket.Make(skillId, targetId, finalTargetX, finalTargetY));
            
            // 【關鍵修復】在實際發送魔法封包後才更新時間戳，確保與服務器同步
            // 這樣可以避免因其他檢查失敗等原因導致時間戳被提前更新
            EnhancedSpeedManager.RecordActionPerformed(ActionType.Magic);
            
            return true;
        }

	        private void MaybeLogMagicCooldown(int skillId, int actionId, int reuseDelayMs, long animIntervalMs, int effectiveCooldownMs)
	        {
	            long now = (long)Time.GetTicksMsec();
            if (now - _lastMagicCooldownLogTime < MAGIC_COOLDOWN_LOG_INTERVAL_MS && skillId == _lastMagicCooldownLogSkillId)
                return;
            _lastMagicCooldownLogTime = now;
            _lastMagicCooldownLogSkillId = skillId;
            GD.Print($"[Magic-CD] skillId={skillId} actionId={actionId} reuseDelayMs={reuseDelayMs} animIntervalMs={animIntervalMs} effectiveCooldownMs={effectiveCooldownMs}");
        }

        /// <summary>魔法目標是否有效：排除地面物品、死亡、己方寵物(attack 時)。僅「有伺服器血條數據」可為魔法目標；不依 list.spr 判斷。</summary>
        private bool IsValidMagicTarget(GameEntity e, bool forAttack)
        {
            if (e == null) return false;
            if (e == _myPlayer) return !forAttack;
            if (e.IsDead) return false;
            if (forAttack && _mySummonObjectIds != null && _mySummonObjectIds.Contains(e.ObjectId)) return false;
            return e.HasServerHp();
        }

        // --- 辅助：检查技能是否已学习 ---
        /// <summary>是否已學習該技能。僅支援 PC 魔法 1–50（對齊 S_SkillAdd lv1..lv10 每級 5 格）。精靈魔法 129+ 使用不同 mask 索引，尚未實作。</summary>
        public bool HasLearnedSkill(int skillId)
        {
            if (skillId < MinPcSkillId || skillId > MaxPcSkillId)
                return false;
            int levelIdx = (skillId - 1) / 5;
            int bitIdx = (skillId - 1) % 5;
            if (levelIdx >= 0 && levelIdx < _skillMasks.Length)
            {
                int mask = _skillMasks[levelIdx];
                return (mask & (1 << bitIdx)) != 0;
            }
            return false;
        }
        
        // --- 辅助：供 UI 获取数据 ---
        public int[] GetLearnedSkillMasks()
        {
            return _skillMasks;
        }
    }
}
