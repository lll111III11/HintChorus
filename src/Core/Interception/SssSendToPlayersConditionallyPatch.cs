using System;
using HarmonyLib;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.Interception;

[HarmonyPatch(typeof(ServerSpecificSettingsSync), "SendToPlayersConditionally")]
internal static class SssSendToPlayersConditionallyPatch
{
	private static void Prefix(Func<ReferenceHub, bool> filter)
	{
		SssForceRewrite.RewriteBeforeSend();
	}
}
