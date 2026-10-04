using HintIsolation.Core.Enums;

namespace HintIsolation.Core.Surfaces;

public interface IUiSurfaceInterceptor
{
	UiSurface Surface { get; }

	string DisplayName { get; }

	bool IsInstalled { get; }

	bool Enabled { get; set; }

	long InterceptedCount { get; }

	long PassedThroughCount { get; }

	void Install();

	void Uninstall();
}
