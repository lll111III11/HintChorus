using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintIsolation.Core.Interception;

[HarmonyPatch]
internal static class CompatPatches
{
	[HarmonyPatch(typeof(EncryptedChannelManager), "TrySendMessageToClient", new Type[]
	{
		typeof(string),
		typeof(EncryptedChannelManager.EncryptedChannel)
	})]
	[HarmonyPriority(800)]
	private static bool Prefix(EncryptedChannelManager __instance, string content, EncryptedChannelManager.EncryptedChannel channel)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		try
		{
			if ((int)channel != 2)
			{
				return true;
			}
			return UiInterception.OnAdminChatSend((__instance == null) ? null : ((Component)__instance).GetComponent<ReferenceHub>(), content);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintIsolation] 拦截 EncryptedChannelManager.TrySendMessageToClient 异常(已放行原生): {arg}");
			return true;
		}
	}
}
