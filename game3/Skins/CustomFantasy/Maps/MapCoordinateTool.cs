// ==================================================================================
// [FILE] Skins/CustomFantasy/MapCoordinateTool.cs
// [DESCRIPTION] 编辑器辅助工具：用于在 Godot 编辑器中查看地图对应的“服务器坐标”。
// [使用方法]
// 1. 打开地图场景 (.tscn)。
// 2. 将此脚本挂载到根节点 (Node2D) 上，或者新建一个 Node 挂载它。
// 3. 在右侧属性面板输入 CSV 中的 StartX, StartY。
// 4. 点击 "Calculate Now" 复选框。
// ==================================================================================

using Godot;
using System;

namespace Skins.CustomFantasy
{
	[Tool] // ⚠️ 关键：这行代码允许脚本在编辑器模式下运行
	public partial class MapCoordinateTool : Node
	{
		[ExportGroup("CSV Configuration")]
		[Export(PropertyHint.None, "输入 maps.csv 里的 StartX (例如 32256)")] 
		public int ServerOriginX { get; set; } = 32256;

		[Export(PropertyHint.None, "输入 maps.csv 里的 StartY (例如 32768)")] 
		public int ServerOriginY { get; set; } = 32768;

		[ExportGroup("Actions")]
		[Export(PropertyHint.None, "点击此框立即计算坐标")]
		public bool Calculate_Now
		{
			get => false;
			set
			{
				if (value) RunAnalysis();
			}
		}

		private void RunAnalysis()
		{
			// 1. 寻找 TileMapLayer
			TileMapLayer layer = null;
			// 先找有没有叫 lowerland 的
			layer = GetParent().GetNodeOrNull<TileMapLayer>("lowerland");
			
			// 没找到就遍历子节点找第一个
			if (layer == null)
			{
				foreach (var child in GetParent().GetChildren())
				{
					if (child is TileMapLayer tml)
					{
						layer = tml;
						break;
					}
				}
			}

			if (layer == null)
			{
				GD.PrintErr("[MapTool] ❌ No TileMapLayer found nearby!");
				return;
			}

			// 2. 获取本地绘制范围 (例如 -47, -82)
			Rect2I usedRect = layer.GetUsedRect();

			// 3. 换算成服务器坐标
			// 公式：服务器坐标 = 原点 + 本地偏移
			int minServerX = ServerOriginX + usedRect.Position.X;
			int minServerY = ServerOriginY + usedRect.Position.Y;
			
			int maxServerX = minServerX + usedRect.Size.X;
			int maxServerY = minServerY + usedRect.Size.Y;

			// 4. 输出报告
			GD.PrintRich($"[b][color=green]=== Map Coordinate Report ===[/color][/b]");
			GD.PrintRich($"[color=yellow]CSV Origin Set To: ({ServerOriginX}, {ServerOriginY})[/color]");
			GD.PrintRich($"[color=cyan]Godot Drawn Area : {usedRect.Position} (Size: {usedRect.Size})[/color]");
			GD.PrintRich($"-------------------------------------------------------------");
			GD.PrintRich($"[b]ACTUAL SERVER RANGE (Valid Teleport Coordinates):[/b]");
			GD.PrintRich($"   Top-Left  (Min): [color=green]X: {minServerX},  Y: {minServerY}[/color]");
			GD.PrintRich($"   Bot-Right (Max): [color=green]X: {maxServerX},  Y: {maxServerY}[/color]");
			GD.PrintRich($"-------------------------------------------------------------");
			GD.PrintRich($"[Tip] If you teleport outside this range, you see GREY screen.");
		}
	}
}
