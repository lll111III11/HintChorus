using System;
using System.Collections.Generic;
using HintChorus.Core.Broker;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Enums;
using HintChorus.Core.Interception;
using HintChorus.Core.Localization;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Compat;

/// <summary>
/// <b>第三方 Hint 插件兼容模式</b>。
///
/// <para>根据配置 <c>CompatMode</c> 执行三种策略:</para>
/// <list type="bullet">
/// <item><c>Auto</c>(默认): 检测到主流 Hint 插件时自动把它的提示切到原生直通,
/// 不再并入本底层的复合信口, 双方互不干扰; 其它插件仍由本底层托管。</item>
/// <item><c>Notify</c>: 只检测并提醒, 不改变任何插件的接管状态。</item>
/// <item><c>Off</c>: 关闭检测。</item>
/// </list>
///
/// <para>Auto / Notify 下如果仍检测到外部反复清场(补丁护盾多次重装), 会再向控制台报告一次,
/// 并提示用 <c>Hint orvs</c> 查看插件编号、单插件回退, 或 <c>Hint orvs native</c> 整体退回原生。</para>
///
/// <para>兼容处理只影响运行时, 不写配置; 服主手动切换过的插件不会被反复覆盖。</para>
/// </summary>
internal static class CompatibilityMode
{
    /// <summary>首次检测延迟(秒): 等其它插件加载并有机会发出第一条提示。</summary>
    private const float FirstDetectDelay = 3f;

    /// <summary>后续检测间隔(秒): 运行中新装/新加载的插件也能被发现。</summary>
    private const float DetectInterval = 10f;

    /// <summary>冲突统计窗口(秒)。</summary>
    private const float ConflictWindow = 60f;

    /// <summary>窗口内护盾重装达到这个次数, 视为仍然存在冲突。</summary>
    private const int ConflictThreshold = 3;

    /// <summary>两次冲突报告的最小间隔(秒), 避免刷屏。</summary>
    private const float ConflictReportCooldown = 600f;

    private static readonly HashSet<string> HandledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> Announced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static CompatibilityPolicy _policy = CompatibilityPolicy.Auto;

    private static bool _watching;

    private static float _nextDetect;

    private static float _windowStart;

    private static float _nextConflictReport;

    private static long _lastShieldCount;

    private static int _shieldInWindow;

    /// <summary>兼容策略: Auto / Notify / Off。</summary>
    internal static CompatibilityPolicy Policy
    {
        get => _policy;
        set
        {
            _policy = value;
            if (value == CompatibilityPolicy.Off)
            {
                Reset();
            }
        }
    }

    internal static void Reset()
    {
        HandledIds.Clear();
        Announced.Clear();
        _watching = false;
        _nextDetect = 0f;
        _windowStart = 0f;
        _nextConflictReport = 0f;
        _shieldInWindow = 0;
        _lastShieldCount = PatchShield.ReinstallCount;
    }

    /// <summary>由每秒维护循环调用。</summary>
    internal static void Tick()
    {
        if (_policy == CompatibilityPolicy.Off)
        {
            return;
        }
        // 已整体退回原生 / 已让出通道时, 兼容检测没有意义, 也不再报告。
        if (HintTakeover.NativeMode || UiInterception.Instance.AllowExternalTakeover)
        {
            return;
        }

        float now = UnityEngine.Time.time;
        if (_nextDetect <= 0f)
        {
            _nextDetect = now + FirstDetectDelay;
        }
        else if (now >= _nextDetect)
        {
            _nextDetect = now + DetectInterval;
            Detect(now);
        }

        if (_watching)
        {
            WatchConflicts(now);
        }
    }

    private static void Detect(float now)
    {
        IReadOnlyList<MainstreamPlugin> plugins;
        try
        {
            plugins = MainstreamHintPlugins.Detect();
        }
        catch (Exception ex)
        {
            UiLog.Report("CompatDetect", ex);
            return;
        }

        foreach (MainstreamPlugin plugin in plugins)
        {
            if (plugin.RawIds.Length == 0)
            {
                continue;
            }

            if (!_watching)
            {
                _watching = true;
                _windowStart = now;
                _shieldInWindow = 0;
                _lastShieldCount = PatchShield.ReinstallCount;
                _nextConflictReport = 0f;
            }

            if (_policy == CompatibilityPolicy.Notify)
            {
                if (Announced.Add(plugin.DisplayName))
                {
                    Logger.Warn(Lang.Format("compat.notifyDetected", plugin.DisplayName, plugin.Source));
                    Logger.Warn(Lang.Get("compat.notifyAdvice"));
                    UiLog.ReportEvent("COMPAT", Lang.Format("compat.reportNotice", plugin.DisplayName, plugin.Source));
                }
                continue;
            }

            bool adopted = false;
            foreach (string id in plugin.RawIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !HandledIds.Add(id))
                {
                    continue;
                }
                HintFallback.SetNative(id, true);
                adopted = true;
            }

            if (adopted && Announced.Add(plugin.DisplayName))
            {
                Logger.Warn(Lang.Format("compat.detected", plugin.DisplayName, plugin.Source));
                Logger.Warn(Lang.Format("compat.entered", plugin.DisplayName));
                Logger.Warn(Lang.Get("compat.advice"));
                UiLog.ReportEvent("COMPAT", Lang.Format("compat.report", plugin.DisplayName, plugin.Source));
            }
        }
    }

    private static void WatchConflicts(float now)
    {
        long shield = PatchShield.ReinstallCount;
        if (shield > _lastShieldCount)
        {
            _shieldInWindow += (int)(shield - _lastShieldCount);
            _lastShieldCount = shield;
        }

        if (now - _windowStart < ConflictWindow)
        {
            return;
        }

        if (_shieldInWindow >= ConflictThreshold && now >= _nextConflictReport)
        {
            _nextConflictReport = now + ConflictReportCooldown;
            Logger.Warn(Lang.Format("compat.conflict", _shieldInWindow));
            Logger.Warn(Lang.Get("compat.advice"));
            UiLog.ReportEvent("COMPAT", Lang.Format("compat.conflictReport", _shieldInWindow));
        }

        _shieldInWindow = 0;
        _windowStart = now;
    }
}
