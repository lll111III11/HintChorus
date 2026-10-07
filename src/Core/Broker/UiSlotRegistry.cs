using System;
using System.Collections.Generic;
using System.Linq;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Interfaces;
using HintChorus.Core.Layout;
using HintChorus.Core.Models;

namespace HintChorus.Core.Broker;

public static class UiSlotRegistry
{
	private static readonly Dictionary<Guid, UiSlot> Slots = new Dictionary<Guid, UiSlot>();

	private static readonly Dictionary<UiSurface, List<UiSlot>> SortedBySurface = new Dictionary<UiSurface, List<UiSlot>>();

	/// <summary>
	/// <b>插件级位置预设</b>(自有写法的编程通道)。
	/// <para>插件调用 <c>SetHintPosition</c> 时可能<b>还没有信口</b>(信口是首次发 UI 才创建的),
	/// 所以位置要按插件名先记住, 等信口创建时再套上去。</para>
	/// </summary>
	private static readonly Dictionary<string, HintPosition> PluginPositions = new Dictionary<string, HintPosition>(StringComparer.OrdinalIgnoreCase);

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
			// 套用插件级位置预设 —— 插件完全可能在"还没有信口"时就先声明过位置。
			if (PluginPositions.TryGetValue(id.PluginId, out HintPosition preset))
			{
				uiSlot.ExplicitPosition = preset;
			}
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
			PluginPositions.Clear();
			_version++;
		}
	}

	/// <summary>
	/// <b>给一个插件预设屏幕位置</b>(自有写法 · 编程通道)。
	/// <para>对<b>已存在</b>的信口立即生效; 对<b>之后才创建</b>的信口, 在创建时自动套用。</para>
	/// </summary>
	/// <param name="pluginId">插件标识(与归因得到的 PluginId 一致, 通常是程序集名)。</param>
	/// <param name="position">目标位置。</param>
	/// <returns>被立即改写的已存在信口数。</returns>
	public static int SetPluginPosition(string pluginId, HintPosition position)
	{
		if (string.IsNullOrWhiteSpace(pluginId))
		{
			return 0;
		}

		string key = pluginId.Trim();
		lock (Sync)
		{
			PluginPositions[key] = position;

			int affected = 0;
			foreach (UiSlot slot in Slots.Values)
			{
				if (string.Equals(slot.PluginId, key, StringComparison.OrdinalIgnoreCase))
				{
					slot.ExplicitPosition = position;
					affected++;
				}
			}
			return affected;
		}
	}

	/// <summary>撤销一个插件的预设位置(回到解析链推断)。返回是否确有预设被撤销。</summary>
	public static bool ClearPluginPosition(string pluginId)
	{
		if (string.IsNullOrWhiteSpace(pluginId))
		{
			return false;
		}

		string key = pluginId.Trim();
		lock (Sync)
		{
			bool removed = PluginPositions.Remove(key);
			foreach (UiSlot slot in Slots.Values)
			{
				if (string.Equals(slot.PluginId, key, StringComparison.OrdinalIgnoreCase))
				{
					slot.ExplicitPosition = null;
				}
			}
			return removed;
		}
	}

	/// <summary>已预设位置的插件数(诊断用)。</summary>
	public static int PluginPositionCount
	{
		get
		{
			lock (Sync)
			{
				return PluginPositions.Count;
			}
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
