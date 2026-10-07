using System;
using HarmonyLib;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;
using UnityEngine;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(Hitmarker), "SendHitmarkerDirectly", new Type[]
{
	typeof(NetworkConnection),
	typeof(float)
})]
internal static class HitmarkerSendPatch
{
	[HarmonyPriority(800)]
	private static bool Prefix(NetworkConnection conn, float size)
	{
		try
		{
			object hub;
			if (conn == null)
			{
				hub = null;
			}
			else
			{
				NetworkIdentity identity = conn.identity;
				hub = ((identity != null) ? ((Component)identity).GetComponent<ReferenceHub>() : null);
			}
			return UiInterception.OnHitMarker((ReferenceHub?)hub, size);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 拦截 Hitmarker.SendHitmarkerDirectly 异常(已放行原生): {arg}");
			return true;
		}
	}
}
