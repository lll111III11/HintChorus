using HintIsolation.Core.Broker;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interception;
using HintIsolation.Core.Models;
using HintIsolation.Core.Transport;
using UnityEngine;

namespace HintIsolation.Core.Surfaces;

public sealed class ConsoleSurfaceIsolation : IUiSurfaceInterceptor
{
	public static ConsoleSurfaceIsolation Instance { get; } = new ConsoleSurfaceIsolation();

	public UiSurface Surface => UiSurface.Console;

	public string DisplayName => "玩家控制台消息";

	public bool IsInstalled { get; private set; }

	public bool Enabled { get; set; } = true;

	public long InterceptedCount { get; private set; }

	public long PassedThroughCount { get; private set; }

	public bool AutoAttribute { get; set; } = true;

	public byte SlotPriority { get; set; } = 128;

	public bool ShowLabels { get; set; }

	public int MaxEntries { get; set; } = 1;

	public float MaxDuration { get; set; } = 15f;

	public float RepeatInterval { get; set; }

	private ConsoleSurfaceIsolation()
	{
	}

	public void Install()
	{
		IsInstalled = true;
	}

	public void Uninstall()
	{
		IsInstalled = false;
	}

	internal bool OnConsoleSend(ReferenceHub? hub, string text, string color)
	{
		if (!Enabled || string.IsNullOrEmpty(text))
		{
			PassedThroughCount++;
			return true;
		}
		CallerInfo callerInfo = PluginCallerResolver.Resolve();
		if (!callerInfo.IsPlugin || !AutoAttribute)
		{
			PassedThroughCount++;
			return true;
		}
		UiId id = UiIdRegistry.ResolveRoute(UiIdRegistry.Resolve(Surface, callerInfo.Assembly ?? typeof(ConsoleSurfaceIsolation).Assembly, callerInfo.Method, callerInfo.IlOffset));
		UiSlot orCreate = UiSlotRegistry.GetOrCreate(id, UiIdRegistry.DisplayNameOf(id), SlotPriority, ShowLabels, MaxEntries, MaxDuration);
		float time = Time.time;
		orCreate.Push(text, time, MaxDuration);
		if (RepeatInterval <= 0f)
		{
			PassedThroughCount++;
			return true;
		}
		if (((orCreate.LastPassedAt > 0f) ? (time - orCreate.LastPassedAt) : float.MaxValue) < RepeatInterval)
		{
			InterceptedCount++;
			return false;
		}
		orCreate.LastPassedAt = time;
		PassedThroughCount++;
		return true;
	}

	public static bool DirectSend(ReferenceHub? hub, string text, string color = "green")
	{
		return EngineDirect.SendConsole(hub, text, color);
	}
}
