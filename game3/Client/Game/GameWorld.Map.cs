// ============================================================================
// [FILE] GameWorld.Map.cs
// [DESCRIPTION] GameWorld 的地图与环境管理分部类 (整合版)。
// [功能] 
// 1. 响应网络 MapID 变更 (OpCode 40)。
// 2. 加载/卸载地图资源 (.tscn)。
// 3. 将背景音乐 (BGM) 播放请求转发给 Boot。
// [修改记录]
// 1. 适配 AssetMapProvider (原 CustomMapProvider)。
// 2. 移除 GameWorld 层面的原点逻辑，因为 Provider 已经处理了物理偏移。

// ============================================================================

using Godot;
using Core.Interfaces;
using Skins.CustomFantasy; // 引用 AssetMapProvider 所在的命名空间

namespace Client.Game
{
    public partial class GameWorld
    {
        // --- 核心依赖 (由 GameWorld.Setup.cs 初始化) ---
        private IMapProvider _mapProvider;

        // --- 运行时状态 ---
        private Node2D _currentMapNode;
        private int _currentMapId = -1;

        // =====================================================================
        // [SECTION] Core Map Logic (响应网络)
        // =====================================================================

        /// <summary>
        /// 网络层通知 MapID 变更时的回调 (OpCode 40)。
        /// </summary>
        /// <param name="mapId">服务器下发的 MapID</param>
        private void OnMapChanged(int mapId)
        {
            // 收到服务器指令，立即执行加载
            LoadWorldMap(mapId);
        }

        /// <summary>
        /// 执行地图资源加载与挂载，并自动切换 BGM。
        /// </summary>
        public void LoadWorldMap(int mapId)
        {
            GD.Print($"[GameWorld] 🔄 Start Loading World Map ID: {mapId}");

            // 1. 卸载旧地图
            if (_currentMapNode != null)
            {
                GD.Print($"[GameWorld] Unloading previous map {_currentMapId}...");
                _currentMapNode.QueueFree();
                _currentMapNode = null;
            }

            // 2. 检查加载器 (使用新的 AssetMapProvider)
            if (_mapProvider == null)
            {
                GD.Print("[GameWorld] Creating new AssetMapProvider instance...");
                _mapProvider = new AssetMapProvider();
            }

            // 3. 加载地图
            // 注意：AssetMapProvider 内部已经将 mapNode 移动到了 mapId 对应的 CSV 世界坐标
            var mapNode = _mapProvider.LoadMap(mapId, Vector2I.Zero);
            if (mapNode == null)
            {
                GD.PrintErr($"[GameWorld] ❌ Failed to load map {mapId} (Result is null)");
                return;
            }

            _currentMapNode = mapNode;
            _currentMapId = mapId;

            // 4. 挂载到 MapLayer，避免 y_sort 時地圖蓋住角色陰影
            // World 有 y_sort_enabled，子節點按 Y 排序；地圖根節點 Y 與角色接近時會蓋住角色。
            // 將地圖掛到 MapLayer（Y=-999999），保證地圖永遠先繪製。
            Vector2 worldPos = mapNode.Position;
            Node2D mapLayer = GetOrCreateMapLayer();
            mapLayer.AddChild(mapNode);
            mapNode.Position = new Vector2(worldPos.X, worldPos.Y + 999999f);

            // =============================================================
            // 【徹底重構】座標系統已統一，不再使用 CurrentMapOrigin
            // =============================================================
            // 所有座標轉換現在使用 CoordinateSystem.GridToPixel() 統一處理
            // 地圖節點已經物理移動到世界座標，實體也使用絕對座標
            // CurrentMapOrigin 保留為 (0,0) 僅為向後兼容
            
            CurrentMapOrigin = Vector2I.Zero; 
            GD.Print($"[GameWorld] ✅ Map {mapId} Mounted. Coordinate system unified (using CoordinateSystem.GridToPixel()).");

            // ---------------------------------------------------------
            // 刷新主角位置 (如果有)
            // ---------------------------------------------------------
            if (_myPlayer != null)
            {
                GD.Print($"[GameWorld] Refreshing Player Position: {_myPlayer.MapX}, {_myPlayer.MapY}");
                // 强制刷新一次位置，确保摄像机瞬间对齐
                _myPlayer.SetMapPosition(_myPlayer.MapX, _myPlayer.MapY, _myPlayer.Heading);
            }

            // 5. 【核心修改】调用托管音乐逻辑
            PlayBGM(mapId);
        }

        /// <summary>
        /// 取得或建立 MapLayer 節點：Y = -999999、y_sort_enabled = false，
        /// 作為 World 的第一個子節點，確保地圖永遠在角色之前繪製（角色陰影不被 lowerland 遮住）。
        /// </summary>
        private Node2D GetOrCreateMapLayer()
        {
            var layer = GetNodeOrNull<Node2D>("MapLayer");
            if (layer != null) return layer;
            layer = new Node2D { Name = "MapLayer", Position = new Vector2(0, -999999f), YSortEnabled = false };
            AddChild(layer);
            MoveChild(layer, 0);
            return layer;
        }

        // =====================================================================
        // [SECTION] Audio/BGM Logic (托管转发)
        // =====================================================================

        /// <summary>
        /// 转发播放背景音乐请求给 Boot 全局单例
        /// </summary>
        private void PlayBGM(int bgmId)
        {
            // 绝不使用本地播放器，直接请求 Boot 切换音乐
            if (Boot.Instance != null)
            {
                GD.Print($"[GameWorld] Requesting Boot to play BGM for Map {bgmId}");
                Boot.Instance.PlayBgm(bgmId);
            }
            else
            {
                GD.PrintErr("[GameWorld] PlayBGM Failed: Boot.Instance is null!");
            }
        }
    }
}