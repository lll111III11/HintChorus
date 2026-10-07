namespace HintChorus.Core.Identity;

public sealed class UiIdRecord
{
	public string Id { get; set; } = string.Empty;

	public string ShortId { get; set; } = string.Empty;

	public string Surface { get; set; } = string.Empty;

	public string Plugin { get; set; } = string.Empty;

	public string Member { get; set; } = string.Empty;

	public string Granularity { get; set; } = string.Empty;

	public long Hits { get; set; }

	public string FirstSeenUtc { get; set; } = string.Empty;

	public string LastSeenUtc { get; set; } = string.Empty;

	public string? Alias { get; set; }

	public string? MergeInto { get; set; }
}
