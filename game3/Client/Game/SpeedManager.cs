using System.Collections.Generic;
using Godot;

public enum ActionType
{
    Attack,
    Move,
    Magic
}

/// <summary>
/// 【已過時】請使用 EnhancedSpeedManager 代替
/// 此類保留僅用於向後兼容，內部調用 EnhancedSpeedManager
/// </summary>
[System.Obsolete("Use EnhancedSpeedManager instead. This class is kept for backward compatibility only.")]
public static class SpeedManager
{
    // 【優化】所有方法都委託給 EnhancedSpeedManager，避免重複實現
    public static void SetHaste(bool isActive)
    {
        Client.Game.EnhancedSpeedManager.SetHaste(isActive);
    }
    
    public static void SetBrave(bool isActive)
    {
        Client.Game.EnhancedSpeedManager.SetBrave(isActive);
    }
    
    public static void SetSlow(bool isActive)
    {
        Client.Game.EnhancedSpeedManager.SetSlow(isActive);
    }
    
    // 【向後兼容】保留舊方法，但標記為過時
    [System.Obsolete("Use SetHaste/SetBrave/SetSlow instead")]
    public static void SetBuffState(ActionType buff, bool isActive)
    {
        switch (buff)
        {
            case ActionType.Attack:
                Client.Game.EnhancedSpeedManager.SetBrave(isActive);
                break;
            case ActionType.Move:
                Client.Game.EnhancedSpeedManager.SetHaste(isActive);
                break;
            case ActionType.Magic:
                Client.Game.EnhancedSpeedManager.SetSlow(isActive);
                break;
        }
    }

    /// <summary>
    /// 【已過時】請使用 EnhancedSpeedManager.CanPerformAction() 代替
    /// 注意：此方法會在檢查通過時立即更新時間戳，可能導致時間戳更新時機錯誤
    /// 建議使用 EnhancedSpeedManager.CanPerformAction() + RecordActionPerformed()
    /// </summary>
    [System.Obsolete("Use EnhancedSpeedManager.CanPerformAction() instead. This method updates timestamp immediately, which may cause timing issues.")]
    public static bool CanPerformAction(ActionType type, int gfxId, int actionId = 0)
    {
        // 【優化】委託給 EnhancedSpeedManager，但需要手動更新時間戳以保持向後兼容
        bool canPerform = Client.Game.EnhancedSpeedManager.CanPerformAction(type, gfxId, actionId, out _);
        if (canPerform)
        {
            // 【警告】為了向後兼容，這裡立即更新時間戳，但這可能導致時間戳更新時機錯誤
            // 建議遷移到 EnhancedSpeedManager 並在實際發送封包後調用 RecordActionPerformed()
            Client.Game.EnhancedSpeedManager.RecordActionPerformed(type);
        }
        return canPerform;
    }

    public static void Reset()
    {
        Client.Game.EnhancedSpeedManager.Reset();
    }
}
