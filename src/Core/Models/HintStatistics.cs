using System.Collections.Generic;

namespace HintChorus.Core.Models;

public sealed class HintStatistics
{
	public long RenderTicks { get; internal set; }

	public long HintsSent { get; internal set; }

	public long SuppressedResends { get; internal set; }

	public long InputSignatureSkips { get; internal set; }

	public long Intercepted { get; internal set; }

	public long PassedThrough { get; internal set; }

	public long Transients { get; internal set; }

	public int ChannelCount { get; internal set; }

	public int SourceCount { get; internal set; }

	public int AttributedChannelCount { get; internal set; }

	public int TrackedPlayers { get; internal set; }

	public int UiIdCount { get; internal set; }

	public IReadOnlyList<string> AttributedPlugins { get; internal set; } = new List<string>();
}
