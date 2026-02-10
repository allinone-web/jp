using System;
using System.Collections.Generic;
using Godot;

namespace Client.Game
{
    /// <summary>
    /// 【增強版速度管理器】參考現代遊戲最佳實踐設計
    /// 
    /// 核心特性：
    /// 1. 預測性冷卻：提前計算並返回剩餘時間，避免"卡頓"感
    /// 2. 寬鬆時間窗口：允許小範圍誤差（10-20ms），適應幀率波動
    /// 3. 智能重試機制：根據剩餘冷卻時間動態調整重試間隔
    /// 4. 客戶端預測：允許提前一點點執行，服務器做最終驗證
    /// 5. 流暢的視覺反饋：提供冷卻進度信息
    /// </summary>
    public static class EnhancedSpeedManager
    {
        // 時間戳記錄
        private static Dictionary<ActionType, long> _lastActionTimestamps = new Dictionary<ActionType, long>();
        
        // Buff 狀態
        private static bool _isHaste = false;
        private static bool _isBrave = false;
        private static bool _isSlow = false;
        
        // 【現代遊戲特性】寬鬆時間窗口（允許小範圍誤差，避免因幀率波動導致的卡頓）
        private const long TOLERANCE_MS = 15; // 15ms 寬鬆窗口，適應 60fps 的幀率波動
        
        // 【現代遊戲特性】客戶端預測提前量（允許提前一點點執行，服務器會做最終驗證）
        private const long PREDICTION_LEAD_MS = 10; // 10ms 提前量，讓操作更流暢
        
        // 【現代遊戲特性】智能重試間隔（根據剩餘冷卻時間動態調整）
        private const float MIN_RETRY_INTERVAL = 0.016f; // 最小重試間隔（1幀，約16ms）
        private const float MAX_RETRY_INTERVAL = 0.1f;   // 最大重試間隔（100ms）
        
        static EnhancedSpeedManager()
        {
            _lastActionTimestamps[ActionType.Attack] = 0;
            _lastActionTimestamps[ActionType.Move] = 0;
            _lastActionTimestamps[ActionType.Magic] = 0;
        }
        
        public static void SetHaste(bool isActive)
        {
            _isHaste = isActive;
        }
        
        public static void SetBrave(bool isActive)
        {
            _isBrave = isActive;
        }
        
        public static void SetSlow(bool isActive)
        {
            _isSlow = isActive;
        }
        
        /// <summary>
        /// 【增強版】檢查是否可以執行動作，並返回剩餘冷卻時間
        /// </summary>
        /// <param name="type">動作類型</param>
        /// <param name="gfxId">角色GFX ID</param>
        /// <param name="actionId">動作ID</param>
        /// <param name="remainingMs">輸出參數：剩餘冷卻時間（毫秒）</param>
        /// <returns>true 表示可以執行，false 表示仍在冷卻中</returns>
        public static bool CanPerformAction(ActionType type, int gfxId, int actionId, out float remainingMs)
        {
            long currentTime = (long)Time.GetTicksMsec();
            long lastActionTime = _lastActionTimestamps[type];
            
            // 計算基礎冷卻時間
            long baseCooldown = SprDataTable.GetInterval(type, gfxId, actionId);
            long finalCooldown = baseCooldown;
            
            // 應用 Buff 效果（與服務器 CheckSpeed.java 一致）
            if (_isHaste)
            {
                finalCooldown = (long)(finalCooldown * 0.75);
            }
            if (_isBrave && type == ActionType.Attack)
            {
                finalCooldown = (long)(finalCooldown * 0.75);
            }
            if (_isSlow)
            {
                finalCooldown = (long)(finalCooldown / 0.75);
            }
            
            // 計算已過時間
            long elapsed = currentTime - lastActionTime;
            
            // 【JP協議對齊】攻擊必須嚴格對齊服務器（不允許提前）
            // 服務器 AcceleratorChecker 不接受提前觸發，攻擊使用嚴格門檻
            long predictionLead = (type == ActionType.Attack) ? 0 : PREDICTION_LEAD_MS;
            long tolerance = (type == ActionType.Attack) ? 0 : TOLERANCE_MS;
            
            // 【現代遊戲特性】寬鬆時間窗口 + 客戶端預測（非攻擊）
            // 允許提前 predictionLead 執行，並給予 tolerance 的寬鬆窗口
            long effectiveCooldown = finalCooldown - predictionLead;
            long remaining = effectiveCooldown - elapsed;
            
            // 計算剩餘時間（用於視覺反饋和智能重試）
            remainingMs = Math.Max(0, remaining);
            
            // 【關鍵修復】使用寬鬆窗口判斷，避免因幀率波動導致的卡頓
            // 如果剩餘時間在寬鬆窗口內，允許執行（服務器會做最終驗證）
            bool canPerform = elapsed >= (effectiveCooldown - tolerance);
            
            // 【關鍵修復】不在此處更新時間戳，而是在實際發送封包後才更新
            // 這樣可以確保時間戳記錄的時間與實際發送封包的時間一致
            // 避免因距離檢查失敗等原因導致時間戳被提前更新
            if (canPerform)
            {
                remainingMs = 0;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 【向後兼容】保留原有接口，內部調用增強版
        /// </summary>
        public static bool CanPerformAction(ActionType type, int gfxId, int actionId = 0)
        {
            return CanPerformAction(type, gfxId, actionId, out _);
        }
        
        /// <summary>
        /// 【關鍵修復】在實際發送封包後調用此方法更新時間戳
        /// 這樣可以確保時間戳記錄的時間與實際發送封包的時間一致
        /// </summary>
        public static void RecordActionPerformed(ActionType type)
        {
            long currentTime = (long)Time.GetTicksMsec();
            _lastActionTimestamps[type] = currentTime;
        }
        
        /// <summary>
        /// 【現代遊戲特性】獲取智能重試間隔
        /// 根據剩餘冷卻時間動態調整重試間隔，避免每幀都重試
        /// </summary>
        /// <param name="remainingMs">剩餘冷卻時間（毫秒）</param>
        /// <returns>建議的重試間隔（秒）</returns>
        public static float GetSmartRetryInterval(float remainingMs)
        {
            if (remainingMs <= 0)
                return MIN_RETRY_INTERVAL;
            
            // 如果剩餘時間很短（<50ms），每幀檢查（快速響應）
            if (remainingMs < 50)
                return MIN_RETRY_INTERVAL;
            
            // 如果剩餘時間中等（50-200ms），每 2-3 幀檢查一次
            if (remainingMs < 200)
                return 0.033f; // 約 2 幀（33ms）
            
            // 如果剩餘時間較長（>200ms），每 5-6 幀檢查一次（減少性能開銷）
            return Math.Min(MAX_RETRY_INTERVAL, remainingMs / 1000.0f * 0.3f);
        }
        
        /// <summary>
        /// 【現代遊戲特性】獲取冷卻進度（0.0 - 1.0）
        /// 用於 UI 顯示冷卻進度條
        /// </summary>
        /// <param name="type">動作類型</param>
        /// <param name="gfxId">角色GFX ID</param>
        /// <param name="actionId">動作ID</param>
        /// <returns>冷卻進度（0.0 = 剛開始，1.0 = 完成）</returns>
        public static float GetCooldownProgress(ActionType type, int gfxId, int actionId)
        {
            long currentTime = (long)Time.GetTicksMsec();
            long lastActionTime = _lastActionTimestamps[type];
            
            long baseCooldown = SprDataTable.GetInterval(type, gfxId, actionId);
            long finalCooldown = baseCooldown;
            
            // 應用 Buff 效果
            if (_isHaste)
                finalCooldown = (long)(finalCooldown * 0.75);
            if (_isBrave && type == ActionType.Attack)
                finalCooldown = (long)(finalCooldown * 0.75);
            if (_isSlow)
                finalCooldown = (long)(finalCooldown / 0.75);
            
            long elapsed = currentTime - lastActionTime;
            long effectiveCooldown = finalCooldown - PREDICTION_LEAD_MS;
            
            if (elapsed >= effectiveCooldown)
                return 1.0f;
            
            return Math.Clamp((float)elapsed / effectiveCooldown, 0.0f, 1.0f);
        }
        
        public static void Reset()
        {
            _lastActionTimestamps[ActionType.Attack] = 0;
            _lastActionTimestamps[ActionType.Move] = 0;
            _lastActionTimestamps[ActionType.Magic] = 0;
            _isHaste = false;
            _isBrave = false;
            _isSlow = false;
        }
    }
}
