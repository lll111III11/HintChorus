using System;
using System.Reflection;
using HarmonyLib;
using HintChorus.Core.Broker;
using HintChorus.Core.Compat;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Interception;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintChorus.Core.Surfaces;

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

	/// <summary>强接管(配置项): 其它插件的提示补丁抢先吞掉提示口时, 立即把本底层的复合块重新盖回去。</summary>
	public bool ForceTakeover { get; set; } = true;

	/// <summary>整体退回原生(运行时状态, 由 Hint orvs 设置): 所有提示原样放行, 本底层不再接管。</summary>
	public bool ForceNative { get; set; }

	/// <summary>强接管触发次数(诊断用)。</summary>
	public long TakeoverCount { get; private set; }

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
		if (!Enabled)
		{
			PassedThroughCount++;
			return true;
		}
		ReferenceHub val = ((Component)display).GetComponent<ReferenceHub>() ?? ((Component)display).GetComponentInParent<ReferenceHub>();
		if (val == null)
		{
			PassedThroughCount++;
			return true;
		}
		if (ForceNative || HintTakeover.NativeMode)
		{
			PassedThroughCount++;
			return true;
		}

		// 归因必须在类型分支之前完成: 强接管需要知道"这是不是插件发的"。
		CallerInfo callerInfo = PluginCallerResolver.Resolve();
		bool plugin = callerInfo.IsPlugin;
		bool passThrough = plugin && HintFallback.ShouldPassThrough(callerInfo.PluginId);
		bool force = ForceTakeover && plugin && !passThrough;

		TextHint val2 = (TextHint)(object)((hint is TextHint) ? hint : null);
		TranslationHint val3 = (TranslationHint)(object)((hint is TranslationHint) ? hint : null);

		if (val2 == null && val3 == null)
		{
			// 未知类型: 强接管下由本底层保持提示口; 否则照旧让路。
			if (force)
			{
				HintBroker.Instance.ForceResend(val);
				TakeoverCount++;
				InterceptedCount++;
				return false;
			}
			YieldToForeignHint(val, hint);
			PassedThroughCount++;
			return true;
		}

		if (val3 != null)
		{
			if (NativePolicy == NativeHintPolicy.Isolate
				&& TranslateNativeHints && NativeHintTranslator.TryTranslate(
					val3,
					NativeHintTranslator.ResolvePlayerLanguage(val, NativeHintLanguage),
					out string translated))
			{
				float nativeDuration = ((DisplayableObject<SharedHintData>)(object)val3).DurationScalar;
				if (plugin)
				{
					HintTakeover.Record(val, translated, nativeDuration, callerInfo.PluginId);
				}
				UiSlotRegistry.GetOrCreate(UiIdRegistry.ResolveRoute(NativeId), "系统原生提示", 64, ShowLabels, MaxEntries, MaxDuration, HintOrigin.Native).Push(translated, Time.time, nativeDuration);
				UiLog.Hint(plugin ? callerInfo.PluginId : HintFallback.NativeOwner, DisplayName, translated, nativeDuration, "并入(译文)");
				if (force)
				{
					HintBroker.Instance.ForceResend(val);
					TakeoverCount++;
				}
				else
				{
					HintBroker.Instance.MarkDirty(val);
				}
				InterceptedCount++;
				return false;
			}
			// 这一条我们吸收不了(未知类型 / 没开翻译 / 翻译不出来)。放行它, 但必须"让位":
			// 游戏只有一个提示口, 放行就等于把它整个顶掉 —— 而合并块最久要等下一次续期(约 2.35 秒)才回来。
			// YieldToForeignHint 让它在自己的时长内独占提示口, 时长一到我们下一轮(≤40ms)立刻恢复。
			if (force)
			{
				HintBroker.Instance.ForceResend(val);
				TakeoverCount++;
				InterceptedCount++;
				return false;
			}
			YieldToForeignHint(val, hint);
			PassedThroughCount++;
			return true;
		}

		EnsureTextGetter();
		string text2 = _textGetter?.Invoke(val2);
		if (plugin)
		{
			// 无论后面是吸收还是压制, 先把"覆盖前的数据"记下来 —— Hint orvs 1 就靠它回放。
			HintTakeover.Record(val, text2, ((DisplayableObject<SharedHintData>)(object)val2).DurationScalar, callerInfo.PluginId);
		}

		if (string.IsNullOrEmpty(text2))
		{
			// 空文本放行只会把合并块清成空白块(还是一样顶掉), 没有任何意义 —— 直接吞掉
			if (force)
			{
				HintBroker.Instance.ForceResend(val);
				TakeoverCount++;
			}
			InterceptedCount++;
			return false;
		}
		if (text2.IndexOf('{') >= 0)
		{
			// 有些框架(HintServiceMeow 一类)自己拿 {0} 模板做替换, 硬吸收会把它们拆坏。
			// 强接管下不再让路: 记下快照, 压掉这条模板, 立刻把本底层的复合块盖回去。
			if (force)
			{
				HintBroker.Instance.ForceResend(val);
				TakeoverCount++;
				InterceptedCount++;
				return false;
			}
			YieldToForeignHint(val, val2);
			PassedThroughCount++;
			return true;
		}

		byte priority = SlotPriority;
		UiId id;
		if (plugin)
		{
			// 插件级原生回退(Hint orvs): 被选中的插件恢复原版行为, 不再并入合并层。
			// 其它插件不受影响 —— 多个 Hint 插件可以同时存在且互不冲突。
			if (passThrough)
			{
				YieldToForeignHint(val, val2);
				PassedThroughCount++;
				UiLog.Hint(callerInfo.PluginId, DisplayName, text2, ((DisplayableObject<SharedHintData>)(object)val2).DurationScalar, "原生直通(orvs)");
				return true;
			}
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
		float duration = ((DisplayableObject<SharedHintData>)(object)val2).DurationScalar;
		UiSlotRegistry.GetOrCreate(id2, callerInfo.IsPlugin ? UiIdRegistry.DisplayNameOf(id2) : "系统原生提示", priority, ShowLabels, MaxEntries, MaxDuration, callerInfo.IsPlugin ? HintOrigin.Attributed : HintOrigin.Native).Push(text2, Time.time, duration);
		UiLog.Hint(callerInfo.IsPlugin ? callerInfo.PluginId : HintFallback.NativeOwner, DisplayName, text2, duration);
		if (force)
		{
			// 强接管: 不等续期, 立即把含这条内容的复合块重新下发, 把占口的外部提示顶掉。
			HintBroker.Instance.ForceResend(val);
			TakeoverCount++;
		}
		else
		{
			HintBroker.Instance.MarkDirty(val);
		}
		InterceptedCount++;
		return false;
	}

	/// <summary>
	/// <b>外部前缀抢先吞掉提示时的兜底</b>。
	/// <para>别的插件用更高优先级的前缀返回 false, 会让我们的 <c>Show</c> 前缀整段被跳过。
	/// 这时由 Postfix 调用本方法, 立刻强制重发本底层的复合块 —— "敢站前面的直接踹掉"。</para>
	/// </summary>
	internal static void OnForeignPrefixWon(HintDisplay? display)
	{
		if (display == null)
		{
			return;
		}
		HintSurfaceIsolation instance = Instance;
		if (!instance.ForceTakeover || instance.ForceNative || HintTakeover.NativeMode)
		{
			return;
		}
		try
		{
			ReferenceHub hub = ((Component)display).GetComponent<ReferenceHub>() ?? ((Component)display).GetComponentInParent<ReferenceHub>();
			if (hub == null)
			{
				return;
			}
			HintBroker.Instance.ForceResend(hub);
			instance.TakeoverCount++;
			UiLog.Detail("TAKEOVER", "外部前缀抢先吞掉提示, 已强制重发复合块");
		}
		catch (Exception ex)
		{
			UiLog.Report("OnForeignPrefixWon", ex);
		}
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
			Logger.Error((object)("[HintChorus] 无法绑定 TextHint.Text 读取器, 提示条拦截将只放行: " + ex.Message));
			_textGetter = (TextHint _) => (string?)null;
		}
	}
}
