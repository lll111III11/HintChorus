using System;
using System.Reflection;
using HarmonyLib;
using HintChorus.Core.Broker;
using HintChorus.Core.Diagnostics;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintChorus.Core.Interception;

/// <summary>
/// <b>补丁护盾</b> —— 对「外部框架直接 Unpatch 清场」的对抗层。
///
/// <para>HintServiceMeow 这类框架在安装时会执行
/// <c>Harmony.Unpatch(HintDisplay.Show, HarmonyPatchType.All)</c>，
/// 把别人挂在这个方法上的补丁<b>整体摘掉</b>；优先级再高也不在表里，等于没有。</para>
///
/// <para>实测（真实 HarmonyX 沙盒）：给 <c>Harmony.Unpatch</c> 挂后置钩子不会触发，
/// 所以唯一可靠的路线是<b>快速检测 + 立即重装</b>。本类每 0.25 秒检查一次
/// <c>HintDisplay.Show</c> 上是否还有本底层的补丁，没有就立刻重装并重发 UI。</para>
///
/// <para>它只做「发现被摘就装回去」，不阻止任何插件正常卸载，也不会和其它框架抢着卸载。</para>
/// </summary>
internal static class PatchShield
{
    /// <summary>检测间隔（秒）。0.25 秒是"肉眼基本无感"和"开销可忽略"的折中。</summary>
    private const float CheckInterval = 0.25f;

    private static MethodInfo? _hintShow;

    private static float _nextCheck;

    private static bool _hooked;

    private static DateTime _assemblyDue = DateTime.MaxValue;

    /// <summary>护盾触发次数（诊断用）。</summary>
    internal static long ReinstallCount { get; private set; }

    internal static void Install()
    {
        _hintShow = AccessTools.Method(typeof(HintDisplay), "Show");
        if (_hooked)
        {
            return;
        }
        try
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            _hooked = true;
        }
        catch (Exception ex)
        {
            UiLog.Report("ShieldHook", ex);
        }
    }

    internal static void Uninstall()
    {
        if (!_hooked)
        {
            return;
        }
        try
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }
        catch (Exception)
        {
        }
        finally
        {
            _hooked = false;
        }
    }

    /// <summary>
    /// 有新程序集加载 = 可能是一个 Hint 框架刚被载入。
    /// 只记一个"稍后检查"的时间点，不在加载线程里做 Harmony 操作。
    /// </summary>
    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        try
        {
            string name = args.LoadedAssembly.GetName().Name ?? string.Empty;
            if (name.Length == 0
                || name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Unity", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("HintChorus", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _assemblyDue = DateTime.UtcNow.AddSeconds(0.6);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>由 0.25 秒协程调用。</summary>
    internal static void Tick()
    {
        float now = Time.time;
        bool due = DateTime.UtcNow >= _assemblyDue;
        if (due)
        {
            _assemblyDue = DateTime.MaxValue;
        }

        if (!due && now < _nextCheck)
        {
            return;
        }
        _nextCheck = now + CheckInterval;

        if (_hintShow == null)
        {
            _hintShow = AccessTools.Method(typeof(HintDisplay), "Show");
            if (_hintShow == null)
            {
                return;
            }
        }

        if (HasOurPatch(_hintShow))
        {
            return;
        }

        try
        {
            ReinstallCount++;
            Logger.Warn("[HintChorus] 提示条补丁被外部摘掉, 护盾立即重装并在下一轮重发 UI");
            UiInterception.Instance.Repair();
            // 不等续期: 让所有玩家的复合块在下一轮(≤40ms)重新盖回去。
            HintBroker.Instance.MarkDirty();
            UiLog.ReportEvent("SHIELD", "提示条补丁被外部摘掉, 已立即重装并重发 UI (第 " + ReinstallCount + " 次)");
        }
        catch (Exception ex)
        {
            Logger.Error("[HintChorus] 护盾重装失败: " + ex.Message);
            UiLog.Report("ShieldRepair", ex);
        }
    }

    private static bool HasOurPatch(MethodBase method)
    {
        try
        {
            Patches? info = Harmony.GetPatchInfo(method);
            if (info == null)
            {
                return false;
            }
            foreach (Patch patch in info.Prefixes)
            {
                if (string.Equals(patch.owner, UiInterception.HarmonyId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            foreach (Patch patch in info.Postfixes)
            {
                if (string.Equals(patch.owner, UiInterception.HarmonyId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
        catch (Exception)
        {
            // 查不到就当"还在"，避免误判导致反复摘挂补丁。
            return true;
        }
    }
}
