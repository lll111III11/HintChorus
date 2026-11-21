using System;
using System.Collections.Generic;
using System.Linq;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Interfaces;

namespace HintChorus.Core.Broker;

/// <summary>
/// <b>插件级原生回退(Hint orvs)</b>。
///
/// <para>本底层的默认行为是把所有插件的提示并入自己的复合信口。若某个插件与合并层不兼容,
/// 服主可在服务器控制台执行 <c>Hint orvs</c>, 把<b>指定插件</b>切回"原生直通" ——
/// 它的提示不再被吸收, 而是像装本插件之前一样由游戏原版 <c>HintDisplay</c> 直接显示。</para>
///
/// <para>切换只影响运行时, 不写配置文件; 重启服务器即恢复默认(全部合并)。
/// 只有被选中的插件走原生, 其它插件仍由合并层托管 —— 因此<b>多个 Hint 插件可以同时存在且互不冲突</b>。</para>
/// </summary>
public static class HintFallback
{
    /// <summary>游戏原生提示的归属名(不允许对它做回退)。</summary>
    public const string NativeOwner = "系统原生提示";

    private static readonly HashSet<string> NativePlugins =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly object Sync = new object();

    /// <summary>该插件当前是否已被切到原生直通。</summary>
    public static bool IsNative(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }
        lock (Sync)
        {
            return NativePlugins.Contains(pluginId.Trim());
        }
    }

    /// <summary>内部热路径判断(与 <see cref="IsNative"/> 相同, 名字用于表达用途)。</summary>
    internal static bool ShouldPassThrough(string? pluginId)
    {
        return IsNative(pluginId);
    }

    /// <summary>切换单个插件的模式。返回是否真的发生了变化。</summary>
    public static bool SetNative(string? pluginId, bool native)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }
        string key = pluginId.Trim();
        if (string.Equals(key, NativeOwner, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lock (Sync)
        {
            bool changed = native ? NativePlugins.Add(key) : NativePlugins.Remove(key);
            if (changed)
            {
                UiLog.ReportEvent("FALLBACK", (native ? "切回原生直通: " : "恢复合并渲染: ") + key);
            }
            return changed;
        }
    }

    /// <summary>把所有已发现的 Hint 插件统一切到原生(或统一恢复合并)。返回受影响的插件数。</summary>
    public static int SetAllNative(bool native)
    {
        IReadOnlyList<string> candidates = Candidates();
        int changed = 0;
        foreach (string plugin in candidates)
        {
            if (SetNative(plugin, native))
            {
                changed++;
            }
        }
        return changed;
    }

    /// <summary>清空所有回退(全部恢复合并)。返回被恢复的插件数。</summary>
    public static int ResetAll()
    {
        lock (Sync)
        {
            int count = NativePlugins.Count;
            if (count > 0)
            {
                NativePlugins.Clear();
                UiLog.ReportEvent("FALLBACK", "已恢复全部插件为合并渲染, 共 " + count + " 个");
            }
            return count;
        }
    }

    /// <summary>当前处于原生直通的插件快照。</summary>
    public static IReadOnlyList<string> NativeSnapshot()
    {
        lock (Sync)
        {
            return NativePlugins.OrderBy((string s) => s, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    /// <summary>
    /// <b>已发现的 Hint 插件</b> —— 信口里有记录的插件 + 通过 API 注册了通道/文本源的插件。
    /// 排除游戏原生提示自身。
    /// </summary>
    public static IReadOnlyList<string> Candidates()
    {
        HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (string id in UiSlotRegistry.PluginIds)
            {
                AddCandidate(set, id);
            }
        }
        catch (Exception)
        {
        }

        try
        {
            foreach (IHintChannel channel in HintBroker.Instance.Channels)
            {
                AddCandidate(set, channel.ModuleId);
            }
            foreach (IHintTextSource source in HintBroker.Instance.Sources)
            {
                AddCandidate(set, source.ModuleId);
            }
        }
        catch (Exception)
        {
        }

        return set.OrderBy((string s) => s, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddCandidate(HashSet<string> set, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }
        string key = id.Trim();
        if (string.Equals(key, NativeOwner, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (string.Equals(key, "HintChorus", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        set.Add(key);
    }
}
