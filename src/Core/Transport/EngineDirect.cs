using System;
using Cassie;
using HintChorus.Core.Surfaces;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;
using Utils.Networking;

namespace HintChorus.Core.Transport;

public static class EngineDirect
{
	private static bool TryGetConnection(ReferenceHub? hub, out NetworkConnection connection)
	{
		connection = null;
		try
		{
			if (hub == null)
			{
				return false;
			}
			NetworkIdentity netIdentity = ((NetworkBehaviour)hub).netIdentity;
			NetworkConnection val = (NetworkConnection)(object)((netIdentity != null) ? netIdentity.connectionToClient : null);
			if (val == null)
			{
				return false;
			}
			connection = val;
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warn((object)("[HintChorus] 取玩家连接失败: " + ex.Message));
			return false;
		}
	}

	public static bool SendHint(ReferenceHub? hub, Hint hint)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (hint == null || !TryGetConnection(hub, out NetworkConnection connection))
		{
			return false;
		}
		try
		{
			if (HintDisplay.SuppressedReceivers.Contains(connection))
			{
				return true;
			}
			connection.Send<HintMessage>(new HintMessage(hint), 0);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warn((object)("[HintChorus] 提示条引擎直连失败: " + ex.Message));
			return false;
		}
	}

	public static bool SendConsole(ReferenceHub? hub, string text, string color = "green")
	{
		if (hub == null || string.IsNullOrEmpty(text))
		{
			return false;
		}
		try
		{
			return EncryptedChannelManager.TrySendMessageToClient(hub, color + "#" + text, (EncryptedChannelManager.EncryptedChannel)1);
		}
		catch (Exception ex)
		{
			Logger.Warn((object)("[HintChorus] 控制台引擎直连失败: " + ex.Message));
			return false;
		}
	}

	public static bool SendCassie(string message, string subtitles = "", bool playBackground = true)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(message))
		{
			return false;
		}
		try
		{
			NetworkUtils.SendToAuthenticated<CassieTtsPayload>(new CassieTtsPayload(message, subtitles, playBackground), 0);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warn((object)("[HintChorus] CASSIE 引擎直连失败: " + ex.Message));
			return false;
		}
	}

	public static bool SendAdminChat(ReferenceHub? hub, string content)
	{
		if (hub == null || string.IsNullOrEmpty(content))
		{
			return false;
		}
		AdminChatSurfaceIsolation.SelfSending = true;
		try
		{
			return EncryptedChannelManager.TrySendMessageToClient(hub, content, (EncryptedChannelManager.EncryptedChannel)2);
		}
		catch (Exception ex)
		{
			Logger.Warn((object)("[HintChorus] 管理端聊天直连失败: " + ex.Message));
			return false;
		}
		finally
		{
			AdminChatSurfaceIsolation.SelfSending = false;
		}
	}

	public static bool SendHitMarker(ReferenceHub? hub, float size = 1f)
	{
		if (hub == null)
		{
			return false;
		}
		HitMarkerSurfaceIsolation.SelfSending = true;
		try
		{
			Hitmarker.SendHitmarkerDirectly(hub, size, true, (HitmarkerType)1);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warn((object)("[HintChorus] 命中标记直连失败: " + ex.Message));
			return false;
		}
		finally
		{
			HitMarkerSurfaceIsolation.SelfSending = false;
		}
	}

	public static bool SendBroadcast(string data, ushort time, Broadcast.BroadcastFlags flags = (Broadcast.BroadcastFlags)0, ReferenceHub? onlyFor = null)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(data))
		{
			return false;
		}
		BroadcastSurfaceIsolation.EnterSelfSend();
		try
		{
			Broadcast singleton = Broadcast.Singleton;
			if (singleton == null)
			{
				return false;
			}
			if (onlyFor == null)
			{
				singleton.RpcAddElement(data, time, flags);
				return true;
			}
			if (!TryGetConnection(onlyFor, out NetworkConnection connection))
			{
				return false;
			}
			singleton.TargetAddElement(connection, data, time, flags);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warn((object)("[HintChorus] 广播下发失败: " + ex.Message));
			return false;
		}
		finally
		{
			BroadcastSurfaceIsolation.ExitSelfSend();
		}
	}
}
