// ============================================================================
// [FILE] GameEntity.Audio.cs
// [作用] 负责实体动作音效的触发与播放。
// [逻辑] 监听 _mainSprite.FrameChanged 信号 -> 查 SprFrame -> 播 SoundIds
// ============================================================================

using Godot;
using Client.Utility;
using Core.Interfaces;
using System.Collections.Generic;

namespace Client.Game
{
    public partial class GameEntity
    {
        // 音效播放器 (2D 空间音效)
        private AudioStreamPlayer2D _soundPlayer;
        
        // 音频资源接口 (从 GameWorld 注入)
        private IAudioProvider _audioProvider;

        
        // =====================================================================
        // [SECTION] Init & Setup (初始化)
        // =====================================================================
        
        public void SetupAudio(IAudioProvider audioProvider)
        {
            _audioProvider = audioProvider;

            if (_soundPlayer == null)
            {
                _soundPlayer = new AudioStreamPlayer2D();
                _soundPlayer.Name = "SoundPlayer";
                _soundPlayer.Bus = "SFX"; // 建议在 Godot 项目设置里建一个 SFX 总线
                _soundPlayer.MaxDistance = 800; // 声音衰减距离
                AddChild(_soundPlayer);
            }

            // 確保信號掛載成功
            if (_mainSprite != null && !_mainSprite.IsConnected(AnimatedSprite2D.SignalName.FrameChanged, Callable.From(OnFrameChanged)))
            {
                _mainSprite.FrameChanged += OnFrameChanged;
            }
        }

        // =====================================================================
        // [SECTION] Frame Event (帧事件回调)
        // =====================================================================

        private void OnFrameChanged()
        {
            if (_mainSprite == null || _mainSprite.SpriteFrames == null || _audioProvider == null) return;


            // 1. 获取当前动画名称 (通常是 "0"~"7") 和 帧索引
            int frameIdx = _mainSprite.Frame;

            // 2. 获取 SPR 定义 (如果缓存无效则重新查找)
            // 注意：这里需要根据 GfxId 和 CurrentAction 找到对应的 list.spr 定义
            var def = ListSprLoader.Get(GfxId);
            if (def == null) return;

            // 【不允許修改】動畫幀播放規則：第一幀必須是 FrameIdx=1 的那一幀；與 BuildLayer 一致。音效僅依 SprFrame.SoundIds（< 與 [ 皆為音效）。
            var seq = ListSprLoader.GetActionSequence(def, CurrentAction);
            if (seq != null)
            {
                var sortedFrames = new List<SprFrame>(seq.Frames);
                sortedFrames.Sort((a, b) => a.FrameIdx.CompareTo(b.FrameIdx));
                int startIdx = -1;
                for (int i = 0; i < sortedFrames.Count; i++)
                {
                    if (sortedFrames[i].FrameIdx == SprPlaybackRule.MinPlaybackFrameIdx) { startIdx = i; break; }
                }
                if (startIdx < 0) return;
                var order = new List<SprFrame>();
                for (int k = startIdx; k < sortedFrames.Count; k++) order.Add(sortedFrames[k]);
                for (int k = 0; k < startIdx; k++) order.Add(sortedFrames[k]);
                int playCount = order.Count;
                if (frameIdx >= 0 && frameIdx < playCount)
                {
                    var sprFrame = order[frameIdx];
                    if (sprFrame.SoundIds != null && sprFrame.SoundIds.Count > 0)
                    {
                        foreach (var sid in sprFrame.SoundIds) PlaySound(sid);
                    }
                }
            }
        }

        // =====================================================================
        // [SECTION] Play Logic (播放逻辑)
        // =====================================================================

        private void PlaySound(int soundId)
        {
            if (soundId <= 0) return;
            

            var stream = _audioProvider.GetSound(soundId);
            if (stream != null)
            {
                // 【修復】支持同時播放多個音效：每個音效都創建臨時播放器，避免相互影響
                // 這樣可以確保同一時間多個角色可以同時播放音效而不相互影響
                var tempPlayer = new AudioStreamPlayer2D();
                tempPlayer.Stream = stream;
                tempPlayer.GlobalPosition = GlobalPosition;
                tempPlayer.MaxDistance = 800f;
                tempPlayer.Bus = "SFX";
                GetTree().Root.AddChild(tempPlayer);
                tempPlayer.Play();
                tempPlayer.Finished += tempPlayer.QueueFree;
                
                // GD.Print($"[Audio] Playing Sound: {soundId} for Actor {RealName}");
            }
        }
    }
}