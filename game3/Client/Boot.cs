// ============================================================================
// [FILE] Boot.cs
// [说明] 游戏启动入口。
// [更新] 新增了 BGM 播放器 (_bgmPlayer) 和 PlayBgm 接口，替代 SoundManager。
// [修复] SetWorldActive 增加对 CanvasLayer 的支持，彻底解决 HUD 重叠问题。
// [更新] 移除了对 GameWorld.InitSkin 的过时调用，适配新的模块化初始化流程。
// [整合版本] 
// 1. 保留了 SetWorldActive (CanvasLayer支持) 以解决 HUD 重叠。
// 2. 保留了完整的 UI 场景流转 (LoadScene, ToCharacterSelect 等)。
// 3. 移除了对 GameWorld.InitSkin 的调用 (适配 GameWorld 自初始化)。
// 4. Boot 保留自己的 SkinBridge 用于登录阶段的 BGM 播放。
//
//. 创建角色流程：先发67询问，必须带type，必须使用已经写好的helper类。再发12传输具体数据.不是112.
//
// ============================================================================

using Godot;
using System;
using System.Text;
using Client.Network;
using Client.Data; 
using Client.Game;
using Client.Utility; 
using Core.Interfaces;      
using Skins.CustomFantasy;  
using System.Collections.Generic; // 确保 List 可用
namespace Client
{
	public partial class Boot : Node
	{
	
		// [新增] 专门用于放置全屏 UI (Login, CharSelect) 的画布层
		private CanvasLayer _uiLayer; 
		
		// [合并] 统一使用这一个变量引用当前显示的场景 (UI 或 GameWorld)
		private Node _currentScene;

		public static Boot Instance { get; private set; } = null!;

		// --- 网络配置 ---
		[Export] public string Host = "127.0.0.1";
		[Export] public int Port = 2000;
		[Export] public string LoginIp = "127.0.0.1";

		// --- 测试账号 ---
		[Export] public string TestUser = "test001";
		[Export] public string TestPass = "111111";
		[Export] public string AutoLoginCharName = "kkkk"; 

		// =========================================================
		// 开关：DevMode ( true =直接进游戏, false =显示登录界面)
		// =========================================================
		[Export] public bool DevMode_DirectLogin = false;

		// --- 核心引用 ---
		private GodotTcpSession _netSession = null!;
		private PacketHandler _ph = null!; 
		
		// 【新增】全局 BGM 播放器
		private AudioStreamPlayer _bgmPlayer = null!;

		// [新增] 全域音效播放器 (UI音、系統音、Opcode 74)
		private AudioStreamPlayer _sfxPlayer; 
		
		// [新增] 緩存 AudioProvider，避免重複 new
		private IAudioProvider _audioProvider;

		// World 节点引用 (兼容 DevMode 下预先存在的 World)
		private Node _gameWorldNode = null;

		// --- 状态标志 ---
		private bool _handshakeCompleted = false;
		private bool _isInWorld = false; 
		private bool _hasRequestedEnterWorld = false;

		// --- 列表接收逻辑控制 ---
		private int _expectedCharCount = -1;
		
		// --- 公开属性 ---
		public bool IsInGame { get => _isInWorld; set => _isInWorld = value; }
		public string CurrentCharName { get; set; } = "";
		// ====================================================================
		// ====================================================================
		
		// 1. 角色列表缓存 (CharacterSelect.cs 依赖这个)
		public List<Client.Data.CharacterInfo> CharacterList { get; private set; } = new();
		
		// 角色列表缓存 (核心数据源)
		public Client.Data.CharacterInfo MyCharInfo { get; set; } = new Client.Data.CharacterInfo();

		/// <summary>伺服器世界時間（秒，來自 GameTimeReceived）。用於 ChaWindow LVLabel17 等顯示。</summary>
		public int WorldTimeSeconds { get; private set; }

		// --- Boot.cs 信号定义区 (必须唯一) ---
		[Signal] public delegate void LoginSuccessEventHandler();
		[Signal] public delegate void LoginFailedEventHandler(string message);
		[Signal] public delegate void CreateCharFailedEventHandler(string reason);
		[Signal] public delegate void EnterWorldSuccessEventHandler();

		// 创角成功相关信号：严格对齐服务器 Op 5 和 Op 4

		// 1. 使用 Client.Data.CharacterInfo 明确类型。
		// 2. 这里的 CharacterInfoReceived 用于单条数据通知。
		[Signal] public delegate void CharacterInfoReceivedEventHandler(Client.Data.CharacterInfo info);

		// [重点删除！！！] 
		// 下面这行被我删除了，因为它接收的是 Array (数组)。
		// 服务器是“一个一个”发包的，不是发数组。这个信号会导致逻辑错误。
		// [Signal] public delegate void CharacterListReceivedEventHandler(Godot.Collections.Array<CharacterInfo> list);

		// [保留] 通用刷新信号。收到数据后通知 UI "去读 Boot.CharacterList 缓存"。
		[Signal] public delegate void CharacterListUpdatedEventHandler();

		// [恢复] 游戏内装备逻辑信号 (只保留这一个定义，不要重复)
		[Signal] public delegate void ItemEquipStatusChangedEventHandler(int objectId, bool isEquipped);
		
		// 皮肤接口 (Boot 自身持有，用于 UI 阶段资源)
		public ISkinBridge SkinBridge { get; private set; }

		// 场景状态
		public bool IsInCreateCharScene { get; set; } = false;

		// =====================================================================
		// Godot Lifecycle
		// =====================================================================
		public override void _Ready()
		{
			Instance = this;
			GD.Print("[Boot] System Starting...");

			// [匯出後] 非編輯器環境一律走登入流程，避免直接進 Hub（含 iOS 模擬器／實機）
			if (!Engine.IsEditorHint())
				DevMode_DirectLogin = false;
			GD.Print($"[Boot] IsEditorHint={Engine.IsEditorHint()}, DevMode_DirectLogin={DevMode_DirectLogin}");

				// ============================================================
				// [核心新增] 初始化 SPR 系统（必须在 SkinLoader 之前）
				// ============================================================
				InitializeSpriteSystem();


			// 1. 初始化表 (保留原逻辑)
			var st = new Client.Utility.StringTable(); st.Name = "StringTable"; AddChild(st);
			var dt = new Client.Utility.DescTable(); dt.Name = "DescTable"; AddChild(dt);

			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);



			// 2. 初始化皮肤 (Boot 阶段使用)
			SkinBridge = new CustomSkinLoader(); 
			GD.Print("[Boot] Skin System Initialized (CustomFantasy).");


			// 初始化 AudioProvider
			_audioProvider = new CustomAudioProvider();
			// 載入音訊設定（音量、開關）
			Client.Data.ClientConfig.Load();
			// 確保 Music / SFX 巴士存在（若編輯器未建則程式建立）
			EnsureAudioBuses();
			// 3. 初始化 BGM 播放器（循環：播完後自動重播）
			_bgmPlayer = new AudioStreamPlayer();
			_bgmPlayer.Name = "GlobalBGM";
			_bgmPlayer.Bus = "Music";
			_bgmPlayer.Finished += OnBgmFinished;
			AddChild(_bgmPlayer);

			// [新增] 初始化 SFX 播放器
			_sfxPlayer = new AudioStreamPlayer();
			_sfxPlayer.Bus = "SFX";
			AddChild(_sfxPlayer);
			ApplyAudioSettings();

			// 4. 网络初始化
			SetupNetwork();
			EnsurePacketHandler();
			
			// 5. 绑定逻辑 (仅在开发模式自动登录)
			if (DevMode_DirectLogin)
			{
				LoginSuccess += () => OnLoginResult(true, "Auto Login Signal");
			}


			// ============================================================
			// [核心修改] 初始化 UI 层
			// ============================================================
			// [删除] _uiRoot = new Control(); ... AddChild(_uiRoot);
			
			// [新增] 创建一个 CanvasLayer。
			// 它的作用是：让子节点忽略 2D 摄像机，永远固定在屏幕上，且坐标系为屏幕像素。
			_uiLayer = new CanvasLayer();
			_uiLayer.Layer = 1; // 层级设为 1，确保在最底层（如果以后有 Loading 界面可以设更高）
			_uiLayer.Name = "BootUILayer";
			AddChild(_uiLayer);

			// [關鍵] 先加入 AssetManager（deferred 避免 Parent is busy setting up 錯誤），Login/CharacterSelect 等 UI 才能從 Img182.pak 取得圖片
			var tree = GetTree();
			if (tree != null && tree.Root != null)
			{
				var am = tree.Root.GetNodeOrNull<AssetManager>("AssetManager");
				if (am == null)
				{
					am = new AssetManager();
					am.Name = "AssetManager";
					tree.Root.CallDeferred("add_child", am);
				}
			}

			// 获取 World 节点 (如果场景树中已存在)
			_gameWorldNode = GetNodeOrNull("World");
				// 如果是 DevMode 且没找到 World，报个警，但在 UI 流程中这是正常的
			if (_gameWorldNode == null) GD.PrintErr("[Boot] ⚠️ World node not found!");

			// 6. 启动模式分流
			if (DevMode_DirectLogin)
			{
				GD.Print("[Boot] Mode: Direct Login (Dev)");
				SetWorldActive(true); // 显示 World
				InjectSkin();         // 立即注入皮肤
			}
			else
			{
				GD.Print("[Boot] Mode: Full UI Flow");
				SetWorldActive(false); // 隐藏 World
				
				// [修正] 这里直接调用 LoadScene，它现在使用了正确的 _uiLayer 逻辑
				LoadScene("res://Client/UI/Scenes/Login.tscn");
			}





		}

		// =====================================================================
		// 场景切换逻辑 (已合并旧的 LoadScene 和新的 ChangeScene)
		// =====================================================================
		public void LoadScene(string scenePath)
		{
			// 1. 移除旧场景
			if (_currentScene != null && IsInstanceValid(_currentScene))
			{
				_currentScene.QueueFree();
				_currentScene = null;
			}

			// 2. 加载并实例化新场景
			if (ResourceLoader.Exists(scenePath))
			{
				var scenePkg = ResourceLoader.Load<PackedScene>(scenePath);
				var instance = scenePkg.Instantiate();

				// [核心关键点] 分流挂载
				// 如果是 UI 场景 (Login, Select) -> 挂到 _uiLayer (CanvasLayer) 下 -> 解决错位问题
				// 如果是 游戏世界 (GameWorld) -> 挂到 Boot 根节点下 -> 确保 Camera2D 生效
				if (instance is Control) 
				{
					_uiLayer.AddChild(instance);
				}
				else 
				{
					AddChild(instance);
				}
				
				_currentScene = instance;
				GD.Print($"[Boot] Scene Loaded: {scenePath}");
			}
			else
			{
				GD.PrintErr($"[Boot] 场景不存在: {scenePath}");
			}
		}

		// =====================================================================
		// 显隐控制逻辑
		// =====================================================================
		private void SetWorldActive(bool active)
		{
			if (_gameWorldNode == null) return;

			_gameWorldNode.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;

			foreach(var child in _gameWorldNode.GetChildren())
			{
				if (child is CanvasLayer cl) cl.Visible = active;
				else if (child is CanvasItem ci) ci.Visible = active;
				else if (child is Node3D n3) n3.Visible = active;
			}
			
			GD.Print($"[Boot] Set World Active (Recursive): {active}");
		}

		private void InjectSkin()
		{
			if (_gameWorldNode == null) return;
			// GameWorld 现在自初始化，此处仅打印日志确认
			GD.Print("[Boot] GameWorld detected. Assuming self-initialization complete.");
		}

		// =====================================================================
		// 场景跳转辅助
		// =====================================================================
		/// <summary>退回角色列表頁面。若在遊戲中會先隱藏 World 再載入選角場景。清空當前角色名，確保設定存檔依新角色名寫入。</summary>
		public void ToCharacterSelectScene()
		{
			IsInCreateCharScene = false;
			SetWorldActive(false); // 若在遊戲中，先隱藏世界
			CurrentCharName = "";
			Client.Data.ClientConfig.CurrentCharacterName = "";
			LoadScene("res://Client/UI/Scenes/CharacterSelect.tscn");
		}

		/// <summary>退回登入畫面。對齊伺服器設計：Restart 時先送 Op 15 再斷線，需重新連線故回到登入頁。</summary>
		public void ToLoginScene()
		{
			IsInCreateCharScene = false;
			SetWorldActive(false);
			IsInGame = false;
			CurrentCharName = "";
			_hasRequestedEnterWorld = false;
			CharacterList.Clear();
			LoadScene("res://Client/UI/Scenes/Login.tscn");
		}

		public void ToCharacterCreateScene()
		{
			IsInCreateCharScene = true;
			LoadScene("res://Client/UI/Scenes/CharacterCreate.tscn");
		}

		public void ToGameWorldScene(string charName)
		{
			CurrentCharName = charName;
			Client.Data.ClientConfig.SwitchCharacter(charName);
			// 依新角色載入該角色專屬快捷欄，避免仍顯示上一角快捷欄
			var gw = GetNodeOrNull<GameWorld>("World");
			if (gw != null) gw.RefreshBottomBarHotkeysFromConfig();
			// 正常選角進入時同步 MyCharInfo，讓角色面板 LVLabel1 顯示正確名字
			foreach (var c in CharacterList)
			{
				if (c.Name == charName)
				{
					MyCharInfo.Name = c.Name;
					MyCharInfo.Level = c.Level;
					MyCharInfo.Type = c.Type;
					MyCharInfo.Sex = c.Sex;
					MyCharInfo.Str = c.Str; MyCharInfo.Dex = c.Dex; MyCharInfo.Con = c.Con;
					MyCharInfo.Wis = c.Wis; MyCharInfo.Cha = c.Cha; MyCharInfo.Int = c.Int;
					MyCharInfo.ClanName = c.ClanName;
					MyCharInfo.Lawful = c.Lawful;
					MyCharInfo.Ac = c.Ac;
					MyCharInfo.Exp = c.Exp;
					MyCharInfo.CurrentHP = c.CurrentHP; MyCharInfo.MaxHP = c.MaxHP;
					MyCharInfo.CurrentMP = c.CurrentMP; MyCharInfo.MaxMP = c.MaxMP;
					MyCharInfo.AccessLevel = c.AccessLevel;
					break;
				}
			}
			Action_EnterWorld(charName);
			GD.Print("[Boot] Requesting Enter World Protocol...");
			
			if (!DevMode_DirectLogin)
			{
				// 1. 移除当前的 UI 场景 (CharacterSelect)
				if (_currentScene != null)
				{
					_currentScene.QueueFree();
					_currentScene = null;
				}

				// 2. 显示 World (包括 HUD)
				SetWorldActive(true);
				
				// 3. 触发注入检查
				InjectSkin();
			}
		}

		public void NotifyWorldSceneReady()
		{
			IsInGame = true;
			InjectSkin(); // 再次检查
			EmitSignal(SignalName.EnterWorldSuccess);
		}

		// =====================================================================
		// 音乐播放接口
		// =====================================================================
		/// <summary>
		/// 播放音乐 (供 UI 场景调用)。檔名規則：music{mapId}.mp3（與 map id 對應），播完後自動循環。
		/// </summary>
		public void PlayBgm(int musicId)
		{
			if (SkinBridge?.Audio == null) return;

			var stream = SkinBridge.Audio.GetBGM(musicId);
			if (stream != null)
			{
				if (_bgmPlayer.Stream == stream && _bgmPlayer.Playing) return;
				_bgmPlayer.Stream = stream;
				_bgmPlayer.Play();
			}
			else
			{
				_bgmPlayer.Stop();
			}
		}

		private void OnBgmFinished()
		{
			if (_bgmPlayer?.Stream != null) _bgmPlayer.Play();
		}

		/// <summary>若編輯器未建立 Music / SFX 巴士，則程式建立，供選項音量／開關使用。</summary>
		private void EnsureAudioBuses()
		{
			if (AudioServer.GetBusIndex("Music") < 0)
			{
				int idx = AudioServer.BusCount;
				AudioServer.AddBus(idx);
				AudioServer.SetBusName(idx, "Music");
			}
			if (AudioServer.GetBusIndex("SFX") < 0)
			{
				int idx = AudioServer.BusCount;
				AudioServer.AddBus(idx);
				AudioServer.SetBusName(idx, "SFX");
			}
		}

		/// <summary>依 ClientConfig 設定音樂／音效巴士的靜音與音量；OptionsWindow 變更設定後呼叫。</summary>
		public void ApplyAudioSettings()
		{
			int musicIdx = AudioServer.GetBusIndex("Music");
			int sfxIdx = AudioServer.GetBusIndex("SFX");
			if (musicIdx >= 0)
			{
				AudioServer.SetBusMute(musicIdx, !Client.Data.ClientConfig.MusicEnabled);
				AudioServer.SetBusVolumeDb(musicIdx, Client.Data.ClientConfig.MusicVolumeDb);
			}
			if (sfxIdx >= 0)
			{
				AudioServer.SetBusMute(sfxIdx, !Client.Data.ClientConfig.SoundEnabled);
				AudioServer.SetBusVolumeDb(sfxIdx, Client.Data.ClientConfig.SFXVolumeDb);
			}
		}

		// [新增] 統一的音效播放接口
		// 任何地方調用：Boot.Instance.PlayGlobalSound(145);
		public void PlayGlobalSound(int soundId)
		{
			if (soundId <= 0) return;

			var stream = _audioProvider.GetSound(soundId);
			if (stream != null)
			{
				_sfxPlayer.Stream = stream;
				_sfxPlayer.Play();
			}
		}

		// =====================================================================
		// 网络与数据逻辑 (核心修复区)
		// =====================================================================

		private void SetupNetwork()
		{
			_netSession = GetNodeOrNull<GodotTcpSession>("NetSession");
			if (_netSession == null) {
				_netSession = new GodotTcpSession();
				_netSession.Name = "NetSession";
				AddChild(_netSession);
			}

			_netSession.PacketReceived += OnNetworkDataReceived;
			_netSession.Connected += () => GD.Print("[NET] Connected!");
			_netSession.ConnectServer(Host, Port);
		}

			// --- 修复 EnsurePacketHandler 中的转发逻辑 ---
			private void EnsurePacketHandler()
					{
						_ph = GetNodeOrNull<PacketHandler>("PacketHandler");
						if (_ph == null) { _ph = new PacketHandler(); _ph.Name = "PacketHandler"; AddChild(_ph); }

			// 1. 登录结果
			_ph.LoginFailed += (reason) => {
				if (IsInCreateCharScene) EmitSignal(SignalName.CreateCharFailed, reason);
				else EmitSignal(SignalName.LoginFailed, reason);
			};
			
			_ph.LoginSuccess += () => {
				// 登录成功时，先清空列表，防止脏数据
				CharacterList.Clear();
				EmitSignal(SignalName.LoginSuccess);
			};

			// ====================================================================
			// [核心修复] 还原旧代码的 "Count -> Accumulate" 逻辑
			// 解决 "CharacterListReceived" 覆盖导致的列表丢失问题
			// ====================================================================
			
			// 阶段 A: 服务器通知角色数量 (Opcode 可能为 PacketBox 或专门的 Count 包)
			// 对应旧代码: _ph.OnLoginCharacterCount
			_ph.OnLoginCharacterCount += (count) => 
			{
				_expectedCharCount = count;
				CharacterList.Clear(); // 只在这里清空一次！
				GD.Print($"[Boot] 准备接收角色列表，期望数量: {count}");
				
				// 如果数量为0，直接通知更新（可能直接跳创建）
				if (count <= 0) EmitSignal(SignalName.CharacterListUpdated);
			};

			// 阶段 B: 收到单个角色数据 (Opcode S_OPCODE_CHARLIST 分包)
			// 对应旧代码: _ph.OnLoginCharacterItem
			// 注意：如果你的 PacketHandler 新版叫 CharacterInfoReceived，这里做桥接
			_ph.OnLoginCharacterItem += (info) =>
			{
				// 1. 查重
				bool exists = false;
				foreach(var c in CharacterList) {
					if (c.Name == info.Name) { exists = true; break; }
				}

				// 2. 累加 (Accumulate)
				if (!exists) {
					CharacterList.Add(info);
					GD.Print($"[Boot] 列表累加: {info.Name} (当前: {CharacterList.Count}/{_expectedCharCount})");
				}

				// 3. 通知 UI 单项更新 (可选)
				EmitSignal(SignalName.CharacterInfoReceived, info);
				
				// 4. 全局刷新通知
				// 只要收到数据就刷新 UI，确保用户能看到逐步加载的过程
				EmitSignal(SignalName.CharacterListUpdated);
			};

			// 創角成功：伺服器發 S_LoginFail(2) 後發 Opcode 5 (S_CharacterAdd)，PacketHandler 只發 CharacterInfoReceived 信號
			_ph.Connect(PacketHandler.SignalName.CharacterInfoReceived, Callable.From((Client.Data.CharacterInfo info) =>
			{
				if (IsInCreateCharScene)
				{
					CharacterList.Add(info);
					EmitSignal(SignalName.CharacterListUpdated);
				}
				EmitSignal(SignalName.CharacterInfoReceived, info);
			}));

			// [防御性代码] 如果 PacketHandler 同时也触发了 ListReceived (新版事件)
			// 我们只用它来兜底，绝对不允许它执行 Clear()
			_ph.CharacterListReceived += (list) => 
			{
				// 只有当我们的列表为空，且传入列表有效时才采纳
				// 避免被空的 List 包覆盖
				if (CharacterList.Count == 0 && list != null && list.Count > 0)
				{
					GD.Print("[Boot] 触发兜底 List 接收逻辑...");
					foreach(var item in list) CharacterList.Add(item);
					EmitSignal(SignalName.CharacterListUpdated);
				}
			};

			_ph.Connect(PacketHandler.SignalName.GameTimeReceived, Callable.From<int>(OnGameTimeReceived));
		}

		private void OnGameTimeReceived(int worldTimeSeconds)
		{
			WorldTimeSeconds = worldTimeSeconds;
		}


		public void OnNetworkDataReceived(byte[] data)
		{
			if (data == null || data.Length == 0) return;
			int opcode = data[0];

			// 【JP協議對齊】S_OPCODE_SERVERVERSION = 151
			if (opcode == 151 && !_handshakeCompleted)
			{
				_handshakeCompleted = true;
				// 仅在开发模式且开启直连时自动登录
				if (DevMode_DirectLogin) Action_Login(TestUser, TestPass);
			}

			if (_ph != null) _ph.HandlePacket(data);
		}

		private void OnLoginResult(bool success, string msg)
		{
			// 仅在开发模式下自动进游戏
			if (success && DevMode_DirectLogin && !_hasRequestedEnterWorld)
			{
				_hasRequestedEnterWorld = true;
				// 【修復】DevMode 直連時必須設定 CurrentCharName，否則 IsPlayerSelf 為 false，主角不會被識別、攝影機不跟隨、畫面看不到角色
				CurrentCharName = AutoLoginCharName;
				Client.Data.ClientConfig.SwitchCharacter(AutoLoginCharName);
				// 【修復】先設定名字，避免 CharacterList 尚未收到時 ChaWindow LVLabel1 顯示 unknown
				MyCharInfo.Name = AutoLoginCharName;
				// 從角色列表中同步 MyCharInfo 其餘欄位（若已收到列表）
				foreach (var c in CharacterList)
				{
					if (c.Name == AutoLoginCharName)
					{
						MyCharInfo.Name = c.Name;
						MyCharInfo.Level = c.Level;
						MyCharInfo.Type = c.Type;
						MyCharInfo.Sex = c.Sex;
						MyCharInfo.Str = c.Str; MyCharInfo.Dex = c.Dex; MyCharInfo.Con = c.Con;
						MyCharInfo.Wis = c.Wis; MyCharInfo.Cha = c.Cha; MyCharInfo.Int = c.Int;
						MyCharInfo.ClanName = c.ClanName;
						MyCharInfo.Lawful = c.Lawful;
						MyCharInfo.Ac = c.Ac;
						MyCharInfo.Exp = c.Exp;
						MyCharInfo.CurrentHP = c.CurrentHP; MyCharInfo.MaxHP = c.MaxHP;
						MyCharInfo.CurrentMP = c.CurrentMP; MyCharInfo.MaxMP = c.MaxMP;
						break;
					}
				}
				GetTree().CreateTimer(0.5f).Timeout += () => Action_EnterWorld(AutoLoginCharName);
			}
		}

		// =====================================================================
		// 协议发送
		// =====================================================================
		/// <summary>【JP協議對齊】登入封包 Opcode 57，對齊 jp C_AuthLogin.java：readS() 帳號、readS() 密碼，僅兩字串。</summary>
		public void Action_Login(string user, string pass)
		{
			if (!_handshakeCompleted) return;
			CharacterList.Clear();
			var w = new PacketWriter();
			w.WriteByte(57); // jp C_OPCODE_LOGINPACKET
			WritePacketString(w, user);
			WritePacketString(w, pass);
			SendPacket(w);
		}

		/// <summary>【JP協議對齊】請求角色數量/列表 Opcode 53 (C_OPCODE_COMMONCLICK)。</summary>
		public void Action_CommonClick()
		{
			if (!_handshakeCompleted) return;
			var w = new PacketWriter();
			w.WriteByte(53); // jp C_OPCODE_COMMONCLICK
			SendPacket(w);
		}

		// --- 【JP協議對齊】進入世界 (Op 131) ---
		public void Action_EnterWorld(string charName)
		{
			var w = new PacketWriter(); w.WriteByte(131); // jp C_OPCODE_LOGINTOSERVER
			WritePacketString(w, charName);
			SendPacket(w);
		}

		/// <summary>退出遊戲 (Op 15)。對齊伺服器 C_QuitGame：發送後伺服器會 lc.close() 關閉連線。</summary>
		public void Action_QuitGame()
		{
			var w = new PacketWriter();
			w.WriteByte(104); // C_OPCODE_QUITGAME
			SendPacket(w);
			GD.Print("[Boot] Sent Opcode 104 (QuitGame) -> 伺服器將關閉連線");
		}

		/// <summary>刪除角色 (Op 7)。發送後從本地列表移除並刷新選角 UI。</summary>
		public void Action_DeleteCharacter(string charName)
		{
			if (string.IsNullOrEmpty(charName)) return;
			byte[] data = C_CharacterDeletePacket.Make(charName);
			if (_netSession != null) _netSession.Send(data);
			for (int i = CharacterList.Count - 1; i >= 0; i--)
			{
				if (CharacterList[i].Name == charName)
				{
					CharacterList.RemoveAt(i);
					break;
				}
			}
			EmitSignal(SignalName.CharacterListUpdated);
		}

		// --- 核心：请求新角色类型 (Op 67) ---
		// 必须接受 int type，才能像 StartCreateCharSequence 那样发送 w.WriteByte((byte)type)
		public void Action_RequestNewChar(int type)
		{
			// 伺服器 (jp) 無對應 Op 67，避免送出未知封包
			GD.PrintRich($"[b][color=yellow]>>> [Boot] Skip Op 67 (no server opcode). Type: {type}[/color][/b]");
		}

		// --- 核心：创建角色提交 (Op 12) ---
		public void Action_CreateChar(string name, int type, int sex, int str, int dex, int con, int wis, int cha, int intel)
		{
			// [回归成功逻辑] 直接调用 Helper 类，确保属性顺序和名字编码正确
			// 只要 Op 67 成功了，这里的 Op 12 就一定能通过
			GD.PrintRich($"[b][color=yellow]>>> [Boot] (Op 12) 使用 C_CreateCharPacket 构建 (Name: {name})[/color][/b]");

			// 进行枚举强转
			byte[] packet = C_CreateCharPacket.Make(
				name,
				(C_CreateCharPacket.ClassType)type,
				(C_CreateCharPacket.SexType)sex,
				str, dex, con, wis, cha, intel
			);
			
			_netSession.Send(packet);
		}

		private void WritePacketString(PacketWriter w, string msg)
		{
			if (msg == null) msg = "";
			byte[] bytes = Encoding.GetEncoding("GBK").GetBytes(msg);
			foreach (var b in bytes) w.WriteByte(b); 
			w.WriteByte(0);
		}


		
		private void SendPacket(PacketWriter w)
		{
			// 这句正常可用。 下面是调试。如果不行，就开启这句，删除下面测试句
			// if (_netSession != null) _netSession.Send(w.GetBytes());


			if (_netSession != null) 
			{
				byte[] data = w.GetBytes();
				
				// [调试日志] 以十六进制格式打印所有发出的包，方便与服务器日志对比
				string hex = BitConverter.ToString(data).Replace("-", " ");
				int opcode = data.Length > 0 ? data[0] : -1;
				GD.PrintRich($"[DEBUG-NET] <<< [SEND] Opcode:{opcode} | Len:{data.Length} | Data: [color=cyan]{hex}[/color]");
				
				_netSession.Send(data);
			}
		}




		// =====================================================================
		// [SPR 系统初始化] —— 系统级，只允许在 Boot 调用一次
		// =====================================================================
		private void InitializeSpriteSystem()
		{
			GD.Print("[Boot] Initializing SPR system...");

			// 1. 素材 pak 由 AssetManager 在 _Ready 時載入 (Img182.pak)
			// 2. 加载 list.spr
			string listPath = "res://Assets/list.spr";

			if (!Godot.FileAccess.FileExists(listPath))
			{
				GD.PrintErr("[Boot] list.spr not found in any known path.");
				// 匯出環境（如 iOS）若未包含 list.spr，不拋出例外，讓流程能繼續到 Login
				if (!Engine.IsEditorHint())
				{
					GD.PrintErr("[Boot] Continuing without SPR (export build). Login may have limited graphics.");
					return;
				}
				throw new Exception("list.spr missing");
			}

			try
			{
				ListSprLoader.Load(listPath);
				GD.Print($"[Boot] list.spr loaded successfully: {listPath}");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[Boot][FATAL] list.spr load failed: {ex}");
				throw;
			}

			// 3. 可选：关键 gfxId 验证（防止运行期才炸）
			VerifySprDefinition(56);
			VerifySprDefinition(61);
		}

		private void VerifySprDefinition(int gfxId)
		{
			try
			{
				// 只要 Get 不抛异常，就说明存在
				var def = ListSprLoader.Get(gfxId);

				if (def == null)
				{
					GD.PrintErr($"[Boot][FATAL] SprDefinition is null for GfxId={gfxId}");
				}
				else
				{
					GD.Print($"[Boot] SprDefinition OK: GfxId={gfxId}");
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[Boot][FATAL] Missing SprDefinition for GfxId={gfxId} | {ex.Message}");
			}
		}


		// =====================================================================
		// [SPR 系统初始化] —— 系统级，只允许在 Boot 调用一次
		// =====================================================================
	




	}
}
