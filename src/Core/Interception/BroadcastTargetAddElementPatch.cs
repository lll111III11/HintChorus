using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintIsolation.Core.Interception;

[HarmonyPatch(typeof(Broadcast), "TargetAddElement")]
internal static class BroadcastTargetAddElementPatch
{
	[HarmonyPriority(800)]
	private static bool Prefix(string data, ref ushort time, Broadcast.BroadcastFlags flags)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return UiInterception.OnBroadcastAdd(data, ref time, flags);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintIsolation] 拦截 Broadcast.TargetAddElement 异常(已放行原生): {arg}");
			return true;
		}
	}
}
