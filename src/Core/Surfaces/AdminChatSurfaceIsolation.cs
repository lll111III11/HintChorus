using System;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interception;
using HintIsolation.Core.Models;
using HintIsolation.Core.Transport;
using UnityEngine;

namespace HintIsolation.Core.Surfaces;

public sealed class AdminChatSurfaceIsolation : IUiSurfaceInterceptor
{
	/// <summary>"本底层自己正在发管理端聊天"的标记。按线程隔离, 避免并发时被别的线程提前清掉。</summary>
	[ThreadStatic]
	internal static bool SelfSending;

	public static AdminChatSurfaceIsolation Instance { get; } = new AdminChatSurfaceIsolation();

	public UiSurface Surface => UiSurface.AdminChat;

	public string DisplayName => "管理端聊天面板";

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

	private AdminChatSurfaceIsolation()
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

	internal bool OnAdminChatSend(ReferenceHub? hub, string content)
	{
		if (SelfSending || !Enabled || string.IsNullOrEmpty(content))
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
		UiId id = UiIdRegistry.ResolveRoute(UiIdRegistry.Resolve(Surface, callerInfo.Assembly ?? typeof(AdminChatSurfaceIsolation).Assembly, callerInfo.Method, callerInfo.IlOffset));
		UiSlot orCreate = UiSlotRegistry.GetOrCreate(id, UiIdRegistry.DisplayNameOf(id), SlotPriority, ShowLabels, MaxEntries, MaxDuration);
		float time = Time.time;
		orCreate.Push(content, time, MaxDuration);
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

	public static bool DirectSend(ReferenceHub? hub, string content)
	{
		return EngineDirect.SendAdminChat(hub, content);
	}
}
