using HintChorus.Core.Enums;

namespace HintChorus.Core.Surfaces;

public sealed class UiSurfaceDescriptor
{
	public UiSurface Surface { get; }

	public string DisplayName { get; }

	public bool Implemented { get; }

	public string Handler { get; }

	public string EntryPoint { get; }

	internal UiSurfaceDescriptor(UiSurface surface, string displayName, bool implemented, string handler, string entryPoint)
	{
		Surface = surface;
		DisplayName = displayName;
		Implemented = implemented;
		Handler = handler;
		EntryPoint = entryPoint;
	}

	public override string ToString()
	{
		return string.Format("{0,-15} {1} {2} ← {3}", Surface, Implemented ? "[已接管]" : "[预留]", Handler, EntryPoint);
	}
}
