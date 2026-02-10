using Godot;
using Client.Game;

namespace Client.UI
{
	public partial class SkillSlot : TextureRect
	{
		public int SkillId { get; private set; }
		public string SkillName { get; private set; }
		public string SkillDescription { get; private set; }
		public bool IsLearned { get; private set; } = false;

		private static CanvasLayer _tooltipLayer;
		private static Control _currentTooltipInstance;
		private bool _isHovering;

		public override void _Ready()
		{
			MouseFilter = MouseFilterEnum.Stop;
			ExpandMode = ExpandModeEnum.IgnoreSize;
			StretchMode = StretchModeEnum.KeepAspectCentered;
			CustomMinimumSize = new Vector2(40, 40);
			MouseEntered += OnMouseEntered;
			MouseExited += OnMouseExited;
		}

		/// <summary>
		/// 魔法圖標路徑：res://Assets/Skills/{skillId}.png，對應 1.png～50.png 共 50 個魔法 ID。
		/// </summary>
		public void Setup(int skillId, string iconName, string name, string description = null)
		{
			SkillId = skillId;
			SkillName = name ?? $"魔法 #{skillId}";
			SkillDescription = description ?? $"技能 ID: {skillId}";

			// 魔法圖標固定依 skillId 對應：/Assets/Skills/1.png ～ 50.png
			string path = $"res://Assets/Skills/{skillId}.png";
			string resolvedPath = path;
			if (!ResourceLoader.Exists(path))
			{
				resolvedPath = "res://Assets/default_item.png";
				if (!ResourceLoader.Exists(resolvedPath))
					resolvedPath = "res://icon.svg";
				GD.Print($"[SkillWindow][Icon] 魔法編號:{skillId} 名稱:{SkillName} 圖標:{skillId}.png 不存在 -> 使用替代:{resolvedPath}");
			}
			Texture = ResourceLoader.Load<Texture2D>(resolvedPath);
			SetLearned(false);
		}

		public void SetLearned(bool learned)
		{
			IsLearned = learned;
			Modulate = learned ? new Color(1, 1, 1, 1) : new Color(0.3f, 0.3f, 0.3f, 1);
		}

		private void OnMouseEntered()
		{
			_isHovering = true;
			CleanupTooltip();
			if (_tooltipLayer == null || !IsInstanceValid(_tooltipLayer))
			{
				_tooltipLayer = new CanvasLayer();
				_tooltipLayer.Layer = 128;
				_tooltipLayer.Name = "SkillTooltipLayer";
				GetTree().Root.AddChild(_tooltipLayer);
			}

			var scene = GD.Load<PackedScene>("res://Client/UI/Scenes/Components/SkillTooltip.tscn");
			if (scene != null)
			{
				var tooltip = scene.Instantiate() as SkillTooltip;
				if (tooltip != null)
				{
					_tooltipLayer.AddChild(tooltip);
					_currentTooltipInstance = tooltip;
					tooltip.Position = GetViewport().GetMousePosition() + new Vector2(15, 15);
					tooltip.Setup(SkillId, SkillName, SkillDescription);
				}
			}
		}

		private void OnMouseExited()
		{
			_isHovering = false;
			CleanupTooltip();
		}

		public override void _Process(double delta)
		{
			if (_isHovering && _currentTooltipInstance != null && IsInstanceValid(_currentTooltipInstance))
				_currentTooltipInstance.Position = GetViewport().GetMousePosition() + new Vector2(15, 15);
		}

		public override void _ExitTree()
		{
			CleanupTooltip();
		}

		private static void CleanupTooltip()
		{
			if (_currentTooltipInstance != null && IsInstanceValid(_currentTooltipInstance))
			{
				_currentTooltipInstance.QueueFree();
				_currentTooltipInstance = null;
			}
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (!IsLearned) return;

			if (@event is InputEventMouseButton mb)
			{
				if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick)
				{
					GD.Print($"[UI] Cast Magic: {SkillName} (ID: {SkillId})");
					var world = GetNodeOrNull<Client.Game.GameWorld>("/root/Boot/World");
					if (world != null) world.UseMagic(SkillId);
				}
			}
		}

		// 【新增拖拽源实现】
		public override Variant _GetDragData(Vector2 atPosition)
		{
			if (!IsLearned) return default;

			var preview = new TextureRect();
			preview.Texture = Texture;
			preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			preview.CustomMinimumSize = new Vector2(40, 40);
			SetDragPreview(preview);

			var dragData = new Godot.Collections.Dictionary();
			dragData["type"] = "skill";
			dragData["id"] = SkillId;
			dragData["icon_path"] = Texture.ResourcePath;

			return dragData;
		}
	}
}
