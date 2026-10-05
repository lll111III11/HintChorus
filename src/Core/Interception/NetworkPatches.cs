using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cassie;
using HarmonyLib;
using HintIsolation.Core.Transport;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;

namespace HintIsolation.Core.Interception;

internal static class NetworkPatches
{
	private static readonly (Type Message, string PrefixName)[] Targets = new (Type, string)[3]
	{
		(typeof(HintMessage), "PrefixHintMessage"),
		(typeof(CassieTtsPayload), "PrefixCassiePayload"),
		(typeof(RpcMessage), "PrefixRpcMessage")
	};

	private static readonly List<MethodInfo> Patched = new List<MethodInfo>();

	internal static void Install(Harmony harmony)
	{
		if (Patched.Count > 0)
		{
			return;
		}
		MethodInfo methodInfo = ResolveOpenSend();
		if ((object)methodInfo == null)
		{
			Logger.Error((object)"[HintIsolation] 未找到 Mirror.NetworkConnection.Send<T>, 网络哨兵未安装(功能降级, 不影响其它层)");
			return;
		}
		Type typeFromHandle = typeof(NetworkPatches);
		(Type, string)[] targets = Targets;
		for (int i = 0; i < targets.Length; i++)
		{
			var (type, name) = targets[i];
			try
			{
				MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(type);
				MethodInfo method = typeFromHandle.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
				if ((object)method != null)
				{
					harmony.Patch(methodInfo2, new HarmonyMethod(method));
					Patched.Add(methodInfo2);
				}
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintIsolation] 安装网络层补丁 Send<" + type.Name + "> 失败: " + ex.Message));
			}
		}
		try
		{
			MethodInfo methodInfo3 = ResolveByteStreamSend();
			MethodInfo method2 = typeFromHandle.GetMethod("PrefixByteStream", BindingFlags.Static | BindingFlags.NonPublic);
			if ((object)methodInfo3 != null && (object)method2 != null)
			{
				harmony.Patch(methodInfo3, new HarmonyMethod(method2));
				Patched.Add(methodInfo3);
			}
		}
		catch (Exception ex2)
		{
			Logger.Error((object)("[HintIsolation] 安装网络层补丁 Send(ArraySegment<byte>) 失败: " + ex2.Message));
		}
		StartupLog.Info("[HintIsolation] 网络哨兵已安装 —— 已挂上 Mirror 消息汇流点 NetworkConnection.Send<T> " + $"({Targets.Length} 个闭合类型) + 字节流重载 Send(ArraySegment<byte>), 可看住绕过语义层的直发与伪造包");
	}

	internal static void Uninstall(Harmony harmony)
	{
		foreach (MethodInfo item in Patched)
		{
			try
			{
				harmony.Unpatch(item, HarmonyPatchType.Prefix, harmony.Id);
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintIsolation] 卸载网络层补丁失败: " + ex.Message));
			}
		}
		Patched.Clear();
		NetworkSentinel.Reset();
		StartupLog.Info("[HintIsolation] 网络哨兵已卸载");
	}

	private static MethodInfo? ResolveOpenSend()
	{
		return typeof(NetworkConnection).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "Send" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
	}

	private static MethodInfo? ResolveByteStreamSend()
	{
		return typeof(NetworkConnection).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "Send" && !m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(ArraySegment<byte>));
	}

	private static bool PrefixHintMessage(NetworkConnection __instance, HintMessage message)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return NetworkSentinel.OnHintMessage(__instance, message);
	}

	private static bool PrefixCassiePayload(NetworkConnection __instance, CassieTtsPayload message)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return NetworkSentinel.OnCassiePayload(__instance, message);
	}

	private static bool PrefixRpcMessage(NetworkConnection __instance, RpcMessage message)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return NetworkSentinel.OnRpcMessage(__instance, message);
	}

	private static bool PrefixByteStream(ArraySegment<byte> segment)
	{
		return NetworkSentinel.OnByteStream(segment.Count);
	}
}
