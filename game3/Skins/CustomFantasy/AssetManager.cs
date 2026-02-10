using Godot;
using System;
using System.Collections.Generic;
using Client.Utility;

namespace Skins.CustomFantasy
{
	/*
	 * ==============================================================================
	 * 【类：AssetManager (重写移植版)】
	 * ------------------------------------------------------------------------------
	 * 圖片來源：res://Assets/Img182.pak（單一 .pak，加密索引）或舊 .idx+.pak，經 PakArchiveReader + ImgDecoder 解碼。
	 * 1. SprResult 緩存機制 (_sprResultCache)。
	 * 2. 智能後綴補全 (.img)。
	 * 3. 專用於 UI 與登入流程 (Login/CharacterSelect/CharacterCreate)。
	 * ==============================================================================
	 */
	public partial class AssetManager : Node
	{
		private static AssetManager _instance;
		public static AssetManager Instance
		{
			get
			{
				if (_instance == null)
				{
					var mainLoop = Godot.Engine.GetMainLoop();
					if (mainLoop is SceneTree tree)
					{
						var existing = tree.Root.GetNodeOrNull<AssetManager>("AssetManager");
						if (existing != null) _instance = existing;
						else
						{
							_instance = new AssetManager();
							_instance.Name = "AssetManager";
							tree.Root.CallDeferred("add_child", _instance);
						}
					}
				}
				return _instance;
			}
		}

		private const string MaterialPakRoot = "res://Assets/";
		private const string MaterialPakName = "Img182";

		private Dictionary<string, SprResult> _sprResultCache = new Dictionary<string, SprResult>();
		private PakArchiveReader _materialPak;

		public override void _Ready()
		{
			_instance = this;
			EnsureMaterialPakLoaded();
		}

		/// <summary>
		/// 載入素材 pak（Img182.pak 單一檔，或舊 Img182.idx+.pak）。_Ready 與懶加載皆會呼叫。
		/// </summary>
		private void EnsureMaterialPakLoaded()
		{
			if (_materialPak != null && _materialPak.IsLoaded) return;
			GD.Print("[AssetManager] >>> 初始化 (素材 pak: " + MaterialPakName + ")...");
			try
			{
				_materialPak = new PakArchiveReader();
				_materialPak.Load(MaterialPakRoot, MaterialPakName);
				_sprResultCache.Clear();
				if (_materialPak.IsLoaded)
					GD.Print("[AssetManager] ✅ 素材 pak 載入成功，共 " + _materialPak.GetAllFilenames().Count + " 條");
				else
					GD.PrintErr("[AssetManager] ❌ 素材 pak 載入失敗 (IsLoaded=false)");
			}
			catch (Exception e)
			{
				GD.PrintErr($"[AssetManager] 💥 初始化崩潰: {e.Message}");
			}
		}

		/// <summary>
		/// 內部通用載入器：緩存、後綴、透明度參數
		/// </summary>
		private SprResult LoadSprResult(string fileName, bool keepBlack)
		{
			if (string.IsNullOrEmpty(fileName)) return null;
			EnsureMaterialPakLoaded();
			if (_materialPak == null || !_materialPak.IsLoaded) return null;

			string lowerName = fileName.ToLowerInvariant();

			if (!lowerName.EndsWith(".img") && !lowerName.EndsWith(".spr"))
				lowerName += ".img";

			string cacheKey = $"{lowerName}_{keepBlack}";

			if (_sprResultCache.TryGetValue(cacheKey, out var cached)) return cached;

			byte[] rawData = _materialPak.GetFile(lowerName);
			if (rawData == null || rawData.Length < 4) return null;

			var maskMode = keepBlack ? ImgDecoder.MaskMode.None : ImgDecoder.MaskMode.Black;
			Image img = ImgDecoder.Decode(rawData, ImgDecoder.ColorFormat.ARGB1555, maskMode);
			if (img == null) return null;

			var sprRes = new SprResult();
			sprRes.Frames = new List<Image> { img };
			sprRes.Width = img.GetWidth();
			sprRes.Height = img.GetHeight();
			sprRes.FileType = "IMG";

			_sprResultCache[cacheKey] = sprRes;
			return sprRes;
		}

		// ========================================================================
		// [API 1] 获取 UI 图片 (修复按钮不显示)
		// ========================================================================
		
		// [修改] 默认 keepBlack = true (不透明)。
		// 这将解决 Login 按钮不显示的问题（之前因为默认透明导致按钮消失）。
		// 同时也满足了“所有图片黑色都不需要变透明”的要求。
		public Texture2D GetUITexture(string name, bool keepBlack = true)
		{
			try
			{
				SprResult result = LoadSprResult(name, keepBlack);

				if (result != null && result.Frames != null && result.Frames.Count > 0)
				{
					// 创建纹理
					return ImageTexture.CreateFromImage(result.Frames[0]);
				}
				return null;
			}
			catch (Exception e)
			{
				GD.PrintErr($"[AssetManager] GetUITexture 异常 ({name}): {e.Message}");
				return null;
			}
		}

		// ========================================================================
		// [API 2] 创建角色动画 (完全复原旧版区间循环逻辑)
		// ========================================================================
		
		/// <summary>
		/// 复原旧版逻辑：
		/// idleId: 待机图片 (单张)
		/// walkStart: 走路起始ID
		/// walkEnd: 走路结束ID
		/// 将从 walkStart 到 walkEnd 的所有图片加载为 walk 动画
		/// </summary>
		public SpriteFrames CreateCharacterFrames(int idleId, int walkStart, int walkEnd)
		{
			SpriteFrames sf = new SpriteFrames();
			
			// 1. 设置 Idle 动画 (单帧)
			// 不需要透明度特殊处理，默认透明
			sf.AddAnimation("idle");
			sf.SetAnimationLoop("idle", true); 
			
			// [修改] 强制 keepBlack = true (不透明)
			var idleTex = GetUITexture($"{idleId}.img", true);
			if (idleTex != null) 
			{
				sf.AddFrame("idle", idleTex);
			}

			// 2. 设置 Walk 动画 (区间循环)
			sf.AddAnimation("walk");
			sf.SetAnimationLoop("walk", true);
			
			// [修改] 速度调快到 12.0 (原 4.0 太慢)
			sf.SetAnimationSpeed("walk", 12.0f); 

			// [关键复原] 循环加载 start 到 end 的所有图片
			for (int i = walkStart; i <= walkEnd; i++)
			{
				// [修改] 强制 keepBlack = true (不透明)
				var tex = GetUITexture($"{i}.img", true);
				if (tex != null)
				{
					sf.AddFrame("walk", tex);
				}
			}
			
			return sf;
		}
	}
}
