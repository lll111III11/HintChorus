using System;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;

namespace HintIsolation.Core.Interfaces;

public interface IUiSlot
{
	UiId Id { get; }

	UiSurface Surface { get; }

	string SlotId { get; }

	string PluginId { get; }

	string DisplayName { get; }

	byte Priority { get; set; }

	bool ShowLabel { get; set; }

	int EntryCount { get; }

	long TotalReceived { get; }

	DateTime LastActivityUtc { get; }

	string RenderText();
}
