using System;
using System.Collections.Generic;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Interfaces;
using HintChorus.Core.Layout;
using HintChorus.Core.Models;
using HintChorus.Core.Surfaces;
using HintChorus.Core.Transport;
using Hints;
using LabApi.Features.Wrappers;
using UserSettings.ServerSpecific;

namespace HintChorus;

public static class UiIsolation
{
	public static IUiIsolation Service { get; } = new UiIsolationService();

	public static IHintBroker Broker => Service.Broker;

	public static IUiInterception Interception => Service.Interception;

	public static IUiIdCatalog UiIds => UiIdCatalog.Instance;

	public static IReadOnlyList<UiSurfaceDescriptor> Surfaces => UiSurfaceRegistry.All;

	public static bool IsSssForceRewriteEnabled => Service.IsSssForceRewriteEnabled;

	public static long SssForceRewriteCount => Service.SssForceRewriteCount;

	public static bool IsThirdPartyInterceptionEnabled => Service.IsThirdPartyInterceptionEnabled;

	public static IReadOnlyList<string> HintChannels => Service.HintChannels;

	public static IReadOnlyList<string> HintSources => Service.HintSources;

	public static IReadOnlyList<string> SssPorts => Service.SssPorts;

	public static IReadOnlyList<string> AttributedPlugins => Service.AttributedPlugins;

	public static IHintChannel? RegisterHintChannel(string moduleId, string displayName, string text = "", float duration = 2f, byte priority = 128)
	{
		return Service.RegisterHintChannel(moduleId, displayName, text, duration, priority);
	}

	public static bool UnregisterHintChannel(string moduleId)
	{
		return Service.UnregisterHintChannel(moduleId);
	}

	public static IHintChannel? GetHintChannel(string moduleId)
	{
		return Service.GetHintChannel(moduleId);
	}

	public static bool RegisterHintSource(IHintTextSource source)
	{
		return Service.RegisterHintSource(source);
	}

	public static bool UnregisterHintSource(string moduleId)
	{
		return Service.UnregisterHintSource(moduleId);
	}

	public static void ShowTransient(ReferenceHub hub, string text, float duration = 3f)
	{
		Service.ShowTransient(hub, text, duration);
	}

	public static void ShowTransient(Player player, string text, float duration = 3f)
	{
		if (player != null)
		{
			Service.ShowTransient(player.ReferenceHub, text, duration);
		}
	}

	public static RegisterResult RegisterSssPorts(string moduleId, IEnumerable<ServerSpecificSettingBase> settings)
	{
		return Service.RegisterSssPorts(moduleId, settings);
	}

	public static bool UnregisterSssPorts(string moduleId)
	{
		return Service.UnregisterSssPorts(moduleId);
	}

	public static void SubscribeSssValue(string moduleId, Action<ReferenceHub, ServerSpecificSettingBase> handler)
	{
		Service.SubscribeSssValue(moduleId, handler);
	}

	public static void UnsubscribeSssValue(Action<ReferenceHub, ServerSpecificSettingBase> handler)
	{
		Service.UnsubscribeSssValue(handler);
	}

	public static void SubscribeSssStatus(string moduleId, Action<ReferenceHub, SSSUserStatusReport> handler)
	{
		Service.SubscribeSssStatus(moduleId, handler);
	}

	public static void UnsubscribeSssStatus(Action<ReferenceHub, SSSUserStatusReport> handler)
	{
		Service.UnsubscribeSssStatus(handler);
	}

	public static void EnableSssForceRewrite()
	{
		Service.EnableSssForceRewrite();
	}

	public static void DisableSssForceRewrite()
	{
		Service.DisableSssForceRewrite();
	}

	public static bool ForceRewriteSettings()
	{
		return Service.ForceRewriteSettings();
	}

	public static Action WrapEvent(string moduleId, Action handler)
	{
		return Service.WrapEvent(moduleId, handler);
	}

	public static Action<T1> WrapEvent<T1>(string moduleId, Action<T1> handler)
	{
		return Service.WrapEvent(moduleId, handler);
	}

	public static Action<T1, T2> WrapEvent<T1, T2>(string moduleId, Action<T1, T2> handler)
	{
		return Service.WrapEvent(moduleId, handler);
	}

	public static Action<T1, T2, T3> WrapEvent<T1, T2, T3>(string moduleId, Action<T1, T2, T3> handler)
	{
		return Service.WrapEvent(moduleId, handler);
	}

	public static void EnableThirdPartyInterception()
	{
		Service.EnableThirdPartyInterception();
	}

	public static void DisableThirdPartyInterception()
	{
		Service.DisableThirdPartyInterception();
	}

	public static UiScope CreateScope(string moduleId)
	{
		return Service.CreateScope(moduleId);
	}

	public static bool SendHintDirect(ReferenceHub hub, string text, float duration = 3f)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected Obj, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected Obj, but got Unknown
		if (hub == null || string.IsNullOrEmpty(text))
		{
			return false;
		}
		return EngineDirect.SendHint(hub, (Hint)new TextHint(text, new HintParameter[1] { (HintParameter)new StringHintParameter(string.Empty) }, (HintEffect[])null, duration));
	}

	public static bool SendHintDirect(Player player, string text, float duration = 3f)
	{
		if (player != null)
		{
			return SendHintDirect(player.ReferenceHub, text, duration);
		}
		return false;
	}

	public static bool SendConsoleDirect(ReferenceHub hub, string text, string color = "green")
	{
		return EngineDirect.SendConsole(hub, text, color);
	}

	public static bool SendConsoleDirect(Player player, string text, string color = "green")
	{
		if (player != null)
		{
			return EngineDirect.SendConsole(player.ReferenceHub, text, color);
		}
		return false;
	}

	public static bool SendCassie(string message, string subtitles = "", bool playBackground = true)
	{
		return EngineDirect.SendCassie(message, subtitles, playBackground);
	}

	public static bool SendBroadcastDirect(string text, ushort duration = 5, ReferenceHub? onlyFor = null)
	{
		return EngineDirect.SendBroadcast(text, duration, (Broadcast.BroadcastFlags)0, onlyFor);
	}

	public static bool SendAdminChatDirect(ReferenceHub hub, string content)
	{
		return EngineDirect.SendAdminChat(hub, content);
	}

	public static bool SendHitMarkerDirect(ReferenceHub hub, float size = 1f)
	{
		return EngineDirect.SendHitMarker(hub, size);
	}

	public static string CreateDiagnosticsReport()
	{
		return HintChorusDiagnostics.CreateReport();
	}

	public static HintStatistics CaptureStatistics()
	{
		return HintChorusDiagnostics.Capture();
	}

	public static IReadOnlyList<IUiSlot> GetSlots(UiSurface? surface = null, string? pluginId = null)
	{
		return Service.GetSlots(surface, pluginId);
	}

	/// <summary>
	/// <b>给一个插件预设屏幕位置</b>(「自有写法」的编程通道)。
	/// <para>插件可以完全不写位置标记, 直接声明自己的落点。对<b>已存在</b>的信口立即生效,
	/// 对<b>之后才创建</b>的信口在创建时自动套用 —— 所以在插件加载阶段先调用一次即可。</para>
	/// <para>典型用法: <c>UiIsolation.SetHintPosition("MyPlugin", HintAnchor.MiddleCenter, -90f);</c></para>
	/// </summary>
	/// <param name="pluginId">插件标识(与归因得到的 PluginId 一致, 通常是程序集名)。</param>
	/// <param name="anchor">九宫格锚点。</param>
	/// <param name="offsetUnits">附加偏移(voffset 单位, <b>正 = 上移</b>; 参考: 整屏约 2140)。</param>
	/// <returns>被立即改写的已存在信口数。</returns>
	public static int SetHintPosition(string pluginId, HintAnchor anchor, float offsetUnits = 0f)
	{
		return Service.SetHintPosition(pluginId, anchor, offsetUnits);
	}

	/// <summary>撤销一个插件的预设位置, 回到「已收录表 / 功能区推断 / 默认」的自动链路。</summary>
	public static bool ClearHintPosition(string pluginId)
	{
		return Service.ClearHintPosition(pluginId);
	}
}
