using System;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Interfaces;

namespace HintIsolation.Core.Models;

public sealed class HintChannel : IHintChannel
{
	public string ModuleId { get; }

	public string DisplayName { get; }

	public string Text { get; private set; }

	public byte Priority { get; set; }

	public bool Enabled { get; set; } = true;

	public bool ShowLabel { get; set; } = true;

	public HintAlignment Alignment { get; set; }

	public float Duration { get; set; }

	public HintOrigin Origin => HintOrigin.Plugin;

	public Func<ReferenceHub, bool>? ReceiverFilter { get; set; }

	public long TextRevision { get; private set; }

	public HintChannel(string moduleId, string displayName, string text = "", float duration = 2f, byte priority = 128)
	{
		if (string.IsNullOrWhiteSpace(moduleId))
		{
			throw new ArgumentException("moduleId 不能为空", "moduleId");
		}
		ModuleId = moduleId;
		DisplayName = displayName ?? moduleId;
		Text = text ?? string.Empty;
		Duration = duration;
		Priority = priority;
	}

	public void SetText(string text)
	{
		string text2 = text ?? string.Empty;
		if (!string.Equals(Text, text2, StringComparison.Ordinal))
		{
			Text = text2;
			TextRevision++;
		}
	}
}
