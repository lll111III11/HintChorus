using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HintIsolation.Core.ServerSpecific;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.Interception;

/// <summary>
/// <c>SendToPlayer(hub, collection, versionOverride)</c> —— <b>盖掉式复写(Transpiler)</b>。
///
/// <para>这个重载吃的是<b>显式集合参数</b>, 压根不读 <c>DefinedSettings</c> ——
/// 所以"改静态字段"那一招对它完全无效。</para>
///
/// <para>而且它的 <c>collection</c> 是<b>非 ref 参数</b>: Harmony 前缀即使声明成
/// <c>ref ServerSpecificSettingBase[]</c> 也会因签名不匹配而挂不上补丁(静默失效,
/// 早期版本正是如此)。唯一能改写它的是 <b>Transpiler</b> —— 把方法体内每一次
/// "加载 collection"(<c>ldarg.1</c>)替换成"加载合并结果"。</para>
///
/// <para>这样无论调用方想给这个玩家看什么, 本核心的设置项都不会被漏掉。
/// EXILED 的 <c>UserSettings.SendToPlayer(player, settings)</c> 走的正是这条路。</para>
/// </summary>
[HarmonyPatch(typeof(ServerSpecificSettingsSync), nameof(ServerSpecificSettingsSync.SendToPlayer),
    new[] { typeof(ReferenceHub), typeof(ServerSpecificSettingBase[]), typeof(int?) })]
internal static class SssSendToPlayerCollectionPatch
{
	private static readonly MethodInfo? Merge = AccessTools.Method(
		typeof(SssRegistry), nameof(SssRegistry.MergeCollectionForOverride));

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		if (Merge is null)
		{
			// 合并器解析失败 → 原样返回, 退化为"不盖掉"(安全降级, 不会挂不上补丁)。
			foreach (CodeInstruction instruction in instructions)
			{
				yield return instruction;
			}

			yield break;
		}

		foreach (CodeInstruction instruction in instructions)
		{
			// ldarg.1 = collection 参数。把每次加载都换成"加载后再合并":
			// 语义上等价于"本方法看到的集合永远含本核心设置项"。
			if (instruction.opcode == OpCodes.Ldarg_1)
			{
				yield return new CodeInstruction(OpCodes.Ldarg_1);
				yield return new CodeInstruction(OpCodes.Call, Merge);
				continue;
			}

			yield return instruction;
		}
	}
}
