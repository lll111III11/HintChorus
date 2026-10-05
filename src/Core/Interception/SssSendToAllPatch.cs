using HarmonyLib;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.Interception;

[HarmonyPatch(typeof(ServerSpecificSettingsSync), "SendToAll")]
internal static class SssSendToAllPatch
{
	private static void Prefix()
	{
		SssForceRewrite.RewriteBeforeSend();
	}
}
