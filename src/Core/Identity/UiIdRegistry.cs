using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HintIsolation.Core.Enums;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Yaml;

namespace HintIsolation.Core.Identity;

public static class UiIdRegistry
{
	private readonly struct CacheKey(UiSurface surface, UiIdGranularity granularity, IntPtr method, int ilOffset) : IEquatable<CacheKey>
	{
		private readonly UiSurface _surface = surface;

		private readonly UiIdGranularity _granularity = granularity;

		private readonly IntPtr _method = method;

		private readonly int _ilOffset = ilOffset;

		public bool Equals(CacheKey other)
		{
			if (_surface == other._surface && _granularity == other._granularity && _method == other._method)
			{
				return _ilOffset == other._ilOffset;
			}
			return false;
		}

		public override bool Equals(object? obj)
		{
			if (obj is CacheKey other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (((17 * 31 + (int)_surface) * 31 + (int)_granularity) * 31 + _method.GetHashCode()) * 31 + _ilOffset;
		}
	}

	private const string Namespace = "HintIsolation.UiId/v1";

	private const string FileName = "uiids.yml";

	private const int MaxMergeHops = 8;

	private static readonly Dictionary<CacheKey, UiId> Cache = new Dictionary<CacheKey, UiId>();

	private static readonly Dictionary<string, UiIdRecord> RecordsByShortId = new Dictionary<string, UiIdRecord>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<Guid, UiIdRecord> RecordsById = new Dictionary<Guid, UiIdRecord>();

	private static readonly object Sync = new object();

	private static bool _loaded;

	private static bool _dirty;

	/// <summary>只有"命中次数/最后活跃时间"变了(结构没变)。这类统计不是实时数据, 落盘频率要低得多。</summary>
	private static bool _statsDirty;

	private static DateTime _lastSaveUtc = DateTime.UtcNow;

	private static DateTime _lastStatsSaveUtc = DateTime.UtcNow;

	/// <summary>统计类改动的最小落盘间隔(秒)。</summary>
	private const double StatsSaveIntervalSeconds = 300.0;

	/// <summary>结构类改动的最小落盘间隔(秒)。</summary>
	private const double StructuralSaveIntervalSeconds = 10.0;

	public static UiIdGranularity Granularity { get; set; } = UiIdGranularity.Method;

	public static bool Persist { get; set; } = true;

	public static int Count
	{
		get
		{
			lock (Sync)
			{
				return RecordsById.Count;
			}
		}
	}

	public static UiId Resolve(UiSurface surface, Assembly assembly, MethodBase? method, int ilOffset)
	{
		CacheKey key = new CacheKey(surface, Granularity, method?.MethodHandle.Value ?? IntPtr.Zero, ilOffset);
		lock (Sync)
		{
			if (Cache.TryGetValue(key, out var value))
			{
				TouchRecordLocked(value);
				return value;
			}
			string text = assembly.GetName().Name ?? "Unknown";
			string member = BuildCanonicalMember(method);
			Guid value2 = DeterministicGuid(BuildCanonicalName(surface, text, member, ilOffset));
			UiId uiId = new UiId(value2, text, member, surface, Granularity);
			Cache[key] = uiId;
			RegisterRecordLocked(uiId);
			return uiId;
		}
	}

	public static UiId ResolveRoute(UiId id)
	{
		lock (Sync)
		{
			if (!RecordsById.TryGetValue(id.Value, out UiIdRecord value) || string.IsNullOrEmpty(value.MergeInto))
			{
				return id;
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { value.ShortId };
			for (int i = 0; i < 8; i++)
			{
				string mergeInto = value.MergeInto;
				if (!RecordsByShortId.TryGetValue(mergeInto, out UiIdRecord value2))
				{
					Logger.Error((object)("[HintIsolation] UiId 合并链指向不存在的信口 '" + mergeInto + "', 已回退到原信口 '" + id.ShortId + "'"));
					return id;
				}
				if (!hashSet.Add(value2.ShortId))
				{
					Logger.Error((object)("[HintIsolation] UiId 合并链成环于 '" + value2.ShortId + "', 已回退到原信口 '" + id.ShortId + "'"));
					return id;
				}
				value = value2;
				if (string.IsNullOrEmpty(value.MergeInto))
				{
					return new UiId(Guid.Parse(value.Id), value.Plugin, value.Member, id.Surface, id.Granularity);
				}
			}
			Logger.Error((object)$"[HintIsolation] UiId 合并链超过 {8} 跳(疑似成环), 起点 '{id.ShortId}', 已回退到原信口");
			return id;
		}
	}

	public static string DisplayNameOf(UiId id)
	{
		lock (Sync)
		{
			if (RecordsById.TryGetValue(id.Value, out UiIdRecord value))
			{
				return string.IsNullOrWhiteSpace(value.Alias) ? id.PluginId : value.Alias;
			}
			return id.PluginId;
		}
	}

	public static IReadOnlyList<UiIdRecord> Snapshot()
	{
		lock (Sync)
		{
			return RecordsById.Values.OrderBy((UiIdRecord r) => r.Plugin, StringComparer.Ordinal).ThenBy((UiIdRecord r) => r.Member, StringComparer.Ordinal).ToArray();
		}
	}

	public static bool TryGetByShortId(string shortId, out UiIdRecord record)
	{
		lock (Sync)
		{
			return RecordsByShortId.TryGetValue(shortId, out record);
		}
	}

	public static bool SetAlias(string shortId, string? newAlias)
	{
		lock (Sync)
		{
			if (!RecordsByShortId.TryGetValue(shortId, out UiIdRecord value))
			{
				return false;
			}
			value.Alias = (string.IsNullOrWhiteSpace(newAlias) ? null : newAlias);
			_dirty = true;
			return true;
		}
	}

	public static bool Merge(string shortId, string intoShortId)
	{
		if (string.Equals(shortId, intoShortId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		lock (Sync)
		{
			if (!RecordsByShortId.TryGetValue(shortId, out UiIdRecord value) || !RecordsByShortId.ContainsKey(intoShortId))
			{
				return false;
			}
			value.MergeInto = intoShortId;
			_dirty = true;
			return true;
		}
	}

	public static bool Unmerge(string shortId)
	{
		lock (Sync)
		{
			if (!RecordsByShortId.TryGetValue(shortId, out UiIdRecord value) || value.MergeInto == null)
			{
				return false;
			}
			value.MergeInto = null;
			_dirty = true;
			return true;
		}
	}

	public static void Load()
	{
		lock (Sync)
		{
			if (_loaded)
			{
				return;
			}
			_loaded = true;
			if (!Persist)
			{
				return;
			}
			try
			{
				string path = ResolvePath();
				if (!File.Exists(path))
				{
					return;
				}
				List<UiIdRecord> list = YamlConfigParser.Deserializer.Deserialize<List<UiIdRecord>>(File.ReadAllText(path));
				if (list == null)
				{
					return;
				}
				foreach (UiIdRecord item in list)
				{
					if (!string.IsNullOrEmpty(item.ShortId) && Guid.TryParse(item.Id, out var result))
					{
						RecordsByShortId[item.ShortId] = item;
						RecordsById[result] = item;
					}
				}
				StartupLog.Info($"[HintIsolation] UiId 注册表已载入 {RecordsById.Count} 条记录");
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintIsolation] UiId 注册表载入失败(将使用内存态): " + ex.Message));
			}
		}
	}

	public static void Tick()
	{
		lock (Sync)
		{
			if (!Persist)
			{
				return;
			}
			DateTime utcNow = DateTime.UtcNow;
			if (_dirty && (utcNow - _lastSaveUtc).TotalSeconds >= StructuralSaveIntervalSeconds)
			{
				SaveLocked();
				return;
			}
			// 命中次数/最后活跃时间只是统计。以前每次 UI 调用都会标脏, 于是只要 UI 还在刷新,
			// uiids.yml 就会被整份重写(每 10 秒一次, 写盘期间还占着 Sync)。统计改为低频落盘。
			if (_statsDirty && (utcNow - _lastStatsSaveUtc).TotalSeconds >= StatsSaveIntervalSeconds)
			{
				SaveLocked();
			}
		}
	}

	public static void Save()
	{
		lock (Sync)
		{
			SaveLocked();
		}
	}

	private static void SaveLocked()
	{
		if (!Persist)
		{
			_dirty = false;
			_statsDirty = false;
			return;
		}
		try
		{
			string path = ResolvePath();
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			List<UiIdRecord> list = RecordsById.Values.OrderBy((UiIdRecord r) => r.Plugin, StringComparer.Ordinal).ThenBy((UiIdRecord r) => r.Member, StringComparer.Ordinal).ToList();
			string contents = YamlConfigParser.Serializer.Serialize((object)list);
			// 原子替换: 直接 WriteAllText 覆盖时, 万一正好在写的中途崩服/断电, uiids.yml 会被截断成一堆废数据,
			// 下次 Load 只打一条错误日志就把用户手工写的 alias/Merge 全丢掉了。
			string tempPath = path + ".tmp";
			File.WriteAllText(tempPath, contents);
			if (File.Exists(path))
			{
				try
				{
					File.Replace(tempPath, path, null);
				}
				catch (Exception)
				{
					// 某些文件系统不允许 Replace, 退回直接覆盖
					File.Copy(tempPath, path, overwrite: true);
					File.Delete(tempPath);
				}
			}
			else
			{
				File.Move(tempPath, path);
			}
			_dirty = false;
			_statsDirty = false;
			_lastSaveUtc = DateTime.UtcNow;
			_lastStatsSaveUtc = _lastSaveUtc;
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] UiId 注册表落盘失败: " + ex.Message));
		}
	}

	public static void Clear()
	{
		lock (Sync)
		{
			Cache.Clear();
			RecordsByShortId.Clear();
			RecordsById.Clear();
			_loaded = false;
			_dirty = false;
			_statsDirty = false;
		}
	}

	public static string ResolvePath()
	{
		return Path.Combine(PathManager.Configs.FullName, Server.Port.ToString(CultureInfo.InvariantCulture), "HintIsolation", "uiids.yml");
	}

	private static string BuildCanonicalMember(MethodBase? method)
	{
		if ((object)method == null)
		{
			return "<unknown>";
		}
		string text = method.DeclaringType?.FullName ?? method.DeclaringType?.Name ?? "<type>";
		if (Granularity == UiIdGranularity.Assembly)
		{
			return "<assembly>";
		}
		if (Granularity == UiIdGranularity.Type)
		{
			return text;
		}
		string text2 = string.Join(",", from p in method.GetParameters()
			select p.ParameterType.Name);
		return text + "." + method.Name + "(" + text2 + ")";
	}

	private static string BuildCanonicalName(UiSurface surface, string assembly, string member, int ilOffset)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("HintIsolation.UiId/v1").Append('|').Append(surface)
			.Append('|')
			.Append(assembly)
			.Append('|')
			.Append(member);
		if (Granularity == UiIdGranularity.CallSite && ilOffset >= 0)
		{
			stringBuilder.Append('|').Append(ilOffset);
		}
		return stringBuilder.ToString();
	}

	private static Guid DeterministicGuid(string name)
	{
		byte[] array;
		using (MD5 mD = MD5.Create())
		{
			array = mD.ComputeHash(Encoding.UTF8.GetBytes(name));
		}
		char[] array2 = new char[32];
		for (int i = 0; i < 16; i++)
		{
			array2[i * 2] = "0123456789abcdef"[array[i] >> 4];
			array2[i * 2 + 1] = "0123456789abcdef"[array[i] & 0xF];
		}
		array2[12] = '5';
		array2[16] = "89ab"[(array[8] >> 4) & 3];
		return Guid.ParseExact(new string(array2), "N");
	}

	private static void RegisterRecordLocked(UiId id)
	{
		if (!RecordsById.ContainsKey(id.Value))
		{
			string text = DateTime.UtcNow.ToString("o");
			UiIdRecord uiIdRecord = new UiIdRecord
			{
				Id = id.FullId,
				ShortId = id.ShortId,
				Surface = id.Surface.ToString(),
				Plugin = id.PluginId,
				Member = id.Member,
				Granularity = id.Granularity.ToString(),
				Hits = 0L,
				FirstSeenUtc = text,
				LastSeenUtc = text
			};
			RecordsById[id.Value] = uiIdRecord;
			if (!RecordsByShortId.ContainsKey(uiIdRecord.ShortId))
			{
				RecordsByShortId[uiIdRecord.ShortId] = uiIdRecord;
			}
			_dirty = true;
		}
	}

	private static void TouchRecordLocked(UiId id)
	{
		if (RecordsById.TryGetValue(id.Value, out UiIdRecord value))
		{
			value.Hits++;
			value.LastSeenUtc = DateTime.UtcNow.ToString("o");
			// 只标"统计脏": 不参与结构落盘的 10 秒判定, 否则 UI 活跃期间整份文件会被反复重写
			_statsDirty = true;
		}
	}
}
