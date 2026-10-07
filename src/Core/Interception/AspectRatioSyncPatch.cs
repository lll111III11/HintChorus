using System;
using HarmonyLib;
using HintChorus.Core.Broker;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(AspectRatioSync), "UserCode_CmdSetAspectRatio__Single")]
internal static class AspectRatioSyncPatch
{
	private static void Postfix(AspectRatioSync __instance)
	{
		try
		{
			if (__instance != null)
			{
				NetworkIdentity netIdentity = ((NetworkBehaviour)__instance).netIdentity;
				NetworkConnection connection = ((netIdentity != null) ? netIdentity.connectionToClient : null);
				if (ReferenceHub.TryGetHub(connection, out ReferenceHub val) && val != null)
				{
					HintBroker.Instance.MarkDirty(val);
				}
			}
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 宽高比变更标脏异常(已放行原生): {arg}");
		}
	}
}
