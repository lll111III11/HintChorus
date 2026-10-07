using System;
using System.Collections.Generic;
using Cassie;
using HarmonyLib;
using HintChorus.Core.Broker;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Interfaces;
using HintChorus.Core.Surfaces;
using HintChorus.Core.Transport;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;
using UnityEngine;

namespace HintChorus.Core.Interception;

public sealed class UiInterception : IUiInterception
{
	internal const string HarmonyId = "com.labapi.HintChorus.interception";

	private Harmony? _harmony;

	private static readonly Type[] SurfacePatchTypes = new Type[12]
	{
		typeof(HintDisplayShowPatch),
		typeof(BroadcastTargetAddElementPatch),
		typeof(BroadcastRpcAddElementPatch),
		typeof(BroadcastTargetClearElementsPatch),
		typeof(BroadcastRpcClearElementsPatch),
		typeof(GameConsoleSendToClientPatch),
		typeof(CassieAddToQueuePatch),
		typeof(CassieClearAllPatch),
		typeof(CompatPatches),
		typeof(HitmarkerSendPatch),
		typeof(IntercomDisplaySerializePatch),
		typeof(AspectRatioSyncPatch)
	};

	public static UiInterception Instance { get; } = new UiInterception();

	public bool IsInstalled { get; private set; }

	public bool AutoAttribute { get; set; } = true;

	public NativeHintPolicy NativePolicy { get; set; }

	public UiIdGranularity Granularity
	{
		get
		{
			return UiIdRegistry.Granularity;
		}
		set
		{
			UiIdRegistry.Granularity = value;
		}
	}

	public bool HintEnabled { get; set; } = true;

	public bool BroadcastEnabled { get; set; } = true;

	public bool BlockThirdPartyBroadcastClear { get; set; } = true;

	public byte HintSlotPriority { get; set; } = 128;

	public bool HintShowLabels { get; set; }

	public bool NativeHintTranslate { get; set; } = true;

	public string NativeHintLanguage { get; set; } = "zh";

	public int HintMaxEntries { get; set; } = 1;

	public float HintMaxDuration { get; set; } = 10f;

	public byte BroadcastSlotPriority { get; set; } = 128;

	public bool BroadcastShowLabels { get; set; }

	public int BroadcastMaxEntries { get; set; } = 2;

	public float BroadcastMaxDuration { get; set; } = 15f;

	public float BroadcastRepeatInterval { get; set; } = 1f;

	public ushort BroadcastRepeatDuration { get; set; } = 1;

	public bool ConsoleEnabled { get; set; } = true;

	public byte ConsoleSlotPriority { get; set; } = 128;

	public bool ConsoleShowLabels { get; set; }

	public int ConsoleMaxEntries { get; set; } = 1;

	public float ConsoleMaxDuration { get; set; } = 15f;

	public float ConsoleRepeatInterval { get; set; }

	public bool CassieEnabled { get; set; } = true;

	public bool BlockThirdPartyCassieClear { get; set; } = true;

	public byte CassieSlotPriority { get; set; } = 128;

	public bool CassieShowLabels { get; set; }

	public int CassieMaxEntries { get; set; } = 1;

	public float CassieMaxDuration { get; set; } = 30f;

	public bool NetworkSentinelEnabled { get; set; }

	public bool NetworkSentinelIntercept { get; set; }

	public bool AdminChatEnabled { get; set; } = true;

	public byte AdminChatSlotPriority { get; set; } = 128;

	public bool AdminChatShowLabels { get; set; }

	public int AdminChatMaxEntries { get; set; } = 1;

	public float AdminChatMaxDuration { get; set; } = 15f;

	public float AdminChatRepeatInterval { get; set; }

	public bool HitMarkerEnabled { get; set; } = true;

	public byte HitMarkerSlotPriority { get; set; } = 128;

	public long InterceptedCount => HintSurfaceIsolation.Instance.InterceptedCount + BroadcastSurfaceIsolation.Instance.InterceptedCount + ConsoleSurfaceIsolation.Instance.InterceptedCount + CassieSurfaceIsolation.Instance.InterceptedCount + AdminChatSurfaceIsolation.Instance.InterceptedCount + HitMarkerSurfaceIsolation.Instance.InterceptedCount;

	public long PassedThroughCount => HintSurfaceIsolation.Instance.PassedThroughCount + BroadcastSurfaceIsolation.Instance.PassedThroughCount + ConsoleSurfaceIsolation.Instance.PassedThroughCount + CassieSurfaceIsolation.Instance.PassedThroughCount + AdminChatSurfaceIsolation.Instance.PassedThroughCount + HitMarkerSurfaceIsolation.Instance.PassedThroughCount;

	public IReadOnlyList<string> AttributedPlugins => UiSlotRegistry.PluginIds;

	public IReadOnlyList<IUiSurfaceInterceptor> Surfaces { get; } = new IUiSurfaceInterceptor[6]
	{
		HintSurfaceIsolation.Instance,
		BroadcastSurfaceIsolation.Instance,
		ConsoleSurfaceIsolation.Instance,
		CassieSurfaceIsolation.Instance,
		AdminChatSurfaceIsolation.Instance,
		HitMarkerSurfaceIsolation.Instance
	};

	private UiInterception()
	{
	}

	public void Install()
	{
		if (IsInstalled)
		{
			return;
		}
		UiIdRegistry.Load();
		ApplyConfig();
		_harmony = new Harmony("com.labapi.HintChorus.interception");
		Type[] surfacePatchTypes = SurfacePatchTypes;
		foreach (Type type in surfacePatchTypes)
		{
			try
			{
				_harmony.CreateClassProcessor(type).Patch();
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintChorus] 安装表面补丁 " + type.Name + " 失败: " + ex.Message));
			}
		}
		NetworkSentinel.Enabled = NetworkSentinelEnabled;
		NetworkSentinel.Intercept = NetworkSentinelIntercept;
		if (NetworkSentinelEnabled)
		{
			NetworkPatches.Install(_harmony);
		}
		IsInstalled = true;
		StartupLog.Info("[HintChorus] 拦截层已安装 —— 提示条 + 屏幕广播 + 玩家控制台 + CASSIE + 管理端聊天 + 命中标记 六条 UI 通道被接管, LabAPI 与 EXILED 两套框架的写法均按【调用方 + 注册点】自动分配独立 UiId 信口");
	}

	public void Uninstall()
	{
		if (IsInstalled)
		{
			if (_harmony != null)
			{
				NetworkPatches.Uninstall(_harmony);
				_harmony.UnpatchAll("com.labapi.HintChorus.interception");
			}
			_harmony = null;
			IsInstalled = false;
			HintSurfaceIsolation.Instance.Uninstall();
			BroadcastSurfaceIsolation.Instance.Uninstall();
			ConsoleSurfaceIsolation.Instance.Uninstall();
			CassieSurfaceIsolation.Instance.Uninstall();
			AdminChatSurfaceIsolation.Instance.Uninstall();
			HitMarkerSurfaceIsolation.Instance.Uninstall();
			PluginCallerResolver.ClearCache();
			UiIdRegistry.Save();
			StartupLog.Info("[HintChorus] 拦截层已卸载, 恢复原生渲染");
		}
	}

	public void ApplyConfig()
	{
		HintSurfaceIsolation instance = HintSurfaceIsolation.Instance;
		instance.Enabled = HintEnabled;
		instance.AutoAttribute = AutoAttribute;
		instance.NativePolicy = NativePolicy;
		instance.SlotPriority = HintSlotPriority;
		instance.ShowLabels = HintShowLabels;
		instance.TranslateNativeHints = NativeHintTranslate;
		instance.NativeHintLanguage = NativeHintLanguage;
		instance.MaxEntries = HintMaxEntries;
		instance.MaxDuration = HintMaxDuration;
		BroadcastSurfaceIsolation instance2 = BroadcastSurfaceIsolation.Instance;
		instance2.Enabled = BroadcastEnabled;
		instance2.AutoAttribute = AutoAttribute;
		instance2.NativePolicy = NativePolicy;
		instance2.BlockThirdPartyClear = BlockThirdPartyBroadcastClear;
		instance2.SlotPriority = BroadcastSlotPriority;
		instance2.ShowLabels = BroadcastShowLabels;
		instance2.MaxEntries = BroadcastMaxEntries;
		instance2.MaxDuration = BroadcastMaxDuration;
		instance2.RepeatInterval = BroadcastRepeatInterval;
		instance2.RepeatDuration = BroadcastRepeatDuration;
		ConsoleSurfaceIsolation instance3 = ConsoleSurfaceIsolation.Instance;
		instance3.Enabled = ConsoleEnabled;
		instance3.AutoAttribute = AutoAttribute;
		instance3.SlotPriority = ConsoleSlotPriority;
		instance3.ShowLabels = ConsoleShowLabels;
		instance3.MaxEntries = ConsoleMaxEntries;
		instance3.MaxDuration = ConsoleMaxDuration;
		instance3.RepeatInterval = ConsoleRepeatInterval;
		CassieSurfaceIsolation instance4 = CassieSurfaceIsolation.Instance;
		instance4.Enabled = CassieEnabled;
		instance4.AutoAttribute = AutoAttribute;
		instance4.BlockThirdPartyClear = BlockThirdPartyCassieClear;
		instance4.SlotPriority = CassieSlotPriority;
		instance4.ShowLabels = CassieShowLabels;
		instance4.MaxEntries = CassieMaxEntries;
		instance4.MaxDuration = CassieMaxDuration;
		AdminChatSurfaceIsolation instance5 = AdminChatSurfaceIsolation.Instance;
		instance5.Enabled = AdminChatEnabled;
		instance5.AutoAttribute = AutoAttribute;
		instance5.SlotPriority = AdminChatSlotPriority;
		instance5.ShowLabels = AdminChatShowLabels;
		instance5.MaxEntries = AdminChatMaxEntries;
		instance5.MaxDuration = AdminChatMaxDuration;
		instance5.RepeatInterval = AdminChatRepeatInterval;
		HitMarkerSurfaceIsolation instance6 = HitMarkerSurfaceIsolation.Instance;
		instance6.Enabled = HitMarkerEnabled;
		instance6.AutoAttribute = AutoAttribute;
		instance6.SlotPriority = HitMarkerSlotPriority;
		UiSlotRegistry.ApplyProfile(HintSlotPriority, HintShowLabels);
	}

	internal static bool OnHintShow(HintDisplay display, Hint hint)
	{
		return HintSurfaceIsolation.Instance.OnHintShow(display, hint);
	}

	internal static bool OnBroadcastAdd(NetworkConnection? conn, string message, ushort duration, Broadcast.BroadcastFlags flags)
	{
		return BroadcastSurfaceIsolation.Instance.OnBroadcastAdd(conn, message, duration, flags);
	}

	internal static bool OnBroadcastClear()
	{
		return BroadcastSurfaceIsolation.Instance.OnBroadcastClear();
	}

	internal static bool OnConsoleSend(ReferenceHub? hub, string text, string color)
	{
		return ConsoleSurfaceIsolation.Instance.OnConsoleSend(hub, text, color);
	}

	internal static bool OnCassieQueue(CassieAnnouncement? announcement)
	{
		return CassieSurfaceIsolation.Instance.OnCassieQueue(announcement);
	}

	internal static bool OnCassieClear()
	{
		return CassieSurfaceIsolation.Instance.OnCassieClear();
	}

	internal static bool OnAdminChatSend(ReferenceHub? hub, string content)
	{
		return AdminChatSurfaceIsolation.Instance.OnAdminChatSend(hub, content);
	}

	internal static bool OnHitMarker(ReferenceHub? hub, float size)
	{
		return HitMarkerSurfaceIsolation.Instance.OnHitMarker(hub, size);
	}

	public static void Tick()
	{
		UiIdRegistry.Tick();
		UiSlotRegistry.PruneAll(Time.time);
	}

	internal void Repair()
	{
		if (_harmony == null)
		{
			return;
		}
		_harmony.UnpatchAll("com.labapi.HintChorus.interception");
		Type[] surfacePatchTypes = SurfacePatchTypes;
		foreach (Type type in surfacePatchTypes)
		{
			try
			{
				_harmony.CreateClassProcessor(type).Patch();
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintChorus] 自愈重装补丁 " + type.Name + " 失败: " + ex.Message));
			}
		}
		if (NetworkSentinelEnabled)
		{
			NetworkPatches.Uninstall(_harmony);
			NetworkPatches.Install(_harmony);
		}
		Logger.Warn((object)"[HintChorus] 检测到拦截补丁被外部摘掉, 已自动补回(自愈)");
	}
}
