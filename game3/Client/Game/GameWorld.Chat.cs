// ============================================================================
// [FILE] GameWorld.Chat.cs
// [说明] 处理聊天输入与系统指令。包含你要求的可视化测试工具 (.test)。
// ============================================================================
using Godot;
using System;
using Client.Network;

namespace Client.Game
{
	// =========================================================================
	// [FILE] GameWorld.Chat.cs
	// 说明：聊天发送（HUD.ChatSubmitted -> GameWorld.SendChatPacket -> NetSession.Send）。
	// 1. HandleChatInput: 统一入口，分流指令与普通聊天。
	// 2. HandleDebugCommand: 处理本地测试指令 (.test)。
	// 3. SendChatPacket: 发送网络封包。
	// =========================================================================
	public partial class GameWorld
	{
		// =====================================================================
		// [SECTION] Chat Input Entry (输入入口)
		// =====================================================================

		/// <summary>聊天入口：密語 /w 或 !w 發送 C_122，其餘發送 C_OPCODE_CHAT(190)。</summary>
		public void HandleChatInput(string text)
		{
			if (string.IsNullOrEmpty(text)) return;

			string trimmed = text.TrimStart();
			if (trimmed.StartsWith("/w ") || trimmed.StartsWith("!w "))
			{
				string rest = trimmed.Substring(3).TrimStart();
				int firstSpace = rest.IndexOf(' ');
				if (firstSpace > 0)
				{
					string targetName = rest.Substring(0, firstSpace);
					string msg = rest.Substring(firstSpace + 1);
					if (!string.IsNullOrEmpty(targetName) && !string.IsNullOrEmpty(msg))
					{
						SendWhisperPacket(targetName, msg);
						return;
					}
				}
				AddSystemMessage("[color=yellow]密語格式: /w 角色名 內容 或 !w 角色名 內容[/color]");
				return;
			}

			if (trimmed.StartsWith("/exclude ") || trimmed.StartsWith("/block "))
			{
				string prefix = trimmed.StartsWith("/exclude ") ? "/exclude " : "/block ";
				string name = trimmed.Substring(prefix.Length).Trim();
				if (!string.IsNullOrEmpty(name))
				{
					SendExclude(name);
					AddSystemMessage($"[color=green]已將 {name} 加入拒絕名單。[/color]");
					return;
				}
				AddSystemMessage("[color=yellow]格式: /exclude 角色名 或 /block 角色名[/color]");
				return;
			}

			SendChatPacket(text);
		}

		private void SendWhisperPacket(string targetName, string text)
		{
			if (_netSession == null) return;
			_netSession.Send(C_ChatWhisperPacket.Make(targetName, text));
		}

		// =====================================================================
		// [SECTION] Debug Tools (.test Visualization)
		// =====================================================================

		// =====================================================================
		// [SECTION] Chat Send: SendChatPacket (客户端发送聊天封包)
		// 说明：Opcode=19 (C_Chatting)；支持普通/大喊前缀。
		// =====================================================================
		private void SendChatPacket(string text)
		{
			if (_netSession == null) return;

			var w = new PacketWriter();

			// 1) 写入头：Opcode 190 (C_OPCODE_CHAT)
			w.WriteByte(190);

			// 2) 写入频道类型
			int type = 0; // 0=普通
			if (text.StartsWith("!"))
			{
				type = 2; // 2=大喊
				text = text.Substring(1);
			}
			w.WriteByte(type);

			// 3) 写入字符串 (GBK 编码) + 0 结尾
			byte[] bytes = System.Text.Encoding.GetEncoding("GBK").GetBytes(text);
			foreach (var b in bytes) w.WriteByte(b);
			w.WriteByte(0);

			_netSession.Send(w.GetBytes());
		}

		/// <summary>發送客戶端選項封包 (Opcode 13 = C_ClientOption)。type=3 為怪物血條開關，伺服器依此設定 hpBar 並決定是否發送 104。</summary>
		public void SendClientOption(int type, int onoff)
		{
			// jp 伺服器未定義 Opcode 13；不發送未知封包，僅本地生效
			GD.Print($"[ClientOption] Skip sending (no server opcode). type={type} onoff={onoff}");
		}
		// =====================================================================
		// [SECTION END] Chat Send: SendChatPacket
		// =====================================================================
	}
}
