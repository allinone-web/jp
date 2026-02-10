// ==================================================================================
// [FILE] Skins/CustomFantasy/CustomSkinLoader.cs
// [NAMESPACE] Skins.CustomFantasy
// [DESCRIPTION] 皮肤系统桥接器 (PAK版)
// ==================================================================================

using Godot;
using Core.Interfaces;
using Client.Utility;

namespace Skins.CustomFantasy
{
	public class CustomSkinLoader : ISkinBridge
	{
		// ==========================================
		// 属性定义
		// ==========================================
		public IMapProvider Map { get; private set; }
		public ICharacterProvider Character { get; private set; }
		public IAudioProvider Audio { get; private set; }
		public IImageProvider Image { get; private set; }

		// ==========================================
		// 构造函数：初始化所有子系统
		// ==========================================
		public CustomSkinLoader()
		{
			GD.Print("[CustomFantasy] 开始初始化皮肤系统 (PAK Mode)...");

			// 1. 地图系统
			Map = new AssetMapProvider(); 
			
			// 2. 角色系统 (核心)
			// 实例化时会自动加载 res://Assets/sprites.pak
			Character = new CustomCharacterProvider(); 

			// 3. 音频
			Audio = new CustomAudioProvider();
			
			// 4. 图片 (UI等)
			Image = new BridgeImageProvider();
			
			GD.Print("[CustomFantasy] Skin Loaded (PAK Mode) - Ready.");
		}


		public void UnloadAll()
		{
			GD.Print("[CustomFantasy] Skin Unloaded.");
		}

		// ====================================================================
		// [内部修复类] BridgeImageProvider
		// 作用：填补已删除的 CustomImageProvider，直接对接 AssetManager
		// ====================================================================
		private class BridgeImageProvider : IImageProvider
		{
			// 实现接口：获取图片
			public Texture2D GetTexture(string name) 
			{
				if (string.IsNullOrEmpty(name)) return null;
				// 直接对接 AssetManager 获取 UI 图片
				return AssetManager.Instance.GetUITexture(name);
			}

			// 实现接口：获取图标
			public Texture2D GetIcon(int iconId) 
			{
				return GetTexture($"{iconId}.img");
			}

			// 实现接口：获取预览动画 (防止 CS0535 错误)
			public SpriteFrames GetPreviewFrames(int idle, int walk, int attack)
			{
				// [修复 CS0234] 直接调用 AssetManager
				return AssetManager.Instance.CreateCharacterFrames(idle, walk, attack);
			}
		}
	}
}
