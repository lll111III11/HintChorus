using System;
using HarmonyLib;
using HintIsolation.Core.ServerSpecific;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.Interception;

[HarmonyPatch(typeof(ServerSpecificSettingsSync), "SendToPlayer", new Type[]
{
	typeof(ReferenceHub),
	typeof(ServerSpecificSettingBase[]),
	typeof(int?)
})]
internal static class SssSendToPlayerCollectionPatch
{
	private static void Prefix(ref ServerSpecificSettingBase[] collection)
	{
		if (SssRegistry.TryCoverCollection(collection, out ServerSpecificSettingBase[] merged))
		{
			collection = merged;
		}
	}
}
