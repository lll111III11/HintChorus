using System;
using System.Collections.Generic;
using System.ComponentModel;
using HintChorus.Commands;
using HintChorus.Core.Bootstrap;
using HintChorus.Core.Broker;
using HintChorus.Core.Compat;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Enums;
using HintChorus.Core.Interception;
using HintChorus.Core.Localization;
using HintChorus.Core.ServerSpecific;
using HintChorus.Core.Setup;
using HintChorus.Core.Templates;
using HintChorus.Core.Updates;
using LabApi.Features;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using MEC;

namespace HintChorus;

public sealed class HintChorusPlugin : Plugin<HintChorusPlugin.PluginConfig>
{
	public class PluginConfig
	{
		[Description("启用 Hint 合并渲染核心(唯一写者)")]
		public bool EnableHintChorus { get; set; } = true;

		[Description("合并刷新间隔(秒), 越小越及时、网络包越多")]
		public float HintRefreshInterval { get; set; } = 0.75f;

		[Description("续期提前量(秒), 在到期前提前重发防止闪烁消失")]
		public float HintResendLeeway { get; set; } = 0.15f;

		[Description("打印每个玩家的复合 Hint 内容(排障用)")]
		public bool HintDebugLog { get; set; }

		[Description("崩溃 / 异常报告日志(默认开): 插件自身异常、补丁自愈失败、命令异常等写入 configs/<端口>/HintChorus/logs/report-*.log; 这是默认保留的唯一日志功能")]
		public bool EnableCrashReportLog { get; set; } = true;

		[Description("详细 UI 日志(默认关): 记录每个插件的提示内容与最终屏幕位置、每个玩家的合成结果, 写入 logs/ui-*.log; 排障时再打开, 打开后 IO 开销明显")]
		public bool EnableDetailedUiLog { get; set; }

		[Description("详细 UI 日志 · 逐玩家记录最终合成文本(默认关; 需要同时打开 EnableDetailedUiLog 才生效)")]
		public bool DetailedUiLogPerPlayer { get; set; }

		[Description("自动更新(默认关): 只检查并下载到 HintChorus.dll.new, 绝不覆盖正在运行的 DLL")]
		public bool AutoUpdate { get; set; }

		[Description("更新后回合结束重启(RNR, 默认关): 自动更新下载并替换 DLL 后, 等当前回合结束再彻底重启服务器加载新版本")]
		public bool RnrAfterUpdate { get; set; }

		[Description("界面/控制台语言: auto=跟随系统; 可填 zh/en/fr/ru/es; 检测不到地区时回退英文")]
		public string Language { get; set; } = "auto";

		[Description("时区(配置向导自动写入, 仅记录): auto=自动检测")]
		public string Timezone { get; set; } = "auto";

		[Description("地区(配置向导自动写入, 仅记录): auto=自动检测; 检测不到时语言回退英文")]
		public string Region { get; set; } = "auto";

		[Description("网络语言/时区探测: auto=有网时用公网 IP + 网络时区校验, 无网回退本地; off=只用本地")]
		public string NetworkLocale { get; set; } = "auto";

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

		// ── 位置(功能区 ①②③: 兼容原有写法 / 自有写法 / 自动排版) ─────────────────

		[Description("兼容原有写法(默认开): 插件文本里已自带位置标签(<voffset>/<pos>/<align>/<line-height> 等)时, 判为'插件自己摆了位', 原样放行不重排; 关掉则一律并入底部自然堆叠")]
		public bool HonorPluginPositionSyntax { get; set; } = true;

		[Description("自动排版(默认开): 未声明位置的插件按名称查'已收录表 + 功能区关键词'推断落点(如 exp/level/rank -> 屏幕中部再往下 90); 关掉则一律留在默认底部区")]
		public bool AutoLayoutByPluginName { get; set; } = true;

		[Description("自有写法标记(默认开): 识别并剥离文本里的 {{hc:pos=top-right,offset=-90}} 标记; 关掉时标记会原样显示(仅排障)")]
		public bool EnablePositionMarkers { get; set; } = true;

		[Description("屏幕高度(voffset 单位; 参考 2140): 决定'中部/顶部'锚点换算成多大的 voffset, 不同分辨率与宽高比下可微调")]
		public float ScreenHeightUnits { get; set; } = 2140f;

		[Description("位置覆盖表(默认空): 插件关键词 -> 位置。值可写 'top-right' 或 'middle,offset=-90'(正=上移, 单位同 voffset); 命中优先于内置目录")]
		public Dictionary<string, string> PositionOverrides { get; set; } = new Dictionary<string, string>();

		[Description("安装拦截层: 把各 UI 通道按调用方自动分配独立 UiId 信口(默认关)")]
		public bool InterceptThirdPartyUi { get; set; }

		[Description("拦截到的 UI 按【调用方插件】自动归因(插件无需改代码)")]
		public bool AutoAttributeThirdPartyUi { get; set; } = true;

		[Description("UiId 派生粒度: Assembly=每插件一份 | Type=每类 | Method=每方法(推荐) | CallSite=每调用点(最细)")]
		public UiIdGranularity UiIdGranularity { get; set; } = UiIdGranularity.Method;

		[Description("是否把 UiId 明细落盘到 configs/<端口>/HintChorus/uiids.yml")]
		public bool PersistUiIds { get; set; } = true;

		[Description("游戏自身 UI 的处理: Isolate=也并入隔离信口(默认, 消除互相顶掉) | PassThrough=放行原生(会与合并结果交替, 表现为闪烁)")]
		public NativeHintPolicy NativeHintPolicy { get; set; } = NativeHintPolicy.Isolate;

		[Description("接管提示条表面(HintDisplay.Show)")]
		public bool InterceptHints { get; set; } = true;

		[Description("强接管(默认开): 其它插件的 Hint 补丁抢先吞掉提示口、导致本底层 UI 直接不显示时, 用最高优先级前缀 + Postfix 兜底强制把复合块重新盖回去; 关掉退回只观测/让路")]
		public bool ForceTakeover { get; set; } = true;

		[Description("第三方 Hint 插件兼容策略: Auto=检测到主流 Hint 插件时自动把它切到原生直通, 双方互不干扰(默认); Notify=只提醒, 不改变接管状态; Off=关闭检测")]
		public CompatibilityPolicy CompatMode { get; set; } = CompatibilityPolicy.Auto;

		[Description("提示条信口优先级(64/96/128/160/192, 越小越靠上)")]
		public byte HintSlotPriority { get; set; } = 128;

		[Description("提示条信口是否显示 [来源] 标签(默认关, 保持插件原本观感)")]
		public bool HintShowLabels { get; set; }

		[Description("原生提示翻译合并(默认开): 游戏自己的译文提示用内嵌全量模板翻成纯文本并入复合体, 与插件 UI 真共存; 关掉退回'让路'")]
		public bool TranslateNativeHints { get; set; } = true;

		[Description("原生提示译文兜底语言(默认 zh=内置中文; 可选 en/fr/ru/es=内置联合国常用语)。每个玩家会先按 playerPreferences.Language 自适应取自己的语言, 其余客户端语言从服务器 translations\\目录加载, 探测失败才回退本配置")]
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

		[Description("Hint 显示模板(默认空=不启用, 保持现有排版; 可填 zh/en 或自定义模板名; 自定义模板放 configs/<端口>/HintChorus/templates/<名字>.ini)")]
		public string Template { get; set; } = string.Empty;

		[Description("释放并启用抢加载引导器(0.dll): 最短文件名 + 最高优先级, 让底层抢在其它插件前加载")]
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

	private CoroutineHandle _shieldLoop;

	private CoroutineHandle _setupLoop;

	private CoroutineHandle _updateLoop;

	public override string Name => "HintChorus";

	public override string Description => Lang.Get("plugin.description");

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
		try
		{
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
				// 仲裁层: 挂住 Harmony 的卸载入口, 外部清场后在同一调用栈内恢复本底层。
				PatchArbiter.Install();
				// 模板层(默认关): 读取映射组件的程序表并预热, 不改变现有排版。
				TemplateBridge.Install(base.Config.Template);
				// 护盾: 覆盖被 JIT 内联等极端情况, 0.25 秒内装回来。
				PatchShield.Install();
				_shieldLoop = Timing.RunCoroutine(ShieldRoutine(), Segment.Update);
			}
			HintFallback.ResetAll();
			HintTakeover.Clear();
			// 第三方 Hint 插件兼容模式: 按配置 Auto / Notify / Off 执行。
			CompatibilityMode.Reset();
			CompatibilityMode.Policy = base.Config.CompatMode;
			// 有网时用公网 IP + 网络时区校验; 无网回退本地时区/时间戳。
			NetworkLocaleProbe.Enabled = !string.Equals(base.Config.NetworkLocale, "off", StringComparison.OrdinalIgnoreCase);
			NetworkLocaleProbe.Start();
			LocaleService.Initialize(base.Config);
			// 首次运行配置向导: 等启动文本全部输出完再进入。
			SetupWizard.Initialize(base.Config, SaveConfig);
			UpdateService.Initialize(base.Config);
			if (SetupWizard.ShouldRun())
			{
				_setupLoop = Timing.RunCoroutine(SetupRoutine(), Segment.Update);
			}
			if (base.Config.AutoUpdate)
			{
				_updateLoop = Timing.RunCoroutine(UpdateRoutine(), Segment.Update);
			}
			_maintenanceLoop = Timing.RunCoroutine(MaintenanceRoutine(), Segment.Update);
			UiLog.WriteStartupReport(BuildStartupReport());
			StartupLog.Info(Lang.Get("plugin.enableDone"));
			StartupLog.Info(Lang.Get("plugin.betaBanner"));
		}
		catch (Exception ex)
		{
			Logger.Error((object)Lang.Format("plugin.enableFailed", ex));
			UiLog.Report("Enable", ex);
			throw;
		}
	}

	/// <summary>启动报告(默认日志): 版本、关键开关、语言包与已发现的 Hint 插件。</summary>
	private string BuildStartupReport()
	{
		return Lang.Format("plugin.reportVersion", GetType().Assembly.GetName().Version)
			+ "\n" + Lang.Format("plugin.reportPort", LabApi.Features.Wrappers.Server.Port)
			+ "\n" + Lang.Format(
				"plugin.reportCore",
				base.Config.EnableHintChorus,
				base.Config.InterceptThirdPartyUi,
				PatchArbiter.IsInstalled,
				base.Config.ForceTakeover,
				base.Config.LayoutMode)
			+ "\n" + Lang.Format("plugin.reportTemplate", TemplateBridge.Status)
			+ "\n" + Lang.Format("plugin.reportLogs", base.Config.EnableCrashReportLog, base.Config.EnableDetailedUiLog, base.Config.DetailedUiLogPerPlayer)
			+ "\n" + Lang.Format("plugin.reportLanguage", LanguagePack.Report)
			+ "\n" + Lang.Format("plugin.reportCandidates", string.Join(", ", HintFallback.Candidates()));
	}

	private void ReleaseBootstrapIfNeeded()
	{
		if (!base.Config.EnableBootstrapFirstLoader)
		{
			return;
		}

		BootstrapReleaseResult result = BootstrapInstaller.EnsureReleased(out string detail);
		switch (result)
		{
			case BootstrapReleaseResult.Released:
				Logger.Warn((object)("[HintChorus] " + detail));
				Logger.Warn((object)Lang.Get("plugin.bootstrapBanner1"));
				Logger.Warn((object)Lang.Get("plugin.bootstrapBanner2"));
				Logger.Warn((object)Lang.Get("plugin.bootstrapBanner3"));
				break;
			case BootstrapReleaseResult.Staged:
				Logger.Warn((object)("[HintChorus] " + detail));
				break;
			case BootstrapReleaseResult.Failed:
				Logger.Error((object)("[HintChorus] " + detail));
				break;
			default:
				StartupLog.Info("[HintChorus] " + detail);
				break;
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
		Timing.KillCoroutines(new CoroutineHandle[1] { _shieldLoop });
		Timing.KillCoroutines(new CoroutineHandle[1] { _setupLoop });
		Timing.KillCoroutines(new CoroutineHandle[1] { _updateLoop });
		PatchShield.Uninstall();
		CompatibilityMode.Policy = CompatibilityPolicy.Off;
		CompatibilityMode.Reset();
		HintFallback.ResetAll();
		HintTakeover.Clear();
		SssForceRewrite.Uninstall();
		PatchArbiter.Uninstall();
		UiInterception.Instance.Uninstall();
		HintBroker.Instance.Stop();
		SssRegistry.Terminate();
		UiSlotRegistry.Clear();
		RuntimeHome.UninstallResolver();
		UiLog.ReportEvent("Disable", Lang.Get("plugin.disableReport"));
		StartupLog.Info(Lang.Get("plugin.disableDone"));
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
				Logger.Error((object)Lang.Format("plugin.sssGuardError", ex));
			}
		}
	}

	private static IEnumerator<float> ShieldRoutine()
	{
		while (true)
		{
			yield return Timing.WaitForSeconds(0.25f);
			try
			{
				PatchShield.Tick();
			}
			catch (Exception ex)
			{
				Logger.Error((object)Lang.Format("plugin.shieldError", ex));
			}
		}
	}

	private static IEnumerator<float> SetupRoutine()
	{
		yield return Timing.WaitForSeconds(6f);
		try
		{
			SetupWizard.Start();
		}
		catch (Exception ex)
		{
			Logger.Error((object)Lang.Format("plugin.setupStartError", ex));
		}
	}

	private static IEnumerator<float> UpdateRoutine()
	{
		yield return Timing.WaitForSeconds(15f);
		while (true)
		{
			try
			{
				UpdateService.CheckInBackground();
			}
			catch (Exception ex)
			{
				Logger.Error((object)Lang.Format("plugin.updateStartError", ex));
			}
			// 每 6 小时检查一次; 有新版本且开启 RNR 时会安排回合结束重启
			yield return Timing.WaitForSeconds(21600f);
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
				CompatibilityMode.Tick();
			}
			catch (Exception ex)
			{
				Logger.Error((object)Lang.Format("plugin.maintenanceError", ex));
				UiLog.Report("Maintenance", ex);
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
				Logger.Error((object)Lang.Format("plugin.patchGuardError", ex2));
			}
		}
	}
}
