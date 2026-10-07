namespace HintChorus.Core.Interfaces;

public interface IHintTextSource
{
	string ModuleId { get; }

	string DisplayName { get; }

	byte Priority { get; }

	bool TryGetText(ReferenceHub hub, out string text);
}
