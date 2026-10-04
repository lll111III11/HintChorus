using System.Collections.Generic;

namespace HintIsolation.Core.Interfaces;

public interface IHintBroker
{
	bool IsRunning { get; }

	bool Enabled { get; set; }

	IReadOnlyCollection<IHintChannel> Channels { get; }

	IReadOnlyCollection<IHintTextSource> Sources { get; }

	IReadOnlyList<IUiSlot> AttributedSlots { get; }

	void Start(float refreshInterval = 0.75f, float resendLeeway = 0.15f);

	void Stop();

	void ShowTransient(ReferenceHub hub, string text, float duration = 3f);

	void ClearTransients(ReferenceHub hub);

	void ReSort();
}
