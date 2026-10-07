using System;
using Cassie;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(CassieAnnouncementDispatcher), "ClearAll")]
internal static class CassieClearAllPatch
{
	[HarmonyPriority(800)]
	private static bool Prefix()
	{
		try
		{
			return UiInterception.OnCassieClear();
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 拦截 CassieAnnouncementDispatcher.ClearAll 异常(已放行原生): {arg}");
			return true;
		}
	}
}
