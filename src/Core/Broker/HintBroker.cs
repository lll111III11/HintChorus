using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Interfaces;
using HintIsolation.Core.Layout;
using HintIsolation.Core.Models;
using HintIsolation.Core.Transport;
using HintIsolation.Core.Utilities;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using UnityEngine;

namespace HintIsolation.Core.Broker;

public sealed class HintBroker : IHintBroker
{
	private sealed class PlayerState
	{
		public string LastText = string.Empty;

		public float NextSendAt;

		public float HoldUntil;

		public long LastSignature;

		public bool HasSignature;
	}

	private sealed class TransientHint
	{
		public required ReferenceHub Hub;

		public required string Text;

		public required float Until;

		public int TextHash;
	}

	private enum UnitKind : byte
	{
		Channel,
		Source,
		Slot
	}

	private readonly struct RenderUnit(byte priority, string moduleId, UnitKind kind, IHintChannel? channel, IHintTextSource? source, UiSlot? slot)
	{
		public byte Priority { get; } = priority;

		public string ModuleId { get; } = moduleId;

		public UnitKind Kind { get; } = kind;

		public IHintChannel? Channel { get; } = channel;

		public IHintTextSource? Source { get; } = source;

		public UiSlot? Slot { get; } = slot;
	}

	private sealed class HubComparer : IEqualityComparer<ReferenceHub>
	{
		public static readonly HubComparer Instance = new HubComparer();

		public bool Equals(ReferenceHub? x, ReferenceHub? y)
		{
			return x == y;
		}

		public int GetHashCode(ReferenceHub obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}

	private readonly Dictionary<string, HintChannel> _channels = new Dictionary<string, HintChannel>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, IHintTextSource> _sources = new Dictionary<string, IHintTextSource>(StringComparer.OrdinalIgnoreCase);

	private readonly LinkedList<TransientHint> _transients = new LinkedList<TransientHint>();

	private readonly Dictionary<ReferenceHub, PlayerState> _states = new Dictionary<ReferenceHub, PlayerState>(HubComparer.Instance);

	private readonly HashSet<Hint> _ownHints = new HashSet<Hint>();

	private readonly object _sync = new object();

	private List<RenderUnit>? _renderUnits;

	private long _unitsRevision = -1L;

	private long _channelRevision;

	private long _slotRevision = -1L;

	private CoroutineHandle _loop;

	private float _refresh = 0.75f;

	private float _leeway = 0.15f;

	private const float FastInterval = 0.04f;

	private const float MinSendDuration = 2.5f;

	private const float DefaultSendDuration = 2f;

	private readonly HashSet<ReferenceHub> _dirty = new HashSet<ReferenceHub>(HubComparer.Instance);

	private bool _dirtyAll;

	private const ulong SignatureSeed = 14695981039346656037uL;

	public static HintBroker Instance { get; } = new HintBroker();

	public bool IsRunning { get; private set; }

	public bool Enabled { get; set; } = true;

	public bool DebugLog { get; set; }

	public HintEffect[]? GlobalEffects { get; set; }

	public long RenderTicks { get; private set; }

	public long HintsSent { get; private set; }

	public long TransientCount { get; private set; }

	public long SuppressedResends { get; private set; }

	public long InputSignatureSkips { get; private set; }

	public bool SuppressUnchangedResend { get; set; } = true;

	public bool InputSignatureEnabled { get; set; } = true;

	// ── 排版(治"上下位置错位") ────────────────────────────────────────────────
	/// <summary>固定行位排版(默认开): 易变区固定行数 + 常驻区每个归属预留一行, 于是块高恒定、
	/// 任何 UI 的进出都不会推动别人。关掉退回紧凑排版(省屏, 但位置会随内容增减而变)。</summary>
	public bool FixedLayout { get; set; } = true;

	/// <summary>排版模式。
	/// <para><see cref="LayoutMode.Rows"/> (默认): 固定行位 —— 用空行占位, 位置绝对稳定, 代价是占屏。</para>
	/// <para><see cref="LayoutMode.Offsets"/>: 算偏移 —— 不给空行, 每行用 &lt;voffset&gt; 摆到自己的固定槽位;
	/// 思路取自 CC0 的 RueI("不是网格/行基, 而是算偏移")。不占屏, 位置同样稳定。</para>
	/// <para><see cref="LayoutMode.Compact"/>: 紧凑 —— 什么都不补, 最省屏, 但位置会随内容增减而变。</para></summary>
	public enum LayoutMode : byte
	{
		Compact = 0,
		Rows = 1,
		Offsets = 2
	}

	/// <summary>当前排版模式。</summary>
	public LayoutMode Layout { get; set; } = LayoutMode.Rows;

	private static int _layoutModeWarned;

	/// <summary>
	/// 容错解析 layout_mode。
	/// <para>刻意用字符串而不是枚举: 配置里的枚举一旦拼错, YamlDotNet 反序列化会抛异常,
	/// 而 LabAPI 的 TryLoadConfig 会把<b>整份配置</b>丢掉回落到全默认 —— 一个字母的笔误代价过大。
	/// 这里解析不了就退回默认并告警, 其余配置照常生效。</para>
	/// </summary>
	public static LayoutMode ParseLayoutMode(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return LayoutMode.Rows;
		}
		switch (value.Trim().ToLowerInvariant())
		{
		case "rows":
		case "row":
		case "fixed":
			return LayoutMode.Rows;
		case "offsets":
		case "offset":
		case "voffset":
			return LayoutMode.Offsets;
		case "compact":
		case "none":
			return LayoutMode.Compact;
		default:
			if (System.Threading.Interlocked.Increment(ref _layoutModeWarned) == 1)
			{
				Logger.Warn("[HintIsolation] layout_mode 取值无法识别, 已改用默认值 rows; 可选 rows / offsets / compact; 收到: " + value);
			}
			return LayoutMode.Rows;
		}
	}

	/// <summary>偏移模式下的"一行高度"(voffset 单位, 与 RueI 的坐标同一量级: 整屏约 2140)。</summary>
	public float OffsetRowHeight { get; set; } = 40f;

	/// <summary>偏移模式下的方向。若整体朝反方向偏, 改成 -1。</summary>
	public float OffsetSign { get; set; } = 1f;

	/// <summary>偏移模式下量宽度用的字号(游戏提示默认约 20)。</summary>
	public float OffsetFontSize { get; set; } = 20f;

	/// <summary>易变区行数(一次性提示 + 吸收进来的原生提示)。多了丢最老的, 少了补空行。</summary>
	public int VolatileRows { get; set; } = 3;

	/// <summary>同一个归属(插件/模块)最多占几行。1 = 与旧行为相当但顺序稳定; 0 = 不限。</summary>
	public int RowsPerPlugin { get; set; } = 1;

	/// <summary>常驻区最多预留几行空位(只限制"补空行"的数量, 有内容的行永远不丢)。</summary>
	public int PersistentRowsMax { get; set; } = 8;

	public int TrackedPlayers
	{
		get
		{
			lock (_sync)
			{
				return _states.Count;
			}
		}
	}

	public IReadOnlyCollection<IHintChannel> Channels
	{
		get
		{
			lock (_sync)
			{
				EnsureRenderUnitsLocked();
				return (from u in _renderUnits
					where u.Kind == UnitKind.Channel
					select u.Channel).ToArray();
			}
		}
	}

	public IReadOnlyCollection<IHintTextSource> Sources
	{
		get
		{
			lock (_sync)
			{
				EnsureRenderUnitsLocked();
				return (from u in _renderUnits
					where u.Kind == UnitKind.Source
					select u.Source).ToArray();
			}
		}
	}

	public IReadOnlyList<IUiSlot> AttributedSlots => UiSlotRegistry.Snapshot(UiSurface.Hint);

	private HintBroker()
	{
	}

	public void Start(float refreshInterval = 0.75f, float resendLeeway = 0.15f)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		lock (_sync)
		{
			if (IsRunning)
			{
				return;
			}
			IsRunning = true;
			_refresh = Mathf.Max(0.05f, refreshInterval);
			_leeway = Mathf.Max(0f, resendLeeway);
		}
		_loop = Timing.RunCoroutine(LoopRoutine(), (Segment)0);
		StartupLog.Info($"[HintIsolation] 合并渲染核心已启动 (刷新间隔 {_refresh}s)");
	}

	public void Stop()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		lock (_sync)
		{
			if (!IsRunning)
			{
				return;
			}
			IsRunning = false;
			Timing.KillCoroutines(new CoroutineHandle[1] { _loop });
			_channels.Clear();
			_sources.Clear();
			_transients.Clear();
			_states.Clear();
			_ownHints.Clear();
			_dirty.Clear();
			_dirtyAll = false;
			_renderUnits = null;
			_unitsRevision = -1L;
			_channelRevision = 0L;
			_slotRevision = -1L;
		}
		StartupLog.Info("[HintIsolation] 合并渲染核心已停止");
	}

	public void ReSort()
	{
		lock (_sync)
		{
			_channelRevision++;
			EnsureRenderUnitsLocked();
		}
		MarkDirty();
	}

	public void HoldSlot(ReferenceHub? hub, float seconds)
	{
		if (hub == null || seconds <= 0f)
		{
			return;
		}
		float num = Time.time + seconds;
		lock (_sync)
		{
			if (!_states.TryGetValue(hub, out PlayerState value))
			{
				value = new PlayerState();
				_states.Add(hub, value);
			}
			if (num > value.HoldUntil)
			{
				value.HoldUntil = num;
			}
			value.LastText = string.Empty;
			value.NextSendAt = 0f;
			value.LastSignature = 0L;
			value.HasSignature = false;
		}
	}

	public bool IsHolding(ReferenceHub hub)
	{
		if (hub == null)
		{
			return false;
		}
		lock (_sync)
		{
			PlayerState value;
			return _states.TryGetValue(hub, out value) && Time.time < value.HoldUntil;
		}
	}

	public void MarkDirty(ReferenceHub? hub = null)
	{
		lock (_sync)
		{
			if (hub == null)
			{
				_dirtyAll = true;
			}
			else
			{
				_dirty.Add(hub);
			}
		}
	}

	public bool RegisterChannel(HintChannel channel)
	{
		if (channel == null)
		{
			throw new ArgumentNullException("channel");
		}
		lock (_sync)
		{
			if (_channels.ContainsKey(channel.ModuleId) || _sources.ContainsKey(channel.ModuleId))
			{
				Logger.Warn((object)("[HintIsolation] 通道 '" + channel.ModuleId + "' 已存在, 拒绝重复注册(绝不抢占)"));
				return false;
			}
			_channels.Add(channel.ModuleId, channel);
			_channelRevision++;
		}
		MarkDirty();
		StartupLog.Info("[HintIsolation] 通道 '" + channel.ModuleId + "' 已独立注册");
		return true;
	}

	public HintChannel? RegisterChannel(string moduleId, string displayName, string text, float duration, byte priority)
	{
		HintChannel hintChannel = new HintChannel(moduleId, displayName, text, duration, priority);
		if (!RegisterChannel(hintChannel))
		{
			return null;
		}
		return hintChannel;
	}

	public bool UnregisterChannel(string moduleId)
	{
		lock (_sync)
		{
			if (!_channels.Remove(moduleId))
			{
				return false;
			}
			_channelRevision++;
		}
		MarkDirty();
		StartupLog.Info("[HintIsolation] 通道 '" + moduleId + "' 已卸载");
		return true;
	}

	public bool TryGetChannel(string moduleId, out HintChannel channel)
	{
		lock (_sync)
		{
			return _channels.TryGetValue(moduleId, out channel);
		}
	}

	public bool RegisterSource(IHintTextSource source)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		lock (_sync)
		{
			if (_channels.ContainsKey(source.ModuleId) || _sources.ContainsKey(source.ModuleId))
			{
				Logger.Warn((object)("[HintIsolation] 文本源 '" + source.ModuleId + "' 已存在, 拒绝重复注册(绝不抢占)"));
				return false;
			}
			_sources.Add(source.ModuleId, source);
			_channelRevision++;
		}
		MarkDirty();
		StartupLog.Info("[HintIsolation] 文本源 '" + source.ModuleId + "' 已独立注册");
		return true;
	}

	public bool UnregisterSource(string moduleId)
	{
		lock (_sync)
		{
			if (!_sources.Remove(moduleId))
			{
				return false;
			}
			_channelRevision++;
		}
		MarkDirty();
		StartupLog.Info("[HintIsolation] 文本源 '" + moduleId + "' 已卸载");
		return true;
	}

	public void ShowTransient(ReferenceHub hub, string text, float duration = 3f)
	{
		if (hub != null && !string.IsNullOrEmpty(text))
		{
			lock (_sync)
			{
				_transients.AddLast(new TransientHint
				{
					Hub = hub,
					Text = text,
					Until = Time.time + Mathf.Max(0.5f, duration),
					TextHash = text.GetHashCode()
				});
				TransientCount++;
			}
			MarkDirty(hub);
		}
	}

	public void ClearTransients(ReferenceHub hub)
	{
		if (hub == null)
		{
			return;
		}
		lock (_sync)
		{
			LinkedListNode<TransientHint> linkedListNode = _transients.First;
			while (linkedListNode != null)
			{
				LinkedListNode<TransientHint> next = linkedListNode.Next;
				if (linkedListNode.Value.Hub == hub)
				{
					_transients.Remove(linkedListNode);
				}
				linkedListNode = next;
			}
			if (_states.TryGetValue(hub, out PlayerState value))
			{
				_states.Remove(hub);
				value.LastText = string.Empty;
			}
		}
		MarkDirty(hub);
	}

	public void ClearAllTransients()
	{
		lock (_sync)
		{
			_transients.Clear();
		}
		MarkDirty();
	}

	internal bool IsOwnMergedHint(Hint hint)
	{
		lock (_sync)
		{
			return hint != null && _ownHints.Contains(hint);
		}
	}

	private void Tick(bool full)
	{
		if (!Enabled)
		{
			return;
		}
		float time = Time.time;
		RenderUnit[] units;
		TransientHint[] transients;
		bool dirtyAll;
		lock (_sync)
		{
			RenderTicks++;
			TryStep(PruneTransientsLocked, "PruneTransientsLocked");
			TryStep(PruneStatesLocked, "PruneStatesLocked");
			TryStep(EnsureRenderUnitsLocked, "EnsureRenderUnitsLocked");
			units = ((_renderUnits == null) ? Array.Empty<RenderUnit>() : _renderUnits.ToArray());
			List<TransientHint> list = new List<TransientHint>();
			foreach (TransientHint transient in _transients)
			{
				if (transient.Hub != (ReferenceHub)null)
				{
					list.Add(transient);
				}
			}
			transients = list.ToArray();
			dirtyAll = _dirtyAll;
			_dirtyAll = false;
		}
		ReferenceHub[] array2;
		if (full | dirtyAll)
		{
			List<ReferenceHub> list2 = new List<ReferenceHub>();
			Player[] array = Player.ReadyList.ToArray();
			foreach (Player val in array)
			{
				if (((val != null) ? val.ReferenceHub : null) != (ReferenceHub)null)
				{
					list2.Add(val.ReferenceHub);
				}
			}
			lock (_sync)
			{
				_dirty.Clear();
			}
			array2 = list2.ToArray();
		}
		else
		{
			lock (_sync)
			{
				array2 = _dirty.ToArray();
				_dirty.Clear();
			}
		}
		ReferenceHub[] array3 = array2;
		foreach (ReferenceHub val2 in array3)
		{
			if (val2 != null)
			{
				try
				{
					RenderPlayer(val2, units, transients, time);
				}
				catch (Exception ex)
				{
					Logger.Error((object)("[HintIsolation] 单个玩家渲染异常(已隔离, 不影响其它玩家): " + ex));
				}
			}
		}
	}

	private static void TryStep(Action step, string name)
	{
		try
		{
			step();
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintIsolation] 刷新步骤 {name} 异常(已跳过本轮该步): {arg}");
		}
	}

	private void RenderPlayer(ReferenceHub hub, RenderUnit[] units, TransientHint[] transients, float now)
	{
		if (IsHolding(hub))
		{
			return;
		}
		long num = 0L;
		if (InputSignatureEnabled)
		{
			num = ComputeInputSignature(hub, units, transients);
			lock (_sync)
			{
				if (_states.TryGetValue(hub, out PlayerState value) && value.HasSignature && value.LastSignature == num && (value.LastText.Length == 0 || now < value.NextSendAt))
				{
					InputSignatureSkips++;
					return;
				}
			}
		}
		if (!TryCompose(hub, units, transients, out string composite, out float duration))
		{
			bool flag;
			lock (_sync)
			{
				if (!_states.TryGetValue(hub, out PlayerState value2))
				{
					value2 = new PlayerState();
					_states.Add(hub, value2);
				}
				flag = value2.LastText.Length > 0;
				if (flag)
				{
					value2.LastText = string.Empty;
					value2.NextSendAt = 0f;
				}
				if (InputSignatureEnabled)
				{
					value2.LastSignature = num;
					value2.HasSignature = true;
				}
			}
			if (flag)
			{
				ShowMerged(hub, string.Empty, 0.5f);
			}
			return;
		}
		lock (_sync)
		{
			if (!_states.TryGetValue(hub, out PlayerState value3))
			{
				value3 = new PlayerState();
				_states.Add(hub, value3);
			}
			if (SuppressUnchangedResend && value3.LastText == composite && now < value3.NextSendAt)
			{
				SuppressedResends++;
				if (InputSignatureEnabled)
				{
					value3.LastSignature = num;
					value3.HasSignature = true;
				}
				return;
			}
			value3.LastText = composite;
			value3.NextSendAt = now + Mathf.Max(0.5f, duration - _leeway);
			if (InputSignatureEnabled)
			{
				value3.LastSignature = num;
				value3.HasSignature = true;
			}
			if (DebugLog)
			{
				Logger.Debug((object)string.Format("[HintIsolation→#{0}] {1}", ((NetworkBehaviour)hub).netId, composite.Replace("\n", " | ")), true);
			}
		}
		ShowMerged(hub, composite, duration);
	}

	private static ulong Mix(ulong hash, long value)
	{
		return (hash ^ (ulong)value) * 1099511628211L;
	}

	private static long ComputeInputSignature(ReferenceHub hub, RenderUnit[] units, TransientHint[] transients)
	{
		float time = Time.time;
		ulong num = 14695981039346656037uL;
		for (int i = 0; i < units.Length; i++)
		{
			RenderUnit renderUnit = units[i];
			num = Mix(num, (long)renderUnit.Kind);
			switch (renderUnit.Kind)
			{
			case UnitKind.Channel:
			{
				IHintChannel channel = renderUnit.Channel;
				if (!channel.Enabled)
				{
					num = Mix(num, 1L);
					break;
				}
				if (channel.ReceiverFilter != null && !SafeFilter(channel.ReceiverFilter, channel.ModuleId, hub))
				{
					num = Mix(num, 2L);
					break;
				}
				if (string.IsNullOrEmpty(channel.Text))
				{
					num = Mix(num, 3L);
					break;
				}
				num = Mix(num, (channel is HintChannel hintChannel) ? hintChannel.TextRevision : 0);
				num = Mix(num, channel.ShowLabel ? 1 : 0);
				num = Mix(num, (long)channel.Alignment);
				num = Mix(num, channel.Duration.GetHashCode());
				break;
			}
			case UnitKind.Slot:
			{
				UiSlot slot = renderUnit.Slot;
				num = Mix(num, slot.Revision);
				num = Mix(num, slot.AliveCount(time));
				num = Mix(num, slot.ShowLabel ? 1 : 0);
				num = Mix(num, slot.DisplayName.GetHashCode());
				break;
			}
			case UnitKind.Source:
			{
				IHintTextSource source = renderUnit.Source;
				bool flag = false;
				string text = string.Empty;
				try
				{
					flag = source.TryGetText(hub, out text) && !string.IsNullOrEmpty(text);
				}
				catch (Exception)
				{
					flag = false;
				}
				num = Mix(num, flag ? 1 : 0);
				if (flag)
				{
					num = Mix(num, text.Length);
					num = Mix(num, text.GetHashCode());
				}
				break;
			}
			}
		}
		foreach (TransientHint transientHint in transients)
		{
			if (!(transientHint.Hub != hub))
			{
				num = Mix(num, transientHint.TextHash);
				num = Mix(num, transientHint.Until.GetHashCode());
			}
		}
		return (long)num;
	}

	/// <summary>一个"归属"(插件或模块)在当前这一帧要占的行。</summary>
	private sealed class OwnerBucket
	{
		public required string Owner;

		/// <summary>该归属在 units 里的单元数 —— 预留行数按它算, 才能做到"块高只跟单元数有关, 跟有没有内容无关"。</summary>
		public int Units;

		public readonly List<string> Lines = new List<string>();
	}

	/// <summary>
	/// 把当前所有贡献者合成为"一条"提示文本。
	///
	/// <para><b>排版分两层, 高度恒定:</b></para>
	/// <list type="number">
	///   <item><b>易变区</b>(一次性提示 + 吸收进来的原生提示): 固定 <see cref="VolatileRows"/> 行,
	///     不足补空行、超了丢最老的。它们来去再频繁也不会改变块高。</item>
	///   <item><b>常驻区</b>(注册的通道 / 文本源 / 自动归因信口): 每个"归属"预留一行,
	///     没有内容时输出<b>空行</b> —— 这样各归属之间的相对行位永远不变。</item>
	/// </list>
	///
	/// <para><b>为什么必须固定行位:</b> 游戏只给一个提示口, 所有 UI 被拼成一个文本块。
	/// 只要块内行数变了, 整块的位置就跟着变 —— 这就是"装很多插件之后上下位置错位"的根因。
	/// 固定高度后, 一次性提示 / 原生提示的进出只影响易变区内部, 动不到常驻区一行。</para>
	///
	/// <para>各归属取哪一行, 一律按 <c>units</c> 的稳定顺序(优先级 → ModuleId)取,
	/// <b>不再</b>按"最近活跃"取 —— 按活跃时间取会让同一插件的内容在两条之间来回跳。</para>
	/// </summary>
	private bool TryCompose(ReferenceHub hub, RenderUnit[] units, TransientHint[] transients, out string composite, out float duration)
	{
		composite = string.Empty;
		duration = 2f;
		float now = Time.time;
		float minRemaining = float.MaxValue;
		bool anyContent = false;

		List<string> nativeLines = new List<string>();
		List<OwnerBucket> owners = new List<OwnerBucket>();
		Dictionary<string, OwnerBucket> byOwner = new Dictionary<string, OwnerBucket>(StringComparer.OrdinalIgnoreCase);
		List<string> scratch = new List<string>();

		// 让一个归属"占位"(即使这一帧没有内容, 也要给它留一行, 位置才稳定)
		OwnerBucket TouchOwner(string owner)
		{
			// 归属名理论上不该为空, 但第三方传进来的 ModuleId 什么都可能有;
			// 一旦为空, Dictionary.TryGetValue(null) 会抛 ArgumentNullException。
			if (string.IsNullOrEmpty(owner))
			{
				owner = "(未命名)";
			}
			if (!byOwner.TryGetValue(owner, out OwnerBucket bucket))
			{
				bucket = new OwnerBucket { Owner = owner };
				byOwner[owner] = bucket;
				owners.Add(bucket);
			}
			bucket.Units++;
			return bucket;
		}

		void AddLine(OwnerBucket bucket, string text, float remaining)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			anyContent = true;
			if (remaining < minRemaining)
			{
				minRemaining = remaining;
			}
			bucket.Lines.Add(text);
		}

		foreach (RenderUnit renderUnit in units)
		{
			switch (renderUnit.Kind)
			{
			case UnitKind.Channel:
			{
				IHintChannel channel = renderUnit.Channel;
				if (channel == null)
				{
					break;
				}
				OwnerBucket bucket = TouchOwner(channel.ModuleId);
				if (!channel.Enabled || string.IsNullOrEmpty(channel.Text))
				{
					break;
				}
				if (channel.ReceiverFilter != null && !SafeFilter(channel.ReceiverFilter, channel.ModuleId, hub))
				{
					break;
				}
				AddLine(bucket, HintFormat.Line(channel.DisplayName, channel.ShowLabel, channel.Text, channel.Alignment), channel.Duration);
				break;
			}
			case UnitKind.Source:
			{
				IHintTextSource source = renderUnit.Source;
				if (source == null)
				{
					break;
				}
				OwnerBucket bucket = TouchOwner(source.ModuleId);
				string text;
				try
				{
					if (!source.TryGetText(hub, out text) || string.IsNullOrEmpty(text))
					{
						break;
					}
				}
				catch (Exception arg)
				{
					Logger.Error($"[HintIsolation] 文本源 '{source.ModuleId}' 抛异常(已隔离): {arg}");
					break;
				}
				AddLine(bucket, HintFormat.Line(source.DisplayName, showLabel: true, text, HintAlignment.Left), 2f);
				break;
			}
			case UnitKind.Slot:
			{
				UiSlot slot = renderUnit.Slot;
				if (slot == null)
				{
					break;
				}
				OwnerBucket bucket = TouchOwner(slot.PluginId);
				if (!slot.HasAlive(now))
				{
					break;
				}
				float remaining = slot.MinRemaining(now);
				scratch.Clear();
				slot.AppendAliveLines(scratch, now);
				if (scratch.Count == 0)
				{
					break;
				}
				if (slot.Origin == HintOrigin.Native)
				{
					// 原生提示是"会来会去"的, 归入易变区(否则一条弹药上限提示就会把常驻区顶得上下跳)
					foreach (string line in scratch)
					{
						if (!string.IsNullOrEmpty(line))
						{
							anyContent = true;
							if (remaining < minRemaining)
							{
								minRemaining = remaining;
							}
							nativeLines.Add(HintFormat.Line(slot.DisplayName, slot.ShowLabel, line, HintAlignment.Left));
						}
					}
					break;
				}
				foreach (string line in scratch)
				{
					if (!string.IsNullOrEmpty(line))
					{
						AddLine(bucket, HintFormat.Line(slot.DisplayName, slot.ShowLabel, line, HintAlignment.Left), remaining);
					}
				}
				break;
			}
			}
		}

		// ── 一次性提示 ──
		List<string> transientLines = new List<string>();
		foreach (TransientHint transient in transients)
		{
			if ((object)transient.Hub != hub)
			{
				continue;
			}
			float left = transient.Until - now;
			if (left < minRemaining)
			{
				minRemaining = left;
			}
			transientLines.Add(transient.Text);
			anyContent = true;
		}
		// ── 装配最终行列表(行装配是纯函数, 可离线验证"块高恒定") ──
		List<int> ownerUnits = new List<int>(owners.Count);
		List<List<string>> ownerLines = new List<List<string>>(owners.Count);
		foreach (OwnerBucket bucket in owners)
		{
			ownerUnits.Add(bucket.Units);
			ownerLines.Add(bucket.Lines);
		}

		// 紧凑模式才是"什么都不补"。
		// rows 与 offsets 都需要完整槽位空间: rows 用空行把位置顶住;
		// offsets 靠槽位算目标位置、再让 ComposeOffsetBlock 把空槽位<b>跳过不写入文本</b> ——
		// 所以它一样不占屏, 但每个在线 UI 的落点仍是固定的。
		bool padRows = (Layout != LayoutMode.Compact);
		List<LayoutRow> rows = BuildRows(padRows, VolatileRows, RowsPerPlugin, PersistentRowsMax,
			nativeLines, ownerUnits, ownerLines, transientLines, out anyContent);
		if (!anyContent)
		{
			return false;
		}

		if (Layout == LayoutMode.Offsets)
		{
			composite = ComposeOffsetBlock(rows, OffsetRowHeight, OffsetSign, MeasureVisualRows);
		}
		else
		{
			List<string> lines = new List<string>(rows.Count);
			foreach (LayoutRow row in rows)
			{
				lines.Add(row.Text);
			}
			composite = HintFormat.JoinLines(lines);
		}
		duration = ((minRemaining == float.MaxValue) ? 2f : Mathf.Max(minRemaining, 2.5f));
		return true;
	}

	/// <summary>提示区可用宽度(与 RueI 用的显示区同一量级: 参考屏 1200 × 1080)。</summary>
	private const float HintAreaWidth = 1200f;

	private static readonly System.Text.RegularExpressions.Regex RichTagRegex =
		new System.Text.RegularExpressions.Regex("<[^>]*>", System.Text.RegularExpressions.RegexOptions.Compiled);

	/// <summary>
	/// 用内嵌度量表估一段提示文本会占几个视觉行(去掉富文本标签再量宽度, 再按提示区宽度折算)。
	/// <para>算偏移必须知道每行实际占几行 —— 否则长文本换行后会盖住下一行。</para>
	/// </summary>
	private int MeasureVisualRows(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 1;
		}
		try
		{
			if (!TextMetrics.IsLoaded)
			{
				TextMetrics.Load();
			}
			string plain = RichTagRegex.Replace(text, string.Empty);
			float width = TextMetrics.MeasureAtSize(plain, OffsetFontSize);
			if (width <= 0f)
			{
				return 1;
			}
			return Math.Max(1, (int)Math.Ceiling(width / HintAreaWidth));
		}
		catch (Exception)
		{
			return 1;
		}
	}

	/// <summary>一行在布局里的位置(槽位)与内容。<c>Text</c> 为空 = 占位空行。</summary>
	internal sealed class LayoutRow
	{
		public int Slot;
		public string Text = string.Empty;
	}

	/// <summary>
	/// 行装配(纯函数, 不碰游戏对象): 决定这一帧每个槽位放什么。
	/// <para>layout = [易变区(固定 volatileRows 行: 原生 → 一次性 → 空行)] + [常驻区(每个归属按"单元数"预留行, 空则空行)]。</para>
	/// <para>关键不变量: <b>槽位总数只跟"归属数 × 每归属单元数上限"有关, 与这一帧有没有内容无关</b>。</para>
	/// </summary>
	internal static List<LayoutRow> BuildRows(
		bool padRows, int volatileRows, int rowsPerPlugin, int persistentRowsMax,
		List<string> nativeLines, List<int> ownerUnits, List<List<string>> ownerLines, List<string> transientLines,
		out bool anyContent)
	{
		anyContent = nativeLines.Count > 0 || transientLines.Count > 0;
		for (int i = 0; i < ownerLines.Count && !anyContent; i++)
		{
			if (ownerLines[i].Count > 0)
			{
				anyContent = true;
			}
		}

		int perOwner = ((rowsPerPlugin <= 0) ? int.MaxValue : rowsPerPlugin);
		int paddingCap = ((persistentRowsMax <= 0) ? int.MaxValue : persistentRowsMax);

		List<LayoutRow> result = new List<LayoutRow>(volatileRows + ownerLines.Count + 4);
		if (padRows)
		{
			// 固定行位: 易变区高度写死, 不足补空行、超了丢最老的
			int rows2 = Math.Max(0, volatileRows);
			int natives = Math.Min(nativeLines.Count, rows2);
			for (int i = 0; i < natives; i++)
			{
				result.Add(new LayoutRow { Slot = result.Count, Text = nativeLines[i] });
			}
			int room = rows2 - natives;
			if (room > 0 && transientLines.Count > 0)
			{
				int start = Math.Max(0, transientLines.Count - room);
				for (int i = start; i < transientLines.Count && result.Count < rows2; i++)
				{
					result.Add(new LayoutRow { Slot = result.Count, Text = transientLines[i] });
				}
			}
			while (result.Count < rows2)
			{
				result.Add(new LayoutRow { Slot = result.Count, Text = string.Empty });
			}
		}
		else
		{
			// 省屏模式: 一条空行都不补(紧凑与偏移模式都靠这个)
			foreach (string line in nativeLines)
			{
				result.Add(new LayoutRow { Slot = result.Count, Text = line });
			}
			foreach (string line in transientLines)
			{
				result.Add(new LayoutRow { Slot = result.Count, Text = line });
			}
		}

		for (int i = 0; i < ownerLines.Count; i++)
		{
			List<string> lines = ownerLines[i];
			int reserve = Math.Min(ownerUnits[i], perOwner);
			int take = Math.Min(lines.Count, perOwner);
			for (int k = 0; k < take; k++)
			{
				result.Add(new LayoutRow { Slot = result.Count, Text = lines[k] });
			}
			// 只有"补空行"模式才补空格位(补空行总量受 paddingCap 限制; 有内容的行永远不会被丢)。
			// offsets 模式不补 —— 它靠给每行算绝对偏移来稳住位置, 不需要空行占屏。
			if (padRows)
			{
				for (int k = take; k < reserve && result.Count < paddingCap; k++)
				{
					result.Add(new LayoutRow { Slot = result.Count, Text = string.Empty });
				}
			}
		}
		return result;
	}

	/// <summary>安静模式/补空行模式的投影: 直接把每个槽位的文本拼起来(与旧行为逐字一致)。</summary>
	internal static List<string> AssembleLayout(
		bool fixedLayout, int volatileRows, int rowsPerPlugin, int persistentRowsMax,
		List<string> nativeLines, List<int> ownerUnits, List<List<string>> ownerLines, List<string> transientLines,
		out bool anyContent)
	{
		List<LayoutRow> rows = BuildRows(fixedLayout, volatileRows, rowsPerPlugin, persistentRowsMax,
			nativeLines, ownerUnits, ownerLines, transientLines, out anyContent);
		List<string> lines = new List<string>(rows.Count);
		foreach (LayoutRow row in rows)
		{
			lines.Add(row.Text);
		}
		return lines;
	}

	/// <summary>
	/// 偏移模式的投影(思路取自 CC0 的 RueI: "不是网格/行基, 而是算偏移"):
	/// <para>每个槽位的目标位置是固定的 —— 距块底 <c>(槽位总数-1-槽位) × 行高</c>;</para>
	/// <para>实际下发时 <b>只输出有内容的行</b>, 每行加一个 <c>&lt;voffset&gt;</c> 修正量,
	/// 把它从"自然堆叠位置"拉到"目标位置"。因为每一行都被独立定位,
	/// 缺席的槽位既不占屏、也推不动别人 —— 这就是它比补空行更好的地方。</para>
	/// <para><paramref name="measureRows"/>: 给一段文本, 返回它会占几个视觉行(用 TextMetrics 量宽度再折算);
	/// 传成注入式是为了能离线断言。</para>
	/// </summary>
	internal static string ComposeOffsetBlock(List<LayoutRow> rows, float rowHeight, float sign, Func<string, int> measureRows)
	{
		if (rows == null || rows.Count == 0)
		{
			return string.Empty;
		}
		int totalSlots = rows.Count;
		float h = ((rowHeight > 0f) ? rowHeight : 30f);

		// 先算每行的"自然距底"(自然堆叠下, 它下面那些行的高度和)
		float[] natural = new float[rows.Count];
		float acc = 0f;
		for (int i = rows.Count - 1; i >= 0; i--)
		{
			natural[i] = acc;
			if (rows[i].Text.Length > 0)
			{
				int visual = ((measureRows == null) ? 1 : measureRows(rows[i].Text));
				acc += Math.Max(1, visual) * h;
			}
		}

		List<string> outLines = new List<string>(rows.Count);
		for (int i = 0; i < rows.Count; i++)
		{
			if (rows[i].Text.Length == 0)
			{
				continue;
			}
			float desired = (totalSlots - 1 - rows[i].Slot) * h;
			float delta = desired - natural[i];
			if (Math.Abs(delta) < 0.5f)
			{
				outLines.Add(rows[i].Text);
			}
			else
			{
				float v = delta * ((sign < 0f) ? -1f : 1f);
				outLines.Add("<voffset=" + v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ">" + rows[i].Text);
			}
		}
		return HintFormat.JoinLines(outLines);
	}

	private void ShowMerged(ReferenceHub hub, string text, float duration)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected Obj, but got Unknown
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected Obj, but got Unknown
		TextHint val = new TextHint(text, new HintParameter[1] { (HintParameter)new StringHintParameter(string.Empty) }, GlobalEffects, duration);
		lock (_sync)
		{
			_ownHints.Add((Hint)(object)val);
			HintsSent++;
		}
		try
		{
			if (!TrySendDirect(hub, (Hint)(object)val))
			{
				hub.hints.Show((Hint)(object)val);
			}
		}
		finally
		{
			lock (_sync)
			{
				_ownHints.Remove((Hint)(object)val);
			}
		}
	}

	private static bool TrySendDirect(ReferenceHub hub, Hint hint)
	{
		return EngineDirect.SendHint(hub, hint);
	}

	private void EnsureRenderUnitsLocked()
	{
		long currentVersion = UiSlotRegistry.CurrentVersion;
		if (_renderUnits != null && _unitsRevision == _channelRevision && _slotRevision == currentVersion)
		{
			return;
		}
		List<RenderUnit> list = new List<RenderUnit>(_channels.Count + _sources.Count + 4);
		foreach (HintChannel value in _channels.Values)
		{
			list.Add(new RenderUnit(value.Priority, value.ModuleId, UnitKind.Channel, value, null, null));
		}
		foreach (IHintTextSource value2 in _sources.Values)
		{
			list.Add(new RenderUnit(value2.Priority, value2.ModuleId, UnitKind.Source, null, value2, null));
		}
		foreach (UiSlot item in UiSlotRegistry.SortedSlots(UiSurface.Hint))
		{
			list.Add(new RenderUnit(item.Priority, item.SlotId, UnitKind.Slot, null, null, item));
		}
		list.Sort((RenderUnit a, RenderUnit b) =>
		{
			int num = a.Priority.CompareTo(b.Priority);
			return (num == 0) ? string.CompareOrdinal(a.ModuleId, b.ModuleId) : num;
		});
		_renderUnits = list;
		_unitsRevision = _channelRevision;
		_slotRevision = currentVersion;
	}

	private void PruneTransientsLocked()
	{
		// 必须整链扫描: 链表按插入顺序排列, 而每条的 Until 由各自的 duration 决定,
		// 所以"后插入的更短命"是完全可能的 —— 只看表头的话, 被长命提示压在后面的短命提示
		// 会一直留着(并且每轮仍被当成有效内容参与合成), 表现为 ShowTransient("B", 3s) 在
		// ShowTransient("A", 30s) 之后要等 30 秒才消失。
		float now = Time.time;
		LinkedListNode<TransientHint> node = _transients.First;
		while (node != null)
		{
			LinkedListNode<TransientHint> next = node.Next;
			if (node.Value.Hub == (ReferenceHub)null || now >= node.Value.Until)
			{
				_transients.Remove(node);
			}
			node = next;
		}
	}

	private void PruneStatesLocked()
	{
		ReferenceHub[] array = _states.Keys.Where((ReferenceHub h) => h == (ReferenceHub)null).ToArray();
		foreach (ReferenceHub key in array)
		{
			_states.Remove(key);
		}
	}

	private static bool SafeFilter(Func<ReferenceHub, bool> filter, string moduleId, ReferenceHub hub)
	{
		try
		{
			return filter(hub);
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintIsolation] 通道 '{moduleId}' 的接收过滤抛异常(已隔离): {arg}");
			return false;
		}
	}

	private IEnumerator<float> LoopRoutine()
	{
		float sinceFull = 0f;
		while (true)
		{
			yield return Timing.WaitForSeconds(0.04f);
			sinceFull += 0.04f;
			bool flag = sinceFull >= _refresh;
			if (flag)
			{
				sinceFull = 0f;
			}
			try
			{
				Tick(flag);
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintIsolation] Hint 刷新异常: " + ex));
			}
		}
	}
}
