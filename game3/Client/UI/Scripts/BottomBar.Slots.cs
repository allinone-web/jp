using Godot;

namespace Client.UI
{
    public partial class BottomBar
    {
        private void InitHotkeySlots()
        {
            // 初始化逻辑 (已在 BottomBar.Config.cs 处理加载)
        }

        // 当 HotkeySlot 接收拖拽时调用
        // save = true: 手动拖拽，需要保存
        // save = false: 启动加载，不需要重复保存
        public void OnSlotDropped(int slotIndex, Godot.Collections.Dictionary data, bool save = true)
        {
            // 【修復】檢查 icon_path 是否存在，如果不存在則使用默認路徑
            string iconPath = "";
            if (data.ContainsKey("icon_path"))
            {
                iconPath = data["icon_path"].AsString();
            }
            else
            {
                // 如果沒有 icon_path，使用默認圖標
                iconPath = "res://Assets/default_item.png";
                GD.Print($"[BottomBar] Slot {slotIndex} dropped without icon_path, using default");
            }
            
            var newData = new HotkeyData {
                Type = data["type"].AsString(),
                Id = data["id"].AsInt32(),
                IconPath = iconPath
            };

            if (_hotkeyConfigs.ContainsKey(slotIndex))
                _hotkeyConfigs[slotIndex] = newData;
            else
                _hotkeyConfigs.Add(slotIndex, newData);
                
            GD.Print($"[BottomBar] Slot {slotIndex} bound to {newData.Type} ID: {newData.Id}");
            
            // 【修復】刷新 UI 圖標（使用圖標層，保持背景圖片）
            if (_hotkeyContainer != null)
            {
                var slot = _hotkeyContainer.GetChildOrNull<HotkeySlot>(slotIndex);
                if (slot != null) 
                {
                    var iconLayer = slot.GetNodeOrNull<TextureRect>("IconLayer");
                    if (iconLayer != null)
                    {
                        if (ResourceLoader.Exists(newData.IconPath))
                            iconLayer.Texture = GD.Load<Texture2D>(newData.IconPath);
                        else
                            iconLayer.Texture = null;
                    }
                }
            }

            // 【关键】触发保存
            if (save) SaveHotkeys();
        }
    }
}