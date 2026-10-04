using HintIsolation.Core.Enums;

namespace HintIsolation.Core.Compat;

public sealed record FrameworkRoute(UiFramework Framework, UiSurface Surface, string FrameworkCall, string GameTarget, bool Covered);
