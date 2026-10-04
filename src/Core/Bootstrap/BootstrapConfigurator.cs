using System;
using System.Globalization;
using System.IO;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Compat;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interception;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Yaml;

namespace HintIsolation.Core.Bootstrap;

internal static class BootstrapConfigurator
{
	internal static string ConfigPath => Path.Combine(PathManager.Configs.FullName, Server.Port.ToString(CultureInfo.InvariantCulture), "HintIsolation", "config.yml");

	internal static HintIsolationPlugin.PluginConfig? TryLoadFromDisk()
	{
		try
		{
			string configPath = ConfigPath;
			if (!File.Exists(configPath))
			{
				return null;
			}
			return YamlConfigParser.Deserializer.Deserialize<HintIsolationPlugin.PluginConfig>(File.ReadAllText(configPath));
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 抢先读取配置失败(将交由主插件处理): " + ex.Message));
			return null;
		}
	}

	internal static void Apply(HintIsolationPlugin.PluginConfig config)
	{
		UiIdRegistry.Granularity = config.UiIdGranularity;
		UiIdRegistry.Persist = config.PersistUiIds;
		UiInterception instance = UiInterception.Instance;
		instance.AutoAttribute = config.AutoAttributeThirdPartyUi;
		instance.NativePolicy = config.NativeHintPolicy;
		instance.HintEnabled = config.InterceptHints;
		instance.HintSlotPriority = config.HintSlotPriority;
		instance.HintShowLabels = config.HintShowLabels;
		instance.NativeHintTranslate = config.TranslateNativeHints;
		instance.NativeHintLanguage = config.NativeHintTranslationLanguage;
		instance.HintMaxEntries = config.HintMaxEntriesPerSlot;
		instance.HintMaxDuration = config.HintMaxEntryDuration;
		instance.BroadcastEnabled = config.InterceptBroadcasts;
		instance.BlockThirdPartyBroadcastClear = config.BlockThirdPartyBroadcastClear;
		instance.BroadcastSlotPriority = config.BroadcastSlotPriority;
		instance.BroadcastShowLabels = config.BroadcastShowLabels;
		instance.BroadcastMaxEntries = config.BroadcastMaxEntriesPerSlot;
		instance.BroadcastMaxDuration = config.BroadcastMaxEntryDuration;
		instance.BroadcastRepeatInterval = config.BroadcastRepeatInterval;
		instance.BroadcastRepeatDuration = config.BroadcastRepeatDuration;
		instance.ConsoleEnabled = config.InterceptConsole;
		instance.ConsoleSlotPriority = config.ConsoleSlotPriority;
		instance.ConsoleShowLabels = config.ConsoleShowLabels;
		instance.ConsoleMaxEntries = config.ConsoleMaxEntriesPerSlot;
		instance.ConsoleMaxDuration = config.ConsoleMaxEntryDuration;
		instance.ConsoleRepeatInterval = config.ConsoleRepeatInterval;
		instance.CassieEnabled = config.InterceptCassie;
		instance.BlockThirdPartyCassieClear = config.BlockThirdPartyCassieClear;
		instance.CassieSlotPriority = config.CassieSlotPriority;
		instance.CassieShowLabels = config.CassieShowLabels;
		instance.CassieMaxEntries = config.CassieMaxEntriesPerSlot;
		instance.CassieMaxDuration = config.CassieMaxEntryDuration;
		instance.NetworkSentinelEnabled = config.EnableNetworkSentinel;
		instance.NetworkSentinelIntercept = config.NetworkSentinelIntercept;
		instance.AdminChatEnabled = config.InterceptAdminChat;
		instance.AdminChatRepeatInterval = config.AdminChatRepeatInterval;
		instance.HitMarkerEnabled = config.InterceptHitMarker;
		IntercomGuard.Enabled = config.EnableIntercomGuard;
		IntercomGuard.ThrottleInterval = config.IntercomThrottleInterval;
		instance.ApplyConfig();
		HintBroker.Instance.SuppressUnchangedResend = config.SuppressUnchangedResend;
		// 排版: 治上下位置错位(rows=空行占位 / offsets=算偏移 / compact=紧凑)
		HintBroker.Instance.Layout = HintBroker.ParseLayoutMode(config.LayoutMode);
		HintBroker.Instance.OffsetRowHeight = config.OffsetRowHeight;
		HintBroker.Instance.OffsetSign = config.OffsetSign;
		HintBroker.Instance.OffsetFontSize = config.OffsetFontSize;
		HintBroker.Instance.VolatileRows = config.VolatileRows;
		HintBroker.Instance.RowsPerPlugin = config.RowsPerPlugin;
		HintBroker.Instance.PersistentRowsMax = config.PersistentRowsMax;
		HintBroker.Instance.InputSignatureEnabled = config.InputSignatureEnabled;
		if (config.EnableHintIsolation && !HintBroker.Instance.IsRunning)
		{
			HintBroker.Instance.DebugLog = config.HintDebugLog;
			HintBroker.Instance.Start(config.HintRefreshInterval, config.HintResendLeeway);
		}
	}
}
