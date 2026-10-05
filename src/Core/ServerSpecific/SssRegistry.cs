using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HintIsolation.Core.Models;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.ServerSpecific;

public static class SssRegistry
{
	private sealed class ModuleEntry
	{
		public required string ModuleId;

		public required ServerSpecificSettingBase[] Settings;

		public required HashSet<ServerSpecificSettingBase> Instances;
	}

	private static readonly Dictionary<string, ModuleEntry> Modules = new Dictionary<string, ModuleEntry>(StringComparer.OrdinalIgnoreCase);

	private static readonly List<(string ModuleId, Action<ReferenceHub, ServerSpecificSettingBase> Handler)> ValueHandlers = new List<(string, Action<ReferenceHub, ServerSpecificSettingBase>)>();

	private static readonly List<(string ModuleId, Action<ReferenceHub, SSSUserStatusReport> Handler)> StatusHandlers = new List<(string, Action<ReferenceHub, SSSUserStatusReport>)>();

	private static readonly object Sync = new object();

	private static ServerSpecificSettingBase[]? _currentArray;

	private static bool _selfSending;

	public static bool IsInitialized { get; private set; }

	public static bool ProtectFromOverwrites { get; private set; }

	public static bool IsSelfSending
	{
		get
		{
			lock (Sync)
			{
				return _selfSending;
			}
		}
	}

	public static IReadOnlyList<string> RegisteredModules
	{
		get
		{
			lock (Sync)
			{
				return Modules.Keys.OrderBy((string x) => x, StringComparer.Ordinal).ToArray();
			}
		}
	}

	public static void Initialize(bool protectFromOverwrites = true)
	{
		lock (Sync)
		{
			if (IsInitialized)
			{
				ProtectFromOverwrites |= protectFromOverwrites;
				return;
			}
			IsInitialized = true;
			ProtectFromOverwrites = protectFromOverwrites;
			ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnNativeValueReceived;
			ServerSpecificSettingsSync.ServerOnStatusReceived += OnNativeStatusReceived;
			_currentArray = ServerSpecificSettingsSync.DefinedSettings;
		}
		StartupLog.Info("[HintIsolation] SSS 端口隔离核心已启动 (防劫持=" + ProtectFromOverwrites + ")");
	}

	public static void Terminate()
	{
		lock (Sync)
		{
			if (!IsInitialized)
			{
				return;
			}
			IsInitialized = false;
			ProtectFromOverwrites = false;
			ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnNativeValueReceived;
			ServerSpecificSettingsSync.ServerOnStatusReceived -= OnNativeStatusReceived;
			RewriteArrayLocked();
			Modules.Clear();
			ValueHandlers.Clear();
			StatusHandlers.Clear();
			_currentArray = ServerSpecificSettingsSync.DefinedSettings;
		}
		// 还原后的面板也要在锁外下发一次, 否则客户端还挂着本核心的设置项
		SendAllNow();
		StartupLog.Info("[HintIsolation] SSS 端口隔离核心已停止");
	}

	public static RegisterResult RegisterModule(string moduleId, IEnumerable<ServerSpecificSettingBase> settings)
	{
		if (string.IsNullOrWhiteSpace(moduleId))
		{
			return RegisterResult.Fail(moduleId, "moduleId 不能为空");
		}
		if (settings == null)
		{
			return RegisterResult.Fail(moduleId, "settings 为 null");
		}
		int registeredCount = 0;
		lock (Sync)
		{
			if (!IsInitialized)
			{
				Initialize();
			}
			if (Modules.ContainsKey(moduleId))
			{
				return RegisterResult.Fail(moduleId, "注册口 '" + moduleId + "' 已注册, 请先 UnregisterModule");
			}
			ServerSpecificSettingBase[] array = settings.ToArray();
			if (array.Length == 0)
			{
				return RegisterResult.Fail(moduleId, "没有可注册的设置项");
			}
			List<string> list = CheckConflictsLocked(array);
			if (list.Count > 0)
			{
				Logger.Error((object)("[HintIsolation] 注册口 '" + moduleId + "' 与其它插件存在 SettingId 冲突, 已拒绝注册: " + string.Join("; ", list)));
				return RegisterResult.Fail(moduleId, "SettingId 冲突", list);
			}
			Modules.Add(moduleId, new ModuleEntry
			{
				ModuleId = moduleId,
				Settings = array,
				Instances = new HashSet<ServerSpecificSettingBase>(array)
			});
			RewriteArrayLocked();
			registeredCount = array.Length;
		}
		SendAllNow();
		StartupLog.Info($"[HintIsolation] 注册口 '{moduleId}' 已独立注册 {registeredCount} 个 SSS 设置项");
		return RegisterResult.Ok(moduleId);
	}

	public static bool UnregisterModule(string moduleId)
	{
		lock (Sync)
		{
			if (!Modules.Remove(moduleId))
			{
				return false;
			}
			RewriteArrayLocked();
		}
		SendAllNow();
		StartupLog.Info("[HintIsolation] 注册口 '" + moduleId + "' 的 SSS 设置项已卸载");
		return true;
	}

	public static IReadOnlyList<string> CheckConflicts(IEnumerable<ServerSpecificSettingBase> settings)
	{
		lock (Sync)
		{
			return CheckConflictsLocked(settings.ToArray());
		}
	}

	public static void SubscribeValue(string moduleId, Action<ReferenceHub, ServerSpecificSettingBase> handler)
	{
		if (handler == null)
		{
			return;
		}
		lock (Sync)
		{
			ValueHandlers.Add((moduleId, handler));
		}
	}

	public static void UnsubscribeValue(Action<ReferenceHub, ServerSpecificSettingBase> handler)
	{
		lock (Sync)
		{
			ValueHandlers.RemoveAll(((string ModuleId, Action<ReferenceHub, ServerSpecificSettingBase> Handler) h) => h.Handler == handler);
		}
	}

	public static void SubscribeStatus(string moduleId, Action<ReferenceHub, SSSUserStatusReport> handler)
	{
		if (handler == null)
		{
			return;
		}
		lock (Sync)
		{
			StatusHandlers.Add((moduleId, handler));
		}
	}

	public static void UnsubscribeStatus(Action<ReferenceHub, SSSUserStatusReport> handler)
	{
		lock (Sync)
		{
			StatusHandlers.RemoveAll(((string ModuleId, Action<ReferenceHub, SSSUserStatusReport> Handler) h) => h.Handler == handler);
		}
	}

	public static void GuardianTick()
	{
		bool repaired = false;
		lock (Sync)
		{
			if (IsInitialized && ProtectFromOverwrites && Modules.Count != 0 && ServerSpecificSettingsSync.DefinedSettings != _currentArray)
			{
				Logger.Error((object)"[HintIsolation] 检测到有插件整体覆盖了 ServerSpecificSettingsSync.DefinedSettings (这会导致其它插件 UI 失效), 已自动合并恢复本核心注册的所有设置项");
				RewriteArrayLocked();
				repaired = true;
			}
		}
		if (repaired)
		{
			SendAllNow();
		}
	}

	public static void ReAssert()
	{
		bool rewritten = false;
		lock (Sync)
		{
			if (IsInitialized)
			{
				RewriteArrayLocked();
				rewritten = true;
			}
		}
		if (rewritten)
		{
			SendAllNow();
		}
	}

	private static void RewriteArrayLocked()
	{
		ServerSpecificSettingBase[] array = ServerSpecificSettingsSync.DefinedSettings ?? Array.Empty<ServerSpecificSettingBase>();
		List<ServerSpecificSettingBase> list = new List<ServerSpecificSettingBase>(array.Length);
		ServerSpecificSettingBase[] array2 = array;
		foreach (ServerSpecificSettingBase setting in array2)
		{
			if (!Modules.Values.Any((ModuleEntry m) => m.Instances.Contains(setting)))
			{
				list.Add(setting);
			}
		}
		foreach (ModuleEntry value in Modules.Values)
		{
			list.AddRange(value.Settings);
		}
		_currentArray = list.ToArray();
		ServerSpecificSettingsSync.DefinedSettings = _currentArray;
	}

	/// <summary>
	/// 把当前设置数组下发给全服。
	/// <para><b>必须在没有持有 <see cref="Sync"/> 的时候调用。</b> <c>SendToAll</c> 会按连接逐个
	/// 序列化下发, 玩家越多越慢; 持锁下发会把其它线程上的 SSS 操作(别的插件注册、3 秒守卫轮询、
	/// ForceRewrite 的前缀)整段阻塞住 —— 那种卡顿极难定位。</para>
	/// </summary>
	public static void SendAllNow()
	{
		_selfSending = true;
		try
		{
			ServerSpecificSettingsSync.SendToAll();
		}
		finally
		{
			_selfSending = false;
		}
	}

	public static bool TryCoverCollection(ServerSpecificSettingBase[]? incoming, out ServerSpecificSettingBase[] merged)
	{
		merged = incoming ?? Array.Empty<ServerSpecificSettingBase>();
		lock (Sync)
		{
			if (!IsInitialized || IsSelfSending || Modules.Count == 0)
			{
				return false;
			}
			List<ServerSpecificSettingBase> list = new List<ServerSpecificSettingBase>();
			foreach (ModuleEntry value in Modules.Values)
			{
				list.AddRange(value.Settings);
			}
			if (list.Count == 0)
			{
				return false;
			}
			bool flag = true;
			foreach (ServerSpecificSettingBase item in list)
			{
				if (!CollectionExtensions.Contains<ServerSpecificSettingBase>(merged, item))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return false;
			}
			List<ServerSpecificSettingBase> list2 = new List<ServerSpecificSettingBase>(merged.Length + list.Count);
			list2.AddRange(merged);
			foreach (ServerSpecificSettingBase item2 in list)
			{
				if (!list2.Contains(item2))
				{
					list2.Add(item2);
				}
			}
			merged = list2.ToArray();
			return true;
		}
	}

	public static bool ForceMergeBeforeSend()
	{
		lock (Sync)
		{
			if (!IsInitialized || _selfSending || Modules.Count == 0)
			{
				return false;
			}
			ServerSpecificSettingBase[] current = ServerSpecificSettingsSync.DefinedSettings ?? Array.Empty<ServerSpecificSettingBase>();
			List<ServerSpecificSettingBase> list = new List<ServerSpecificSettingBase>(current.Length);
			ServerSpecificSettingBase[] array = current;
			foreach (ServerSpecificSettingBase setting in array)
			{
				if (!Modules.Values.Any((ModuleEntry m) => m.Instances.Contains(setting)))
				{
					list.Add(setting);
				}
			}
			if (list.Count == current.Length && Modules.Values.All((ModuleEntry m) => m.Settings.All((ServerSpecificSettingBase s) => current.Contains(s))))
			{
				return false;
			}
			foreach (ModuleEntry value in Modules.Values)
			{
				list.AddRange(value.Settings);
			}
			_currentArray = list.ToArray();
			ServerSpecificSettingsSync.DefinedSettings = _currentArray;
			return true;
		}
	}

	private static List<string> CheckConflictsLocked(ServerSpecificSettingBase[] items)
	{
		List<string> list = new List<string>();
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		ServerSpecificSettingBase[] settings;
		foreach (ModuleEntry value2 in Modules.Values)
		{
			settings = value2.Settings;
			foreach (ServerSpecificSettingBase val in settings)
			{
				if (!dictionary.ContainsKey(val.SettingId))
				{
					dictionary.Add(val.SettingId, value2.ModuleId);
				}
			}
		}
		HashSet<int> hashSet = new HashSet<int>();
		settings = items;
		for (int i = 0; i < settings.Length; i++)
		{
			int settingId = settings[i].SettingId;
			string value;
			if (!hashSet.Add(settingId))
			{
				list.Add($"ID {settingId} 在本模块内重复使用");
			}
			else if (dictionary.TryGetValue(settingId, out value))
			{
				list.Add($"ID {settingId} 已被注册口 '{value}' 占用");
			}
		}
		return list;
	}

	private static void OnNativeValueReceived(ReferenceHub hub, ServerSpecificSettingBase setting)
	{
		(string, Action<ReferenceHub, ServerSpecificSettingBase>)[] array;
		lock (Sync)
		{
			array = ValueHandlers.ToArray();
		}
		(string, Action<ReferenceHub, ServerSpecificSettingBase>)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			var (arg, action) = array2[i];
			try
			{
				action(hub, setting);
			}
			catch (Exception arg2)
			{
				Logger.Error((object)$"[HintIsolation] 注册口 '{arg}' 的 SSS 值回调异常(已隔离, 不影响其它模块): {arg2}");
			}
		}
	}

	private static void OnNativeStatusReceived(ReferenceHub hub, SSSUserStatusReport status)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		(string, Action<ReferenceHub, SSSUserStatusReport>)[] array;
		lock (Sync)
		{
			array = StatusHandlers.ToArray();
		}
		(string, Action<ReferenceHub, SSSUserStatusReport>)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			var (arg, action) = array2[i];
			try
			{
				action(hub, status);
			}
			catch (Exception arg2)
			{
				Logger.Error((object)$"[HintIsolation] 注册口 '{arg}' 的 SSS 状态回调异常(已隔离): {arg2}");
			}
		}
	}
}
