using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Interception;

/// <summary>
/// 广播表面补丁: 全服广播入队(<c>RpcAddElement</c>)。
///
/// <para><b>签名与参数名都必须与游戏一致</b>: <c>RpcAddElement(string data, ushort time, BroadcastFlags flags)</c> ——
/// <c>time</c> 非 ref; 参数名 Harmony 按名字绑定, 写成 <c>message/duration/type</c> 会抛
/// <c>Parameter "message" not found in method ...</c> 而<b>完全装不上</b>(实测踩过)。</para>
/// </summary>
[HarmonyPatch(typeof(Broadcast), nameof(Broadcast.RpcAddElement))]
internal static class BroadcastRpcAddElementPatch
{
	[HarmonyPriority(Priority.First)]
	private static bool Prefix(string data, ushort time, Broadcast.BroadcastFlags flags)
	{
		try
		{
			return UiInterception.OnBroadcastAdd(null, data, time, flags);
		}
		catch (Exception e)
		{
			Logger.Error((object)$"[HintChorus] 拦截 Broadcast.RpcAddElement 异常(已放行原生): {e}");
			return true;
		}
	}
}
