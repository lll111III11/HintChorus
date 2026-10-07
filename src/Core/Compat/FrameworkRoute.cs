using HintChorus.Core.Enums;

namespace HintChorus.Core.Compat;

public sealed record FrameworkRoute(UiFramework Framework, UiSurface Surface, string FrameworkCall, string GameTarget, bool Covered);
