using System;
using Cassie;
using HintChorus.Core.Broker;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Interception;
using HintChorus.Core.Transport;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintChorus.Core.Surfaces;

public sealed class CassieSurfaceIsolation : IUiSurfaceInterceptor
{
	public static CassieSurfaceIsolation Instance { get; } = new CassieSurfaceIsolation();

	public UiSurface Surface => UiSurface.Cassie;

	public string DisplayName => "CASSIE 语音播报";

	public bool IsInstalled { get; private set; }

	public bool Enabled { get; set; } = true;

	public long InterceptedCount { get; private set; }

	public long PassedThroughCount { get; private set; }

	public bool AutoAttribute { get; set; } = true;

	public bool BlockThirdPartyClear { get; set; } = true;

	public byte SlotPriority { get; set; } = 128;

	public bool ShowLabels { get; set; }

	public int MaxEntries { get; set; } = 1;

	public float MaxDuration { get; set; } = 30f;

	private CassieSurfaceIsolation()
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

	internal bool OnCassieQueue(CassieAnnouncement? announcement)
	{
		if (!Enabled || announcement == null)
		{
			PassedThroughCount++;
			return true;
		}
		try
		{
			CallerInfo callerInfo = PluginCallerResolver.Resolve();
			if (callerInfo.IsPlugin && AutoAttribute)
			{
				UiId id = UiIdRegistry.ResolveRoute(UiIdRegistry.Resolve(Surface, callerInfo.Assembly ?? typeof(CassieSurfaceIsolation).Assembly, callerInfo.Method, callerInfo.IlOffset));
				UiSlotRegistry.GetOrCreate(id, UiIdRegistry.DisplayNameOf(id), SlotPriority, ShowLabels, MaxEntries, MaxDuration).Push(announcement.Payload.Content ?? string.Empty, Time.time, MaxDuration);
			}
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintChorus] CASSIE 入队归因失败(已放行原生): " + ex.Message));
		}
		PassedThroughCount++;
		return true;
	}

	internal bool OnCassieClear()
	{
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

	public static bool DirectSend(string message, string subtitles = "", bool playBackground = true)
	{
		return EngineDirect.SendCassie(message, subtitles, playBackground);
	}
}
