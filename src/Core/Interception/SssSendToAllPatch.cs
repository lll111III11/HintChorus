using HarmonyLib;
using UserSettings.ServerSpecific;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(ServerSpecificSettingsSync), "SendToAll")]
internal static class SssSendToAllPatch
{
	private static void Prefix()
	{
		SssForceRewrite.RewriteBeforeSend();
	}
}
