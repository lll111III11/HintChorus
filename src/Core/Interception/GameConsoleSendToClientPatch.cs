using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(GameConsoleTransmission), "SendToClient")]
internal static class GameConsoleSendToClientPatch
{
	[HarmonyPriority(800)]
	private static bool Prefix(GameConsoleTransmission __instance, string text, string color)
	{
		try
		{
			return UiInterception.OnConsoleSend((__instance == null) ? null : ((Component)__instance).GetComponent<ReferenceHub>(), text, color);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 拦截 GameConsoleTransmission.SendToClient 异常(已放行原生): {arg}");
			return true;
		}
	}
}
