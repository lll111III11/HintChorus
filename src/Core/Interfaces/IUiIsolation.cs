using System;
using System.Collections.Generic;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Models;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.Interfaces;

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
}
