using System;
using System.Diagnostics;
using Cassie;
using HarmonyLib;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interception;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;
using UnityEngine;

namespace HintIsolation.Core.Transport;

public static class NetworkSentinel
{
	private const string HintDisplayFrame = "Hints.HintDisplay";

	private const string DispatcherFrame = "Cassie.CassieAnnouncementDispatcher";

	public static bool Enabled { get; set; }

	public static bool Intercept { get; set; }

	public static long HintMessages { get; private set; }

	public static long BypassedHints { get; private set; }

	public static long AdoptedHints { get; private set; }

	public static long CassiePayloads { get; private set; }

	public static long BypassedCassie { get; private set; }

	public static long RpcMessages { get; private set; }

	public static long BypassedBroadcasts { get; private set; }

	public static long ByteStreamSends { get; private set; }

	public static long ByteStreamBytes { get; private set; }

	public static void Reset()
	{
		HintMessages = 0L;
		BypassedHints = 0L;
		AdoptedHints = 0L;
		CassiePayloads = 0L;
		BypassedCassie = 0L;
		RpcMessages = 0L;
		BypassedBroadcasts = 0L;
		ByteStreamSends = 0L;
		ByteStreamBytes = 0L;
	}

	internal static bool OnHintMessage(NetworkConnection? conn, HintMessage message)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (!Enabled)
		{
			return true;
		}
		try
		{
			HintMessages++;
			Hint content = message.Content;
			if (content == null)
			{
				return true;
			}
			if (HintBroker.Instance.IsOwnMergedHint(content))
			{
				return true;
			}
			if (CallStackHasHintDisplayShow())
			{
				return true;
			}
			CallerInfo callerInfo = PluginCallerResolver.Resolve();
			if (!callerInfo.IsPlugin)
			{
				return true;
			}
			BypassedHints++;
			if (!Intercept)
			{
				return true;
			}
			ReferenceHub val = ResolveHub(conn);
			if (val == null)
			{
				return true;
			}
			UiId id = UiIdRegistry.ResolveRoute(UiIdRegistry.Resolve(UiSurface.Hint, callerInfo.Assembly ?? typeof(NetworkSentinel).Assembly, callerInfo.Method, callerInfo.IlOffset));
			string text = ExtractText(content);
			if (text == null || text.Length == 0)
			{
				return true;
			}
			UiSlotRegistry.GetOrCreate(id, UiIdRegistry.DisplayNameOf(id), 128, showLabel: false, 1, 10f).Push(text, Time.time, ((DisplayableObject<SharedHintData>)(object)content).DurationScalar);
			HintBroker.Instance.MarkDirty(val);
			AdoptedHints++;
			return false;
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 网络哨兵处理 HintMessage 异常(已放行): " + ex.Message));
			return true;
		}
	}

	internal static bool OnCassiePayload(NetworkConnection? conn, CassieTtsPayload payload)
	{
		if (!Enabled)
		{
			return true;
		}
		try
		{
			CassiePayloads++;
			if (!CallStackHas("Cassie.CassieAnnouncementDispatcher"))
			{
				BypassedCassie++;
			}
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 网络哨兵处理 CassieTtsPayload 异常(已放行): " + ex.Message));
		}
		return true;
	}

	internal static bool OnRpcMessage(NetworkConnection? conn, RpcMessage message)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (!Enabled)
		{
			return true;
		}
		try
		{
			RpcMessages++;
			Broadcast singleton = Broadcast.Singleton;
			if (singleton != null && ((NetworkBehaviour)singleton).netId == message.netId && ((NetworkBehaviour)singleton).ComponentIndex == message.componentIndex)
			{
				BypassedBroadcasts++;
			}
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 网络哨兵处理 RpcMessage 异常(已放行): " + ex.Message));
		}
		return true;
	}

	internal static bool OnByteStream(int byteCount)
	{
		if (!Enabled)
		{
			return true;
		}
		try
		{
			ByteStreamSends++;
			ByteStreamBytes += byteCount;
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 网络哨兵处理字节流异常(已放行): " + ex.Message));
		}
		return true;
	}

	private static bool CallStackHasHintDisplayShow()
	{
		return CallStackHas("Hints.HintDisplay");
	}

	private static bool CallStackHas(string typeName)
	{
		try
		{
			StackTrace stackTrace = new StackTrace(2, fNeedFileInfo: false);
			for (int i = 0; i < stackTrace.FrameCount; i++)
			{
				Type type = stackTrace.GetFrame(i)?.GetMethod()?.DeclaringType;
				if ((object)type != null && type.FullName == typeName)
				{
					return true;
				}
			}
		}
		catch
		{
			return true;
		}
		return false;
	}

	private static ReferenceHub? ResolveHub(NetworkConnection? conn)
	{
		try
		{
			NetworkIdentity val = ((conn != null) ? conn.identity : null);
			return (val == null) ? null : ((Component)val).GetComponent<ReferenceHub>();
		}
		catch
		{
			return null;
		}
	}

	private static string? ExtractText(Hint hint)
	{
		TextHint val = (TextHint)(object)((hint is TextHint) ? hint : null);
		if (val == null)
		{
			return null;
		}
		try
		{
			return AccessTools.Property(typeof(TextHint), "Text")?.GetValue(val) as string;
		}
		catch
		{
			return null;
		}
	}
}
