using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HintChorus.Core.Broker;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Localization;
using LabApi.Features.Console;

namespace HintChorus.Core.Interception;

/// <summary>
/// <b>补丁仲裁层</b> —— 本插件自有的补丁管理中间层。
///
/// <para>外部框架（典型如 HintServiceMeow）安装时会执行
/// <c>Harmony.Unpatch(HintDisplay.Show, HarmonyPatchType.All)</c>。
/// HarmonyX 的第三个参数默认值是 <c>"*"</c>，而 <c>PatchInfo.Remove("*")</c>
/// 会把该方法上的<b>所有</b>补丁整表清空 —— 优先级再高也没用，因为已经不在表里了。</para>
///
/// <para>仲裁层不去阻止别人卸载（那会破坏其它插件的正常卸载流程），而是挂住
/// Harmony 的全部卸载入口：<c>Harmony.Unpatch</c> / <c>Harmony.UnpatchAll</c> /
/// <c>PatchProcessor.Unpatch</c>。任何一次卸载之后，只要受保护的方法上少了本底层的补丁，
/// 就在<b>同一个调用栈内</b>立即装回去，然后让 Broker 重发 UI。</para>
///
/// <para>效果：外部可以卸载整个世界，但卸载不掉本底层；等它挂自己的前缀时，
/// 我们的前缀已经是 int.MaxValue 且先入表，仍然第一个执行。</para>
///
/// <para>与 <see cref="PatchShield"/> 的分工：仲裁层是同步的一线（覆盖所有 Harmony API 路径），
/// 护盾是 0.25 秒轮询的兜底（覆盖被 JIT 内联等极端情况）。</para>
/// </summary>
internal static class PatchArbiter
{
    /// <summary>仲裁层自己的 Harmony 实例：与拦截层分离，避免自愈时把自己一起摘掉。</summary>
    private const string ArbiterId = "com.labapi.HintChorus.arbiter";

    /// <summary>受保护方法 -> 期望存在的 [prefix, postfix, transpiler] 数量。</summary>
    private static readonly Dictionary<MethodBase, int[]> Expected = new Dictionary<MethodBase, int[]>();

    private static readonly object Gate = new object();

    private static Harmony? _harmony;

    private static FieldInfo? _processorOriginal;

    private static readonly List<HookPoint> HookPoints = new List<HookPoint>();

    private static int _suppress;

    private static bool _installed;

    /// <summary>一个受控的 Harmony 卸载入口。</summary>
    private sealed class HookPoint
    {
        internal readonly MethodInfo Target;

        internal readonly MethodInfo Postfix;

        internal HookPoint(MethodInfo target, MethodInfo postfix)
        {
            Target = target;
            Postfix = postfix;
        }
    }

    /// <summary>仲裁层已恢复次数（诊断用）。</summary>
    internal static long RestoreCount { get; private set; }

    internal static bool IsInstalled => _installed;

    internal static void Install()
    {
        if (_installed)
        {
            return;
        }

        _processorOriginal = AccessTools.Field(typeof(PatchProcessor), "original");
        _harmony = new Harmony(ArbiterId);

        Hook(typeof(Harmony), "Unpatch", new[] { typeof(MethodBase), typeof(HarmonyPatchType), typeof(string) }, nameof(AfterUnpatch));
        Hook(typeof(Harmony), "Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }, nameof(AfterUnpatch));
        Hook(typeof(Harmony), "UnpatchAll", new[] { typeof(string) }, nameof(AfterUnpatchAll));
        Hook(typeof(PatchProcessor), "Unpatch", new[] { typeof(HarmonyPatchType), typeof(string) }, nameof(AfterProcessorUnpatch));
        Hook(typeof(PatchProcessor), "Unpatch", new[] { typeof(MethodInfo) }, nameof(AfterProcessorUnpatch));

        _installed = true;
        Capture();
        StartupLog.Info(Lang.Get("arbiter.installed"));
    }

    internal static void Uninstall()
    {
        if (!_installed)
        {
            return;
        }

        _installed = false;
        Suspend();
        try
        {
            _harmony?.UnpatchAll(ArbiterId);
        }
        catch (Exception ex)
        {
            UiLog.ReportEvent("ARBITER", Lang.Format("arbiter.uninstallFailed", ex.Message));
        }
        finally
        {
            Resume();
        }

        _harmony = null;
        lock (Gate)
        {
            Expected.Clear();
        }
        HookPoints.Clear();
    }

    /// <summary>本底层自己改补丁时调用，避免仲裁层把自愈当成外部攻击。</summary>
    internal static void Suspend()
    {
        _suppress++;
    }

    internal static void Resume()
    {
        if (_suppress > 0)
        {
            _suppress--;
        }
    }

    /// <summary>
    /// 记录当前由本底层持有补丁的方法与数量。
    /// 拦截层每次安装 / 自愈后调用一次，仲裁只认这份清单。
    /// </summary>
    internal static void Capture()
    {
        try
        {
            Dictionary<MethodBase, int[]> captured = new Dictionary<MethodBase, int[]>();
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
            {
                Patches? info = Harmony.GetPatchInfo(method);
                if (info == null)
                {
                    continue;
                }
                int prefixes = CountOurs(info.Prefixes);
                int postfixes = CountOurs(info.Postfixes);
                int transpilers = CountOurs(info.Transpilers);
                if (prefixes + postfixes + transpilers > 0)
                {
                    captured[method] = new[] { prefixes, postfixes, transpilers };
                }
            }
            lock (Gate)
            {
                Expected.Clear();
                foreach (KeyValuePair<MethodBase, int[]> pair in captured)
                {
                    Expected[pair.Key] = pair.Value;
                }
            }
        }
        catch (Exception ex)
        {
            UiLog.Report("ArbiterCapture", ex);
        }
        EnsureHooks();
    }

    private static void Hook(Type type, string name, Type[] args, string postfixName)
    {
        MethodInfo? target = AccessTools.Method(type, name, args);
        MethodInfo? postfix = AccessTools.Method(typeof(PatchArbiter), postfixName);
        if (target == null || postfix == null)
        {
            Logger.Warn(Lang.Format("arbiter.hookFailed", type.Name, name));
            return;
        }
        HookPoints.Add(new HookPoint(target, postfix));
        ApplyHook(HookPoints[HookPoints.Count - 1]);
    }

    private static void ApplyHook(HookPoint point)
    {
        try
        {
            _harmony?.Patch(point.Target, postfix: new HarmonyMethod(point.Postfix));
        }
        catch (Exception ex)
        {
            UiLog.Report("ArbiterHook:" + point.Target.DeclaringType?.Name + "." + point.Target.Name, ex);
        }
    }

    /// <summary>仲裁层自己的钩子也可能被 UnpatchAll(null) 一起清掉，这里逐个补回。</summary>
    private static bool EnsureHooks()
    {
        if (_harmony == null)
        {
            return false;
        }
        bool repaired = false;
        foreach (HookPoint point in HookPoints)
        {
            if (HasArbiterPatch(point.Target))
            {
                continue;
            }
            ApplyHook(point);
            repaired = true;
        }
        return repaired;
    }

    private static bool HasArbiterPatch(MethodBase method)
    {
        Patches? info = Harmony.GetPatchInfo(method);
        if (info == null)
        {
            return false;
        }
        foreach (Patch patch in info.Postfixes)
        {
            if (string.Equals(patch.owner, ArbiterId, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void AfterUnpatch(MethodBase __0)
    {
        Ensure("Harmony.Unpatch", __0);
    }

    private static void AfterUnpatchAll()
    {
        Ensure("Harmony.UnpatchAll", null);
    }

    private static void AfterProcessorUnpatch(PatchProcessor __instance)
    {
        Ensure("PatchProcessor.Unpatch", ResolveOriginal(__instance));
    }

    private static MethodBase? ResolveOriginal(PatchProcessor processor)
    {
        if (processor == null || _processorOriginal == null)
        {
            return null;
        }
        try
        {
            return _processorOriginal.GetValue(processor) as MethodBase;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Ensure(string source, MethodBase? affected)
    {
        if (!_installed || _suppress > 0)
        {
            return;
        }
        if (UiInterception.Instance.AllowExternalTakeover)
        {
            return;
        }

        EnsureHooks();

        lock (Gate)
        {
            if (Expected.Count == 0)
            {
                return;
            }
            if (affected != null && !Expected.ContainsKey(affected))
            {
                return;
            }
        }
        if (!AnyMissing(affected))
        {
            return;
        }

        Restore(source);
    }

    /// <summary>护盾每 0.25 秒调用：兜住被 JIT 内联、钩子被整表清掉等极端情况。</summary>
    internal static void Verify()
    {
        if (!_installed || _suppress > 0)
        {
            return;
        }
        if (UiInterception.Instance.AllowExternalTakeover)
        {
            return;
        }
        EnsureHooks();
        if (Expected.Count > 0 && AnyMissing(null))
        {
            Restore("护盾轮询");
        }
    }

    private static void Restore(string source)
    {
        RestoreCount++;
        Logger.Warn(Lang.Format("arbiter.restoreWarn", source));
        UiLog.ReportEvent("ARBITER", Lang.Format("arbiter.restoreEvent", source, RestoreCount));

        UiInterception.Instance.Repair();
        HintBroker.Instance.MarkDirty();
        EnsureHooks();
        Capture();
    }

    private static bool AnyMissing(MethodBase? only)
    {
        lock (Gate)
        {
            if (only != null)
            {
                return Missing(only);
            }
            foreach (MethodBase method in Expected.Keys)
            {
                if (Missing(method))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static bool Missing(MethodBase method)
    {
        int[] expected;
        if (!Expected.TryGetValue(method, out expected))
        {
            return false;
        }
        Patches? info = Harmony.GetPatchInfo(method);
        if (info == null)
        {
            return true;
        }
        return CountOurs(info.Prefixes) < expected[0]
            || CountOurs(info.Postfixes) < expected[1]
            || CountOurs(info.Transpilers) < expected[2];
    }

    private static int CountOurs(IEnumerable<Patch> patches)
    {
        int count = 0;
        foreach (Patch patch in patches)
        {
            if (string.Equals(patch.owner, UiInterception.HarmonyId, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }
}
