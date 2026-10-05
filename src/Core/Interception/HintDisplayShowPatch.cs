using System;
using HarmonyLib;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintIsolation.Core.Interception;

[HarmonyPatch(typeof(HintDisplay), "Show")]
internal static class HintDisplayShowPatch
{
	[HarmonyPriority(800)]
	private static bool Prefix(HintDisplay __instance, Hint hint)
	{
		try
		{
			return UiInterception.OnHintShow(__instance, hint);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintIsolation] 拦截 HintDisplay.Show 异常(已放行原生): {arg}");
			return true;
		}
	}
}
