using System.Collections.Generic;

namespace HintIsolation.Core.Identity;

public interface IUiIdCatalog
{
	int Count { get; }

	IReadOnlyList<UiIdRecord> Records { get; }

	string FilePath { get; }

	bool TryGet(string shortId, out UiIdRecord record);

	bool SetAlias(string shortId, string? newAlias);

	bool Merge(string shortId, string intoShortId);

	bool Unmerge(string shortId);
}
