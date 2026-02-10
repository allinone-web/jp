// ============================================================================
// [FILE] DayNightOverlay.cs
// 說明：根據伺服器下發的遊戲世界時間 (Opcode 33 / Opcode 12 內 writeD(WORLDTIME))
//       驅動全螢幕日夜 Shader：黑夜三檔（晚上/深夜/極度黑夜）、白天色彩濾鏡（晨昏）。
// 對齊：server S_WorldStatPacket() writeC(33), writeD(Config.WORLDTIME)；
//       server Util.WorldTimeToHour() 使用 Config.WORLDTIME * 1000 為秒級時間。
// ============================================================================

using Godot;
using Client.Network;

namespace Client.Game
{
	/// <summary>日夜遮罩層：訂閱 PacketHandler.GameTimeReceived，驅動 Shader 與 DarknessChanged 信號。</summary>
	public partial class DayNightOverlay : Node
	{
		[Signal] public delegate void DarknessChangedEventHandler(float darkness, int tier);

		private ShaderMaterial _mat;
		private CanvasLayer _layer;
		private ColorRect _rect;

		/// <summary>黎明開始小時 (6 = 早上 6 點天亮)。</summary>
		private const float DawnHour = 6f;
		/// <summary>黃昏開始小時 (18 = 下午 6 點天黑)。</summary>
		private const float DuskHour = 18f;
		/// <summary>黎明/黃昏過渡時長（小時），用於平滑。</summary>
		private const float TransitionHours = 1.5f;

		/// <summary>黑夜檔位：0=白天，1=晚上(暗角)，2=深夜，3=極度黑夜。</summary>
		public const int TierDay = 0;
		public const int TierEvening = 1;
		public const int TierMidnight = 2;
		public const int TierDeepNight = 3;

		/// <summary>進入黑夜後隱藏名字/血條的門檻（>= 此值即視為黑夜）。</summary>
		public const float NightHideUiThreshold = 0.5f;

		/// <summary>黑夜遮罩顏色：純黑 (0,0,0)，三檔僅以 darkness／radius／ground_darken 區分濃度與範圍，不混合其他色。</summary>
		private Vector3 _nightColor = new Vector3(0f, 0f, 0f);

		/// <summary>強制預覽時段：F9 下一檔、F10 上一檔、F11 關閉預覽恢復伺服器時間；F12 切換參數可視化。</summary>
		private bool _debugOverrideActive;
		private int _debugPresetIndex;
		private bool _showDebugPanel = true;
		private Label _debugLabel;
		/// <summary>10 檔除錯預覽：含 19.25 時段使 darkness 落在 0.8~0.95，F9 可看到「深夜」。</summary>
		private static readonly (float Hour, string Name)[] DebugPresets = new[]
		{
			(0f, "極度黑夜(小亮區+最強地面暗)"),
			(4f, "凌晨(極度黑夜)"),
			(7f, "黎明"),
			(9f, "早晨暖黃"),
			(12f, "中午"),
			(16f, "下午"),
			(18f, "黃昏橙紅"),
			(19f, "晚上(暗角0.5~0.85)"),
			(19.25f, "深夜(0.8~0.95)"),
			(20f, "極度黑夜(0.8+ ro=0.55)"),
		};

		public override void _Ready()
		{
			var gw = GetParent() as GameWorld;
			var ph = gw?.PacketHandlerRef;
			if (ph == null) return;

			_layer = new CanvasLayer();
			_layer.Layer = 1;
			_layer.Name = "DayNightCanvasLayer";

			_rect = new ColorRect();
			_rect.MouseFilter = Control.MouseFilterEnum.Ignore;
			_rect.Color = new Color(0, 0, 0, 0);
			UpdateOverlaySize();

			var shader = GD.Load<Shader>("res://Client/Shaders/day_night.gdshader");
			if (shader == null) return;
			_mat = new ShaderMaterial();
			_mat.Shader = shader;
			ApplyShaderDefaults();
			_rect.Material = _mat;

			_layer.AddChild(_rect);

			_debugLabel = new Label();
			_debugLabel.Name = "DayNightDebugLabel";
			_debugLabel.Position = new Vector2(8, 8);
			_debugLabel.AddThemeFontSizeOverride("font_size", 14);
			_debugLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.2f));
			_debugLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
			_debugLabel.AddThemeConstantOverride("outline_size", 4);
			_debugLabel.Text = "";
			_debugLabel.Visible = _showDebugPanel;
			_layer.AddChild(_debugLabel);

			AddChild(_layer);

			ph.Connect(PacketHandler.SignalName.GameTimeReceived, Callable.From<int>(OnGameTimeReceivedFromServer));
			if (ph.LastWorldTimeSeconds >= 0)
				CallDeferred(nameof(ApplyCachedWorldTime));

			GetViewport().SizeChanged += UpdateOverlaySize;
			CallDeferred(nameof(UpdateOverlaySize));
		}

		private void ApplyShaderDefaults()
		{
			_mat.SetShaderParameter("darkness", 0f);
			_mat.SetShaderParameter("radius_in", 0.25f);
			_mat.SetShaderParameter("radius_out", 0.65f);
			_mat.SetShaderParameter("ground_darken", 0.25f);
			_mat.SetShaderParameter("night_color", _nightColor);
			_mat.SetShaderParameter("tint_color", new Vector3(1f, 1f, 1f));
			_mat.SetShaderParameter("tint_strength", 0f);
			_mat.SetShaderParameter("tint_radius_in", 0.2f);
			_mat.SetShaderParameter("tint_radius_out", 0.7f);
		}

		private void ApplyCachedWorldTime()
		{
			var gw = GetParent() as GameWorld;
			int cached = gw?.PacketHandlerRef?.LastWorldTimeSeconds ?? -1;
			if (cached >= 0) OnGameTimeReceived(cached);
		}

		private void UpdateOverlaySize()
		{
			if (_rect == null || !IsInsideTree()) return;
			Rect2 visible = GetViewport().GetVisibleRect();
			_rect.Position = visible.Position;
			_rect.Size = visible.Size;
		}

		public override void _UnhandledInput(InputEvent e)
		{
			if (e is InputEventKey key && key.Pressed && !key.Echo)
			{
				if (key.Keycode == Key.F9)
				{
					_debugOverrideActive = true;
					_debugPresetIndex = (_debugPresetIndex + 1) % DebugPresets.Length;
					ApplyDebugPreset();
					GetViewport().SetInputAsHandled();
				}
				else if (key.Keycode == Key.F10)
				{
					_debugOverrideActive = true;
					_debugPresetIndex = (_debugPresetIndex - 1 + DebugPresets.Length) % DebugPresets.Length;
					ApplyDebugPreset();
					GetViewport().SetInputAsHandled();
				}
				else if (key.Keycode == Key.F11)
				{
					_debugOverrideActive = false;
					var gw = GetParent() as GameWorld;
					int cached = gw?.PacketHandlerRef?.LastWorldTimeSeconds ?? -1;
					if (cached >= 0) OnGameTimeReceived(cached);
					GetViewport().SetInputAsHandled();
				}
				else if (key.Keycode == Key.F12)
				{
					_showDebugPanel = !_showDebugPanel;
					_debugLabel.Visible = _showDebugPanel;
					GetViewport().SetInputAsHandled();
				}
			}
		}

		private void OnGameTimeReceivedFromServer(int worldTimeSeconds)
		{
			if (_debugOverrideActive) return;
			OnGameTimeReceived(worldTimeSeconds);
		}

		private void ApplyDebugPreset()
		{
			var (hour, _) = DebugPresets[_debugPresetIndex];
			OnGameTimeReceived((int)(hour * 3600f));
		}

		private void OnGameTimeReceived(int worldTimeSeconds)
		{
			if (_mat == null) return;
			int secInDay = ((worldTimeSeconds % 86400) + 86400) % 86400;
			float hour = secInDay / 3600f;
			float darkness = ComputeDarkness(hour);
			int tier = GetNightTier(darkness);
			float appliedDark = ApplyTierParams(darkness, tier, hour);
			EmitSignal(SignalName.DarknessChanged, appliedDark, tier);
		}

		/// <summary>依小時 (0~24) 計算 darkness：白天 6~18 為 0，黎明/黃昏平滑過渡。</summary>
		private static float ComputeDarkness(float hour)
		{
			if (hour >= DawnHour && hour < DuskHour)
				return 0f;
			if (hour < DawnHour)
			{
				float tDawn = (hour - (DawnHour - TransitionHours)) / TransitionHours;
				return Mathf.Clamp(1f - tDawn, 0f, 1f);
			}
			float tDusk = (hour - DuskHour) / TransitionHours;
			return Mathf.Clamp(tDusk, 0f, 1f);
		}

		/// <summary>黑夜檔位：0=白天，1=晚上(0.5~0.8)，2=深夜(0.8~0.95)，3=極度黑夜(≥0.95)。</summary>
		private static int GetNightTier(float darkness)
		{
			if (darkness < 0.01f) return TierDay;
			if (darkness >= 0.95f) return TierDeepNight;
			if (darkness >= 0.8f) return TierMidnight;
			if (darkness >= 0.5f) return TierEvening;
			return TierDay;
		}

		/// <summary>套用檔位參數與白天色彩濾鏡。黑夜三檔：晚上 0.5/0.85、深夜 0.8/0.55、極度黑夜 0.95/0.4。回傳寫入 Shader 的 darkness。</summary>
		private float ApplyTierParams(float darkness, int tier, float hour)
		{
			float ri = 0.25f, ro = 0.65f, ground = 0.25f, darkVal = darkness;
			Vector3 tint = new Vector3(1f, 1f, 1f);
			float tintStr = 0f, tri = 0.2f, tro = 0.7f;

			if (tier == TierDay)
			{
				GetDayTint(hour, out tint, out tintStr);
			}
			else if (tier == TierEvening)
			{
				darkVal = 0.5f;
				ri = 0.2f;
				ro = 0.65f;
				ground = 0.85f;
			}
			else if (tier == TierMidnight)
			{
				darkVal = 0.8f;
				ri = 0.12f;
				ro = 0.55f;
				ground = 0.55f;
			}
			else
			{
				darkVal = 0.95f;
				ri = 0.03f;
				ro = 0.4f;
				ground = 0.4f;
			}

			_mat.SetShaderParameter("darkness", darkVal);
			_mat.SetShaderParameter("radius_in", ri);
			_mat.SetShaderParameter("radius_out", ro);
			_mat.SetShaderParameter("ground_darken", ground);
			_mat.SetShaderParameter("night_color", _nightColor);
			_mat.SetShaderParameter("tint_color", tint);
			_mat.SetShaderParameter("tint_strength", tintStr);
			_mat.SetShaderParameter("tint_radius_in", tri);
			_mat.SetShaderParameter("tint_radius_out", tro);
			// 白天且無濾鏡時隱藏遮罩，跳過全螢幕繪製以減輕 GPU 負擔
			_rect.Visible = darkVal >= 0.001f || tintStr >= 0.001f;
			UpdateDebugLabel(darkVal, tier, ri, ro, ground);
			return darkVal;
		}

		private static string GetTierName(int tier)
		{
			switch (tier)
			{
				case TierEvening: return "晚上";
				case TierMidnight: return "深夜";
				case TierDeepNight: return "極度黑夜";
				default: return "白天";
			}
		}

		private void UpdateDebugLabel(float darkVal, int tier, float ri, float ro, float ground)
		{
			if (!_showDebugPanel) { _debugLabel.Visible = false; return; }
			_debugLabel.Text = $"[F12] tier={GetTierName(tier)} dark={darkVal:F2} ri={ri:F2} ro={ro:F2} gd={ground:F2} night=({_nightColor.X:F2},{_nightColor.Y:F2},{_nightColor.Z:F2})";
			_debugLabel.Visible = true;
		}

		/// <summary>白天依小時回傳濾鏡色與強度（早晨暖、中午淡、傍晚橙）。</summary>
		private static void GetDayTint(float hour, out Vector3 tint, out float strength)
		{
			tint = new Vector3(1f, 1f, 1f);
			strength = 0f;
			if (hour >= DawnHour && hour < DuskHour)
			{
				// 早晨 6~9：暖黃
				if (hour < 9f)
				{
					float t = (hour - DawnHour) / 3f;
					tint = new Vector3(1f, 0.95f, 0.85f);
					strength = 0.15f + 0.1f * (1f - t);
				}
				// 中午 11~13：幾乎無
				else if (hour >= 11f && hour < 13f)
				{
					tint = new Vector3(1f, 1f, 1f);
					strength = 0.02f;
				}
				// 傍晚 16~18：橙紅
				else if (hour >= 16f)
				{
					float t = (hour - 16f) / 2f;
					tint = new Vector3(1f, 0.75f, 0.5f);
					strength = 0.08f + 0.12f * t;
				}
			}
		}
	}
}
