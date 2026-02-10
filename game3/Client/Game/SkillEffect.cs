// ============================================================================
// [FILE] SkillEffect.cs
// 修复说明：
// 1. [关键修复] 在 Init 中强制关闭动画循环 (SetAnimationLoop = false)。
// 2. [兜底机制] 根据帧数和FPS计算时长，增加 SafetyTimer 强制销毁，防止信号丢失。
// 3. [日志] 增加生命周期埋点，验证销毁原因。
// 修复说明：
// 1. [异步适配] Init 获取 null 时，启动 Timer 轮询等待资源就绪。
// 2. [稳健性] 增加最大重试次数 (5秒超时)，防止无限堆积。

// [修复] 解决“弓箭/魔法弹道不显示”问题。
// [原理] 将资源等待机制从 "Timer(0.1s)" 改为 "_Process(每帧)"。
//       对于飞行时间极短的投射物(0.2s)，每帧检查能确保它尽早显示，避免飞完即灭。
// jan 29.3 am test

// ============================================================================

using Godot;
using Client.Utility; 
using Core.Interfaces;
using System;

namespace Client.Game
{
	/// <summary>
	/// 一次性视觉特效控制器
	/// 职责：播放指定 GfxId 的动画，处理帧音效，播放完毕后自动销毁
	/// [修复] 使用叠加混合模式处理黑色光晕，移除硬编码映射逻辑
	/// </summary>
	public partial class SkillEffect : Node2D
	{
		private AnimatedSprite2D _sprite;
		private IAudioProvider _audioProvider; // 用于播放魔法音效（SprFrame.SoundIds：< 與 [ 皆為音效）；PlaySound 用臨時節點掛 Root 避免播畢銷毀截斷

		private GameEntity _followTarget; // 新增跟隨目標（用於位置跟隨）
		private GameEntity _alignTarget; // 用於對齊計算的目標（即使不跟隨位置，也需要用於對齊）
		
		// 调试信息
		private int _gfxId = 0;
		// 【109.effect 连环播放】缓存位置和跟随目标，用于播放下一个特效
		private Vector2 _chainPosition = Vector2.Zero;
		private GameEntity _chainFollowTarget = null;
		private int _chainHeading = 0;
		private Action<int, Vector2, int, GameEntity> _chainCallback = null; // 回调函数，用于触发下一个特效
		// [NEW] 异步重试相关
        // [修改] 移除 RetryTimer，改用 _Process 状态标记
        private bool _isWaitingForResource = false;
        private double _waitTimeoutTimer = 0.0;
        private const double MAX_WAIT_TIME = 5.0; // 5秒超时

		// [核心修復] 補全缺失的重試計數變數
		private int _retryCount = 0;
		private const int MAX_RETRIES = 50;
		// Init 参数缓存 (用于重试)
		private int _cacheHeading;
		private ISkinBridge _cacheSkinBridge;

		// [淡出效果] 動畫結束前的平滑淡出
		private bool _isFadingOut = false;
		private double _fadeOutTimer = 0.0;
		private double _fadeOutDuration = 0.15; // 預設淡出時間 0.15 秒（可通過 Init 參數調整）
		private double _animationTotalDuration = 0.0;
		private double _animationElapsedTime = 0.0;

		// ====================================================================
		// sucess code .dont change .[静态资源] 共享着色器材质（确保全游戏只占用一个渲染批次）
		// ====================================================================
		private static ShaderMaterial _blackFilterMaterial;

		private static ShaderMaterial GetFilterMaterial()
		{
			if (_blackFilterMaterial == null)
			{
				var shader = new Shader();
				// 【還原】使用你原本調好的「去黑色」方案：
				// - 基於亮度閾值丟棄純黑/極暗像素
				// - 不額外提高亮度，只做黑底透明化
				//  必須  0.4 才可以徹底去處黑色為透明。而且不能加亮度。
				//  已經成功去處黑色，不允許修改。 brightness < 0.4
				shader.Code = @"
					shader_type canvas_item;

					void fragment() {
						vec4 tex_color = texture(TEXTURE, UV);
						float brightness = max(max(tex_color.r, tex_color.g), tex_color.b);
						// 丟掉極暗像素（黑底/壓縮噪點）
						if (brightness < 0.4) {
							discard;
						}
						COLOR = tex_color;
					}
				";
				_blackFilterMaterial = new ShaderMaterial();
				_blackFilterMaterial.Shader = shader;
			}
			return _blackFilterMaterial;
		}

	  	// sucess code .dont change .
		


		public override void _Ready()
		{
			GD.Print("[SkillEffect] _Ready()");
			// 初始化 Sprite 组件
			_sprite = new AnimatedSprite2D();
			_sprite.Centered = true; // [核心修復] 與GameEntity一致，使用Centered=true讓動畫中心對齊節點原點（角色中心）
			_sprite.ZIndex = 20;     // 确保渲染在角色/尸体之上
			_sprite.TextureFilter = TextureFilterEnum.Nearest; // 像素风过滤
			
			// ====================================================================
			// [核心应用] 应用黑底透明化材质
			// ====================================================================
			_sprite.Material = GetFilterMaterial();
			AddChild(_sprite);

			// 【不允許修改】播完即銷毀：只訂閱 AnimationFinished，不設計時器；動畫播畢即呼叫 OnAnimationFinished 並 QueueFree
			_sprite.AnimationFinished += OnAnimationFinished;
			_sprite.FrameChanged += OnFrameChanged;

			// 魔法音效由 PlaySound 內建臨時 AudioStreamPlayer2D 掛場景根播放，播畢即 QueueFree，不依賴本節點
            // [默认] 关闭 Process，只有在等待资源时才开启
            SetProcess(false);
		}


		// 魔法效果跟隨與同步修復
	    public override void _Process(double delta)
	    {
	        // 如果有跟隨目標，每幀同步全球座標，確保特效永遠在角色中心
	        if (_followTarget != null && IsInstanceValid(_followTarget))
	        {
	            this.GlobalPosition = _followTarget.GlobalPosition;
				_chainPosition = GlobalPosition; // 同步更新缓存位置
	        }

	        if (_isWaitingForResource)
	        {
	            _waitTimeoutTimer += delta;
	            if (_waitTimeoutTimer > MAX_WAIT_TIME) 
				{
					// [日誌要求] 輸出異步加載超時錯誤
					GD.PrintErr($"[Magic-Error] SprID:{_gfxId} | Status:Load Failed - Timeout after {MAX_WAIT_TIME}s");
					QueueFree(); 
					return; 
				}
	            TryLoadAndPlay();
	            return;
	        }

			// [淡出效果] 處理動畫結束前的平滑淡出
			if (_sprite != null && _sprite.SpriteFrames != null && _animationTotalDuration > 0)
			{
				_animationElapsedTime += delta;

				// 如果淡出時間為 0，不使用淡出效果
				if (_fadeOutDuration <= 0.0)
				{
					// 動畫結束時直接處理結束邏輯
					if (_animationElapsedTime >= _animationTotalDuration)
					{
						OnFadeOutComplete();
						return;
					}
				}
				else
				{
					// 在動畫結束前開始淡出
					if (!_isFadingOut && _animationElapsedTime >= _animationTotalDuration - _fadeOutDuration)
					{
						_isFadingOut = true;
						_fadeOutTimer = 0.0;
					}

					// 執行淡出
					if (_isFadingOut)
					{
						_fadeOutTimer += delta;
						float alpha = 1.0f - (float)(_fadeOutTimer / _fadeOutDuration);
						alpha = Mathf.Clamp(alpha, 0.0f, 1.0f);
						
						if (_sprite != null)
						{
							_sprite.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
						}

						// 淡出完成後銷毀
						if (_fadeOutTimer >= _fadeOutDuration)
						{
							OnFadeOutComplete();
							return;
						}
					}
				}
			}
	    }
	
	
		/// <summary>
		/// 初始化并播放特效
		/// </summary>
		/// <param name="gfxId">魔法的 GfxId (List.spr 编号)</param>
		/// <param name="heading">方向 (0-7)，光箭等飞行道具需要这个</param>
		/// <param name="skinBridge">资源接口</param>
		/// <param name="audioProvider">音频接口 (用于播放魔法自带的音效)</param>
		// public void Init(int gfxId, int heading, ISkinBridge skinBridge, IAudioProvider audioProvider, GameEntity followTarget = null)
	    // 修改 Init 接口，支持傳入跟隨目標；useFollowForPosition=false 時僅用於 109 連貫落點，不每幀跟隨（飛行魔法由 Tween 控制位置）
	    // fadeOutDuration: 淡出時間（秒），0.0 表示不使用淡出效果，直接消失
	    public void Init(int gfxId, int heading, ISkinBridge skinBridge, IAudioProvider audioProvider, GameEntity followTarget = null, Action<int, Vector2, int, GameEntity> chainCallback = null, bool useFollowForPosition = true, double fadeOutDuration = 0.15)
	    {
	        _gfxId = gfxId;
	        _cacheHeading = heading;
	        _cacheSkinBridge = skinBridge;
	        _audioProvider = audioProvider;
	        _followTarget = useFollowForPosition ? followTarget : null; // 飛行魔法不跟隨，由 Tween 控制
			_alignTarget = followTarget; // 【修復】即使不跟隨位置，也保留目標用於對齊計算
			_chainCallback = chainCallback;
			_chainPosition = GlobalPosition;
			_chainFollowTarget = followTarget; // 連貫時仍用目標位置
			_chainHeading = heading;
			_fadeOutDuration = Mathf.Max(0.0, fadeOutDuration); // [淡出效果] 設置淡出時間，確保非負數
			// [魔法日誌] Init 參數：用於對應 list.spr 與實際動畫檔
			GD.Print($"[Magic][SkillEffect.Init] GfxId:{_gfxId} Heading:{_cacheHeading} WorldPos:{GlobalPosition} HasBridge:{(_cacheSkinBridge != null)} HasAudio:{(_audioProvider != null)} FollowTarget:{(followTarget != null ? followTarget.ObjectId : 0)} ChainCallback:{(_chainCallback != null)} FadeOut:{_fadeOutDuration:0.###}s");

	        TryLoadAndPlay();
	    }




		private void TryLoadAndPlay()
		{
			if (_cacheSkinBridge == null) { QueueFree(); return; }

			int magicActionId = 0;
			bool isFirstAttempt = !_isWaitingForResource;
			var def = ListSprLoader.Get(_gfxId);
			if (isFirstAttempt)
			{
				if (def != null)
				{
					bool has0 = def.Actions.TryGetValue(0, out var seq0);
					bool has3 = def.Actions.TryGetValue(3, out var seq3);
					GD.Print($"[Magic][SkillEffect.TryLoad] GfxId:{_gfxId} List.spr Attr:{def.Attr} SpriteId:{def.SpriteId} Action0:{(has0 ? seq0.Name : "無")} Action3:{(has3 ? seq3?.Name : "無")} Heading:{_cacheHeading} -> GetBodyFrames(gfx:{_gfxId}, action:{magicActionId}, heading:{_cacheHeading})");
				}
				else
					GD.Print($"[Magic][SkillEffect.TryLoad] GfxId:{_gfxId} List.spr 無定義 仍請求 GetBodyFrames(gfx:{_gfxId}, action:{magicActionId}, heading:{_cacheHeading})");
			}

			var frames = _cacheSkinBridge.Character.GetBodyFrames(_gfxId, magicActionId, _cacheHeading);

			if (frames == null)
			{
				if (!_isWaitingForResource)
				{
					_isWaitingForResource = true;
					_waitTimeoutTimer = 0.0;
					_retryCount = 0;
					SetProcess(true);
					GD.Print($"[Magic][SkillEffect.TryLoad] 幀為空 進入輪詢 GfxId:{_gfxId} ActionId:{magicActionId} Heading:{_cacheHeading} (可能缺 SpriteId-{_cacheHeading}-xxx.png 或 list.spr 未載入)");
				}
				return;
			}
			
			// 资源加载成功，停止轮询
			_isWaitingForResource = false;
			SetProcess(false);

			_sprite.SpriteFrames = frames;
			
			// 【服務器對齊】8方向映射統一規則：所有圖像（包括弓箭、魔法、角色等）都使用相同的映射規則
			// 對齊服務器方向系統和客戶端成功的8方向映射邏輯（ListSprLoader.GetFileOffset）
			// DirectionFlag=0（無方向）使用 "0"，DirectionFlag=1（有方向）使用 GetFileOffset(heading, dirFlag).ToString()
			string finalAnimName = "";
			var seq = ListSprLoader.GetActionSequence(def, magicActionId);
			if (seq != null && seq.DirectionFlag == 0)
			{
				// 無方向動畫：優先使用 "0"
				if (frames.HasAnimation("0")) finalAnimName = "0";
				else if (frames.HasAnimation("default")) finalAnimName = "default";
				else
				{
					var animNames = frames.GetAnimationNames();
					GD.PrintErr($"[Magic][SkillEffect.TryLoad] 無方向動畫 GfxId:{_gfxId} 需要 '0' 或 'default' 實際:[{string.Join(",", animNames)}]");
					QueueFree();
					return;
				}
			}
			else
			{
				// 【核心修復】有方向動畫：使用 GetFileOffset 獲取正確的文件偏移，對齊客戶端成功的8方向映射邏輯
				// 服務器方向：0=正上, 1=右上, 2=正右, 3=右下, 4=正下, 5=左下, 6=正左, 7=左上
				// GetFileOffset 映射：7→0, 0→1, 1→2, 2→3, 3→4, 4→5, 5→6, 6→7
				// 動畫名稱應該使用文件偏移（GetFileOffset），而不是原始 heading
				int fileOffset = ListSprLoader.GetFileOffset(_cacheHeading, seq != null ? seq.DirectionFlag : 1);
				string dirAnimName = fileOffset.ToString();
				if (frames.HasAnimation(dirAnimName)) finalAnimName = dirAnimName;
				else if (frames.HasAnimation(_cacheHeading.ToString())) finalAnimName = _cacheHeading.ToString();
				else if (frames.HasAnimation("0")) finalAnimName = "0";
				else if (frames.HasAnimation("default")) finalAnimName = "default";
				else
				{
					var animNames = frames.GetAnimationNames();
					GD.PrintErr($"[Magic][SkillEffect.TryLoad] 有方向動畫 GfxId:{_gfxId} Heading:{_cacheHeading} FileOffset:{fileOffset} 需要動畫名稱:'{dirAnimName}' 或 '0'/'default' 實際:[{string.Join(",", animNames)}]");
					QueueFree();
					return;
				}
			}

			frames.SetAnimationLoop(finalAnimName, false);
			int frameCount = frames.GetFrameCount(finalAnimName);

			// 嚴格按每幀的 RealDuration 累加，保持與 list.spr/SprDataTable 完全一致
			double duration = 0.0;
			for (int i = 0; i < frameCount; i++)
			{
				duration += frames.GetFrameDuration(finalAnimName, i);
			}
			if (duration <= 0) duration = 0.5; // 保險值，理論上不會走到

			// [淡出效果] 記錄動畫總時長並初始化淡出狀態
			_animationTotalDuration = duration;
			_animationElapsedTime = 0.0;
			_isFadingOut = false;
			_fadeOutTimer = 0.0;
			if (_sprite != null)
			{
				_sprite.Modulate = Colors.White; // 重置為完全不透明
			}

			_sprite.Play(finalAnimName);
            
            // 【核心修复，不允許修改或刪除】魔法播放速度由 Provider 建幀時每幀 duration（純魔法已 ×2）決定；此處固定 1.0 不受綠水/勇水等加速影響。
            _sprite.SpeedScale = 1.0f;
            
            // 確保特效在角色上方
            _sprite.ZIndex = 100;
			// 【关键修复】不要启用 TopLevel！
			// TopLevel=true 会让该 Sprite 脱离世界/相机变换，使用"屏幕坐标系"渲染。
			// 由于我们的世界坐标非常大(例如 1,043,216)，TopLevel 会导致特效直接画到屏幕外，看起来像"没动画"。
            _sprite.TopLevel = false;

			Texture2D t0 = null;
			try { if (frameCount > 0) t0 = frames.GetFrameTexture(finalAnimName, 0); } catch { }
			string t0Info = (t0 == null) ? "null" : $"{t0.GetSize()}";
			GD.Print($"[Magic][SkillEffect.Play] GfxId:{_gfxId} Anim:'{finalAnimName}' Frames:{frameCount} Dur:{duration:0.###}s FadeOut:{_fadeOutDuration:0.###}s 播畢即銷毀");

			// [淡出效果] 啟動 Process 以監控動畫進度並處理淡出
			SetProcess(true);

			OnFrameChanged();
		}

		// ====================================================================
		//   帧事件处理 (移植自 GameEntity.Visuals)
		//   用于播放 list.spr 中定义的音效 (如 [123)
		// ====================================================================
		private void OnFrameChanged()
		{
			if (_sprite == null || _sprite.SpriteFrames == null) return;

			string anim = _sprite.Animation;
			int frame = _sprite.Frame;
			var tex = _sprite.SpriteFrames.GetFrameTexture(anim, frame);
			if (tex == null) return;

			// [核心修復] 參考 Shadow 對齊角色的邏輯，讓魔法動畫中心對齊角色中心
			// Shadow 對齊公式：ox = sDx - bodyDx - bodyW/2 + sW/2
			// 魔法對齊公式：ox = magicDx - bodyDx - bodyW/2 + magicW/2
			if (tex.HasMeta("spr_anchor_x") && tex.HasMeta("spr_anchor_y"))
			{
				int magicDx = (int)tex.GetMeta("spr_anchor_x");
				int magicDy = (int)tex.GetMeta("spr_anchor_y");
				int magicW = tex.GetWidth();
				int magicH = tex.GetHeight();

				// 【修復】使用 _alignTarget 而不是 _followTarget，因為飛行魔法可能不跟隨位置但仍需對齊
				GameEntity alignEntity = _alignTarget ?? _followTarget;
				if (alignEntity != null && IsInstanceValid(alignEntity))
				{
					// 獲取角色的主體信息（使用首幀 frame 0，與 Shadow 對齊邏輯一致）
					var bodySprite = alignEntity.GetNodeOrNull<AnimatedSprite2D>("MainBody");
					if (bodySprite != null && bodySprite.SpriteFrames != null)
					{
						var bodyTex = bodySprite.SpriteFrames.GetFrameTexture(bodySprite.Animation, 0);
						if (bodyTex != null && bodyTex.HasMeta("spr_anchor_x") && bodyTex.HasMeta("spr_anchor_y"))
						{
							int bodyDx = (int)bodyTex.GetMeta("spr_anchor_x");
							int bodyDy = (int)bodyTex.GetMeta("spr_anchor_y");
							int bodyW = bodyTex.GetWidth();
							int bodyH = bodyTex.GetHeight();

							// 使用與 Shadow 相同的對齊公式
							float ox = magicDx - bodyDx - bodyW / 2f + magicW / 2f;
							float oy = magicDy - bodyDy - bodyH / 2f + magicH / 2f;
							_sprite.Offset = new Vector2(ox, oy);
							
							// ====================================================================
							// 【音效修復】關鍵修復：移除此處的 return，確保音效播放代碼能夠執行
							// ====================================================================
							// 【問題原因】
							// 在對齊邏輯中，當成功獲取到 alignEntity 的 bodyTex 並完成對齊計算後，
							// 原本在此處有一個 return 語句，導致後續的音效播放代碼（第 390-407 行）被跳過。
							// 
							// 【為什麼會導致音效失效】
							// 1. 大部分魔法都有 alignEntity（跟隨目標或對齊目標），因此會進入此分支
							// 2. 當 bodyTex 存在且包含 spr_anchor_x/spr_anchor_y 元數據時，會執行對齊計算
							// 3. 原本的 return 會直接結束方法，導致音效播放代碼永遠不會執行
							// 4. 只有少數魔法（如 gfx=243）可能沒有 alignEntity 或 bodyTex 缺少元數據，
							//    因此不會進入此分支，音效播放代碼得以執行，所以這些魔法音效正常
							//
							// 【修復方案】
							// 移除此處的 return，讓對齊邏輯執行完畢後，繼續執行後續的音效播放代碼。
							// 這樣可以確保：
							// - 對齊邏輯正常執行（設置 _sprite.Offset）
							// - 音效播放代碼在對齊之後執行（讀取 spr_sound_ids 並播放音效）
							// - 所有魔法都能正常播放音效
							// ====================================================================
						}
					}
				}

				// 如果沒有跟隨目標或無法獲取角色信息，使用簡化方案：讓動畫中心對齊節點原點
				// Centered=true 時，Offset = (w/2 - dx, h/2 - dy) 可讓動畫中心對齊節點原點
				if (_sprite.Offset == Vector2.Zero || (_alignTarget == null && _followTarget == null))
				{
					_sprite.Offset = new Vector2(magicW / 2f - magicDx, magicH / 2f - magicDy);
				}
			}
			else
			{
				// 如果沒有錨點數據，使用紋理中心（Offset = 0，因為 Centered=true）
				_sprite.Offset = Vector2.Zero;
			}

			// 【核心修復】動畫幀音效必須在對齊邏輯之後執行，確保所有魔法都能播放音效
			// 動畫幀音效僅依 SprFrame.SoundIds（list.spr 中 < 與 [ 皆為音效，已寫入紋理 spr_sound_ids）
			if (tex.HasMeta("spr_sound_ids"))
			{
				try
				{
					var arr = (Godot.Collections.Array<int>)tex.GetMeta("spr_sound_ids");
					if (arr != null && arr.Count > 0)
						foreach (int sid in arr) PlaySound(sid);
				}
				catch { }
			}
			else if (tex.HasMeta("spr_sound_id"))
			{
				try
				{
					int sid = (int)tex.GetMeta("spr_sound_id");
					PlaySound(sid);
				}
				catch { }
			}
		}

		// ------------------------------------------------------------
		// 魔法音效：使用掛在場景根的臨時播放器，避免動畫播畢 QueueFree 時音效被截斷
		// ------------------------------------------------------------
		private void PlaySound(int soundId)
		{
			if (soundId <= 0) return;
			
			// 【診斷】檢查 _audioProvider 是否為 null
			if (_audioProvider == null)
			{
				GD.PrintErr($"[Magic][SkillEffect.PlaySound] _audioProvider is null! GfxId:{_gfxId} SoundId:{soundId} - 音效無法播放");
				return;
			}

			var stream = _audioProvider.GetSound(soundId);
			if (stream == null)
			{
				GD.PrintErr($"[Magic][SkillEffect.PlaySound] GetSound({soundId}) returned null for GfxId:{_gfxId} - 音效資源未加載或不存在");
				return;
			}

			var tempPlayer = new AudioStreamPlayer2D();
			tempPlayer.Stream = stream;
			tempPlayer.GlobalPosition = GlobalPosition;
			tempPlayer.MaxDistance = 800f;
			tempPlayer.Bus = "SFX";
			GetTree().Root.AddChild(tempPlayer);
			tempPlayer.Play();
			tempPlayer.Finished += tempPlayer.QueueFree;
			GD.Print($"[Magic][SkillEffect.PlaySound] Playing sound {soundId} for GfxId:{_gfxId} at {GlobalPosition}");
		}

		// ====================================================================
		//   淡出完成處理
		// ====================================================================
		private void OnFadeOutComplete()
		{
			// 淡出完成後執行正常的結束邏輯
			if (_animationFinishedHandled) return;
			_animationFinishedHandled = true;

			int nextGfxId = 0;
			bool isNegativeFrame = false;
			var def = ListSprLoader.Get(_gfxId);
			if (def != null && def.EffectChain.Count > 0)
			{
				foreach (var kv in def.EffectChain)
				{
					if (kv.Value > 0)
					{
						nextGfxId = kv.Value;
						break;
					}
					else if (kv.Value == -1)
					{
						isNegativeFrame = true;
						nextGfxId = _gfxId;
						GD.Print($"[Magic][SkillEffect.GroundItem] GfxId:{_gfxId} 109.effect(3 -1) 倒數第一幀，應在落點創建地面物品（由服務器 S_ObjectAdd 創建）");
						break;
					}
				}
			}
			
			if (nextGfxId > 0 && _chainCallback != null && !isNegativeFrame)
			{
				Vector2 chainPos = (_chainFollowTarget != null && IsInstanceValid(_chainFollowTarget))
					? _chainFollowTarget.GlobalPosition
					: GlobalPosition;
				GD.Print($"[Magic][SkillEffect.Chain] 109.effect 連貫 GfxId:{_gfxId} -> NextGfxId:{nextGfxId} Pos:{chainPos} Heading:{_chainHeading}");
				_chainCallback(nextGfxId, chainPos, _chainHeading, _chainFollowTarget);
				QueueFree();
				return;
			}
			else if (isNegativeFrame)
			{
				Vector2 groundPos = (_chainFollowTarget != null && IsInstanceValid(_chainFollowTarget))
					? _chainFollowTarget.GlobalPosition
					: GlobalPosition;
				GD.Print($"[Magic][SkillEffect.GroundItem] GfxId:{_gfxId} 動畫播畢，應在落點創建地面物品 Pos:{groundPos} (等待服務器 S_ObjectAdd)");
			}
			
			GD.Print($"[Magic][SkillEffect.Finish] 動畫淡出完成 GfxId:{_gfxId} 銷毀");
			QueueFree();
		}

		// ====================================================================
		//   生命周期结束 + 109.effect 连环播放
		// list.spr 109.effect(a b)：當前特效播畢後，在相同位置播放下一個 GfxId=b（a 為條目鍵，可為 0/5 等，取第一個 value>0）
		// [修改] 動畫結束時如果淡出尚未完成，則等待淡出完成；否則立即處理連貫動畫。
		// ====================================================================
		private bool _animationFinishedHandled = false;
		private void OnAnimationFinished()
		{
			// 如果已經在淡出或已處理，直接返回
			if (_animationFinishedHandled) return;

			// 如果淡出時間為 0，立即處理結束邏輯
			if (_fadeOutDuration <= 0.0)
			{
				OnFadeOutComplete();
				return;
			}

			// 如果淡出尚未開始，立即開始淡出（動畫已結束但可能還有淡出時間）
			if (!_isFadingOut)
			{
				_isFadingOut = true;
				_fadeOutTimer = 0.0;
				SetProcess(true);
			}

			// 注意：不在此處設置 _animationFinishedHandled，讓淡出完成後再處理
		}
	}
}
