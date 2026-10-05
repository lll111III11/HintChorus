using System;
using HintIsolation.Core.Enums;

namespace HintIsolation.Core.Interfaces;

public interface IHintChannel
{
	string ModuleId { get; }

	string DisplayName { get; }

	string Text { get; }

	byte Priority { get; set; }

	bool Enabled { get; set; }

	bool ShowLabel { get; set; }

	HintAlignment Alignment { get; set; }

	float Duration { get; set; }

	HintOrigin Origin { get; }

	Func<ReferenceHub, bool>? ReceiverFilter { get; set; }

	void SetText(string text);
}
