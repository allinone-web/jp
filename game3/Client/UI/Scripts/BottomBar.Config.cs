using Godot;
using Client.Data;

namespace Client.UI
{
    public partial class BottomBar
    {
        private void InitConfig()
        {
            LoadHotkeys();
        }

        private void SaveHotkeys()
        {
            // 【修復】保存所有8個欄位，包括空欄位（清空已刪除的欄位）
            for (int i = 0; i < 8; i++)
            {
                if (_hotkeyConfigs.TryGetValue(i, out var data))
                {
                    ClientConfig.SetHotkeySlot(i, data.Type ?? "", data.Id, data.IconPath ?? "");
                }
                else
                {
                    // 【修復】清空空欄位，確保設置文件正確反映當前狀態
                    ClientConfig.SetHotkeySlot(i, "", 0, "");
                }
            }
            ClientConfig.Save();
        }

        private void LoadHotkeys()
        {
            for (int i = 0; i < 8; i++)
            {
                var (type, id, icon) = ClientConfig.GetHotkeySlot(i);
                if (string.IsNullOrEmpty(type)) continue;
                var dict = new Godot.Collections.Dictionary();
                dict["type"] = type;
                dict["id"] = id;
                dict["icon_path"] = icon;
                OnSlotDropped(i, dict, false);
            }
        }

        /// <summary>依當前角色從 ClientConfig 重新載入快捷欄（切換角色後由 Boot/GameWorld 呼叫，確保顯示該角色專屬快捷欄）。</summary>
        public void RefreshHotkeysFromConfig()
        {
            _hotkeyConfigs.Clear();
            if (_hotkeyContainer != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    var slot = _hotkeyContainer.GetChildOrNull<HotkeySlot>(i);
                    if (slot != null)
                    {
                        var iconLayer = slot.GetNodeOrNull<TextureRect>("IconLayer");
                        if (iconLayer != null) iconLayer.Texture = null;
                    }
                }
            }
            for (int i = 0; i < 8; i++)
            {
                var (type, id, icon) = ClientConfig.GetHotkeySlot(i);
                if (string.IsNullOrEmpty(type)) continue;
                var dict = new Godot.Collections.Dictionary();
                dict["type"] = type;
                dict["id"] = id;
                dict["icon_path"] = icon;
                OnSlotDropped(i, dict, false);
            }
        }
    }
}
