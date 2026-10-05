using System.Collections.Generic;
using System.Text;
using HintIsolation.Core.Enums;

namespace HintIsolation.Core.Utilities;

public static class HintFormat
{
	public const string LabelColor = "#999999";

	public const string ResetSuffix = "</size></color></b></i></u></line-height></indent></margin></align>";

	public static string LabelPrefix(string? displayName)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return string.Empty;
		}
		return "<color=#999999><b>[" + displayName + "]</b></color> ";
	}

	public static string Align(string text, HintAlignment alignment)
	{
		return alignment switch
		{
			HintAlignment.Center => "<align=center>" + text + "</align>", 
			HintAlignment.Right => "<align=right>" + text + "</align>", 
			_ => text, 
		};
	}

	public static string Line(string? displayName, bool showLabel, string text, HintAlignment alignment)
	{
		return Align(showLabel ? (LabelPrefix(displayName) + text) : text, alignment);
	}

	public static string JoinLines(IReadOnlyList<string> lines)
	{
		if (lines.Count == 0)
		{
			return string.Empty;
		}
		if (lines.Count == 1)
		{
			return lines[0];
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < lines.Count; i++)
		{
			stringBuilder.Append(lines[i]);
			if (i < lines.Count - 1)
			{
				stringBuilder.Append("</size></color></b></i></u></line-height></indent></margin></align>").Append('\n');
			}
		}
		return stringBuilder.ToString();
	}
}
