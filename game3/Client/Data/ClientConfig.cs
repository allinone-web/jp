using Godot;
using System.Collections.Generic;
using System.IO;

namespace Client.Data
{
    public static class ClientConfig
    {
        public static string Language = "cn";
        public static bool MusicEnabled = true;
        public static bool SoundEnabled = true;
        public static float MusicVolumeDb = 0f;
        public static float SFXVolumeDb = 0f;
        public static bool ShowMonsterHealthBar = true;
        public static bool DayNightOverlayEnabled = false; // 【修復】默認關閉
        /// <summary>法師 Z 鍵魔法攻擊開關：開啟時 Z 鍵使用魔法（默認 skill=4 光箭，可被快捷鍵 1-8 替換）；關閉時 Z 鍵使用普通攻擊（支持近戰、弓箭，但不支持魔法）。</summary>
        public static bool MageZMagicAttackEnabled = true;

        /// <summary>當前角色名，進入世界時由 Boot.CurrentCharName 設定；存檔依此寫入 Setting 目錄下的獨立文件。</summary>
        public static string CurrentCharacterName = "";

        /// <summary>快捷欄 8 格：slot index → (Type, Id, IconPath)，與 BottomBar 一致，依角色分節存讀。</summary>
        public static Dictionary<int, (string Type, int Id, string IconPath)> HotkeySlots = new Dictionary<int, (string, int, string)>();

        /// <summary>設定檔目錄：user://Setting/ 為每個角色ID的配置文件目錄。</summary>
        private static string SettingsDir => Path.Combine(OS.GetUserDataDir(), "Setting");
        
        /// <summary>取得當前角色的配置文件路徑（基於角色ID/名稱）。</summary>
        private static string GetCharacterConfigPath(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return Path.Combine(SettingsDir, "global.cfg");
            // 清理文件名中的非法字符
            string safeCharId = charId.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            return Path.Combine(SettingsDir, $"{safeCharId}.cfg");
        }

        /// <summary>取得設定檔所在目錄的完整路徑（用於除錯或說明）。</summary>
        public static string GetSettingsDir() => SettingsDir;

        /// <summary>Boot 啟動時呼叫：從 global.cfg 載入全局設定。</summary>
        public static void Load()
        {
            // 確保 Setting 目錄存在
            if (!DirAccess.DirExistsAbsolute(SettingsDir))
            {
                DirAccess.MakeDirRecursiveAbsolute(SettingsDir);
            }

            // 從新格式 global.cfg 載入
            string globalPath = GetCharacterConfigPath("");
            var cfg = new ConfigFile();
            if (cfg.Load(globalPath) == Error.Ok)
            {
                LoadFromConfig(cfg, "settings");
            }
        }

        /// <summary>進入世界後呼叫：依角色ID載入該角色專屬設定（語言、音效、黑夜、血條、快捷欄）；若無該文件則用 global 補齊。</summary>
        public static void SwitchCharacter(string charId)
        {
            CurrentCharacterName = charId ?? "";
            
            // 確保 Setting 目錄存在
            if (!DirAccess.DirExistsAbsolute(SettingsDir))
            {
                DirAccess.MakeDirRecursiveAbsolute(SettingsDir);
            }

            string charPath = GetCharacterConfigPath(charId);
            var cfg = new ConfigFile();
            if (cfg.Load(charPath) == Error.Ok)
            {
                LoadFromConfig(cfg, "settings");
            }
            else
            {
                // 若無該角色配置文件，從 global 載入
                string globalPath = GetCharacterConfigPath("");
                var globalCfg = new ConfigFile();
                if (globalCfg.Load(globalPath) == Error.Ok)
                {
                    LoadFromConfig(globalCfg, "settings");
                }
            }
        }

        private static void LoadFromConfig(ConfigFile cfg, string section)
        {
            if (cfg.HasSectionKey(section, "language")) Language = (string)cfg.GetValue(section, "language");
            if (cfg.HasSectionKey(section, "music_enabled")) MusicEnabled = (bool)cfg.GetValue(section, "music_enabled");
            if (cfg.HasSectionKey(section, "sound_enabled")) SoundEnabled = (bool)cfg.GetValue(section, "sound_enabled");
            if (cfg.HasSectionKey(section, "music_volume_db")) MusicVolumeDb = (float)cfg.GetValue(section, "music_volume_db");
            if (cfg.HasSectionKey(section, "sfx_volume_db")) SFXVolumeDb = (float)cfg.GetValue(section, "sfx_volume_db");
            if (cfg.HasSectionKey(section, "show_monster_health_bar")) ShowMonsterHealthBar = (bool)cfg.GetValue(section, "show_monster_health_bar");
            if (cfg.HasSectionKey(section, "day_night_overlay_enabled")) DayNightOverlayEnabled = (bool)cfg.GetValue(section, "day_night_overlay_enabled");
            if (cfg.HasSectionKey(section, "mage_z_magic_attack_enabled")) MageZMagicAttackEnabled = (bool)cfg.GetValue(section, "mage_z_magic_attack_enabled");
            HotkeySlots.Clear();
            for (int i = 0; i < 8; i++)
            {
                string type = (string)cfg.GetValue(section, $"slot_{i}_type", "");
                if (string.IsNullOrEmpty(type)) continue;
                int id = (int)cfg.GetValue(section, $"slot_{i}_id", 0);
                string icon = (string)cfg.GetValue(section, $"slot_{i}_icon", "");
                HotkeySlots[i] = (type, id, icon);
            }
        }

        public static void Save()
        {
            string currentName = Client.Boot.Instance?.CurrentCharName ?? CurrentCharacterName;
            
            // 確保 Setting 目錄存在
            if (!DirAccess.DirExistsAbsolute(SettingsDir))
            {
                DirAccess.MakeDirRecursiveAbsolute(SettingsDir);
            }

            string configPath = GetCharacterConfigPath(currentName);
            GD.Print($"[ClientConfig] Saving to: {configPath}, Character: {currentName}");
            
            var cfg = new ConfigFile();
            if (cfg.Load(configPath) == Error.Ok) { }
            cfg.SetValue("settings", "language", Language);
            cfg.SetValue("settings", "music_enabled", MusicEnabled);
            cfg.SetValue("settings", "sound_enabled", SoundEnabled);
            cfg.SetValue("settings", "music_volume_db", MusicVolumeDb);
            cfg.SetValue("settings", "sfx_volume_db", SFXVolumeDb);
            cfg.SetValue("settings", "show_monster_health_bar", ShowMonsterHealthBar);
            cfg.SetValue("settings", "day_night_overlay_enabled", DayNightOverlayEnabled);
            cfg.SetValue("settings", "mage_z_magic_attack_enabled", MageZMagicAttackEnabled);
            
            // 【修復】保存所有8個快捷鍵欄位，包括空欄位
            for (int i = 0; i < 8; i++)
            {
                if (HotkeySlots.TryGetValue(i, out var data))
                {
                    cfg.SetValue("settings", $"slot_{i}_type", data.Type);
                    cfg.SetValue("settings", $"slot_{i}_id", data.Id);
                    cfg.SetValue("settings", $"slot_{i}_icon", data.IconPath);
                }
                else
                {
                    // 清空空欄位
                    cfg.SetValue("settings", $"slot_{i}_type", "");
                    cfg.SetValue("settings", $"slot_{i}_id", 0);
                    cfg.SetValue("settings", $"slot_{i}_icon", "");
                }
            }
            
            Error err = cfg.Save(configPath);
            if (err == Error.Ok)
            {
                GD.Print($"[ClientConfig] ✅ Saved successfully to: {configPath}");
            }
            else
            {
                GD.PrintErr($"[ClientConfig] ❌ Failed to save: {configPath}, Error: {err}");
            }

            // 同步保存全局設定，避免重啟後語言/音量丟失
            SaveGlobalSettings();
        }

        /// <summary>保存全局設定（語言、音樂/音效開關與音量）。</summary>
        private static void SaveGlobalSettings()
        {
            // 確保 Setting 目錄存在
            if (!DirAccess.DirExistsAbsolute(SettingsDir))
            {
                DirAccess.MakeDirRecursiveAbsolute(SettingsDir);
            }

            string globalPath = GetCharacterConfigPath("");
            var cfg = new ConfigFile();
            if (cfg.Load(globalPath) == Error.Ok) { }
            cfg.SetValue("settings", "language", Language);
            cfg.SetValue("settings", "music_enabled", MusicEnabled);
            cfg.SetValue("settings", "sound_enabled", SoundEnabled);
            cfg.SetValue("settings", "music_volume_db", MusicVolumeDb);
            cfg.SetValue("settings", "sfx_volume_db", SFXVolumeDb);

            Error err = cfg.Save(globalPath);
            if (err == Error.Ok)
            {
                GD.Print($"[ClientConfig] ✅ Global settings saved: {globalPath}");
            }
            else
            {
                GD.PrintErr($"[ClientConfig] ❌ Failed to save global settings: {globalPath}, Error: {err}");
            }
        }

        public static (string Type, int Id, string IconPath) GetHotkeySlot(int index)
        {
            return HotkeySlots.TryGetValue(index, out var v) ? v : ("", 0, "");
        }

        public static void SetHotkeySlot(int index, string type, int id, string iconPath)
        {
            if (string.IsNullOrEmpty(type))
                HotkeySlots.Remove(index);
            else
                HotkeySlots[index] = (type, id, iconPath ?? "");
        }
    }
}
