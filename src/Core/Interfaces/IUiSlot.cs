using System;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Layout;

namespace HintChorus.Core.Interfaces;

public interface IUiSlot
{
	UiId Id { get; }

	UiSurface Surface { get; }

	string SlotId { get; }

	string PluginId { get; }

	string DisplayName { get; }

	byte Priority { get; set; }

	bool ShowLabel { get; set; }

	/// <summary>
	/// 该信口<b>当前生效的屏幕位置</b>(只读; 由合成器每帧写回)。
	/// <para>插件可用它自检落点; 想改位置请用 <c>UiIsolation.SetHintPosition</c>。</para>
	/// </summary>
	HintPosition Position { get; }

	int EntryCount { get; }

	long TotalReceived { get; }

	DateTime LastActivityUtc { get; }

	string RenderText();
}
