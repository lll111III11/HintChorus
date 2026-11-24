using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HintChorus.Core.Broker;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Interception;
using HintChorus.Core.Localization;
using HintChorus.Core.Surfaces;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using Mirror;

namespace HintChorus.Core.Transport;

/// <summary>
/// <b>预更新实操作 / 全网络包管理</b>。
///
/// <para>由 <c>Hint preop</c> 或配置 <c>PreOpTakeover</c> 触发。三件事一起做:</para>
/// <list type="number">
/// <item><b>快照内存替换</b>: 先按当前快照重建复合块, 再把每个玩家的 UI 强制重发一遍,
/// 让屏幕内容立刻切到本底层的那一份。</item>
/// <item><b>强行下线其它 Hint 框架</b>: 摘掉 <c>HintDisplay.Show</c> 与 LabAPI <c>Player.SendHint</c> 上
/// 所有不属于本底层的 Harmony 补丁(典型如 HintServiceMeow / RueI 的前缀与转译器),
/// 它们此后无法再改写提示口。</item>
/// <item><b>全网络包管理</b>: 打开 Mirror 汇流点上的过滤, UI 类网络包(提示 / 广播 / CASSIE)
/// 只放行本底层自己发出的那一份; 其它插件直发的包在内存里被直接判为无效并丢弃,
/// 它们的改动不会到达客户端。</item>
/// </list>
///
/// <para>只影响本次运行, 不写配置; 重启服务器后其它插件会重新挂回自己的补丁。</para>
/// </summary>
internal static class PacketGovernor
{
    private static readonly HashSet<string> OurOwners = new HashSet<string>(StringComparer.Ordinal)
    {
        UiInterception.HarmonyId,
        "com.labapi.HintChorus.arbiter"
    };

    private static readonly object Sync = new object();

    /// <summary>全网络包管理是否生效。</summary>
    internal static bool Active { get; private set; }

    internal static long VoidedHints { get; private set; }

    internal static long VoidedBroadcasts { get; private set; }

    internal static long VoidedCassie { get; private set; }

    internal static int DetachedPatchGroups { get; private set; }

    /// <summary>进入预更新实操作: 卸外部补丁 + 开包过滤 + 瞬间内存替换。返回被摘掉的补丁组数与重发玩家数。</summary>
    internal static bool Engage(out int detachedGroups, out int resentPlayers)
    {
        detachedGroups = 0;
        resentPlayers = 0;
        lock (Sync)
        {
            try
            {
                PacketGovernorStateReset();
                Active = true;
                detachedGroups = DetachForeignPatches();
                DetachedPatchGroups = detachedGroups;

                UiInterception.Instance.EnsureNetworkPatches();
                resentPlayers = ResendAll();
                UiLog.ReportEvent(
                    "PREOP",
                    Lang.Format("preop.report", detachedGroups, resentPlayers));
                return true;
            }
            catch (Exception ex)
            {
                Active = false;
                Logger.Error(Lang.Format("preop.failed", ex.Message));
                UiLog.Report("PreOp", ex);
                return false;
            }
        }
    }

    /// <summary>退出全网络包管理(不还原被摘掉的补丁, 重启后自然恢复)。</summary>
    internal static bool Disengage()
    {
        lock (Sync)
        {
            if (!Active)
            {
                return false;
            }
            Active = false;
            UiLog.ReportEvent("PREOP", Lang.Get("preop.off"));
            return true;
        }
    }

    private static void PacketGovernorStateReset()
    {
        VoidedHints = 0;
        VoidedBroadcasts = 0;
        VoidedCassie = 0;
    }

    /// <summary>提示包过滤: 只放行本底层的复合块与原生 Show 路径, 其它插件直发一律判无效。</summary>
    internal static bool AllowHint(NetworkConnection? conn, Hint? hint)
    {
        if (!Active || hint == null)
        {
            return true;
        }
        try
        {
            if (HintBroker.Instance.IsOwnMergedHint(hint))
            {
                return true;
            }
            if (CallStackHas("Hints.HintDisplay"))
            {
                return true;
            }
            CallerInfo caller = PluginCallerResolver.Resolve();
            if (!caller.IsPlugin)
            {
                return true;
            }
            VoidedHints++;
            UiLog.Detail("PREOP", "作废外部提示包: " + caller.PluginId);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>广播包过滤: 外部插件直发的广播 RPC 判无效。</summary>
    internal static bool AllowBroadcast(RpcMessage message)
    {
        if (!Active)
        {
            return true;
        }
        try
        {
            Broadcast? singleton = Broadcast.Singleton;
            if (singleton == null)
            {
                return true;
            }
            if (((UnityEngine.Object)singleton).GetInstanceID() == 0)
            {
                return true;
            }
            if (message.netId != ((NetworkBehaviour)singleton).netId
                || message.componentIndex != ((NetworkBehaviour)singleton).ComponentIndex)
            {
                return true;
            }
            if (BroadcastSurfaceIsolation.SelfSendDepth > 0)
            {
                return true;
            }
            CallerInfo caller = PluginCallerResolver.Resolve();
            if (!caller.IsPlugin)
            {
                return true;
            }
            VoidedBroadcasts++;
            UiLog.Detail("PREOP", "作废外部广播包: " + caller.PluginId);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>CASSIE 包过滤: 外部插件直发的 TTS 包判无效。</summary>
    internal static bool AllowCassie()
    {
        if (!Active)
        {
            return true;
        }
        try
        {
            CallerInfo caller = PluginCallerResolver.Resolve();
            if (!caller.IsPlugin)
            {
                return true;
            }
            VoidedCassie++;
            UiLog.Detail("PREOP", "作废外部 CASSIE 包: " + caller.PluginId);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>摘掉其它框架挂在这些入口上的补丁, 让它们无法再改写 UI。</summary>
    private static int DetachForeignPatches()
    {
        int groups = 0;
        foreach (MethodBase method in ProtectedMethods())
        {
            Patches? info;
            try
            {
                info = Harmony.GetPatchInfo(method);
            }
            catch (Exception)
            {
                continue;
            }
            if (info == null)
            {
                continue;
            }

            HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (Patch patch in info.Prefixes)
            {
                owners.Add(patch.owner);
            }
            foreach (Patch patch in info.Postfixes)
            {
                owners.Add(patch.owner);
            }
            foreach (Patch patch in info.Transpilers)
            {
                owners.Add(patch.owner);
            }

            foreach (string owner in owners)
            {
                if (string.IsNullOrEmpty(owner) || OurOwners.Contains(owner))
                {
                    continue;
                }
                try
                {
                    new Harmony(owner).Unpatch(method, HarmonyPatchType.All, owner);
                    groups++;
                    UiLog.Detail("PREOP", "已摘除补丁组: " + owner + " @ " + method.DeclaringType?.Name + "." + method.Name);
                }
                catch (Exception ex)
                {
                    UiLog.Report("PreOpDetach", ex);
                }
            }
        }
        return groups;
    }

    private static IEnumerable<MethodBase> ProtectedMethods()
    {
        MethodInfo? show = AccessTools.Method(typeof(HintDisplay), "Show");
        if (show != null)
        {
            yield return show;
        }

        // LabAPI 的便捷入口: Meow 这类框架会给它们挂前缀, 一起摘掉才算"下线"。
        Type? wrapper = AccessTools.TypeByName("LabApi.Features.Wrappers.Player");
        if (wrapper != null)
        {
            foreach (MethodInfo method in wrapper.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.Name != "SendHint" || method.IsGenericMethodDefinition)
                {
                    continue;
                }
                yield return method;
            }
        }
    }

    /// <summary>瞬间内存替换: 让所有在线玩家立刻收到本底层重建后的复合块。</summary>
    private static int ResendAll()
    {
        int count = 0;
        try
        {
            foreach (LabApi.Features.Wrappers.Player player in LabApi.Features.Wrappers.Player.ReadyList)
            {
                ReferenceHub? hub = player.ReferenceHub;
                if (hub == null)
                {
                    continue;
                }
                HintBroker.Instance.ForceResend(hub);
                count++;
            }
            HintBroker.Instance.MarkDirty(null);
        }
        catch (Exception ex)
        {
            UiLog.Report("PreOpResend", ex);
        }
        return count;
    }

    internal static string Status()
    {
        if (!Active)
        {
            return Lang.Get("preop.statusOff");
        }
        return Lang.Format("preop.statusOn", DetachedPatchGroups, VoidedHints, VoidedBroadcasts, VoidedCassie);
    }

    private static bool CallStackHas(string typeName)
    {
        try
        {
            StackTrace trace = new StackTrace(2, false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                Type? type = trace.GetFrame(i)?.GetMethod()?.DeclaringType;
                if (type != null && type.FullName == typeName)
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            return true;
        }
        return false;
    }
}
