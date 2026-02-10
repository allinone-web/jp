// ==================================================================================
// [FILE] Skins/CustomFantasy/CustomAudioProvider.cs
// [NAMESPACE] Skins.CustomFantasy
// [DESCRIPTION] 移植自 SoundManager.cs 的资源加载逻辑
// ==================================================================================

using Godot;
using Core.Interfaces;

namespace Skins.CustomFantasy
{
    public class CustomAudioProvider : IAudioProvider
    {
        // 與實際資料夾一致：Assets/CustomFantasy/Sound/，副檔名 .WAV
        private const string SOUND_PATH = "res://Assets/CustomFantasy/Sound/{0}.wav";
        
        // 【核心修改】背景音乐路径规则：增加 'music' 前缀
        // 例如 MapId=38 -> music38.mp3
        private const string BGM_PATH_MP3 = "res://Assets/CustomFantasy/Sound/music{0}.mp3";
        private const string BGM_PATH_OGG = "res://Assets/CustomFantasy/Sound/music{0}.ogg";
        
        // 【默认音乐】找不到特定地图音乐时播放它
        private const string BGM_FALLBACK = "res://Assets/CustomFantasy/Sound/music0.mp3"; 

        public AudioStream GetSound(int soundId)
        {
            if (soundId <= 0) return null;

            string path = string.Format(SOUND_PATH, soundId);
            if (ResourceLoader.Exists(path))
                return ResourceLoader.Load<AudioStream>(path);
            // 區分大小寫的檔案系統：實際檔名可能為 .WAV，補試一次
            if (path.Length > 4 && path.EndsWith(".wav"))
            {
                string pathWav = path.Substring(0, path.Length - 4) + ".WAV";
                if (ResourceLoader.Exists(pathWav))
                    return ResourceLoader.Load<AudioStream>(pathWav);
            }
            GD.PrintErr($"[Audio] GetSound({soundId}) not found: {path}");
            return null;
        }

        public AudioStream GetBGM(int bgmId)
        {
            // 1. 尝试加载 MP3 (例如 music38.mp3)
            string path = string.Format(BGM_PATH_MP3, bgmId);
            if (ResourceLoader.Exists(path)) return ResourceLoader.Load<AudioStream>(path);

            // 2. 尝试加载 OGG (例如 music38.ogg)
            path = string.Format(BGM_PATH_OGG, bgmId);
            if (ResourceLoader.Exists(path)) return ResourceLoader.Load<AudioStream>(path);

            // 3. 【核心修改】尝试回退默认音乐 (music0.mp3)
            // 如果前面的都找不到，就播放这个默认的
            if (ResourceLoader.Exists(BGM_FALLBACK))
            {
                // GD.Print($"[Audio] BGM {bgmId} missing. Fallback to music0.");
                return ResourceLoader.Load<AudioStream>(BGM_FALLBACK);
            }

            // 如果连默认音乐都没有，那就真的没办法了，静音
            return null;
        }
    }
}