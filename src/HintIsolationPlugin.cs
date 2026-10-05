using System;
using System.Collections.Generic;
using System.ComponentModel;
using HintIsolation.Core.Bootstrap;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Enums;
using HintIsolation.Core.Interception;
using HintIsolation.Core.ServerSpecific;
using LabApi.Features;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using MEC;

namespace HintIsolation;

public sealed class HintIsolationPlugin : Plugin<HintIsolationPlugin.PluginConfig>
{
	public class PluginConfig
	{
		[Description("启用 Hint 合并渲染核心(唯一写者)")]
		public bool EnableHintIsolation { get; set; } = true;

		[Description("合并刷新间隔(秒), 越小越及时、网络包越多")]
		public float HintRefreshInterval { get; set; } = 0.75f;

		[Description("续期提前量(秒), 在到期前提前重发防止闪烁消失")]
		public float HintResendLeeway { get; set; } = 0.15f;

		[Description("打印每个玩家的复合 Hint 内容(排障用)")]
		public bool HintDebugLog { get; set; }

		[Description("内容未变时跳过重发(默认开): 一个字没变就绝不重发, 消除 UI 闪烁与后台卡顿; 关掉会退回'每轮都重发'的旧行为")]
		public bool SuppressUnchangedResend { get; set; } = true;

		[Description("输入摘要快筛(默认开): 本轮渲染输入的结构摘要没变就连字符串合成都整段跳过, 比'内容未变跳过重发'更早一步; 关掉退回'每轮都合成'")]
		public bool InputSignatureEnabled { get; set; } = true;
		[Description("排版模式(治上下位置错位): rows=固定行位(默认; 空行占位, 位置绝对稳定, 代价是占屏) | offsets=算偏移(不给空行, 每行用 voffset 摆到自己的固定槽位; 不占屏且位置同样稳定, 思路取自 CC0 的 RueI) | compact=紧凑(最省屏, 但位置会随内容增减而变)")]
		public string LayoutMode { get; set; } = "rows";

		[Description("offsets 模式的一行高度(voffset 单位; 参考: 整屏高度约 2140)")]
		public float OffsetRowHeight { get; set; } = 40f;

		[Description("offsets 模式的方向: 1=默认 | -1=如果整块朝反方向偏了, 改成 -1")]
		public float OffsetSign { get; set; } = 1f;

		[Description("offsets 模式量文本宽度用的字号(游戏提示默认约 20), 用于估算每行实际占几个视觉行")]
		public float OffsetFontSize { get; set; } = 20f;

		[Description("易变区行数(一次性提示 + 吸收进来的原生提示): 多了丢最老的, 少了补空行。它是别人进出不影响常驻 UI 的关键")]
		public int VolatileRows { get; set; } = 3;

		[Description("同一个插件/模块最多占几行: 1=一行(顺序稳定) | 0=不限")]
		public int RowsPerPlugin { get; set; } = 1;

		[Description("常驻区最多补几行空位(只限制空行数量, 有内容的行永远不会丢)")]
		public int PersistentRowsMax { get; set; } = 8;

		[Description("安装拦截层: 把各 UI 通道按调用方自动分配独立 UiId 信口(默认关)")]
		public bool InterceptThirdPartyUi { get; set; }

		[Description("拦截到的 UI 按【调用方插件】自动归因(插件无需改代码)")]
		public bool AutoAttributeThirdPartyUi { get; set; } = true;

		[Description("UiId 派生粒度: Assembly=每插件一份 | Type=每类 | Method=每方法(推荐) | CallSite=每调用点(最细)")]
		public UiIdGranularity UiIdGranularity { get; set; } = UiIdGranularity.Method;

		[Description("是否把 UiId 明细落盘到 configs/<端口>/HintIsolation/uiids.yml")]
		public bool PersistUiIds { get; set; } = true;

		[Description("游戏自身 UI 的处理: Isolate=也并入隔离信口(默认, 消除互相顶掉) | PassThrough=放行原生(会与合并结果交替, 表现为闪烁)")]
		public NativeHintPolicy NativeHintPolicy { get; set; } = NativeHintPolicy.Isolate;

		[Description("接管提示条表面(HintDisplay.Show)")]
		public bool InterceptHints { get; set; } = true;

		[Description("提示条信口优先级(64/96/128/160/192, 越小越靠上)")]
		public byte HintSlotPriority { get; set; } = 128;

		[Description("提示条信口是否显示 [来源] 标签(默认关, 保持插件原本观感)")]
		public bool HintShowLabels { get; set; }

		[Description("原生提示翻译合并(默认开): 游戏自己的译文提示用内嵌全量模板翻成纯文本并入复合体, 与插件 UI 真共存; 关掉退回'让路'")]
		public bool TranslateNativeHints { get; set; } = true;

		[Description("原生提示译文语言: 支持游戏全部 22 种代码(ca cs de en es fr gl it ko pl pt_BR ru sk sr_CYRL-BA sr_LATN-BA tr uk vi zh_Flash_Hans zh_Hans zh_Hans-2 zh_Hant), 也接受 zh/cn/chs/cht/tw/pt/sr 等简写; 取值顺序: 服务器 Translations\\<语言>\\GameHints.txt → 插件内嵌官方译文 → 英文 → 内置兜底")]
		public string NativeHintTranslationLanguage { get; set; } = "zh";

		[Description("提示条: 每个信口最多同时保留几条(1 = 替换, 与原版一致; 内容每秒变化的 HUD 必须用 1, 否则会堆成多份)")]
		public int HintMaxEntriesPerSlot { get; set; } = 1;

		[Description("提示条: 单条最长保留秒数(防插件传超大 duration 占屏)")]
		public float HintMaxEntryDuration { get; set; } = 10f;

		[Description("接管屏幕中央广播表面(Broadcast.TargetAddElement / RpcAddElement / TargetClearElements / RpcClearElements)")]
		public bool InterceptBroadcasts { get; set; } = true;

		[Description("阻止第三方插件清空广播(防一个插件清掉别人全部广播)")]
		public bool BlockThirdPartyBroadcastClear { get; set; } = true;

		[Description("广播信口优先级(64/96/128/160/192)")]
		public byte BroadcastSlotPriority { get; set; } = 128;

		[Description("广播信口是否显示 [来源] 标签")]
		public bool BroadcastShowLabels { get; set; }

		[Description("广播: 每个信口最多同时保留几条")]
		public int BroadcastMaxEntriesPerSlot { get; set; } = 2;

		[Description("广播: 单条最长保留秒数")]
		public float BroadcastMaxEntryDuration { get; set; } = 15f;

		[Description("广播: 重复发送的信口两次放行之间的最小间隔(秒)。广播是追加语义, 插件每秒重发会堆成多团, 靠节流压住")]
		public float BroadcastRepeatInterval { get; set; } = 1f;

		[Description("广播: 重复发送的信口被压到的时长上限(秒)。压短它, 旧的一团才会跟着过期")]
		public ushort BroadcastRepeatDuration { get; set; } = 1;

		[Description("接管玩家控制台表面(GameConsoleTransmission.SendToClient)")]
		public bool InterceptConsole { get; set; } = true;

		[Description("控制台信口优先级(64/96/128/160/192, 仅用于记账排序)")]
		public byte ConsoleSlotPriority { get; set; } = 128;

		[Description("控制台信口是否显示 [来源] 标签")]
		public bool ConsoleShowLabels { get; set; }

		[Description("控制台: 每个信口最多保留几条(仅记账用)")]
		public int ConsoleMaxEntriesPerSlot { get; set; } = 1;

		[Description("控制台: 单条最长保留秒数(仅记账用)")]
		public float ConsoleMaxEntryDuration { get; set; } = 15f;

		[Description("控制台: 重复消息两次放行之间的最小间隔(秒)。0 = 只归因不拦(默认) —— 控制台是追加型日志, 不会互相顶掉")]
		public float ConsoleRepeatInterval { get; set; }

		[Description("接管 CASSIE 播报表面(CassieAnnouncementDispatcher)")]
		public bool InterceptCassie { get; set; } = true;

		[Description("阻止第三方插件清空 CASSIE 播报队列(防一个插件清掉别人全部播报)")]
		public bool BlockThirdPartyCassieClear { get; set; } = true;

		[Description("CASSIE 信口优先级(64/96/128/160/192, 仅用于记账排序)")]
		public byte CassieSlotPriority { get; set; } = 128;

		[Description("CASSIE 信口是否显示 [来源] 标签")]
		public bool CassieShowLabels { get; set; }

		[Description("CASSIE: 每个信口最多保留几条(仅记账用)")]
		public int CassieMaxEntriesPerSlot { get; set; } = 1;

		[Description("CASSIE: 单条最长保留秒数(仅记账用)")]
		public float CassieMaxEntryDuration { get; set; } = 30f;

		[Description("网络哨兵: 在 Mirror 的消息汇流点 NetworkConnection.Send<T> 上挂补丁, 看住一切绕过语义层的直发与伪造包。默认关(开了才有额外开销)")]
		public bool EnableNetworkSentinel { get; set; }

		[Description("网络哨兵: 是否把'绕过语义层的提示'收编进隔离信口。默认关 = 只观测不改写(最安全)")]
		public bool NetworkSentinelIntercept { get; set; }

		[Description("接管管理端聊天面板(LabAPI 的 Server.SendAdminChatMessage 落点)")]
		public bool InterceptAdminChat { get; set; } = true;

		[Description("管理端聊天: 重复消息两次放行之间的最小间隔(秒)。0 = 只归因不拦(默认) —— 它是追加型面板, 不会互相顶掉")]
		public float AdminChatRepeatInterval { get; set; }

		[Description("接管准星命中标记(LabAPI 的 Player.SendHitMarker 落点)。只归因记账, 从不拦截")]
		public bool InterceptHitMarker { get; set; } = true;

		[Description("对讲机显示屏守卫: 它的写入口是 SyncVar 裸字段、写侧无方法可拦, 因此拦 Mirror 的同步序列化, 在下发前把值改回已放行的那一份")]
		public bool EnableIntercomGuard { get; set; } = true;

		[Description("对讲机显示屏: 间隔内被反复改写时的回滚阈值(秒)。0 = 只观测不改写(默认); 大于 0 才会真的回滚")]
		public float IntercomThrottleInterval { get; set; }

		[Description("释放并启用 0 前缀抢先引导器(0HintIsolation.Bootstrap.dll): 让底层抢在其它插件前加载")]
		public bool EnableBootstrapFirstLoader { get; set; } = true;

		[Description("启用 SSS 端口隔离核心")]
		public bool EnableSssRegistry { get; set; } = true;

		[Description("强力复写: 拦截游戏下发入口, 在发出去之前强制拼回本核心设置项(最强的一道保证)")]
		public bool EnableSssForceRewrite { get; set; } = true;

		[Description("防劫持: 检测到其它插件整体覆盖 DefinedSettings 时自动合并恢复")]
		public bool ProtectSssFromOverwrites { get; set; } = true;
	}

	private CoroutineHandle _guardianLoop;

	private CoroutineHandle _maintenanceLoop;

	public override string Name => "HintIsolation";

	public override string Description => "动态 UI 隔离底层: 拦截并归因第三方 UI 调用, 按 UiId 分配独立信口, 支持多 UI 表面";

	public override string Author => "LabAPI-Docs";

	public override Version RequiredApiVersion => LabApiProperties.CurrentVersion;

	/// <summary>最先加载 —— 底层必须先于其它插件就绪, 才能在它们发 UI 时完成拦截归因。</summary>
	public override LoadPriority Priority => LoadPriority.Highest;

	public override bool IsTransparent => !base.Config.InterceptThirdPartyUi;

	public override void Enable()
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		BootstrapConfigurator.Apply(base.Config);
		if (base.Config.EnableSssRegistry)
		{
			SssRegistry.Initialize(base.Config.ProtectSssFromOverwrites);
			if (base.Config.EnableSssForceRewrite)
			{
				SssForceRewrite.Install();
			}
			_guardianLoop = Timing.RunCoroutine(GuardianRoutine(), Segment.Update);
		}
		ReleaseBootstrapIfNeeded();
		if (base.Config.InterceptThirdPartyUi)
		{
			UiInterception.Instance.Install();
		}
		_maintenanceLoop = Timing.RunCoroutine(MaintenanceRoutine(), Segment.Update);
		StartupLog.Info("[HintIsolation] Enable 完成 → 外部插件可经 UiIsolation / IUiIsolation 注册通道、文本源、SSS 端口");
	}

	private void ReleaseBootstrapIfNeeded()
	{
		if (base.Config.EnableBootstrapFirstLoader)
		{
			if (BootstrapInstaller.EnsureReleased(out string detail))
			{
				Logger.Warn((object)("[HintIsolation] " + detail));
				Logger.Warn((object)"[HintIsolation] ┌────────────────────────────────────────────────────┐");
				Logger.Warn((object)"[HintIsolation] │  引导器已释放, 请重启服务器以便它抢占最先加载位  │");
				Logger.Warn((object)"[HintIsolation] └────────────────────────────────────────────────────┘");
			}
			else
			{
				StartupLog.Info("[HintIsolation] " + detail);
			}
		}
	}

	public override void Disable()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		Timing.KillCoroutines(new CoroutineHandle[1] { _guardianLoop });
		Timing.KillCoroutines(new CoroutineHandle[1] { _maintenanceLoop });
		SssForceRewrite.Uninstall();
		UiInterception.Instance.Uninstall();
		HintBroker.Instance.Stop();
		SssRegistry.Terminate();
		UiSlotRegistry.Clear();
		StartupLog.Info("[HintIsolation] Disable 完成: 拦截层已卸载, 信口已释放, SSS 数组已还原");
	}

	private static IEnumerator<float> GuardianRoutine()
	{
		while (true)
		{
			yield return Timing.WaitForSeconds(3f);
			try
			{
				SssRegistry.GuardianTick();
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintIsolation] SSS 守卫检查异常: " + ex));
			}
		}
	}

	private static IEnumerator<float> MaintenanceRoutine()
	{
		int sinceGuard = 0;
		while (true)
		{
			yield return Timing.WaitForSeconds(1f);
			try
			{
				UiInterception.Tick();
			}
			catch (Exception ex)
			{
				Logger.Error((object)("[HintIsolation] 维护循环异常: " + ex));
			}
			int num = sinceGuard + 1;
			sinceGuard = num;
			if (num < 5)
			{
				continue;
			}
			sinceGuard = 0;
			try
			{
				PatchGuard.Verify(() =>
				{
					UiInterception.Instance.Repair();
				});
			}
			catch (Exception ex2)
			{
				Logger.Error((object)("[HintIsolation] 补丁自愈检查异常: " + ex2));
			}
		}
	}
}
