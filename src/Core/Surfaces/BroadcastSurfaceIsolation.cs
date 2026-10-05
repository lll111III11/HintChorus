using System;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interception;
using HintIsolation.Core.Models;
using UnityEngine;

namespace HintIsolation.Core.Surfaces;

public sealed class BroadcastSurfaceIsolation : IUiSurfaceInterceptor
{
	/// <summary>
	/// "本底层自己正在下发广播"的嵌套深度。
	/// <para>按线程隔离: 自发送本来就是逐线程的概念, 用共享字段时并发下会丢一次自减,
	/// 计数一旦卡在正数, 广播的隔离与防清屏就会**永久静默失效**(并且只在卸载时才复位)。</para>
	/// <para>Enter/Exit 成对出现在同一个方法的 try/finally 里, 所以线程局部是安全的。</para>
	/// </summary>
	[ThreadStatic]
	private static int _selfSendDepth;

	internal static int SelfSendDepth => _selfSendDepth;

	private static bool IsSelfSending => _selfSendDepth > 0;

	public static BroadcastSurfaceIsolation Instance { get; } = new BroadcastSurfaceIsolation();

	public UiSurface Surface => UiSurface.Broadcast;

	public string DisplayName => "屏幕中央广播";

	public bool IsInstalled { get; private set; }

	public bool Enabled { get; set; } = true;

	public long InterceptedCount { get; private set; }

	public long PassedThroughCount { get; private set; }

	public bool AutoAttribute { get; set; } = true;

	public NativeHintPolicy NativePolicy { get; set; }

	public bool BlockThirdPartyClear { get; set; } = true;

	public byte SlotPriority { get; set; } = 128;

	public bool ShowLabels { get; set; }

	public int MaxEntries { get; set; } = 2;

	public float MaxDuration { get; set; } = 15f;

	public float RepeatInterval { get; set; } = 1f;

	public ushort RepeatDuration { get; set; } = 1;

	private BroadcastSurfaceIsolation()
	{
	}

	public void Install()
	{
		IsInstalled = true;
	}

	public void Uninstall()
	{
		IsInstalled = false;
		_selfSendDepth = 0;
	}

	internal bool OnBroadcastAdd(string data, ref ushort duration, Broadcast.BroadcastFlags flags)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (IsSelfSending)
		{
			return true;
		}
		if ((flags & Broadcast.BroadcastFlags.AdminChat) != 0)
		{
			PassedThroughCount++;
			return true;
		}
		if (!Enabled || string.IsNullOrEmpty(data))
		{
			PassedThroughCount++;
			return true;
		}
		CallerInfo callerInfo = PluginCallerResolver.Resolve();
		if (callerInfo.IsPlugin)
		{
			if (!AutoAttribute)
			{
				PassedThroughCount++;
				return true;
			}
		}
		else if (NativePolicy == NativeHintPolicy.PassThrough)
		{
			PassedThroughCount++;
			return true;
		}
		UiId id = UiIdRegistry.ResolveRoute(UiIdRegistry.Resolve(Surface, callerInfo.Assembly ?? typeof(BroadcastSurfaceIsolation).Assembly, callerInfo.Method, callerInfo.IlOffset));
		UiSlot orCreate = UiSlotRegistry.GetOrCreate(id, UiIdRegistry.DisplayNameOf(id), SlotPriority, ShowLabels, MaxEntries, MaxDuration);
		float time = Time.time;
		float num = ((orCreate.LastPassedAt > 0f) ? (time - orCreate.LastPassedAt) : float.MaxValue);
		if (num > RepeatInterval * 2f)
		{
			orCreate.Push(data, time, (int)duration);
			orCreate.LastPassedAt = time;
			PassedThroughCount++;
			return true;
		}
		if (num < RepeatInterval)
		{
			// 广播是"列表追加"型表面, 设计语义是【节流 + 压缩时长】, 不是丢弃。
			// 而且只有 UiSurface.Hint 的信口会被 HintBroker 合成下发, 广播信口不会渲染 ——
			// 所以这里一旦 return false 就等于内容永久丢失, 玩家什么都看不到。
			// 改为: 把时长压到 RepeatDuration 以内后照样放行。
			ushort capped = ((duration > RepeatDuration) ? RepeatDuration : duration);
			orCreate.Push(data, time, capped);
			orCreate.LastPassedAt = time;
			PassedThroughCount++;
			duration = capped;
			return true;
		}
		if (duration > RepeatDuration)
		{
			duration = RepeatDuration;
		}
		orCreate.Push(data, time, (int)duration);
		orCreate.LastPassedAt = time;
		PassedThroughCount++;
		return true;
	}

	internal bool OnBroadcastClear()
	{
		if (IsSelfSending)
		{
			return true;
		}
		if (!Enabled || !BlockThirdPartyClear)
		{
			PassedThroughCount++;
			return true;
		}
		if (PluginCallerResolver.Resolve().IsPlugin)
		{
			InterceptedCount++;
			return false;
		}
		PassedThroughCount++;
		return true;
	}

	internal static void EnterSelfSend()
	{
		_selfSendDepth++;
	}

	internal static void ExitSelfSend()
	{
		_selfSendDepth = ((_selfSendDepth > 0) ? (_selfSendDepth - 1) : 0);
	}
}
