using System;
using HarmonyLib;
using HintIsolation.Core.Compat;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using PlayerRoles.Voice;

namespace HintIsolation.Core.Interception;

[HarmonyPatch(typeof(IntercomDisplay), "SerializeSyncVars")]
internal static class IntercomDisplaySerializePatch
{
	[HarmonyPriority(800)]
	private static void Prefix(IntercomDisplay __instance)
	{
		try
		{
			IntercomGuard.OnSerialize(__instance);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintIsolation] 对讲机显示屏守卫异常(已放行原生): {arg}");
		}
	}
}
