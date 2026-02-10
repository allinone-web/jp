// ============================================================================
// [FILE] GameEntity.UI.cs
// 说明：UI 节点创建与布局（名字/血条/BodySprite 等）。只负责“搭建与样式”，不负责移动/战斗逻辑。
// ============================================================================

using Godot;
using Client.Utility;

namespace Client.Game
{
	public partial class GameEntity
	{
		// =====================================================================
		// [SECTION] UI Setup: SetupUI (创建并配置实体 UI 节点)
//		// 说明：为实体搭建名称 Label、血条 ProgressBar、BodySprite 等，并设置默认样式与层级。
//		// =====================================================================

		private void SetupUI()
		{
			// 1. 【優化】名字標籤：直接使用場景節點 "Name"，不創建
			// 場景節點已配置好位置、字體大小、對齊方式等，代碼只負責更新內容和顏色
			_nameLabel = GetNode<Label>("Name");
			// 場景節點已設置 horizontal_alignment = 1 (居中)，不需要代碼設置

			// 2. 聊天氣泡（單一取得/建立；字體由場景 theme_override_font_sizes 控制，不再在此覆寫）
			_chatBubble = GetNodeOrNull<Label>("ChatBubble");
			if (_chatBubble == null)
			{
				_chatBubble = new Label();
				_chatBubble.Name = "ChatBubble";
				_chatBubble.HorizontalAlignment = HorizontalAlignment.Center;
				_chatBubble.Position = new Vector2(-100, -80);
				_chatBubble.Size = new Vector2(200, 30);
				_chatBubble.Modulate = new Color(1, 1, 0);
				_chatBubble.Visible = false;
				_chatBubble.AddThemeFontSizeOverride("font_size", 10);
				AddChild(_chatBubble);
			}


			// 3. 身體色塊（單一取得/建立，預設隱藏）
			_body = GetNodeOrNull<ColorRect>("ColorRect");
			if (_body == null)
			{
				_body = new ColorRect();
				_body.Name = "ColorRect";
				_body.Size = new Vector2(32, 32);
				_body.Position = new Vector2(-16, -16);
				AddChild(_body);
			}
			_body.Visible = false;

			// 4. 血條：僅使用場景節點，不重複建立。0–100 比例、紅條+黑底、高度 1 像素
			_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
			if (_healthBar == null)
			{
				_healthBar = new ProgressBar();
				_healthBar.Name = "HealthBar";
				_healthBar.Position = new Vector2(-15, -51);
				_healthBar.CustomMinimumSize = new Vector2(30, 1);
				_healthBar.Size = new Vector2(30, 1);
				AddChild(_healthBar);
			}
			// 僅設定屬性，不覆寫場景的尺寸/位置
			_healthBar.ShowPercentage = false;
			_healthBar.ZIndex = 10;
			_healthBar.MinValue = 0;
			_healthBar.MaxValue = 100;
			_healthBar.Value = 100;
			_healthBar.Visible = false;

			var styleFill = new StyleBoxFlat();
			styleFill.BgColor = new Color(1, 0, 0);
			_healthBar.AddThemeStyleboxOverride("fill", styleFill);
			var styleBg = new StyleBoxFlat();
			styleBg.BgColor = new Color(0, 0, 0, 0.5f);
			_healthBar.AddThemeStyleboxOverride("background", styleBg);
			


		}
        public void UpdateNameDisplay()
        {
            if (_nameLabel == null) return;

            // [核心修復] 解析服務端發來的 $1234 格式名字
            string displayName = RealName;
            if (displayName.StartsWith("$") && DescTable.Instance != null)
            {
                displayName = DescTable.Instance.ResolveName(displayName);
            }
            
            _nameLabel.Text = displayName;
            UpdateColorDisplay();
        }

        public void UpdateColorDisplay()
        {
            if (_nameLabel == null) return;
            // 【服務器對齊】調用 AlignmentHelper 獲取天堂標準顏色 (綠/藍/白/紅/紫)
            // 對齊服務器 L1PinkName.java 和 S_ObjectLawful.java 的規則
            _nameLabel.Modulate = AlignmentHelper.GetNameColor(Lawful, _isPinkName);
            
            // 【新增】中毒狀態視覺效果（綠色調色）
            if (_isPoison)
            {
                // 中毒時名字稍微變綠（可以疊加）
                _nameLabel.Modulate = _nameLabel.Modulate.Lerp(new Color(0.2f, 1.0f, 0.2f), 0.3f);
            }
        }
		// =====================================================================
		// [SECTION END] UI Setup: SetupUI
		// =====================================================================
	}
}
