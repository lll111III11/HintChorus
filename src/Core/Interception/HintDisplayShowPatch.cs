using System;
using HarmonyLib;
using HintChorus.Core.Surfaces;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Interception;

[HarmonyPatch(typeof(HintDisplay), "Show")]
internal static class HintDisplayShowPatch
{
	/// <summary>
	/// 本线程这一轮 <c>Show</c> 是否执行过我们的前缀。
	/// <para>别的插件用更高优先级的前缀返回 false 时, Harmony 会跳过我们剩下的前缀,
	/// 但 Postfix 仍会执行 —— 用这个标记区分"我们看过"与"我们被跳过了"。</para>
	/// </summary>
	[ThreadStatic]
	private static bool _prefixRan;

	/// <summary>
	/// <b>最高优先级</b>: 尽可能抢在其它 Hint 插件的前缀之前看到提示。
	/// <para>800 只是 <c>Priority.First</c>; 这里用 <see cref="int.MaxValue"/>, 让"敢站前面的"先被我们接管。</para>
	/// </summary>
	[HarmonyPriority(int.MaxValue)]
	private static bool Prefix(HintDisplay __instance, Hint hint)
	{
		_prefixRan = true;
		try
		{
			return UiInterception.OnHintShow(__instance, hint);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 拦截 HintDisplay.Show 异常(已放行原生): {arg}");
			return true;
		}
	}

	/// <summary>
	/// <b>兜底</b>: 如果前缀被外部补丁整段跳过, 这里立刻强制重发本底层的复合块。
	/// <para>这是"UI 直接不显示"的最后一道保险: 原方法执行与否都不影响我们把 UI 盖回来。</para>
	/// </summary>
	[HarmonyPriority(int.MaxValue)]
	private static void Postfix(HintDisplay __instance)
	{
		bool ran = _prefixRan;
		_prefixRan = false;
		if (ran)
		{
			return;
		}
		try
		{
			HintSurfaceIsolation.OnForeignPrefixWon(__instance);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintChorus] 强接管 Postfix 异常: {arg}");
		}
	}
}
