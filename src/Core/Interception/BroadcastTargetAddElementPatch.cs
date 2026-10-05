using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;

namespace HintIsolation.Core.Interception;

/// <summary>
/// 广播表面补丁: 单人广播入队(<c>TargetAddElement</c>)。
/// 拦截点选在真正发包的这一层, 因此 LabAPI 封装与原生直调都会被覆盖。
/// <para>签名必须与游戏一致: <c>TargetAddElement(NetworkConnection, string, ushort, BroadcastFlags)</c> ——
/// 首参是连接, <c>duration</c> 是<b>普通 ushort</b>(非 ref)。</para>
/// </summary>
[HarmonyPatch(typeof(Broadcast), nameof(Broadcast.TargetAddElement))]
internal static class BroadcastTargetAddElementPatch
{
	[HarmonyPriority(Priority.First)]
	private static bool Prefix(NetworkConnection conn, string message, ushort duration, Broadcast.BroadcastFlags type)
	{
		try
		{
			return UiInterception.OnBroadcastAdd(conn, message, duration, type);
		}
		catch (Exception e)
		{
			Logger.Error((object)$"[HintIsolation] 拦截 Broadcast.TargetAddElement 异常(已放行原生): {e}");
			return true;
		}
	}
}
