// ============================================================================
// [FILE] Client/Game/GameEntity.Movement.cs
//
// Visuals.cs（渲染執行）
// GameEntity.cs（狀態控制）、
// ListSprLoader.cs（數據解析）和 
// CustomCharacterProvider.cs（資源分發）
//
// 職責分配表 (Architect's Master Plan)
// GameEntity.cs (主文件): 存放核心數據屬性與動作常量。
// GameEntity.Action.cs (動作狀態機): 唯一的 SetAction 實作地。處理姿勢偏移（Weapon Offset）。
// GameEntity.Visuals.cs (渲染執行): 負責 AnimatedSprite2D 的圖層管理與幀同步。
// GameEntity.Movement.cs (位移邏輯): 負責坐標平滑移動，並請求 SetAction。
// GameEntity.CombatFx.cs (戰鬥表現): 負責血條、飄字，並在受擊時請求 SetAction。
//
// [核心修復] 與 main-branch 對齊：使用 Tween 平滑移動，並在開頭 Kill 舊 Tween，
// 否則點擊後角色無法移動（無 Tween 驅動、且可能與 Action/Visual 狀態不同步）。
// ============================================================================

using Godot;
using Client.Utility;

namespace Client.Game
{
    public partial class GameEntity
    {
        private const int GRID_SIZE = 32;
        private const float TELEPORT_THRESHOLD = 256.0f;
        private Tween _moveTween;

        /// <summary>
        /// 【核心修復】計算 walk 動畫的總時長（從 list.spr 讀取所有幀的 RealDuration 之和）
        /// 確保動畫播放時間 = 移動間隔，實現視覺和邏輯同步
        /// </summary>
        private float CalculateWalkDuration()
        {
            // 方法 1：從 list.spr 計算（精確，符合雙重約定）
            var def = Client.Utility.ListSprLoader.Get(GfxId);
            if (def != null)
            {
                var walkSeq = Client.Utility.ListSprLoader.GetActionSequence(def, ACT_WALK);
                if (walkSeq != null && walkSeq.Frames.Count > 0)
                {
                    float totalDuration = 0.0f;
                    foreach (var frame in walkSeq.Frames)
                    {
                        totalDuration += frame.RealDuration; // RealDuration = DurationUnit * 40ms / 1000.0f
                    }
                    if (totalDuration > 0)
                    {
                        return totalDuration;
                    }
                }
            }
            
            // 方法 2：從 SprDataTable 獲取（服務器認可的移動間隔，作為回退）
            // SprDataTable 和 ActionType 都在全局命名空間中
            float interval = SprDataTable.GetInterval(ActionType.Move, GfxId, 0) / 1000.0f;
            return interval > 0 ? interval : 0.6f; // 最終回退值
        }

        public void SetMapPosition(int x, int y, int h = 0)
        {
            if (x < 0 || y < 0) return;

            int oldMapX = MapX;
            int oldMapY = MapY;

            if (_moveTween != null && _moveTween.IsValid())
            {
                _moveTween.Kill();
                _moveTween = null;
            }

            int dx = x - MapX;
            int dy = y - MapY;
            MapX = x;
            MapY = y;
            SetHeading(h);

            // 【徹底重構】使用統一的座標轉換函數，完全移除 CurrentMapOrigin
            // 地圖節點已經物理移動到世界座標，實體也使用絕對座標，不需要相對原點
            Vector2 targetPos = CoordinateSystem.GridToPixel(x, y);
            
            // 【關鍵修復】實體生成時 Position 為 (0,0)，導致距離計算錯誤
            // 如果是第一次設置位置（Position 為 0 或 MapX/MapY 剛被設置），直接設置 Position，不計算距離
            bool isFirstSet = (oldMapX == 0 && oldMapY == 0) || Position == Vector2.Zero;
            float dist = isFirstSet ? 0 : Position.DistanceTo(targetPos);
            
            // 【性能優化】檢查實體是否在視覺範圍內（15格）
            bool shouldPlayAnimation = true;
            var gameWorld = GetTree().CurrentScene as GameWorld;
            if (gameWorld != null && gameWorld._myPlayer != null && ObjectId != gameWorld._myPlayer.ObjectId)
            {
                int distance = gameWorld.GetEntityDistance(x, y);
                if (distance > 15) shouldPlayAnimation = false;
            }
            
            bool isSmoothMove = !isFirstSet && dist > 0 && dist < TELEPORT_THRESHOLD;

            // 【修復】如果距離過大（>=256像素 = 8格）或首次設置，直接設置位置
            if (isFirstSet || dist >= TELEPORT_THRESHOLD)
            {
                Position = targetPos;  // 直接設置位置
                if (!isFirstSet)
                {
                    GD.Print($"[Pos-Teleport] ObjId={ObjectId} teleported from grid ({oldMapX},{oldMapY}) to ({x},{y}) dist={dist:F0}px (threshold={TELEPORT_THRESHOLD})");
                }
                // 【核心修復】傳送後應該播放待機動作（3.breath），而不是 walk
                // 傳送時不受 _isActionBusy 限制，強制設置為待機動作
                // 如果沒有 breath 動作，SetAction(ACT_BREATH) 會根據待機規則自動處理：
                // - 如果有 3.breath 或 Name 含 breath/idle，會播放待機動作
                // - 如果沒有，CustomCharacterProvider 會使用對應 walk 動作的第一幀作為 fallback（見 README §8.4.1）
                if (shouldPlayAnimation)
                {
                    // 強制設置待機動作，不受 _isActionBusy 限制
                    _isActionBusy = false; // 傳送時重置忙碌狀態
                    SetAction(ACT_BREATH); // 使用 SetAction 會自動處理沒有 breath 的情況（fallback 到 walk 第一幀）
                }
                return;
            }

            // 診斷：怪物消失又出現時，可檢查是否收到異常座標導致瞬移（見 README §9.7）
            if (!isSmoothMove && dist > 0)
                GD.Print($"[Pos-Teleport] ObjId={ObjectId} from grid ({oldMapX},{oldMapY}) to ({x},{y}) dist={dist:F0}px");


            // 【服務器對齊】對齊服務器移動邏輯
            // 服務器 S_ObjectMoving 只有在實體真正移動時才發送（L1Object.setMove(true)）
            // 服務器 L1Object.setMove(false) 時不會發送移動包，實體保持待機狀態
            // 客戶端應該只在位置真正改變時設置 walk 動作，位置未改變時設置待機動作
            
            // 【核心修復】檢查位置是否真正改變（對齊服務器邏輯）
            // 服務器只有在 getDistance(x,y,map,1) 時才發送 S_ObjectMoving，即至少移動1格
            bool isActuallyMoving = (oldMapX != x || oldMapY != y);
            
            if (isActuallyMoving && isSmoothMove && shouldPlayAnimation)
            {
                // 【服務器對齊】位置真正改變且平滑移動，設置 walk 動作
                // 對齊服務器：只有當實體真正移動時才播放 walk 動畫
                float moveDuration = CalculateWalkDuration();
                SetAction(ACT_WALK);
                _moveTween = CreateTween();
                _moveTween.TweenProperty(this, "position", targetPos, moveDuration)
                    .SetTrans(Tween.TransitionType.Linear);
            }
            else if (isActuallyMoving && dist > 0 && shouldPlayAnimation)
            {
                // 【服務器對齊】位置真正改變但距離較大（瞬移），設置 walk 動作
                // 對齊服務器：即使瞬移，位置改變了也應該播放 walk 動畫（短暫）
                Position = targetPos;
                SetAction(ACT_WALK);
            }
            else
            {
                // 【服務器對齊】位置未改變，設置待機動作（對齊服務器 L1Object.setMove(false)）
                // 服務器在實體停止移動時會設置 setMove(false)，客戶端應該設置待機動作
                Position = targetPos;
                // 【核心修復】不受 _isActionBusy 限制，確保待機動作能正確設置
                // 如果實體正在攻擊，待機動作會被 SetAction 的保護邏輯阻止，這是正確的
                if (shouldPlayAnimation) SetAction(ACT_BREATH);
            }
        }

        public void OnDamageStutter()
        {
            if (_moveTween != null && _moveTween.IsValid())
            {
                _moveTween.Kill();
                _moveTween = null;
            }
        }
    }
}
