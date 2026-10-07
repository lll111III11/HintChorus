using System.Collections.Generic;

namespace HintChorus.Core.Identity;

public sealed class UiIdCatalog : IUiIdCatalog
{
	public static UiIdCatalog Instance { get; } = new UiIdCatalog();

	public int Count => UiIdRegistry.Count;

	public IReadOnlyList<UiIdRecord> Records => UiIdRegistry.Snapshot();

	public string FilePath => UiIdRegistry.ResolvePath();

	private UiIdCatalog()
	{
	}

	public bool TryGet(string shortId, out UiIdRecord record)
	{
		return UiIdRegistry.TryGetByShortId(shortId, out record);
	}

	public bool SetAlias(string shortId, string? newAlias)
	{
		return UiIdRegistry.SetAlias(shortId, newAlias);
	}

	public bool Merge(string shortId, string intoShortId)
	{
		return UiIdRegistry.Merge(shortId, intoShortId);
	}

	public bool Unmerge(string shortId)
	{
		return UiIdRegistry.Unmerge(shortId);
	}
}
