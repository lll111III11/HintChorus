using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(Broadcast), "TargetClearElements")]
internal static class BroadcastTargetClearElementsPatch
{
	[HarmonyPriority(800)]
	private static bool Prefix()
	{
		try
		{
			return UiInterception.OnBroadcastClear();
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 拦截 Broadcast.TargetClearElements 异常(已放行原生): {arg}");
			return true;
		}
	}
}
