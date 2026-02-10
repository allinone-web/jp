// Core/Interfaces/ICharacterProvider.cs
using Godot;

namespace Core.Interfaces
{
    public interface ICharacterProvider
    {
        // 获取身体动画
        // gfxId: 变身ID
        // action: 动作ID (对应 GameEntity.ACT_*)
        // heading: 朝向 (0-7)
        SpriteFrames GetBodyFrames(int gfxId, int action, int heading);

        // [新增] 支持传递 referenceGfxId (主人ID) 的接口
        // referenceGfxId: 当 gfxId 为空时，借用此 ID 的骨架
        // 注意：参数名保持 action，不改动
        SpriteFrames GetBodyFrames(int gfxId, int referenceGfxId, int action, int heading);

        // 获取武器动画 (如果有纸娃娃)
        SpriteFrames GetWeaponFrames(int gfxId, int action, int heading, int weaponType);
        
        // 获取特效动画 (Opcode 55)
        SpriteFrames GetEffectFrames(int effectId);

        // 获取修正偏移量 (Lineage 的素材中心点通常不一致，需要修正)
        Vector2 GetOffset(int gfxId, int action);
    }
}