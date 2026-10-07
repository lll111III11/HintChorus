using System;
using System.Collections.Generic;
using System.Linq;
using HintChorus.Core.Broker;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Interception;
using HintChorus.Core.Interfaces;
using HintChorus.Core.Layout;
using HintChorus.Core.Models;
using HintChorus.Core.ServerSpecific;
using HintChorus.Core.Utilities;
using UserSettings.ServerSpecific;

namespace HintChorus;

internal sealed class UiIsolationService : IUiIsolation
{
	public IHintBroker Broker => HintBroker.Instance;

	public IUiInterception Interception => UiInterception.Instance;

	public bool IsSssForceRewriteEnabled => SssForceRewrite.IsInstalled;

	public long SssForceRewriteCount => SssForceRewrite.ForcedCount;

	public bool IsThirdPartyInterceptionEnabled => UiInterception.Instance.IsInstalled;

	public IReadOnlyList<string> HintChannels => HintBroker.Instance.Channels.Select((IHintChannel c) => c.ModuleId).ToArray();

	public IReadOnlyList<string> HintSources => HintBroker.Instance.Sources.Select((IHintTextSource s) => s.ModuleId).ToArray();

	public IReadOnlyList<string> SssPorts => SssRegistry.RegisteredModules;

	public IReadOnlyList<string> AttributedPlugins => UiInterception.Instance.AttributedPlugins.ToArray();

	public IReadOnlyList<string> UiIds => UiSlotRegistry.Ids.Select((UiId i) => i.ShortId).ToArray();

	public IHintChannel? RegisterHintChannel(string moduleId, string displayName, string text = "", float duration = 2f, byte priority = 128)
	{
		return HintBroker.Instance.RegisterChannel(moduleId, displayName, text, duration, priority);
	}

	public bool UnregisterHintChannel(string moduleId)
	{
		return HintBroker.Instance.UnregisterChannel(moduleId);
	}

	public IHintChannel? GetHintChannel(string moduleId)
	{
		if (!HintBroker.Instance.TryGetChannel(moduleId, out HintChannel channel))
		{
			return null;
		}
		return channel;
	}

	public bool RegisterHintSource(IHintTextSource source)
	{
		return HintBroker.Instance.RegisterSource(source);
	}

	public bool UnregisterHintSource(string moduleId)
	{
		return HintBroker.Instance.UnregisterSource(moduleId);
	}

	public void ShowTransient(ReferenceHub hub, string text, float duration = 3f)
	{
		HintBroker.Instance.ShowTransient(hub, text, duration);
	}

	public RegisterResult RegisterSssPorts(string moduleId, IEnumerable<ServerSpecificSettingBase> settings)
	{
		return SssRegistry.RegisterModule(moduleId, settings);
	}

	public bool UnregisterSssPorts(string moduleId)
	{
		return SssRegistry.UnregisterModule(moduleId);
	}

	public void SubscribeSssValue(string moduleId, Action<ReferenceHub, ServerSpecificSettingBase> handler)
	{
		SssRegistry.SubscribeValue(moduleId, handler);
	}

	public void UnsubscribeSssValue(Action<ReferenceHub, ServerSpecificSettingBase> handler)
	{
		SssRegistry.UnsubscribeValue(handler);
	}

	public void SubscribeSssStatus(string moduleId, Action<ReferenceHub, SSSUserStatusReport> handler)
	{
		SssRegistry.SubscribeStatus(moduleId, handler);
	}

	public void UnsubscribeSssStatus(Action<ReferenceHub, SSSUserStatusReport> handler)
	{
		SssRegistry.UnsubscribeStatus(handler);
	}

	public void EnableSssForceRewrite()
	{
		SssForceRewrite.Install();
	}

	public void DisableSssForceRewrite()
	{
		SssForceRewrite.Uninstall();
	}

	public bool ForceRewriteSettings()
	{
		return SssRegistry.ForceMergeBeforeSend();
	}

	public Action WrapEvent(string moduleId, Action handler)
	{
		return SafeEvents.Wrap(moduleId, handler);
	}

	public Action<T1, T2> WrapEvent<T1, T2>(string moduleId, Action<T1, T2> handler)
	{
		return SafeEvents.Wrap(moduleId, handler);
	}

	public Action<T1> WrapEvent<T1>(string moduleId, Action<T1> handler)
	{
		return SafeEvents.Wrap(moduleId, handler);
	}

	public Action<T1, T2, T3> WrapEvent<T1, T2, T3>(string moduleId, Action<T1, T2, T3> handler)
	{
		return SafeEvents.Wrap(moduleId, handler);
	}

	public void EnableThirdPartyInterception()
	{
		UiInterception.Instance.Install();
	}

	public void DisableThirdPartyInterception()
	{
		UiInterception.Instance.Uninstall();
	}

	public UiScope CreateScope(string moduleId)
	{
		return new UiScope(moduleId);
	}

	public IReadOnlyList<IUiSlot> GetSlots(UiSurface? surface = null, string? pluginId = null)
	{
		IEnumerable<IUiSlot> source = ((!surface.HasValue) ? UiSlotRegistry.Snapshot() : UiSlotRegistry.Snapshot(surface.Value));
		if (!string.IsNullOrEmpty(pluginId))
		{
			source = source.Where((IUiSlot s) => string.Equals(s.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
		}
		return source.ToArray();
	}

	/// <inheritdoc/>
	public int SetHintPosition(string pluginId, HintAnchor anchor, float offsetUnits = 0f)
	{
		return UiSlotRegistry.SetPluginPosition(pluginId, new HintPosition(anchor, offsetUnits, managed: true));
	}

	/// <inheritdoc/>
	public bool ClearHintPosition(string pluginId)
	{
		return UiSlotRegistry.ClearPluginPosition(pluginId);
	}
}
