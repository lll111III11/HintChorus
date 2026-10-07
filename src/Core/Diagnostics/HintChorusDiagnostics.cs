using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HintChorus.Core.Bootstrap;
using HintChorus.Core.Broker;
using HintChorus.Core.Compat;
using HintChorus.Core.Identity;
using HintChorus.Core.Interception;
using HintChorus.Core.Interfaces;
using HintChorus.Core.Models;
using HintChorus.Core.ServerSpecific;
using HintChorus.Core.Surfaces;
using HintChorus.Core.Transport;

namespace HintChorus.Core.Diagnostics;

public static class HintChorusDiagnostics
{
	public static HintStatistics Capture()
	{
		HintBroker instance = HintBroker.Instance;
		UiInterception instance2 = UiInterception.Instance;
		return new HintStatistics
		{
			RenderTicks = instance.RenderTicks,
			HintsSent = instance.HintsSent,
			SuppressedResends = instance.SuppressedResends,
			InputSignatureSkips = instance.InputSignatureSkips,
			Transients = instance.TransientCount,
			Intercepted = instance2.InterceptedCount,
			PassedThrough = instance2.PassedThroughCount,
			ChannelCount = instance.Channels.Count,
			SourceCount = instance.Sources.Count,
			AttributedChannelCount = UiSlotRegistry.Count,
			TrackedPlayers = instance.TrackedPlayers,
			AttributedPlugins = UiSlotRegistry.PluginIds,
			UiIdCount = UiIdRegistry.Count
		};
	}

	public static string CreateReport()
	{
		HintBroker instance = HintBroker.Instance;
		UiInterception instance2 = UiInterception.Instance;
		HintStatistics hintStatistics = Capture();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("<color=#00B7EB>════════ HintChorus 诊断报告 ════════</color>");
		stringBuilder.AppendLine("<b>[合并渲染核心]</b>");
		stringBuilder.AppendLine($"  运行中: {instance.IsRunning}   总开关: {instance.Enabled}");
		stringBuilder.AppendLine($"  刷新轮次: {hintStatistics.RenderTicks}   下发次数: {hintStatistics.HintsSent}   跟踪玩家: {hintStatistics.TrackedPlayers}");
		stringBuilder.AppendLine($"  内容未变跳过下发: {hintStatistics.SuppressedResends} 次   (内容去重开关: {instance.SuppressUnchangedResend})");
		stringBuilder.AppendLine($"  输入签名未变整轮跳过: {hintStatistics.InputSignatureSkips} 次   (快筛开关: {instance.InputSignatureEnabled})");
		stringBuilder.AppendLine($"  一次性提示: {hintStatistics.Transients}");
		stringBuilder.AppendLine("<b>[多层拦截层]</b>");
		stringBuilder.AppendLine($"  已安装: {instance2.IsInstalled}   自动归因: {instance2.AutoAttribute}   原生策略: {instance2.NativePolicy}");
		stringBuilder.AppendLine($"  UiId 派生粒度: {instance2.Granularity}");
		stringBuilder.AppendLine($"  已拦截: {hintStatistics.Intercepted}   放行原生: {hintStatistics.PassedThrough}");
		stringBuilder.AppendLine((PatchGuard.HealCount > 0) ? ($"  <color=#EE7600>[注意] 补丁自愈 {PatchGuard.HealCount} 次 —— 有插件 Unpatch 掉了拦截补丁, 已自动补回" + "(最近丢失: " + string.Join(", ", PatchGuard.LastMissing) + ")</color>") : "  补丁自愈: 0 次(拦截层未被外部摘除)");
		if (PatchGuard.ConsecutiveFailures > 0)
		{
			stringBuilder.AppendLine($"  <color=#EE7600>[注意] 自愈连续失败 {PatchGuard.ConsecutiveFailures} 次, 仍在缺失: {string.Join(", ", PatchGuard.LastMissing)}</color>");
		}
		if (PatchGuard.LastUnresolved.Count > 0)
		{
			stringBuilder.AppendLine($"  <color=#FF4444>[严重] 有补丁落点已找不到目标(这部分拦截已经失效, 需核对游戏版本): {string.Join(", ", PatchGuard.LastUnresolved)}</color>");
		}
		stringBuilder.AppendLine("<b>[UI 表面覆盖情况]</b>");
		foreach (UiSurfaceDescriptor item in UiSurfaceRegistry.All)
		{
			string arg = (item.Implemented ? "<color=#32CD32>[已接管]</color>" : "<color=#EE7600>[预留]</color>");
			stringBuilder.AppendLine($"  {arg} {item.Surface,-15} {item.Handler}");
			stringBuilder.AppendLine("           ← " + item.EntryPoint);
		}
		stringBuilder.AppendLine("<b>[手动通道 / 文本源]</b>");
		stringBuilder.AppendLine($"  手动通道: {hintStatistics.ChannelCount}   文本源: {hintStatistics.SourceCount}");
		stringBuilder.AppendLine("<b>[UiId 信口 (每注册点一个)]</b>");
		stringBuilder.AppendLine($"  已登记 UiId: {hintStatistics.UiIdCount}   在役信口: {hintStatistics.AttributedChannelCount}");
		stringBuilder.AppendLine("  持久化: " + UiIdRegistry.ResolvePath());
		IReadOnlyList<IUiSlot> readOnlyList = UiSlotRegistry.Snapshot();
		if (readOnlyList.Count == 0)
		{
			stringBuilder.AppendLine("  (尚无信口 —— 没有第三方插件在发 UI)");
		}
		else
		{
			foreach (IGrouping<string, IUiSlot> item2 in (from s in readOnlyList
				group s by s.PluginId).OrderBy((IGrouping<string, IUiSlot> g) => g.Key, StringComparer.Ordinal))
			{
				stringBuilder.AppendLine("  ★ " + item2.Key);
				foreach (IUiSlot item3 in item2.OrderBy((IUiSlot s) => s.Surface))
				{
					string text = ((item3.EntryCount > 0) ? $"{item3.EntryCount} 条存活" : "空闲");
					stringBuilder.AppendLine($"      · [{item3.Surface}] {item3.SlotId}  {text}  累计 {item3.TotalReceived}  " + $"最后活动 {item3.LastActivityUtc.ToLocalTime():HH:mm:ss}");
				}
			}
		}
		stringBuilder.AppendLine("<b>[SSS 端口 (SettingId 段)]</b>");
		stringBuilder.AppendLine($"  已初始化: {SssRegistry.IsInitialized}   防劫持: {SssRegistry.ProtectFromOverwrites}");
		stringBuilder.AppendLine($"  强力复写补丁: {SssForceRewrite.IsInstalled}   累计强制复写: {SssForceRewrite.ForcedCount} 次");
		stringBuilder.AppendLine((SssForceRewrite.NestedSkipCount > 0) ? $"  <color=#EE7600>[注意] 嵌套命中 {SssForceRewrite.NestedSkipCount} 次 —— 游戏内部把某个下发入口转调到了另一个, 请核对补丁覆盖是否完整</color>" : "  嵌套命中: 0 (正常)");
		IReadOnlyList<string> registeredModules = SssRegistry.RegisteredModules;
		stringBuilder.AppendLine((registeredModules.Count == 0) ? "  (无注册模块)" : ("  · " + string.Join("\n  · ", registeredModules)));
		stringBuilder.AppendLine("<b>[框架兼容 (LabAPI / EXILED 写法 → 游戏落点)]</b>");
		foreach (FrameworkRoute route in FrameworkCompat.Routes)
		{
			string arg2 = (route.Covered ? "<color=#32CD32>[已接管]</color>" : "<color=#EE7600>[盲区]</color>");
			stringBuilder.AppendLine($"  {arg2} [{route.Framework}] {route.FrameworkCall}");
			stringBuilder.AppendLine("           → " + route.GameTarget);
		}
		stringBuilder.AppendLine("<b>[网络哨兵 (Mirror 消息汇流点)]</b>");
		if (!NetworkSentinel.Enabled)
		{
			stringBuilder.AppendLine("  已安装: False —— 默认关闭, 把配置 EnableNetworkSentinel 设为 true 才会挂上");
		}
		else
		{
			stringBuilder.AppendLine($"  已安装: True   主动收编: {NetworkSentinel.Intercept}");
			stringBuilder.AppendLine($"  HintMessage {NetworkSentinel.HintMessages} 条 —— 绕过语义层 {NetworkSentinel.BypassedHints} 条, 已收编 {NetworkSentinel.AdoptedHints} 条");
			stringBuilder.AppendLine($"  CassieTtsPayload {NetworkSentinel.CassiePayloads} 条 —— 绕过队列直发 {NetworkSentinel.BypassedCassie} 条");
			stringBuilder.AppendLine($"  RpcMessage {NetworkSentinel.RpcMessages} 条 —— 发往广播组件 {NetworkSentinel.BypassedBroadcasts} 条");
			stringBuilder.AppendLine($"  字节流 Send(ArraySegment<byte>) {NetworkSentinel.ByteStreamSends} 包 / {NetworkSentinel.ByteStreamBytes} 字节" + " —— 仅观测: 字节流无法反序列化, 只统计有没有非闭合泛型的流量经过");
		}
		stringBuilder.AppendLine("<b>[自宿主目录 (本底层自己的加载根)]</b>");
		stringBuilder.AppendLine("  家目录: " + RuntimeHome.HomePath);
		stringBuilder.AppendLine($"  就绪: {RuntimeHome.IsReady}   装配解析器: {RuntimeHome.ResolverInstalled}");
		stringBuilder.AppendLine($"  模块({RuntimeHome.DiscoveredModules.Count}): " + ((RuntimeHome.DiscoveredModules.Count == 0) ? "(空) —— 把模块 DLL 放进 modules\\ 即可被解析到" : ("· " + string.Join("\n  · ", RuntimeHome.DiscoveredModules))));
		stringBuilder.AppendLine("<color=#00B7EB>════════════════════════════════════════</color>");
		return stringBuilder.ToString();
	}

	public static string CreateUiIdReport()
	{
		IReadOnlyList<UiIdRecord> readOnlyList = UiIdRegistry.Snapshot();
		if (readOnlyList.Count == 0)
		{
			return "尚无 UiId 记录(还没有第三方插件发过 UI)。";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"UiId 明细 (共 {readOnlyList.Count} 条, 文件: {UiIdRegistry.ResolvePath()}):");
		foreach (UiIdRecord item in readOnlyList)
		{
			string arg = (string.IsNullOrWhiteSpace(item.Alias) ? "" : (" alias=" + item.Alias));
			string arg2 = (string.IsNullOrEmpty(item.MergeInto) ? "" : (" →合并到 " + item.MergeInto));
			stringBuilder.AppendLine("  " + item.ShortId + "  [" + item.Surface + "]  " + item.Plugin + "." + item.Member + "  " + $"命中 {item.Hits}{arg}{arg2}");
		}
		return stringBuilder.ToString();
	}

	public static string CreateSurfaceReport()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"UI 表面覆盖 ({UiSurfaceRegistry.Implemented.Count} 已接管 / {UiSurfaceRegistry.Pending.Count} 预留):");
		foreach (UiSurfaceDescriptor item in UiSurfaceRegistry.All)
		{
			stringBuilder.AppendLine(string.Format("  {0} {1,-15} {2}", item.Implemented ? "[已接管]" : "[预留]  ", item.Surface, item.EntryPoint));
		}
		return stringBuilder.ToString();
	}
}
