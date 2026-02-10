// Core/Interfaces/ISkinBridge.cs
using Godot;
namespace Core.Interfaces
{
    // 1. 新增图片提供者接口
    public interface IImageProvider
    {
        // 获取 UI 纹理 (对应原来的 GetUITexture)
        Texture2D GetTexture(string name);

        // 获取角色预览序列帧 (对应原来的 CreateCharacterFrames)
        SpriteFrames GetPreviewFrames(int walkStart, int attackStart, int breathStart);
    }

    // 2. 更新皮肤总接口
    public interface ISkinBridge
    {
        IMapProvider Map { get; }
        ICharacterProvider Character { get; }
        IAudioProvider Audio { get; }
        
        // [新增] 图片资源接口
        IImageProvider Image { get; } 
        
        void UnloadAll();
    }
}
