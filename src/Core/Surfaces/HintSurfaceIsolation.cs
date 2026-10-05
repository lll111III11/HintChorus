using System;
using System.Reflection;
using HarmonyLib;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Compat;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interception;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintIsolation.Core.Surfaces;

public sealed class HintSurfaceIsolation : IUiSurfaceInterceptor
{
	private Func<TextHint, string?>? _textGetter;

	private static readonly UiId NativeId = new UiId(new Guid("A1E5C7D2-3B44-4E8A-9F21-7C6D5E4B3A20"), "系统原生提示", "游戏原生提示", UiSurface.Hint, UiIdGranularity.Assembly);

	private const string NativePluginId = "系统原生提示";

	private const byte NativeSlotPriority = 64;

	public static HintSurfaceIsolation Instance { get; } = new HintSurfaceIsolation();

	public UiSurface Surface => UiSurface.Hint;

	public string DisplayName => "屏幕提示条";

	public bool IsInstalled { get; private set; }

	public bool Enabled { get; set; } = true;

	public long InterceptedCount { get; private set; }

	public long PassedThroughCount { get; private set; }

	public bool AutoAttribute { get; set; } = true;

	public NativeHintPolicy NativePolicy { get; set; } = NativeHintPolicy.Isolate;

	public byte SlotPriority { get; set; } = 128;

	public bool ShowLabels { get; set; }

	public bool TranslateNativeHints { get; set; } = true;

	public string NativeHintLanguage { get; set; } = "zh";

	public int MaxEntries { get; set; } = 1;

	public float MaxDuration { get; set; } = 10f;

	private HintSurfaceIsolation()
	{
	}

	public void Install()
	{
		if (!IsInstalled)
		{
			EnsureTextGetter();
			IsInstalled = true;
		}
	}

	public void Uninstall()
	{
		IsInstalled = false;
	}

	internal bool OnHintShow(HintDisplay display, Hint hint)
	{
		if (display == null || hint == null)
		{
			return true;
		}
		if (HintBroker.Instance.IsOwnMergedHint(hint))
		{
			return true;
		}
		ReferenceHub val = ((Component)display).GetComponent<ReferenceHub>() ?? ((Component)display).GetComponentInParent<ReferenceHub>();
		if (val == null)
		{
			PassedThroughCount++;
			return true;
		}
		TextHint val2 = (TextHint)(object)((hint is TextHint) ? hint : null);
		if (val2 == null)
		{
			TranslationHint val3 = (TranslationHint)(object)((hint is TranslationHint) ? hint : null);
			if (val3 != null && NativePolicy == NativeHintPolicy.Isolate
				&& TranslateNativeHints && NativeHintTranslator.TryTranslate(val3, NativeHintLanguage, out string text))
			{
					UiSlotRegistry.GetOrCreate(UiIdRegistry.ResolveRoute(NativeId), "系统原生提示", 64, ShowLabels, MaxEntries, MaxDuration, HintOrigin.Native).Push(text, Time.time, ((DisplayableObject<SharedHintData>)(object)val3).DurationScalar);
				HintBroker.Instance.MarkDirty(val);
				InterceptedCount++;
				return false;
			}
			// 这一条我们吸收不了(未知类型 / 没开翻译 / 翻译不出来)。放行它, 但必须"让位":
			// 游戏只有一个提示口, 放行就等于把它整个顶掉 —— 而合并块最久要等下一次续期(约 2.35 秒)才回来。
			// YieldToForeignHint 让它在自己的时长内独占提示口, 时长一到我们下一轮(≤40ms)立刻恢复。
			YieldToForeignHint(val, hint);
			PassedThroughCount++;
			return true;
		}
		EnsureTextGetter();
		string text2 = _textGetter?.Invoke(val2);
		if (string.IsNullOrEmpty(text2))
		{
			// 空文本放行只会把合并块清成空白块(还是一样顶掉), 没有任何意义 —— 直接吞掉
			InterceptedCount++;
			return false;
		}
		if (text2.IndexOf('{') >= 0)
		{
			// 保留这条兼容放行: 有些框架(HintServiceMeow 一类)自己拿 {0} 模板做替换, 硬吸收会把它们拆坏。
			// 但放行必须配"让位", 否则它们会把合并块顶掉且最久 2.35 秒不回来。
			YieldToForeignHint(val, val2);
			PassedThroughCount++;
			return true;
		}
		CallerInfo callerInfo = PluginCallerResolver.Resolve();
		byte priority = SlotPriority;
		UiId id;
		if (callerInfo.IsPlugin)
		{
			if (!AutoAttribute)
			{
				YieldToForeignHint(val, val2);
				PassedThroughCount++;
				return true;
			}
			id = UiIdRegistry.Resolve(Surface, callerInfo.Assembly ?? typeof(HintSurfaceIsolation).Assembly, callerInfo.Method, callerInfo.IlOffset);
		}
		else
		{
			if (NativePolicy == NativeHintPolicy.PassThrough)
			{
				YieldToForeignHint(val, val2);
				PassedThroughCount++;
				return true;
			}
			id = NativeId;
			priority = 64;
		}
		UiId id2 = UiIdRegistry.ResolveRoute(id);
		UiSlotRegistry.GetOrCreate(id2, callerInfo.IsPlugin ? UiIdRegistry.DisplayNameOf(id2) : "系统原生提示", priority, ShowLabels, MaxEntries, MaxDuration, callerInfo.IsPlugin ? HintOrigin.Attributed : HintOrigin.Native).Push(text2, Time.time, ((DisplayableObject<SharedHintData>)(object)val2).DurationScalar);
		HintBroker.Instance.MarkDirty(val);
		InterceptedCount++;
		return false;
	}

	/// <summary>
	/// 放行一条本底层吸收不了的提示时调用: 让出提示口给它在自己的时长内显示,
	/// 并在它到期后立刻把合并块接回来。
	/// <para>不做这一步的话, 被放行的提示会把合并块顶掉, 而合并块要等到下一次续期
	/// (受 SuppressUnchangedResend 控制, 最久约 2.35 秒)才会回来 —— 那一段就是玩家看到的"原版顶掉了我的 UI"。</para>
	/// </summary>
	private static void YieldToForeignHint(ReferenceHub? hub, Hint? hint)
	{
		if (hub == null || hint == null)
		{
			return;
		}
		float seconds = 2f;
		try
		{
			seconds = ((DisplayableObject<SharedHintData>)(object)hint).DurationScalar;
		}
		catch (Exception)
		{
		}
		HintBroker.Instance.HoldSlot(hub, Math.Max(0.2f, seconds) + 0.05f);
		HintBroker.Instance.MarkDirty(hub);
	}

	private void EnsureTextGetter()
	{
		if (_textGetter != null)
		{
			return;
		}
		try
		{
			MethodInfo methodInfo = AccessTools.Property(typeof(TextHint), "Text")?.GetGetMethod(nonPublic: true);
			_textGetter = ((methodInfo != null) ? ((Func<TextHint, string>)Delegate.CreateDelegate(typeof(Func<TextHint, string>), methodInfo)) : ((Func<TextHint, string>)((TextHint _) => (string?)null)));
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 无法绑定 TextHint.Text 读取器, 提示条拦截将只放行: " + ex.Message));
			_textGetter = (TextHint _) => (string?)null;
		}
	}
}
