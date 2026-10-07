using System;
using HarmonyLib;
using UserSettings.ServerSpecific;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(ServerSpecificSettingsSync), "SendToPlayersConditionally")]
internal static class SssSendToPlayersConditionallyPatch
{
	private static void Prefix(Func<ReferenceHub, bool> filter)
	{
		SssForceRewrite.RewriteBeforeSend();
	}
}
