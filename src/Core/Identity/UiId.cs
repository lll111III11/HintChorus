using System;
using HintIsolation.Core.Enums;

namespace HintIsolation.Core.Identity;

public readonly struct UiId : IEquatable<UiId>
{
	public Guid Value { get; }

	public string ShortId { get; }

	public string PluginId { get; }

	public string Member { get; }

	public UiSurface Surface { get; }

	public UiIdGranularity Granularity { get; }

	public bool IsEmpty => Value == Guid.Empty;

	public string FullId => Value.ToString("D");

	public UiId(Guid value, string pluginId, string member, UiSurface surface, UiIdGranularity granularity)
	{
		Value = value;
		PluginId = pluginId ?? "Unknown";
		Member = member ?? string.Empty;
		Surface = surface;
		Granularity = granularity;
		string text = value.ToString("N");
		ShortId = ((text.Length >= 8) ? text.Substring(0, 8).ToUpperInvariant() : text.ToUpperInvariant());
	}

	public bool Equals(UiId other)
	{
		return Value.Equals(other.Value);
	}

	public override bool Equals(object? obj)
	{
		if (obj is UiId other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Value.GetHashCode();
	}

	public override string ToString()
	{
		return $"{ShortId}·{Surface}·{PluginId}";
	}

	public static bool operator ==(UiId left, UiId right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(UiId left, UiId right)
	{
		return !left.Equals(right);
	}
}
