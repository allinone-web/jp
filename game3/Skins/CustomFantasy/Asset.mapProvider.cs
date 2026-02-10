// ==================================================================================
// [FILE] Skins/CustomFantasy/Asset.mapProvider.cs
// [NAMESPACE] Skins.CustomFantasy
// [DESCRIPTION] 资源层：地图加载提供者 (原 CustomMapProvider)。
// 负责读取 CSV 配置，并将 .tscn 场景放置在正确的“服务器世界绝对坐标”上。
//
// 【核心修复原理】
// 1. 读取 maps.csv 获取地图的 StartX, StartY (Grid坐标)。
// 2. 将加载的 MapNode.Position 设置为 (StartX * 32, StartY * 32)。
// 3. 这样 GameWorld 和 GameEntity 无需处理复杂的偏移，只需使用标准世界坐标即可。
// 1. 修复 CS0103 错误：将 playerPos 统一更名为 centerPos (匹配接口定义)。
// 2. 包含坐标距离检测逻辑，用于诊断“地图灰屏”问题。
// 1. 变量名修正：playerPos -> centerPos (解决 CS0103 错误)。
// 2. 包含 CSV 读取和世界坐标对齐逻辑。
// 1. 保持原有的 CSV 读取与世界坐标对齐逻辑。
// 2. [新增] AnalyzeMapContent: 自动侦测 .tscn 地图的实际绘制尺寸 (Size) 和 局部偏移 (Position)。
//    这样你就能知道自己画的地图到底有多少格，以及是否画在了负坐标区域。

// ==================================================================================

using Godot;
using System;
using System.Collections.Generic;
using Core.Interfaces;

namespace Skins.CustomFantasy
{
	/// <summary>
	/// 资产地图提供者
	/// </summary>
	public class AssetMapProvider : IMapProvider
	{
		// 本地地图资源路径模板
		private const string MAP_PATH_TEMPLATE = "res://Skins/CustomFantasy/Maps/Map_{0}.tscn";
		
		// 服务器地图配置文件路径 (CSV)
		private const string CSV_CONFIG_PATH = "res://Assets/Data/maps.csv";

		// Lineage 标准网格大小 (像素)
		private const int CELL_SIZE = 32;

		// 地图原点缓存
		// Key   = MapId
		// Value = 服务器定义的“地图左上角世界坐标”(Grid)
		private Dictionary<int, Vector2I> _mapOrigins = new Dictionary<int, Vector2I>();

		// 构造函数：在实例化时加载 CSV 配置
		public AssetMapProvider()
		{
			GD.Print("[AssetMapProvider] Initializing provider...");
			LoadMapConfig();
		}

		/// <summary>
		/// 解析 maps.csv 构建坐标索引
		/// </summary>
		private void LoadMapConfig()
		{
			if (!FileAccess.FileExists(CSV_CONFIG_PATH))
			{
				GD.PrintErr($"[AssetMapProvider] ❌ Critical: Map config not found at {CSV_CONFIG_PATH}");
				return;
			}

			try 
			{
				using var file = FileAccess.Open(CSV_CONFIG_PATH, FileAccess.ModeFlags.Read);
				string content = file.GetAsText();
				var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

				int loadedCount = 0;

				// 跳过 header
				for (int i = 1; i < lines.Length; i++)
				{
					string line = lines[i].Trim();
					if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
						continue;

					string[] parts = line.Split(',');
					
					// maps.csv 结构: #编号(0), X起(1), X终(2), Y起(3), Y终(4), 宽(5)
					if (parts.Length >= 4 &&
						int.TryParse(parts[0], out int mapId) &&
						int.TryParse(parts[1], out int startX) &&
						int.TryParse(parts[3], out int startY))
					{
						_mapOrigins[mapId] = new Vector2I(startX, startY);
						loadedCount++;
					}
				}

				GD.Print($"[AssetMapProvider] ✅ Loaded {loadedCount} map configs from CSV. Ready to offset maps.");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[AssetMapProvider] ❌ Exception reading CSV: {ex.Message}");
			}
		}

		/// <summary>
		/// 加载地图场景，并将其物理移动到世界绝对坐标
		/// </summary>
		/// <param name="mapId">地图编号</param>
		/// <param name="centerPos">玩家/摄像机当前的中心坐标（用于调试距离）</param>
		public Node2D LoadMap(int mapId, Vector2I centerPos)
		{
			string path = string.Format(MAP_PATH_TEMPLATE, mapId);
			
			// GD.Print($"[AssetMapProvider] Request LoadMap: {mapId}. Path: {path}");

			if (!ResourceLoader.Exists(path))
			{
				GD.PrintErr($"[AssetMapProvider] ❌ Map scene not found: {path}");
				return null;
			}

			try
			{
				var scene = ResourceLoader.Load<PackedScene>(path);
				if (scene == null)
				{
					GD.PrintErr($"[AssetMapProvider] ❌ Failed to load map scene (null): {path}");
					return null;
				}
				var mapNode = scene.Instantiate<Node2D>();

				// =============================================================
				// 【关键修复】应用世界坐标偏移
				// =============================================================
				// 1. 获取该地图在服务器上的起始位置 (Grid)
				Vector2I originGrid = GetMapOrigin(mapId);
				
				// 2. 转换为像素坐标 (Grid * 32)
				Vector2 worldPos = new Vector2(originGrid.X * CELL_SIZE, originGrid.Y * CELL_SIZE);

				// 3. 强制设置地图节点的位置
				mapNode.Position = worldPos;
				
				// 确保地图可见；ZIndex 保留場景設定（如 Map_0 的 -100），不覆寫為 0，避免與 GameWorld MapLayer 邏輯衝突
				mapNode.Modulate = Colors.White;

				// =========================================================
				// 【调试诊断】检查玩家是否在地图范围内 (修复灰屏的关键)
				// =========================================================
				AnalyzeMapContent(mapId, mapNode);

				// =========================================================
				// 2. 调试诊断：检查玩家与地图原点的距离
				// =========================================================
				if (centerPos != Vector2I.Zero)
				{
					Vector2I delta = centerPos - originGrid;
					
					GD.PrintRich($"[b][color=yellow][Map Debug] MapId:{mapId} Loaded[/color][/b]");
					GD.Print($"   > Map CSV Origin : {originGrid}");
					GD.Print($"   > Player Center  : {centerPos}");
					GD.Print($"   > Distance Delta : {delta} (Grids)");

					// 警告阈值：如果玩家距离原点超过 400 格 (12800像素)，可能就跑出地图了
					if (Math.Abs(delta.X) > 400 || Math.Abs(delta.Y) > 400)
					{
						GD.PrintRich($"[b][color=red]⚠️ [CRITICAL WARNING] Player is FAR from map origin![/color][/b]");
						GD.PrintRich($"[color=red]   The player is {delta} grids away from where the map starts.[/color]");
						GD.PrintRich($"[color=red]   Ensure your 'maps.csv' StartX/StartY aligns with your Player DB Loc.[/color]");
					}
					else
					{
						GD.PrintRich($"[color=green]   ✅ Player is within reasonable distance from map origin.[/color]");
					}
				}
				else
				{
					// 只有刚登录还没有玩家坐标时才会走到这里
					GD.Print($"[AssetMapProvider] Map loaded at {originGrid}. Player pos unknown.");
				}

				return mapNode;
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[AssetMapProvider] ❌ Failed to instantiate map {mapId}: {ex.Message}");
				return null;
			}
		}

		// =====================================================================
		// 【新增私有方法】分析地图内容尺寸
		// =====================================================================
		private void AnalyzeMapContent(int mapId, Node2D mapNode)
		{
			// 1. 尝试获取 Godot 4.3 的 TileMapLayer (推荐)
			// 这里假设你的地图层名字包含 "land" 或者就是根节点的第一个 Layer
			TileMapLayer targetLayer = null;

			// 策略A: 尝试直接获取名为 "lowerland" 或 "Layer1" 的节点
			targetLayer = mapNode.GetNodeOrNull<TileMapLayer>("lowerland");
			
			// 策略B: 如果没名字，遍历查找第一个 TileMapLayer
			if (targetLayer == null)
			{
				foreach (var child in mapNode.GetChildren())
				{
					if (child is TileMapLayer layer)
					{
						targetLayer = layer;
						break;
					}
					// 兼容旧版 TileMap 节点 (Godot 4.0-4.2)
					else if (child is TileMap tileMap)
					{
						GD.PrintRich($"[Map Analysis] ⚠️ Found 'TileMap' node. Godot 4.3 recommends 'TileMapLayer'.");
						Rect2I used = tileMap.GetUsedRect();
						PrintMapRect(mapId, used);
						return;
					}
				}
			}

			// 输出分析结果
			if (targetLayer != null)
			{
				Rect2I usedRect = targetLayer.GetUsedRect();
				PrintMapRect(mapId, usedRect);
			}
			else
			{
				GD.PrintRich($"[color=orange][Map Analysis] ⚠️ Map {mapId} has no TileMapLayer? Cannot detect size.[/color]");
			}
		}

		private void PrintMapRect(int mapId, Rect2I rect)
		{
			GD.PrintRich($"[b][color=cyan]>>> [Map {mapId} Analysis] Actual Content Size:[/color][/b]");
			GD.PrintRich($"   -> [b]Size:[/b] {rect.Size.X} x {rect.Size.Y} (Grids)");
			GD.PrintRich($"   -> [b]Drawn At:[/b] {rect.Position} (Local Offset)");
			
			if (rect.Position.X < 0 || rect.Position.Y < 0)
			{
				GD.PrintRich($"   -> [color=orange]⚠️ Warning: You drew tiles at NEGATIVE coordinates ({rect.Position}).[/color]"); 
				GD.PrintRich($"      This means part of the map is BEFORE the anchor point.");
			}
		}

		// =====================================================================
		// [辅助接口]
		// =====================================================================
		public Vector2I GetMapOrigin(int mapId)
		{
			if (_mapOrigins.TryGetValue(mapId, out var origin))
			{
				return origin;
			}
			
			GD.PushWarning($"[AssetMapProvider] ⚠️ MapId {mapId} not found in CSV! Defaulting to (0,0).");
			return Vector2I.Zero;
		}

		// 接口实现占位
		public bool IsWalkable(int mapId, int x, int y) => true;
		public int GetMapBgmId(int mapId) => 0; 
	}
}
