// Core/Interfaces/IMapProvider.cs
using Godot;

namespace Core.Interfaces
{
    public interface IMapProvider
    {
        // 加载地图场景
        // mapId: 服务器发来的 ID (如 4)
        // centerPos: 玩家当前坐标 (用于处理偏移)
        // 返回: Node2D (包含了 TileMap 或 Sprite)
        Node2D LoadMap(int mapId, Vector2I centerPos);

        // 获取逻辑数据 (如果你未来要处理碰撞)
        // 如果目前用不到，可以先返回 null 或 true
        bool IsWalkable(int mapId, int x, int y);
        
        // 获取地图的背景音乐 ID (有些地图自带 BGM 配置)
        // int GetMapBgmId(int mapId);
    }
}