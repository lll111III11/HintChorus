using System;
using HintChorus.Core.Enums;

namespace HintChorus.Core.Models;

public sealed class HintEntry
{
	public string Text { get; internal set; }

	public float Duration { get; internal set; }

	public float ExpiresAt { get; internal set; }

	public HintOrigin Origin { get; }

	public string SourceId { get; }

	internal HintEntry(string text, float duration, float expiresAt, HintOrigin origin, string sourceId)
	{
		Text = text;
		Duration = duration;
		ExpiresAt = expiresAt;
		Origin = origin;
		SourceId = sourceId;
	}

	public bool IsExpired(float now)
	{
		return now >= ExpiresAt;
	}

	public float Remaining(float now)
	{
		return Math.Max(0f, ExpiresAt - now);
	}
}
