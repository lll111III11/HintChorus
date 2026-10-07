using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Interception;

internal static class PatchGuard
{
	private static readonly (string TypeName, string MethodName)[] Critical = new (string, string)[12]
	{
		("Hints.HintDisplay", "Show"),
		("Broadcast", "TargetAddElement"),
		("Broadcast", "RpcAddElement"),
		("Broadcast", "TargetClearElements"),
		("Broadcast", "RpcClearElements"),
		("GameConsoleTransmission", "SendToClient"),
		("CassieAnnouncementDispatcher", "AddToQueue"),
		("CassieAnnouncementDispatcher", "ClearAll"),
		("EncryptedChannelManager", "TrySendMessageToClient"),
		("Hitmarker", "SendHitmarkerDirectly"),
		("IntercomDisplay", "SerializeSyncVars"),
		("AspectRatioSync", "UserCode_CmdSetAspectRatio__Single")
	};

	public static long HealCount { get; private set; }

	/// <summary>最近一次检查里"我们的补丁被摘掉了"的目标(可自愈)。</summary>
	public static IReadOnlyList<string> LastMissing { get; private set; } = Array.Empty<string>();

	/// <summary>最近一次检查里"连目标类型/方法都找不到"的目标 —— 这类修不了, 只能提示重新核对游戏版本。</summary>
	public static IReadOnlyList<string> LastUnresolved { get; private set; } = Array.Empty<string>();

	/// <summary>连续自愈失败次数; 超过上限就不再反复摘挂补丁, 只保留一条错误日志。</summary>
	public static int ConsecutiveFailures { get; private set; }

	private const int MaxRepairAttempts = 3;

	private static int _unresolvedLoggedFor;
	private static bool _gaveUpLogged;

	public static IReadOnlyList<string> FindMissing() => FindMissing(out _);

	/// <summary>
	/// 扫描 12 个关键落点:
	/// <para><paramref name="unresolved"/> = 类型或方法名已不存在(游戏更新改了签名/命名), 修不了;</para>
	/// <para>返回值 = 方法在、但挂着的不是我们的前缀(被别的框架整体 Unpatch 了), 可自愈。</para>
	/// </summary>
	public static IReadOnlyList<string> FindMissing(out List<string> unresolved)
	{
		List<string> list = new List<string>();
		unresolved = new List<string>();
		Type type = null;
		List<MethodInfo> list2 = null;
		(string, string)[] critical = Critical;
		for (int i = 0; i < critical.Length; i++)
		{
			var (text, methodName) = critical[i];
			if ((object)type == null || type.FullName != text)
			{
				type = ResolveType(text);
				list2 = (((object)type == null) ? null : (from m in AccessTools.GetDeclaredMethods(type)
					where !m.IsAbstract
					select m).ToList());
			}
			string label = text + "." + methodName;
			if (type == null)
			{
				// 类型解析不到: 以前这里被静默跳过, 于是"拦截层其实已经死了"在诊断里完全看不出来
				unresolved.Add(label + " (类型不存在)");
				continue;
			}
			if (list2 == null)
			{
				unresolved.Add(label + " (枚举方法失败)");
				continue;
			}
			List<MethodInfo> list3 = list2.Where((MethodInfo m) => m.Name == methodName).ToList();
			if (list3.Count == 0)
			{
				unresolved.Add(label + " (方法不存在, 可能是游戏更新改了签名)");
				continue;
			}
			if (!list3.Any(HasOurPatch))
			{
				list.Add(label);
			}
		}
		return list;
	}

	public static bool Verify(Action repair)
	{
		LastMissing = FindMissing(out List<string> unresolved);
		LastUnresolved = unresolved;

		if (unresolved.Count > 0 && _unresolvedLoggedFor != unresolved.Count)
		{
			_unresolvedLoggedFor = unresolved.Count;
			Logger.Error("[HintChorus] 有 " + unresolved.Count + " 个补丁落点已经找不到目标(这部分无法自愈, 请核对游戏版本): " + string.Join(", ", unresolved));
		}

		if (LastMissing.Count == 0)
		{
			// 恢复正常: 计数与"只报一次"标记一起复位, 以便下一次真被摘掉时还能报出来
			ConsecutiveFailures = 0;
			_unresolvedLoggedFor = 0;
			_gaveUpLogged = false;
			return false;
		}

		if (ConsecutiveFailures >= MaxRepairAttempts)
		{
			// 补不回来就别每 5 秒摘挂一次了 —— 那会周期性打断正在工作的补丁, 还会刷爆日志
			if (!_gaveUpLogged)
			{
				_gaveUpLogged = true;
				Logger.Error("[HintChorus] 已连续 " + MaxRepairAttempts + " 次自愈失败, 暂停自动重装以免反复打断补丁; 仍在缺失: " + string.Join(", ", LastMissing) + " (重载插件或重启服务器可重置)");
			}
			return false;
		}

		HealCount++;
		ConsecutiveFailures++;
		try
		{
			repair();
		}
		catch (Exception e)
		{
			Logger.Error("[HintChorus] 补丁自愈重装失败(仍缺失: " + string.Join(", ", LastMissing) + "): " + e);
			return false;
		}
		return true;
	}

	public static void Reset()
	{
		HealCount = 0L;
		ConsecutiveFailures = 0;
		_unresolvedLoggedFor = 0;
		_gaveUpLogged = false;
		LastMissing = Array.Empty<string>();
		LastUnresolved = Array.Empty<string>();
	}

	private static Type? ResolveType(string typeName)
	{
		try
		{
			return AccessTools.TypeByName(typeName);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool HasOurPatch(MethodBase method)
	{
		try
		{
			Patches patchInfo = Harmony.GetPatchInfo(method);
			if (patchInfo == null)
			{
				return false;
			}
			string owner = "com.labapi.HintChorus.interception";
			return patchInfo.Prefixes.Any((Patch p) => string.Equals(p.owner, owner, StringComparison.Ordinal)) || patchInfo.Postfixes.Any((Patch p) => string.Equals(p.owner, owner, StringComparison.Ordinal));
		}
		catch (Exception)
		{
			return true;
		}
	}
}
