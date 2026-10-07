using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HintChorus.Core.Broker;
using HintChorus.Core.Compat;
using HintChorus.Core.Identity;
using HintChorus.Core.Interception;
using HintChorus.Core.Layout;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Yaml;

namespace HintChorus.Core.Bootstrap;

internal static class BootstrapConfigurator
{
	internal static string ConfigPath => Path.Combine(PathManager.Configs.FullName, Server.Port.ToString(CultureInfo.InvariantCulture), "HintChorus", "config.yml");

	internal static HintChorusPlugin.PluginConfig? TryLoadFromDisk()
	{
		try
		{
			string configPath = ConfigPath;
			if (!File.Exists(configPath))
			{
				return null;
			}
			return YamlConfigParser.Deserializer.Deserialize<HintChorusPlugin.PluginConfig>(File.ReadAllText(configPath));
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintChorus] 抢先读取配置失败(将交由主插件处理): " + ex.Message));
			return null;
		}
	}

	internal static void Apply(HintChorusPlugin.PluginConfig config)
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
		// 位置: ①兼容原有写法 ②自有写法标记 ③按名称自动排版(+ 服主覆盖表)
		HintBroker.Instance.HonorPluginPositionSyntax = config.HonorPluginPositionSyntax;
		HintBroker.Instance.AutoLayoutByPluginName = config.AutoLayoutByPluginName;
		HintBroker.Instance.EnablePositionMarkers = config.EnablePositionMarkers;
		HintBroker.Instance.ScreenHeightUnits = config.ScreenHeightUnits;
		PluginPositionCatalog.SetOverrides(ParsePositionOverrides(config.PositionOverrides));
		HintBroker.Instance.InputSignatureEnabled = config.InputSignatureEnabled;
		if (config.EnableHintChorus && !HintBroker.Instance.IsRunning)
		{
			HintBroker.Instance.DebugLog = config.HintDebugLog;
			HintBroker.Instance.Start(config.HintRefreshInterval, config.HintResendLeeway);
		}
	}

	/// <summary>
	/// 把配置里的"插件关键词 → 位置字符串"解析成 <see cref="HintPosition"/>。
	/// <para>值的写法与自有写法标记一致: <c>top-right</c> / <c>middle,offset=-90</c>;
	/// 解析不了的条目会被<b>跳过并告警</b>, 不影响其它条目与其余配置。</para>
	/// </summary>
	private static Dictionary<string, HintPosition>? ParsePositionOverrides(Dictionary<string, string>? raw)
	{
		if (raw == null || raw.Count == 0)
		{
			return null;
		}

		Dictionary<string, HintPosition> parsed = new Dictionary<string, HintPosition>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, string> pair in raw)
		{
			if (string.IsNullOrWhiteSpace(pair.Key))
			{
				continue;
			}

			if (PositionSyntax.TryParseMarker(pair.Value, out HintPosition position))
			{
				parsed[pair.Key.Trim().ToLowerInvariant()] = position;
			}
			else
			{
				Logger.Warn("[HintChorus] position_overrides 条目无法解析, 已跳过: " + pair.Key + " = " + pair.Value);
			}
		}

		return (parsed.Count > 0) ? parsed : null;
	}
}
