using System;
using System.Collections.Generic;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Identity;
using HintIsolation.Core.Interfaces;
using HintIsolation.Core.Utilities;
using UnityEngine;

namespace HintIsolation.Core.Models;

public sealed class UiSlot : IUiSlot
{
	private readonly List<HintEntry> _entries = new List<HintEntry>();

	private readonly object _gate = new object();

	private readonly int _maxEntries;

	private readonly float _maxDuration;

	private readonly HintOrigin _origin;

	public HintOrigin Origin => _origin;

	public UiId Id { get; }

	public UiSurface Surface => Id.Surface;

	public string SlotId => Id.ShortId;

	public string PluginId => Id.PluginId;

	public string DisplayName { get; set; }

	public byte Priority { get; set; }

	public bool ShowLabel { get; set; }

	public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

	public long TotalReceived { get; private set; }

	public float LastPassedAt { get; set; }

	public string Member => Id.Member;

	public long Revision { get; private set; }

	public int EntryCount
	{
		get
		{
			lock (_gate)
			{
				return _entries.Count;
			}
		}
	}

	internal UiSlot(UiId id, string displayName, byte priority, bool showLabel, int maxEntries, float maxDuration, HintOrigin origin = HintOrigin.Attributed)
	{
		Id = id;
		DisplayName = displayName;
		Priority = priority;
		ShowLabel = showLabel;
		_maxEntries = Math.Max(1, maxEntries);
		_maxDuration = Math.Max(0.5f, maxDuration);
		_origin = origin;
	}

	internal void Push(string text, float now, float duration)
	{
		float num = Math.Min(Math.Max(duration, 0.5f), _maxDuration);
		lock (_gate)
		{
			TotalReceived++;
			LastActivityUtc = DateTime.UtcNow;
			for (int i = 0; i < _entries.Count; i++)
			{
				if (string.Equals(_entries[i].Text, text, StringComparison.Ordinal))
				{
					HintEntry hintEntry = _entries[i];
					hintEntry.ExpiresAt = now + num;
					hintEntry.Duration = num;
					_entries.RemoveAt(i);
					_entries.Add(hintEntry);
					Revision++;
					return;
				}
			}
			_entries.Add(new HintEntry(text, num, now + num, _origin, SlotId));
			while (_entries.Count > _maxEntries)
			{
				_entries.RemoveAt(0);
			}
			Revision++;
		}
	}

	internal void Prune(float now)
	{
		lock (_gate)
		{
			for (int num = _entries.Count - 1; num >= 0; num--)
			{
				if (_entries[num].IsExpired(now))
				{
					_entries.RemoveAt(num);
					Revision++;
				}
			}
		}
	}

	internal int AliveCount(float now)
	{
		lock (_gate)
		{
			int num = 0;
			for (int i = 0; i < _entries.Count; i++)
			{
				if (!_entries[i].IsExpired(now))
				{
					num++;
				}
			}
			return num;
		}
	}

	internal float MinRemaining(float now)
	{
		lock (_gate)
		{
			float num = float.MaxValue;
			for (int i = 0; i < _entries.Count; i++)
			{
				float num2 = _entries[i].Remaining(now);
				if (num2 < num)
				{
					num = num2;
				}
			}
			return (num == float.MaxValue) ? 0f : num;
		}
	}

	public string RenderText()
	{
		float time = Time.time;
		lock (_gate)
		{
			List<string> list = new List<string>(_entries.Count);
			for (int i = 0; i < _entries.Count; i++)
			{
				HintEntry hintEntry = _entries[i];
				if (!hintEntry.IsExpired(time))
				{
					list.Add(hintEntry.Text);
				}
			}
			if (list.Count == 0)
			{
				return string.Empty;
			}
			if (list.Count == 1)
			{
				return list[0];
			}
			return HintFormat.JoinLines(list);
		}
	}

	/// <summary>
	/// 按行给出仍然有效的条目文本(一条一行)。
	/// <para>固定行位排版必须"按行"计数: 若像 <see cref="RenderText"/> 那样把多条拼成一整块,
	/// 行数就不可控, 固定行位也就无从谈起。</para>
	/// </summary>
	internal void AppendAliveLines(List<string> buffer, float now)
	{
		lock (_gate)
		{
			for (int i = 0; i < _entries.Count; i++)
			{
				HintEntry hintEntry = _entries[i];
				if (!hintEntry.IsExpired(now))
				{
					buffer.Add(hintEntry.Text);
				}
			}
		}
	}

	/// <summary>该信口当前是否还有有效内容(固定行位用它决定这一行是内容还是空行)。</summary>
	internal bool HasAlive(float now)
	{
		lock (_gate)
		{
			for (int i = 0; i < _entries.Count; i++)
			{
				if (!_entries[i].IsExpired(now))
				{
					return true;
				}
			}
			return false;
		}
	}
}
