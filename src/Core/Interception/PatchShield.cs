using System;
using System.Reflection;
using HarmonyLib;
using HintChorus.Core.Broker;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Localization;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintChorus.Core.Interception;

/// <summary>
/// <b>补丁护盾</b> —— 仲裁层之后的兜底层。
///
/// <para>HintServiceMeow 这类框架在安装时会执行
/// <c>Harmony.Unpatch(HintDisplay.Show, HarmonyPatchType.All)</c>，
/// 把别人挂在这个方法上的补丁<b>整体摘掉</b>；优先级再高也不在表里，等于没有。</para>
///
/// <para>一线防御是 <see cref="PatchArbiter"/>（同步仲裁，挂住 Harmony 的全部卸载入口）。
/// 本类负责兜底：每 0.25 秒让仲裁层自检一次，覆盖调用点被 JIT 内联、
/// 整表被 <c>UnpatchAll(null)</c> 清空等极端情况；若仲裁层未启用，则退化为
/// 直接检查 <c>HintDisplay.Show</c> 并重装。</para>
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

        if (UiInterception.Instance.AllowExternalTakeover)
        {
            return;
        }

        try
        {
            PatchArbiter.Verify();
        }
        catch (Exception ex)
        {
            UiLog.Report("ArbiterVerify", ex);
        }

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
            Logger.Warn(Lang.Get("shield.reinstalled"));
            UiInterception.Instance.Repair();
            // 不等续期: 让所有玩家的复合块在下一轮(≤40ms)重新盖回去。
            HintBroker.Instance.MarkDirty();
            UiLog.ReportEvent("SHIELD", Lang.Format("shield.reinstalledReport", ReinstallCount));
        }
        catch (Exception ex)
        {
            Logger.Error(Lang.Format("shield.failed", ex.Message));
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
