using System;
using Cassie;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(CassieAnnouncementDispatcher), "AddToQueue")]
internal static class CassieAddToQueuePatch
{
	[HarmonyPriority(800)]
	private static bool Prefix(CassieAnnouncement announcement)
	{
		try
		{
			return UiInterception.OnCassieQueue(announcement);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 拦截 CassieAnnouncementDispatcher.AddToQueue 异常(已放行原生): {arg}");
			return true;
		}
	}
}
