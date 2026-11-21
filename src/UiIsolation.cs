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

	/// <summary>
	/// 用<b>自有写法的字符串语法</b>设定位置 —— 与文本标记 <c>{{hc:...}}</c> 是<b>同一套语法</b>,
	/// 因插件配置项、文本标记、C# 代码三处可以通用同一个字符串。
	/// <para>例: <c>UiIsolation.SetHintPosition("MyPlugin", "pos=750,align=left");</c></para>
	/// </summary>
	/// <returns>被立即改写的已存在信口数; <c>-1</c> = 字符串无法识别(不会改动任何信口)。</returns>
	public static int SetHintPosition(string pluginId, string spec)
	{
		return Service.SetHintPosition(pluginId, spec);
	}

	/// <summary>撤销一个插件的预设位置, 回到「已收录表 / 功能区推断 / 默认」的自动链路。</summary>
	public static bool ClearHintPosition(string pluginId)
	{
		return Service.ClearHintPosition(pluginId);
	}

	/// <summary>
	/// <b>新 Hint 写法(Beta / 零配置)</b> —— 一条调用完成「建信口 + 设位置 + 发文本」,
	/// <b>不需要修改任何配置文件</b>, 也不会与其它 Hint 插件冲突。
	/// <para>例: <c>UiIsolation.SendHint("MyPlugin", "你好", 3f, "top-right");</c></para>
	/// <para>内部会为该插件分配独立信口; 同一个 pluginId 反复调用只会更新内容, 不会堆叠。</para>
	/// </summary>
	/// <param name="moduleId">插件标识(建议用程序集名)。</param>
	/// <param name="text">提示文本(支持 TMP 富文本)。</param>
	/// <param name="duration">保留秒数。</param>
	/// <param name="positionSpec">可选位置字符串, 语法同 <c>{{hc:...}}</c>, 如 <c>top-right</c> / <c>pos=750,align=left</c>。</param>
	/// <param name="priority">信口优先级(越小越靠上)。</param>
	public static IHintChannel? SendHint(string moduleId, string text, float duration = 2f,
		string? positionSpec = null, byte priority = 128)
	{
		if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrEmpty(text))
		{
			return null;
		}
		if (!string.IsNullOrWhiteSpace(positionSpec))
		{
			SetHintPosition(moduleId, positionSpec);
		}

		IHintChannel? channel = GetHintChannel(moduleId);
		if (channel == null)
		{
			channel = RegisterHintChannel(moduleId, moduleId, text, duration, priority);
		}
		else
		{
			channel.SetText(text);
			channel.Duration = duration;
		}
		return channel;
	}

	/// <summary>
	/// <b>新 Hint 写法(Beta / 零配置)</b> —— 用九宫格锚点代替位置字符串。
	/// <para>例: <c>UiIsolation.SendHint("MyPlugin", "你好", HintAnchor.TopRight, 0f, 3f);</c></para>
	/// </summary>
	public static IHintChannel? SendHint(string moduleId, string text, HintAnchor anchor,
		float offsetUnits = 0f, float duration = 2f, byte priority = 128)
	{
		SetHintPosition(moduleId, anchor, offsetUnits);
		return SendHint(moduleId, text, duration, null, priority);
	}

	/// <summary>清除一个零配置信口的文本(信口本身保留, 位置不会丢)。</summary>
	public static bool ClearHint(string moduleId)
	{
		IHintChannel? channel = GetHintChannel(moduleId);
		if (channel == null)
		{
			return false;
		}
		channel.SetText(string.Empty);
		return true;
	}
}
