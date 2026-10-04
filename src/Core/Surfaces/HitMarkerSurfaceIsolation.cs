using System;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interception;
using HintIsolation.Core.Transport;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintIsolation.Core.Surfaces;

public sealed class HitMarkerSurfaceIsolation : IUiSurfaceInterceptor
{
	/// <summary>"本底层自己正在发命中标记"的标记。按线程隔离 —— 自发送本来就是逐线程的概念,
	/// 用共享 bool 时并发下会被另一个线程的 finally 提前清掉, 于是自家的包被自己拦下来。</summary>
	[ThreadStatic]
	internal static bool SelfSending;

	public static HitMarkerSurfaceIsolation Instance { get; } = new HitMarkerSurfaceIsolation();

	public UiSurface Surface => UiSurface.HitMarker;

	public string DisplayName => "准星命中标记";

	public bool IsInstalled { get; private set; }

	public bool Enabled { get; set; } = true;

	public long InterceptedCount { get; private set; }

	public long PassedThroughCount { get; private set; }

	public bool AutoAttribute { get; set; } = true;

	public byte SlotPriority { get; set; } = 128;

	public bool ShowLabels { get; set; }

	public int MaxEntries { get; set; } = 1;

	public float MaxDuration { get; set; } = 5f;

	private HitMarkerSurfaceIsolation()
	{
	}

	public void Install()
	{
		IsInstalled = true;
	}

	public void Uninstall()
	{
		IsInstalled = false;
		SelfSending = false;
	}

	internal bool OnHitMarker(ReferenceHub? hub, float size)
	{
		if (SelfSending || !Enabled)
		{
			PassedThroughCount++;
			return true;
		}
		try
		{
			CallerInfo callerInfo = PluginCallerResolver.Resolve();
			if (callerInfo.IsPlugin && AutoAttribute)
			{
				UiId id = UiIdRegistry.ResolveRoute(UiIdRegistry.Resolve(Surface, callerInfo.Assembly ?? typeof(HitMarkerSurfaceIsolation).Assembly, callerInfo.Method, callerInfo.IlOffset));
				UiSlotRegistry.GetOrCreate(id, UiIdRegistry.DisplayNameOf(id), SlotPriority, ShowLabels, MaxEntries, MaxDuration).Push($"命中标记 ×{size:0.##}", Time.time, MaxDuration);
			}
		}
		catch (Exception e)
		{
			// 这个 catch 原本是空的: 一旦归因长期抛异常, 命中标记会静默"永远不计账",
			// 而日志和诊断报告都看不出任何线索。这里限流上报(前 3 次 + 之后每 200 次一次)。
			long n = ++_errorCount;
			if (n <= 3 || (n % 200) == 0)
			{
				Logger.Error($"[HintIsolation] 命中标记归因异常(第 {n} 次, 已放行原生): {e}");
			}
		}
		PassedThroughCount++;
		return true;
	}

	private static long _errorCount;

	public static bool DirectSend(ReferenceHub? hub, float size = 1f)
	{
		return EngineDirect.SendHitMarker(hub, size);
	}
}
