using System.Collections.Generic;
using HintIsolation.Core.Enums;

namespace HintIsolation.Core.Interfaces;

public interface IUiInterception
{
	bool IsInstalled { get; }

	bool AutoAttribute { get; set; }

	NativeHintPolicy NativePolicy { get; set; }

	UiIdGranularity Granularity { get; set; }

	bool HintEnabled { get; set; }

	bool BroadcastEnabled { get; set; }

	bool BlockThirdPartyBroadcastClear { get; set; }

	IReadOnlyList<string> AttributedPlugins { get; }

	long InterceptedCount { get; }

	long PassedThroughCount { get; }

	void Install();

	void Uninstall();
}
