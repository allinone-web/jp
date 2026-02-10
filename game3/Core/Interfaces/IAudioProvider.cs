// Core/Interfaces/IAudioProvider.cs
using Godot;

namespace Core.Interfaces
{
    public interface IAudioProvider
    {
        AudioStream GetSound(int soundId); // 攻击、受击声
        AudioStream GetBGM(int bgmId);     // 背景音乐
    }
}