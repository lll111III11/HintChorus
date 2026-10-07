using System;
using System.Collections.Generic;
using HintChorus.Core.Enums;
using HintChorus.Core.Layout;
using HintChorus.Core.Models;
using UserSettings.ServerSpecific;

namespace HintChorus.Core.Interfaces;

public interface IUiIsolation
{
	bool IsSssForceRewriteEnabled { get; }

	long SssForceRewriteCount { get; }

	bool IsThirdPartyInterceptionEnabled { get; }

	IUiInterception Interception { get; }

	IHintBroker Broker { get; }

	IReadOnlyList<string> HintChannels { get; }

	IReadOnlyList<string> HintSources { get; }

	IReadOnlyList<string> SssPorts { get; }

	IReadOnlyList<string> AttributedPlugins { get; }

	IReadOnlyList<string> UiIds { get; }

	IHintChannel? RegisterHintChannel(string moduleId, string displayName, string text = "", float duration = 2f, byte priority = 128);

	bool UnregisterHintChannel(string moduleId);

	IHintChannel? GetHintChannel(string moduleId);

	bool RegisterHintSource(IHintTextSource source);

	bool UnregisterHintSource(string moduleId);

	void ShowTransient(ReferenceHub hub, string text, float duration = 3f);

	RegisterResult RegisterSssPorts(string moduleId, IEnumerable<ServerSpecificSettingBase> settings);

	bool UnregisterSssPorts(string moduleId);

	void SubscribeSssValue(string moduleId, Action<ReferenceHub, ServerSpecificSettingBase> handler);

	void UnsubscribeSssValue(Action<ReferenceHub, ServerSpecificSettingBase> handler);

	void SubscribeSssStatus(string moduleId, Action<ReferenceHub, SSSUserStatusReport> handler);

	void UnsubscribeSssStatus(Action<ReferenceHub, SSSUserStatusReport> handler);

	void EnableSssForceRewrite();

	void DisableSssForceRewrite();

	bool ForceRewriteSettings();

	Action WrapEvent(string moduleId, Action handler);

	Action<T1> WrapEvent<T1>(string moduleId, Action<T1> handler);

	Action<T1, T2> WrapEvent<T1, T2>(string moduleId, Action<T1, T2> handler);

	Action<T1, T2, T3> WrapEvent<T1, T2, T3>(string moduleId, Action<T1, T2, T3> handler);

	void EnableThirdPartyInterception();

	void DisableThirdPartyInterception();

	UiScope CreateScope(string moduleId);

	IReadOnlyList<IUiSlot> GetSlots(UiSurface? surface = null, string? pluginId = null);

	/// <summary>
	/// <b>给一个插件预设屏幕位置</b>(「自有写法」的编程通道)。
	/// <para>插件无需在文本里写标记, 直接声明自己的落点即可。对已存在的信口立即生效,
	/// 对之后才创建的信口在创建时自动套用 —— 因此可以在插件加载时先调用一次。</para>
	/// </summary>
	/// <param name="pluginId">插件标识(与归因得到的 PluginId 一致, 通常是程序集名)。</param>
	/// <param name="anchor">九宫格锚点。</param>
	/// <param name="offsetUnits">附加偏移(voffset 单位, <b>正 = 上移</b>; 参考: 整屏约 2140)。</param>
	/// <returns>被立即改写的已存在信口数(之后新建的会另行套用)。</returns>
	int SetHintPosition(string pluginId, HintAnchor anchor, float offsetUnits = 0f);

	/// <summary>撤销一个插件的预设位置, 回到自动推断。</summary>
	bool ClearHintPosition(string pluginId);
}
