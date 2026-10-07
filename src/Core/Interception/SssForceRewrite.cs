using System;
using HarmonyLib;
using HintChorus.Core.ServerSpecific;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Interception;

public static class SssForceRewrite
{
	private const string HarmonyId = "com.labapi.HintChorus.sssforcerewrite";

	private static Harmony? _harmony;

	private static readonly Type[] PatchTypes = new Type[4]
	{
		typeof(SssSendToAllPatch),
		typeof(SssSendToPlayersConditionallyPatch),
		typeof(SssSendToPlayerPatch),
		typeof(SssSendToPlayerCollectionPatch)
	};

	public static bool IsInstalled { get; private set; }

	public static long ForcedCount { get; internal set; }

	public static long NestedSkipCount { get; internal set; }

	public static void Install()
	{
		if (IsInstalled)
		{
			return;
		}
		_harmony = new Harmony("com.labapi.HintChorus.sssforcerewrite");
		Type[] patchTypes = PatchTypes;
		foreach (Type type in patchTypes)
		{
			try
			{
				_harmony.CreateClassProcessor(type).Patch();
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintChorus] 安装 SSS 强力复写补丁 " + type.Name + " 失败: " + ex.Message));
			}
		}
		IsInstalled = true;
		StartupLog.Info("[HintChorus] 已安装 SSS 强力复写补丁: 下发前强制拼回本核心的设置项(即使被整体覆盖也会补回来)");
	}

	public static void Uninstall()
	{
		if (IsInstalled)
		{
			_harmony?.UnpatchAll("com.labapi.HintChorus.sssforcerewrite");
			_harmony = null;
			IsInstalled = false;
			StartupLog.Info("[HintChorus] 已卸载 SSS 强力复写补丁");
		}
	}

	internal static void RewriteBeforeSend()
	{
		try
		{
			if (SssRegistry.IsSelfSending)
			{
				NestedSkipCount++;
			}
			else if (SssRegistry.ForceMergeBeforeSend())
			{
				ForcedCount++;
			}
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] SSS 强力复写异常(已放行原生下发): {arg}");
		}
	}
}
