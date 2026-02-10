// ============================================================================
// [FILE] CoordinateSystem.cs
// [DESCRIPTION] 統一座標轉換系統 - 徹底解決座標不一致問題
// 
// 核心原則：
// 1. MapX/MapY（格座標）是權威數據源，來自服務器
// 2. Position（像素座標）永遠從 MapX/MapY 計算，不允許反向計算
// 3. 完全移除 CurrentMapOrigin，使用絕對座標系統
// 4. 所有座標轉換都使用統一的工具函數
// ============================================================================

using Godot;

namespace Client.Game
{
	/// <summary>
	/// 統一座標轉換系統
	/// 核心原則：MapX/MapY 是權威的，Position 永遠從 MapX/MapY 計算
	/// </summary>
	public static class CoordinateSystem
	{
		/// <summary>每格 = 32 像素</summary>
		public const int CELL_SIZE = 32;
		
		/// <summary>格心偏移 = 0.5 格 = 16 像素（用於格心對齊）</summary>
		public const float CELL_CENTER_OFFSET = 0.5f;
		
		/// <summary>
		/// 將格座標轉換為像素座標（唯一轉換函數）
		/// 公式：Position = (MapX + 0.5f) * CELL_SIZE
		/// 說明：+0.5f 用於格心對齊，確保角色顯示在網格中心
		/// </summary>
		/// <param name="mapX">格座標 X</param>
		/// <param name="mapY">格座標 Y</param>
		/// <returns>像素座標 Position</returns>
		public static Vector2 GridToPixel(int mapX, int mapY)
		{
			float pixelX = (mapX + CELL_CENTER_OFFSET) * CELL_SIZE;
			float pixelY = (mapY + CELL_CENTER_OFFSET) * CELL_SIZE;
			return new Vector2(pixelX, pixelY);
		}
		
		/// <summary>
		/// 將像素座標轉換為格座標（用於輸入處理）
		/// 公式：MapX = (int)(pixelX / CELL_SIZE)
		/// 注意：這是近似轉換，用於點擊檢測，不應用於精確計算
		/// </summary>
		/// <param name="pixelX">像素座標 X</param>
		/// <param name="pixelY">像素座標 Y</param>
		/// <returns>格座標 (MapX, MapY)</returns>
		public static Vector2I PixelToGrid(float pixelX, float pixelY)
		{
			int mapX = (int)(pixelX / CELL_SIZE);
			int mapY = (int)(pixelY / CELL_SIZE);
			return new Vector2I(mapX, mapY);
		}
		
		/// <summary>
		/// 驗證座標一致性（用於調試）
		/// 檢查實體的 MapX/MapY 與 Position 是否一致
		/// </summary>
		/// <param name="mapX">格座標 X</param>
		/// <param name="mapY">格座標 Y</param>
		/// <param name="position">像素座標 Position</param>
		/// <param name="tolerance">容差（像素），默認 1 像素</param>
		/// <returns>是否一致</returns>
		public static bool ValidateCoordinate(int mapX, int mapY, Vector2 position, float tolerance = 1.0f)
		{
			Vector2 expectedPos = GridToPixel(mapX, mapY);
			float diff = position.DistanceTo(expectedPos);
			return diff <= tolerance;
		}
	}
}
