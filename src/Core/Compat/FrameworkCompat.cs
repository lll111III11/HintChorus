using System;
using System.Collections.Generic;
using System.Reflection;
using HintChorus.Core.Enums;

namespace HintChorus.Core.Compat;

public static class FrameworkCompat
{
	private const string LabApiAssembly = "LabApi";

	private const string ExiledAssemblyPrefix = "Exiled.";

	public static IReadOnlyList<FrameworkRoute> Routes { get; } = new FrameworkRoute[19]
	{
		new FrameworkRoute(UiFramework.LabApi, UiSurface.Hint, "Player.SendHint(text, duration) ×3 重载", "HintDisplay.Show", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Hint, "Player.ShowHint(text, duration) / ShowHint(Hint)", "HintDisplay.Show", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.Broadcast, "Player.SendBroadcast(...) / Server.SendBroadcast(...)", "Broadcast.TargetAddElement / RpcAddElement", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Broadcast, "Player.Broadcast(duration, message, type, shouldClearPrevious)", "Broadcast.TargetAddElement", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Broadcast, "Map.Broadcast(duration, message, type, shouldClearPrevious)", "Broadcast.RpcAddElement", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.Broadcast, "Player.ClearBroadcasts() / Server.ClearBroadcasts()", "Broadcast.TargetClearElements / RpcClearElements", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Broadcast, "Player.ClearBroadcasts() / Map.ClearBroadcasts()", "Broadcast.TargetClearElements / RpcClearElements", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.Console, "Player.SendConsoleMessage(message, color)", "GameConsoleTransmission.SendToClient", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Console, "Player.SendConsoleMessage(message, color)", "GameConsoleTransmission.SendToClient", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.AdminChat, "Server.SendAdminChatMessage(message, isSilent)", "EncryptedChannelManager.EncryptedChannel.AdminChat", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.Cassie, "Announcer.Message(...) / GlitchyMessage(...) / ScpTermination(...)", "CassieAnnouncementDispatcher.AddToQueue", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Cassie, "Cassie.Message(...) / MessageTranslated(...) / GlitchyMessage(...)", "CassieAnnouncementDispatcher.AddToQueue", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.Cassie, "Announcer.Clear()", "CassieAnnouncementDispatcher.ClearAll", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Cassie, "Cassie.Clear()", "CassieAnnouncementDispatcher.ClearAll", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.ServerSettings, "SSPlaintextSetting / SSTextArea / SSButton / ...", "ServerSpecificSettingsSync.DefinedSettings", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.ServerSettings, "Exiled.API.Features.Core.UserSettings.* (最终也走原生 SSS, 含按玩家推送的集合重载)", "ServerSpecificSettingsSync.DefinedSettings + SendToPlayer(collection) 盖参数", Covered: true),
		new FrameworkRoute(UiFramework.LabApi, UiSurface.HitMarker, "Player.SendHitMarker(size)", "Hitmarker.SendHitmarkerDirectly", Covered: true),
		new FrameworkRoute(UiFramework.Exiled, UiSurface.Intercom, "Intercom.DisplayText = value", "IntercomDisplay.SerializeSyncVars(底层复写)", Covered: true),
		new FrameworkRoute(UiFramework.RueI, UiSurface.Hint, "RueI: Patches.AspectRatioPatch (Postfix, 玩家改宽高比后 RueDisplay.Update() 重排)", "AspectRatioSync.UserCode_CmdSetAspectRatio__Single(float)", Covered: true)
	};

	public static IReadOnlyList<FrameworkRoute> Uncovered
	{
		get
		{
			List<FrameworkRoute> list = new List<FrameworkRoute>();
			foreach (FrameworkRoute route in Routes)
			{
				if (!route.Covered)
				{
					list.Add(route);
				}
			}
			return list;
		}
	}

	public static UiFramework Detect(Assembly? assembly)
	{
		if ((object)assembly == null)
		{
			return UiFramework.Native;
		}
		return Detect(assembly.GetName().Name);
	}

	public static UiFramework Detect(string? assemblyName)
	{
		if (assemblyName == null || assemblyName.Length == 0)
		{
			return UiFramework.Native;
		}
		if (assemblyName.Equals("LabApi", StringComparison.OrdinalIgnoreCase))
		{
			return UiFramework.LabApi;
		}
		if (assemblyName.StartsWith("Exiled.", StringComparison.OrdinalIgnoreCase))
		{
			return UiFramework.Exiled;
		}
		// 枚举里本来就有 RueI, 但这里以前没登记它 —— 于是 RueI 的调用会被判成 Unknown,
		// 诊断报告与归因标签都会显示错的框架名。
		if (assemblyName.Equals("RueI", StringComparison.OrdinalIgnoreCase)
			|| assemblyName.StartsWith("RueI.", StringComparison.OrdinalIgnoreCase))
		{
			return UiFramework.RueI;
		}
		return UiFramework.Unknown;
	}
}
