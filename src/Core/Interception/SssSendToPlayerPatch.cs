using System;
using HarmonyLib;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.Interception;

[HarmonyPatch(typeof(ServerSpecificSettingsSync), "SendToPlayer", new Type[] { typeof(ReferenceHub) })]
internal static class SssSendToPlayerPatch
{
	private static void Prefix(ReferenceHub hub)
	{
		SssForceRewrite.RewriteBeforeSend();
	}
}
