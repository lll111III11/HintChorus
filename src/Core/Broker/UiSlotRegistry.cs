using System;
using System.Collections.Generic;
using System.Linq;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interfaces;
using HintIsolation.Core.Models;

namespace HintIsolation.Core.Broker;

public static class UiSlotRegistry
{
	private static readonly Dictionary<Guid, UiSlot> Slots = new Dictionary<Guid, UiSlot>();

	private static readonly Dictionary<UiSurface, List<UiSlot>> SortedBySurface = new Dictionary<UiSurface, List<UiSlot>>();

	private static readonly object Sync = new object();

	private static long _version;

	public static long CurrentVersion => _version;

	public static int Count
	{
		get
		{
			lock (Sync)
			{
				return Slots.Count;
			}
		}
	}

	public static IReadOnlyList<string> PluginIds
	{
		get
		{
			lock (Sync)
			{
				return Slots.Values.Select((UiSlot s) => s.PluginId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string x) => x, StringComparer.Ordinal)
					.ToArray();
			}
		}
	}

	public static IReadOnlyList<UiId> Ids
	{
		get
		{
			lock (Sync)
			{
				return Slots.Values.Select((UiSlot s) => s.Id).ToArray();
			}
		}
	}

	internal static UiSlot GetOrCreate(UiId id, string displayName, byte priority, bool showLabel, int maxEntries, float maxDuration, HintOrigin origin = HintOrigin.Attributed)
	{
		lock (Sync)
		{
			if (Slots.TryGetValue(id.Value, out UiSlot value))
			{
				return value;
			}
			UiSlot uiSlot = new UiSlot(id, displayName, priority, showLabel, maxEntries, maxDuration, origin);
			Slots.Add(id.Value, uiSlot);
			SortedBySurface.Remove(id.Surface);
			_version++;
			return uiSlot;
		}
	}

	internal static List<UiSlot> SortedSlots(UiSurface surface)
	{
		lock (Sync)
		{
			if (SortedBySurface.TryGetValue(surface, out List<UiSlot> value))
			{
				return value;
			}
			List<UiSlot> list = (from s in Slots.Values
				where s.Surface == surface
				orderby s.Priority
				select s).ThenBy((UiSlot s) => s.SlotId, StringComparer.Ordinal).ToList();
			SortedBySurface[surface] = list;
			return list;
		}
	}

	public static IReadOnlyList<IUiSlot> Snapshot(UiSurface surface)
	{
		lock (Sync)
		{
			return SortedSlots(surface).Cast<IUiSlot>().ToArray();
		}
	}

	public static IReadOnlyList<IUiSlot> Snapshot()
	{
		lock (Sync)
		{
			return Slots.Values.Cast<IUiSlot>().ToArray();
		}
	}

	internal static void PruneAll(float now)
	{
		lock (Sync)
		{
			foreach (UiSlot value in Slots.Values)
			{
				value.Prune(now);
			}
		}
	}

	public static void Clear()
	{
		lock (Sync)
		{
			Slots.Clear();
			SortedBySurface.Clear();
			_version++;
		}
	}

	public static bool Remove(UiId id)
	{
		lock (Sync)
		{
			if (!Slots.Remove(id.Value))
			{
				return false;
			}
			SortedBySurface.Remove(id.Surface);
			_version++;
			return true;
		}
	}

	internal static void ApplyProfile(byte priority, bool showLabel)
	{
		lock (Sync)
		{
			foreach (UiSlot value in Slots.Values)
			{
				value.Priority = priority;
				value.ShowLabel = showLabel;
			}
			SortedBySurface.Clear();
			_version++;
		}
	}
}
