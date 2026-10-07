using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;

namespace HintChorus.Core.Interception;

/// <summary>
/// 广播表面补丁: 单人广播入队(<c>TargetAddElement</c>)。
/// 拦截点选在真正发包的这一层, 因此 LabAPI 封装与原生直调都会被覆盖。
///
/// <para><b>签名与参数名都必须与游戏一致</b>: <c>TargetAddElement(NetworkConnection conn, string data, ushort time, BroadcastFlags flags)</c>。</para>
/// <list type="bullet">
///   <item><c>duration</c> 是<b>普通 ushort(非 ref)</b> —— 写成 <c>ref ushort</c> 会因签名不匹配而静默失败;</item>
///   <item><b>参数名也要一致</b>: Harmony 按<u>名字</u>把补丁参数绑定到目标参数上; 名字对不上会直接抛
///     <c>Parameter "xxx" not found in method ...</c>(Patching exception) —— 补丁<b>完全装不上</b>。
///     实测: 目标名是 <c>data/time/flags</c>, 曾误写成 <c>message/duration/type</c> 导致广播拦截整体失效。</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(Broadcast), nameof(Broadcast.TargetAddElement))]
internal static class BroadcastTargetAddElementPatch
{
	[HarmonyPriority(Priority.First)]
	private static bool Prefix(NetworkConnection conn, string data, ushort time, Broadcast.BroadcastFlags flags)
	{
		try
		{
			return UiInterception.OnBroadcastAdd(conn, data, time, flags);
		}
		catch (Exception e)
		{
			Logger.Error((object)$"[HintChorus] 拦截 Broadcast.TargetAddElement 异常(已放行原生): {e}");
			return true;
		}
	}
}
