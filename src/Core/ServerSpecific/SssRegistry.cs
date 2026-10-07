using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HintChorus.Core.Models;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UserSettings.ServerSpecific;

namespace HintChorus.Core.ServerSpecific;

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
		StartupLog.Info("[HintChorus] SSS 端口隔离核心已启动 (防劫持=" + ProtectFromOverwrites + ")");
	}

	public static void Terminate()
	{
		bool removed = false;
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
			// 必须先"剔除自己的项"再清空 Modules: Modules 里还登记着我们的实例,
			// RemoveAllLocked 靠它识别"哪些是别人的、哪些是我们要剔除的"。
			// (早期版本先 RewriteArrayLocked 再 Clear —— 而 RewriteArrayLocked 是
			//  "剔除自己 + 加回自己" = 原样写回, 于是卸载后我们的设置项仍残留在
			//   DefinedSettings 里, 热重载后变成孤儿项越攒越多。)
			RemoveAllLocked();
			removed = true;
			Modules.Clear();
			ValueHandlers.Clear();
			StatusHandlers.Clear();
			_currentArray = ServerSpecificSettingsSync.DefinedSettings;
		}
		if (removed)
		{
			// 还原后的面板也要在锁外下发一次, 否则客户端还挂着本核心的设置项
			SendAllNow();
		}
		StartupLog.Info("[HintChorus] SSS 端口隔离核心已停止");
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
				Logger.Error((object)("[HintChorus] 注册口 '" + moduleId + "' 与其它插件存在 SettingId 冲突, 已拒绝注册: " + string.Join("; ", list)));
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
		StartupLog.Info($"[HintChorus] 注册口 '{moduleId}' 已独立注册 {registeredCount} 个 SSS 设置项");
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
		StartupLog.Info("[HintChorus] 注册口 '" + moduleId + "' 的 SSS 设置项已卸载");
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
				Logger.Error((object)"[HintChorus] 检测到有插件整体覆盖了 ServerSpecificSettingsSync.DefinedSettings (这会导致其它插件 UI 失效), 已自动合并恢复本核心注册的所有设置项");
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
	/// 剔除本核心注册的<b>全部</b>设置项并下发(供 <see cref="Terminate"/> 使用)。
	/// 与 <see cref="RewriteArrayLocked"/> 不同: 只剔除、<b>不</b>加回。
	/// </summary>
	private static void RemoveAllLocked()
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

	/// <summary>
	/// <b>供 <c>SendToPlayer(hub, collection, version)</c> 的 Transpiler 调用的幂等合并器</b>。
	///
	/// <para>该重载的 <c>collection</c> 是<b>非 ref 参数</b>, Harmony 前缀无法改写它,
	/// 只能靠 Transpiler 把方法体内每一次"加载 collection"换成"加载合并结果"。
	/// 本方法就是那个被注入的合并器。</para>
	///
	/// <para>幂等保证: 本核心设置项已全部在场时, <see cref="TryCoverCollection"/> 返回
	/// 原数组实例(不分配、不改写), 因此对同一集合反复合并是安全的。</para>
	/// </summary>
	public static ServerSpecificSettingBase[] MergeCollectionForOverride(ServerSpecificSettingBase[] incoming)
	{
		TryCoverCollection(incoming, out ServerSpecificSettingBase[] merged);
		return merged;
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
			// 本核心的设置项已全部在场 → 无需改写(短路条件只有这一个;
			// 早期版本误加了 list.Count == current.Length, 与"全部在场"互斥, 导致恒为 false,
			// 每次下发都触发整数组重建)。
			if (Modules.Values.All((ModuleEntry m) => m.Settings.All((ServerSpecificSettingBase s) => current.Contains(s))))
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
				// 端口隔离语义: 订阅者只收到"自己注册口"的设置变化。
				// (早期版本把 setting 广播给所有订阅者, 插件 A 会收到插件 B 的 setting,
				//  只能靠自己在回调里再按 SettingId 二次过滤。)
				if (!IsSettingOwnedBy(arg, setting))
				{
					continue;
				}
				action(hub, setting);
			}
			catch (Exception arg2)
			{
				Logger.Error((object)$"[HintChorus] 注册口 '{arg}' 的 SSS 值回调异常(已隔离, 不影响其它模块): {arg2}");
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
				Logger.Error((object)$"[HintChorus] 注册口 '{arg}' 的 SSS 状态回调异常(已隔离): {arg2}");
			}
		}
	}

	/// <summary>setting 是否属于指定注册口。</summary>
	private static bool IsSettingOwnedBy(string moduleId, ServerSpecificSettingBase setting)
	{
		lock (Sync)
		{
			return Modules.TryGetValue(moduleId, out ModuleEntry? entry) && entry.Instances.Contains(setting);
		}
	}
}
