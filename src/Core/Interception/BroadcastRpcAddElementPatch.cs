using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintIsolation.Core.Interception;

/// <summary>
/// 广播表面补丁: 全服广播入队(<c>RpcAddElement</c>)。
/// <para>签名与游戏一致: <c>RpcAddElement(string, ushort, BroadcastFlags)</c>, <c>duration</c> 非 ref。</para>
/// </summary>
[HarmonyPatch(typeof(Broadcast), nameof(Broadcast.RpcAddElement))]
internal static class BroadcastRpcAddElementPatch
{
	[HarmonyPriority(Priority.First)]
	private static bool Prefix(string message, ushort duration, Broadcast.BroadcastFlags type)
	{
		try
		{
			return UiInterception.OnBroadcastAdd(null, message, duration, type);
		}
		catch (Exception e)
		{
			Logger.Error((object)$"[HintIsolation] 拦截 Broadcast.RpcAddElement 异常(已放行原生): {e}");
			return true;
		}
	}
}
