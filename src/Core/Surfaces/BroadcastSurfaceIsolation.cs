using HintChorus.Core.Broker;
using HintChorus.Core.Enums;
using HintChorus.Core.Identity;
using HintChorus.Core.Interfaces;
using HintChorus.Core.Interception;
using HintChorus.Core.Models;
using Mirror;
using System;

namespace HintChorus.Core.Surfaces;

/// <summary>
/// <b>屏幕中央广播表面(Broadcast)隔离器</b>。
///
/// <para>广播与提示条是两条独立通道, 冲突形态也不同:</para>
/// <list type="bullet">
///   <item><b>重复刷屏</b>: 插件常"每秒重发"维持一条广播, 客户端会把它叠成 N 条
///     → 本隔离器在<b>存活期内对相同文本抑制</b>, 首次出现零延迟放行;</item>
///   <item><b>互相清除</b>: 一个插件调 <c>ClearBroadcasts()</c> 会把别人的广播一起清掉
///     → 本隔离器默认<b>阻止第三方插件清空广播</b>(管理端/游戏自身不受影响)。</item>
/// </list>
///
/// <para>广播与提示条共用同一套 UiId / 信口体系, 只是表面不同 ——
/// 因此同一个插件的广播和提示各自独立, 不会互相干扰。</para>
/// </summary>
public sealed class BroadcastSurfaceIsolation : IUiSurfaceInterceptor
{
	/// <summary>
	/// "本底层自己正在下发广播"的嵌套深度。
	/// <para>按线程隔离: 自发送本来就是逐线程的概念, 用共享字段时并发下会丢一次自减,
	/// 计数一旦卡在正数, 广播的隔离与防清屏就会**永久静默失效**(并且只在卸载时才复位)。</para>
	/// <para>Enter/Exit 成对出现在同一个方法的 try/finally 里, 所以线程局部是安全的。</para>
	/// </summary>
	[ThreadStatic]
	private static int _selfSendDepth;

	internal static int SelfSendDepth => _selfSendDepth;

	private static bool IsSelfSending => _selfSendDepth > 0;

	/// <summary>全局唯一实例。</summary>
	public static BroadcastSurfaceIsolation Instance { get; } = new BroadcastSurfaceIsolation();

	private BroadcastSurfaceIsolation()
	{
	}

	/// <inheritdoc/>
	public UiSurface Surface => UiSurface.Broadcast;

	/// <inheritdoc/>
	public string DisplayName => "屏幕中央广播";

	/// <inheritdoc/>
	public bool IsInstalled { get; private set; }

	/// <inheritdoc/>
	public bool Enabled { get; set; } = true;

	/// <inheritdoc/>
	public long InterceptedCount { get; private set; }

	/// <inheritdoc/>
	public long PassedThroughCount { get; private set; }

	/// <summary>是否按调用方自动归因(由编排器同步配置)。</summary>
	public bool AutoAttribute { get; set; } = true;

	/// <summary>游戏原生广播策略(由编排器同步配置)。</summary>
	public NativeHintPolicy NativePolicy { get; set; } = NativeHintPolicy.PassThrough;

	/// <summary>是否阻止第三方插件清空广播。</summary>
	public bool BlockThirdPartyClear { get; set; } = true;

	/// <summary>信口渲染优先级。</summary>
	public byte SlotPriority { get; set; } = (byte)HintChannelPriority.Medium;

	/// <summary>是否显示 [来源] 标签。</summary>
	public bool ShowLabels { get; set; }

	/// <summary>每个信口最多保留条目数。</summary>
	public int MaxEntries { get; set; } = 2;

	/// <summary>单条最长保留秒数。</summary>
	public float MaxDuration { get; set; } = 15f;

	/// <summary>重复发送的信口两次放行之间的最小间隔(秒)。</summary>
	public float RepeatInterval { get; set; } = 1f;

	/// <summary>重复发送的信口被压到的时长上限(秒)。</summary>
	public ushort RepeatDuration { get; set; } = 1;

	/// <inheritdoc/>
	public void Install()
	{
		IsInstalled = true;
	}

	/// <inheritdoc/>
	public void Uninstall()
	{
		IsInstalled = false;
		_selfSendDepth = 0;
	}

	/// <summary>
	/// 处理一次广播入队。返回 true = 放行原生; false = 抑制。
	///
	/// <para><b>为什么不能按"文本是否重复"判定:</b> 广播在客户端是<b>追加</b>语义
	/// (AddElement 往列表里加一条), 不是替换。而带实时内容(时钟/倒计时)的 HUD
	/// 每秒文本都不同, 按文本去重等于完全不去重 —— 每秒加一条, 屏幕上堆成好几团,
	/// 其中还有早已过期不走的旧团。</para>
	///
	/// <para><b>改为按时间节流:</b></para>
	/// <list type="number">
	///   <item>隔了很久才来一次 → 视为一次性公告, 原样放行(保留插件原本的时长);</item>
	///   <item>同一信口在间隔内又发 → 判定为"重发型 HUD", 直接抑制 —— 旧的一团
	///         不会被追加更新, 自然按原时长过期, 屏幕不会堆叠。</item>
	/// </list>
	///
	/// <para><b>为什么不能"压短时长":</b> 游戏的 <c>TargetAddElement / RpcAddElement</c>
	/// 签名里 <c>duration</c> 是<b>普通 ushort(非 ref)</b>, Harmony 前缀无法改写它。
	/// (早期版本误写成 <c>ref ushort</c>, 导致补丁挂不上、广播拦截整体静默失效。)
	/// 所以节流只做"间隔内抑制", 不做时长压缩。</para>
	/// </summary>
	internal bool OnBroadcastAdd(NetworkConnection? conn, string message, ushort duration, Broadcast.BroadcastFlags flags)
	{
		// 单人广播的连接目前不参与节流判定(广播信口按插件归因, 全局同一份);
		// 保留该参数仅为与游戏签名一致, 供将来需要"按玩家定向节流"时使用。
		_ = conn;

		if (IsSelfSending)
		{
			return true; // 我们自己发的 → 放行
		}

		// 管理端聊天(AdminChat)是管理员/RA 的直发消息, 不属于"插件互相顶掉"的范畴,
		// 而且它本来就该按原样、原时长到达玩家 —— 一律透传, 不参与节流。
		if ((flags & Broadcast.BroadcastFlags.AdminChat) != 0)
		{
			PassedThroughCount++;
			return true;
		}

		if (!Enabled || string.IsNullOrEmpty(message))
		{
			PassedThroughCount++;
			return true;
		}

		CallerInfo caller = PluginCallerResolver.Resolve();

		if (caller.IsPlugin)
		{
			if (!AutoAttribute)
			{
				PassedThroughCount++;
				return true;
			}
		}
		else if (NativePolicy == NativeHintPolicy.PassThrough)
		{
			PassedThroughCount++;
			return true;
		}

		UiId id = UiIdRegistry.Resolve(Surface, caller.Assembly ?? typeof(BroadcastSurfaceIsolation).Assembly,
			caller.Method, caller.IlOffset);
		UiId routed = UiIdRegistry.ResolveRoute(id);

		UiSlot slot = UiSlotRegistry.GetOrCreate(
			routed,
			UiIdRegistry.DisplayNameOf(routed),
			SlotPriority,
			ShowLabels,
			MaxEntries,
			MaxDuration);

		float now = UnityEngine.Time.time;
		float since = slot.LastPassedAt > 0f ? now - slot.LastPassedAt : float.MaxValue;

		// ① 隔得够久 → 一次性公告, 原样放行
		if (since > RepeatInterval * 2f)
		{
			slot.Push(message, now, duration);
			slot.LastPassedAt = now;
			PassedThroughCount++;
			return true;
		}

		// ② 间隔内又来了 → 重发型 HUD, 节流抑制
		slot.Push(message, now, duration);
		slot.LastPassedAt = now;
		InterceptedCount++;
		return false;
	}

	/// <summary>
	/// 处理一次广播清空。返回 true = 放行; false = 抑制。
	/// </summary>
	internal bool OnBroadcastClear()
	{
		if (IsSelfSending)
		{
			return true;
		}

		if (!Enabled || !BlockThirdPartyClear)
		{
			PassedThroughCount++;
			return true;
		}

		CallerInfo caller = PluginCallerResolver.Resolve();
		if (caller.IsPlugin)
		{
			InterceptedCount++;
			return false; // 插件不得清掉别人的广播
		}

		PassedThroughCount++;
		return true;
	}

	/// <summary>在"我们自己发广播"期间抑制拦截(供渲染器包裹发送调用)。</summary>
	internal static void EnterSelfSend() => _selfSendDepth++;

	/// <summary>退出自我发送区。</summary>
	internal static void ExitSelfSend()
	{
		if (_selfSendDepth > 0)
		{
			_selfSendDepth--;
		}
	}
}
