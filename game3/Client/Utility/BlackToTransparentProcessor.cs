// ============================================================================
// [FILE] Client/Utility/BlackToTransparentProcessor.cs
// 用途：1999 年代素材中，光暈等周圍的「黑色」在 BMP 時代用不同深度的黑色表示，
//       轉成 PNG 後黑色之外已是純透明，但圖內黑色需轉為透明區域才符合遊戲顯示。
// 注意：黑色並非純黑 (0,0,0)，故用閾值判定「近黑」；並以柔和過渡帶減少殘留黑邊。
// 不改變亮度，僅做黑色→透明。僅對 104.attr(8) 魔法效果圖像套用（由呼叫端判定）。
// ============================================================================

using Godot;

namespace Client.Utility
{
	/// <summary>
	/// 將圖像中「近黑」像素轉為透明，可選柔和過渡帶使邊緣更自然。
	/// </summary>
	public static class BlackToTransparentProcessor
	{
		/// <summary>預設閾值：max(R,G,B) ≤ 此值視為全透明。提高可去除更多殘留黑。</summary>
		public const byte DefaultThreshold = 40;  //60
		/// <summary>柔和過渡帶寬度：threshold ～ threshold+softBand 之間線性插值 Alpha，邊緣更柔和。</summary>
		public const byte DefaultSoftBand = 68;  //68
		/// <summary>整體透明度：0.0 完全透明，1.0 完全不透明。應用於所有像素的最終 Alpha 值。</summary>
		public const float DefaultGlobalAlpha = 1f;

		/// <summary>
		/// 將圖像中近黑像素轉為透明，可選柔和過渡（不調整亮度），並可調整整體透明度。
		/// </summary>
		/// <param name="source">原始 Image，不會被修改</param>
		/// <param name="threshold">max(R,G,B) ≤ 此值則 Alpha=0；更大則依 softBand 線性過渡</param>
		/// <param name="softBand">過渡帶寬度；0 表示硬切，無過渡</param>
		/// <param name="globalAlpha">整體透明度係數：0.0 完全透明，1.0 完全不透明。應用於所有像素的最終 Alpha 值。</param>
		/// <returns>新 Image；近黑處透明，過渡帶柔和，整體透明度由 globalAlpha 控制</returns>
		public static Image Process(Image source, byte threshold = DefaultThreshold, byte softBand = DefaultSoftBand, float globalAlpha = DefaultGlobalAlpha)
		{
			if (source == null) return null;

			int w = source.GetWidth();
			int h = source.GetHeight();
			Image result = Image.Create(w, h, false, Image.Format.Rgba8);

			globalAlpha = Mathf.Clamp(globalAlpha, 0.0f, 1.0f);

			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					Color c = source.GetPixel(x, y);
					byte maxRgb = (byte)Mathf.Max(c.R8, Mathf.Max(c.G8, c.B8));
					if (maxRgb <= threshold)
						c.A = 0;
					else if (softBand > 0 && maxRgb < threshold + softBand)
					{
						float t = (float)(maxRgb - threshold) / softBand;
						c.A = (float)c.A * t;
					}
					c.A = c.A * globalAlpha;
					result.SetPixel(x, y, c);
				}
			}

			return result;
		}

		/// <summary>
		/// 從 Texture2D 取得 Image，做黑色→透明處理後回傳新的 ImageTexture。
		/// </summary>
		/// <param name="texture">原始 Texture2D</param>
		/// <param name="threshold">max(R,G,B) ≤ 此值則 Alpha=0；更大則依 softBand 線性過渡</param>
		/// <param name="softBand">過渡帶寬度；0 表示硬切，無過渡</param>
		/// <param name="globalAlpha">整體透明度係數：0.0 完全透明，1.0 完全不透明。應用於所有像素的最終 Alpha 值。</param>
		public static Texture2D ProcessTexture(Texture2D texture, byte threshold = DefaultThreshold, byte softBand = DefaultSoftBand, float globalAlpha = DefaultGlobalAlpha)
		{
			if (texture == null) return null;

			Image img = texture.GetImage();
			if (img == null) return null;

			Image processed = Process(img, threshold, softBand, globalAlpha);
			if (processed == null) return null;

			return ImageTexture.CreateFromImage(processed);
		}
	}
}
