using Godot;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Client.Network
{
	public partial class GodotTcpSession : Node
	{
		[Signal] public delegate void ConnectedEventHandler();
		[Signal] public delegate void DisconnectedEventHandler();
		[Signal] public delegate void PacketReceivedEventHandler(byte[] data);
		[Signal] public delegate void LogEventHandler(string msg);

		private TcpClient _client;
		private NetworkStream _stream;
		private Thread _receiveThread;
		private volatile bool _isRunning = false;

		// 【JP協議對齊】使用 jp 服務器的 Cipher 加密算法
		private JpCipher _cipher;
		private List<byte> _receiveBuffer = new List<byte>();
		private bool _cipherInitialized = false; // 標記是否已從握手包中獲取 key

		public override void _Ready()
		{
			_cipher = null; // 等待握手包初始化
			SetProcess(false);
		}

		public void ConnectServer(string host, int port)
		{
			// 1. 安全断开旧连接
			if (_isRunning) Disconnect();

			try
			{
				GD.Print($"[Session] Connecting to {host}:{port}...");
				
				_client = new TcpClient();
				
				// 【优化】禁用延迟 (Nagle算法)，让包即时发送，防止粘包延迟
				_client.NoDelay = true;
				_client.ReceiveBufferSize = 8192;
				_client.SendBufferSize = 8192;

				// 同步连接
				_client.Connect(host, port);
				_stream = _client.GetStream();
				
				_isRunning = true;
				_cipher = null; // 【JP協議對齊】等待握手包初始化
				_cipherInitialized = false;
				_receiveBuffer.Clear();

				GD.Print("[Session] TCP Connected. Socket Configured.");
				EmitSignal(SignalName.Connected);

				// 启动接收线程
				_receiveThread = new Thread(ReceiveLoop);
				_receiveThread.IsBackground = true;
				_receiveThread.Start();
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[Session] Connection failed: {ex.Message}");
				// 如果是“拒绝连接”，通常意味着服务端没开
				Disconnect();
			}
		}

		private void ReceiveLoop()
		{
			// 增加一个小缓冲，防止 Thread 启动过快抢跑
			Thread.Sleep(50);
			
			byte[] buffer = new byte[8192];
			GD.Print("[Session] Receive Thread Started. Waiting for Server Greeting...");

			while (_isRunning)
			{
				try
				{
					if (_client == null || !_client.Connected || _stream == null)
					{
						break;
					}

					// 阻塞读取：只要有数据就读，没有就等，断开就返回 0
					int bytesRead = _stream.Read(buffer, 0, buffer.Length);
					
					if (bytesRead <= 0)
					{
						// 读到 0 字节，说明服务端 FIN 断开
						GD.PrintErr("[Session] Connection closed by Server (Read 0 bytes).");
						// 提示：这通常是因为服务端封禁 IP、报错 crash、或者握手超时
						break;
					}

					// 诊断：打印收到的原始字节数
					// GD.Print($"[Session] RAW RX: {bytesRead} bytes");

					byte[] chunk = new byte[bytesRead];
					Array.Copy(buffer, chunk, bytesRead);
					
					CallDeferred(nameof(ProcessData), chunk);
				}
				catch (System.IO.IOException)
				{
					// 网络中断
					break;
				}
				catch (Exception e)
				{
					if (_isRunning) GD.PrintErr($"[Session] Error: {e.Message}");
					break;
				}
			}
			
			Disconnect();
		}

		private void ProcessData(byte[] chunk)
		{
			if (!_isRunning) return;

			_receiveBuffer.AddRange(chunk);

			// 【JP協議對齊】循環解包 (2字節頭 + Body)
			// jp 服務器封包格式：writeH(length), writeC(opcode), [data...]
			// length 包含頭部 2 字節，所以 bodyLen = length - 2
			while (_receiveBuffer.Count >= 2)
			{
				int low = _receiveBuffer[0];
				int high = _receiveBuffer[1];
				int packetTotalLen = (low & 0xFF) | ((high & 0xFF) << 8);
				
				// 1. 長度校驗
				if (packetTotalLen <= 0 || packetTotalLen > 8192) 
				{
					// 異常數據，移除 1 字節嘗試重新對齊
					_receiveBuffer.RemoveAt(0);
					continue;
				}

				// 2. 數據不足
				if (_receiveBuffer.Count < packetTotalLen) break;

				// 3. 提取 Body
				int bodyLen = packetTotalLen - 2;
				byte[] body = new byte[bodyLen];
				
				if (bodyLen > 0)
				{
					_receiveBuffer.CopyTo(2, body, 0, bodyLen);
				}

				_receiveBuffer.RemoveRange(0, packetTotalLen);

				// 【JP協議對齊】處理握手包 S_OPCODE_INITPACKET (161)
				// 結構: writeC(161), writeC(key[0]), writeC(key[1]), writeC(key[2]), writeC(key[3]), [FIRST_PACKET 11 bytes]
				// 注意：握手包在加密前發送，所以這裡 body 是未加密的，不需要解密
				if (bodyLen > 0 && body[0] == 161 && !_cipherInitialized)
				{
					// 提取加密 key (4 bytes) - Little Endian
					if (bodyLen >= 5)
					{
						int key = (body[1] & 0xFF) | ((body[2] & 0xFF) << 8) 
								| ((body[3] & 0xFF) << 16) | ((body[4] & 0xFF) << 24);
						_cipher = new JpCipher(key);
						_cipherInitialized = true;
						GD.Print($"[Session] 【JP協議對齊】Cipher initialized with key: 0x{key:X8}");
						
						// 【JP協議對齊】發送客戶端版本封包 C_OPCODE_CLIENTVERSION (127)
						// 對齊 jp C_ServerVersion.java：只有 opcode，沒有其他數據
						// 【重要】封包需要至少 4 字節（JpCipher 要求），且需要填充到 4 的倍數
						// 使用 PacketWriter 確保封包格式正確
						PacketWriter writer = new PacketWriter();
						writer.WriteByte(127);
						byte[] clientVersionPacket = writer.GetBytes(); // 會自動填充到 4 的倍數
						Send(clientVersionPacket);
						GD.Print($"[Session] 【JP協議對齊】Sent C_OPCODE_CLIENTVERSION (127), packet length: {clientVersionPacket.Length}");
						
						// 跳過握手包，不發送給 PacketHandler（握手包不加密，且已處理完畢）
						continue;
					}
				}

				// 4. 解密（僅在 Cipher 初始化後，且不是握手包）
				if (_cipher != null && _cipherInitialized && bodyLen > 0)
				{
					_cipher.Decrypt(body);
				}

				EmitSignal(SignalName.PacketReceived, body);
			}
		}

		public void Send(byte[] rawBody)
		{
			if (!_isRunning || _stream == null) return;
			try
			{
				// 【JP協議對齊】對齊 jp ClientThread.readPacket()
				// 服務器讀取：dataLength = (loByte * 256 + hiByte) - 2
				// 然後讀取 dataLength 字節，最後調用 _cipher.decrypt(data)
				// 因此，我們需要：
				// 1. 加密 rawBody（不改變長度）
				// 2. 計算 totalLen = rawBody.Length + 2
				// 3. 發送 [totalLen低字節, totalLen高字節, 加密後的數據...]
				
				// 【重要】JpCipher.Encrypt() 會處理 < 4 字節的情況
				// 但我們需要確保加密後的長度與原始長度相同
				byte[] bodyToEncrypt = (byte[])rawBody.Clone();

				// 【JP協議對齊】使用 jp Cipher 加密
				if (_cipher != null && _cipherInitialized)
				{
					_cipher.Encrypt(bodyToEncrypt);
				}

				// 計算總長度：原始封包長度 + 2（長度字段）
				int totalLen = rawBody.Length + 2;
				byte[] finalPacket = new byte[totalLen];
				// 【JP協議對齊】對齊服務器：loByte * 256 + hiByte
				// 服務器使用 little-endian：loByte 是低字節，hiByte 是高字節
				finalPacket[0] = (byte)(totalLen & 0xFF);        // loByte (低字節)
				finalPacket[1] = (byte)((totalLen >> 8) & 0xFF); // hiByte (高字節)
				Array.Copy(bodyToEncrypt, 0, finalPacket, 2, rawBody.Length);

				_stream.Write(finalPacket, 0, finalPacket.Length);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[Session] Send Error: {ex.Message}");
				Disconnect();
			}
		}

		public void Disconnect()
		{
			if (!_isRunning) return;
			_isRunning = false;
			
			GD.Print("[Session] Disconnected.");
			try { _stream?.Close(); _client?.Close(); } catch { }
			_client = null; _stream = null;
			
			CallDeferred(nameof(EmitDisconnected));
		}

		private void EmitDisconnected()
		{
			EmitSignal(SignalName.Disconnected);
		}
	}
}
