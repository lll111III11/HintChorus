using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Localization;
using HintChorus.Core.Transport;
using Hints;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UnityEngine;

namespace HintChorus.Core.Broker;

/// <summary>
/// <b>强力接管 / 覆盖前快照</b>。
///
/// <para>游戏只有一个提示口。别的 Hint 插件如果先一步吞掉 <c>HintDisplay.Show</c>，
/// 本底层的 UI 就会整块消失。这里保存"被接管之前最后一条外部提示"的快照，
/// 并在需要时用它把外部数据热应用回去。</para>
///
/// <para>快照只在运行时保留，不写盘；插件停用或服务器重启即清空。</para>
/// </summary>
public static class HintTakeover
{
    /// <summary>一条外部提示的快照。</summary>
    public sealed class Snapshot
    {
        public string Text = string.Empty;

        public float Duration;

        public string PluginId = string.Empty;

        public float At;
    }

    private sealed class HubComparer : IEqualityComparer<ReferenceHub>
    {
        public static readonly HubComparer Instance = new HubComparer();

        public bool Equals(ReferenceHub? x, ReferenceHub? y)
        {
            return x == y;
        }

        public int GetHashCode(ReferenceHub obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    private static readonly object Sync = new object();

    /// <summary>每个玩家"被本底层接管之前"的最后一条外部提示。</summary>
    private static readonly Dictionary<ReferenceHub, Snapshot> PreOverride =
        new Dictionary<ReferenceHub, Snapshot>(HubComparer.Instance);

    /// <summary>每个玩家、每个插件各自最后一条外部提示(用于按插件热应用)。</summary>
    private static readonly Dictionary<ReferenceHub, Dictionary<string, Snapshot>> ByPlugin =
        new Dictionary<ReferenceHub, Dictionary<string, Snapshot>>(HubComparer.Instance);

    /// <summary>强接管开关(配置项; 默认开)。</summary>
    public static bool ForceEnabled { get; set; } = true;

    /// <summary>是否已经整体退回原生(运行时状态, 不写配置)。</summary>
    public static bool NativeMode { get; private set; }

    /// <summary>记录一条外部提示(在决定吸收/压制之前调用)。</summary>
    public static void Record(ReferenceHub? hub, string? text, float duration, string? pluginId)
    {
        if (hub == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        string owner = string.IsNullOrWhiteSpace(pluginId) ? HintFallback.NativeOwner : pluginId.Trim();
        Snapshot snapshot = new Snapshot
        {
            Text = text,
            Duration = Mathf.Max(0.2f, duration),
            PluginId = owner,
            At = Time.time
        };

        lock (Sync)
        {
            PreOverride[hub] = snapshot;
            if (!ByPlugin.TryGetValue(hub, out Dictionary<string, Snapshot> map))
            {
                map = new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);
                ByPlugin[hub] = map;
            }
            map[owner] = snapshot;
        }

        UiLog.Detail("TAKEOVER", "记录覆盖前数据: " + owner + " | " + snapshot.Duration.ToString("0.0") + "s | " + OneLine(text));
    }

    /// <summary>取某个玩家"覆盖前"的快照。</summary>
    public static bool TryGetPreOverride(ReferenceHub? hub, out Snapshot snapshot)
    {
        snapshot = null!;
        if (hub == null)
        {
            return false;
        }
        lock (Sync)
        {
            return PreOverride.TryGetValue(hub, out snapshot!);
        }
    }

    /// <summary>取某个玩家、某个插件的最后一条快照。</summary>
    public static bool TryGetPlugin(ReferenceHub? hub, string? pluginId, out Snapshot snapshot)
    {
        snapshot = null!;
        if (hub == null || string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }
        lock (Sync)
        {
            return ByPlugin.TryGetValue(hub, out Dictionary<string, Snapshot> map)
                && map.TryGetValue(pluginId.Trim(), out snapshot!);
        }
    }

    /// <summary>把所有玩家的"覆盖前数据"热应用回去。返回成功下发的数量。</summary>
    public static int ReplayAllPreOverride()
    {
        KeyValuePair<ReferenceHub, Snapshot>[] items;
        lock (Sync)
        {
            items = PreOverride.ToArray();
        }

        int count = 0;
        foreach (KeyValuePair<ReferenceHub, Snapshot> item in items)
        {
            if (Replay(item.Key, item.Value))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>把指定插件在各个玩家上的最后一条数据热应用回去。返回成功下发的数量。</summary>
    public static int ReplayPlugin(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return 0;
        }

        string key = pluginId.Trim();
        List<KeyValuePair<ReferenceHub, Snapshot>> items = new List<KeyValuePair<ReferenceHub, Snapshot>>();
        lock (Sync)
        {
            foreach (KeyValuePair<ReferenceHub, Dictionary<string, Snapshot>> item in ByPlugin)
            {
                if (item.Value.TryGetValue(key, out Snapshot snapshot))
                {
                    items.Add(new KeyValuePair<ReferenceHub, Snapshot>(item.Key, snapshot));
                }
            }
        }

        int count = 0;
        foreach (KeyValuePair<ReferenceHub, Snapshot> item in items)
        {
            if (Replay(item.Key, item.Value))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>把一条快照直接下发到玩家(绕过拦截层)。</summary>
    public static bool Replay(ReferenceHub? hub, Snapshot? snapshot)
    {
        if (hub == null || snapshot == null || string.IsNullOrEmpty(snapshot.Text))
        {
            return false;
        }

        try
        {
            float duration = Mathf.Max(0.2f, snapshot.Duration);
            // 让出提示口, 避免刚下发就被本底层的复合块立刻顶掉。
            HintBroker.Instance.HoldSlot(hub, duration + 0.05f);
            TextHint hint = new TextHint(
                snapshot.Text,
                new HintParameter[] { new StringHintParameter(string.Empty) },
                (HintEffect[]?)null,
                duration);
            bool sent = EngineDirect.SendHint(hub, hint);
            HintBroker.Instance.MarkDirty(hub);
            UiLog.ReportEvent("TAKEOVER", (sent ? "热应用: " : "热应用失败: ") + snapshot.PluginId + " | " + duration.ToString("0.0") + "s");
            return sent;
        }
        catch (Exception ex)
        {
            Logger.Warn(Lang.Format("takeover.replayFailed", ex.Message));
            UiLog.Report("TakeoverReplay", ex);
            return false;
        }
    }

    /// <summary>整体退回原生: 停止复合渲染, 可选把覆盖前数据热应用回去。</summary>
    public static int EnterNativeMode(bool replay)
    {
        NativeMode = true;
        ForceEnabled = false;
        HintBroker.Instance.Enabled = false;
        int count = replay ? ReplayAllPreOverride() : 0;
        UiLog.ReportEvent("TAKEOVER", replay
            ? Lang.Format("takeover.enterNative", count)
            : Lang.Get("takeover.enterNativeNoReplay"));
        return count;
    }

    /// <summary>退出原生模式, 恢复复合渲染与强接管。</summary>
    public static void LeaveNativeMode()
    {
        NativeMode = false;
        ForceEnabled = true;
        HintBroker.Instance.Enabled = true;
        HintBroker.Instance.MarkDirty();
        UiLog.ReportEvent("TAKEOVER", Lang.Get("takeover.leaveNative"));
    }

    /// <summary>清空所有快照并复位运行时状态(插件停用时调用)。</summary>
    public static void Clear()
    {
        lock (Sync)
        {
            PreOverride.Clear();
            ByPlugin.Clear();
        }
        NativeMode = false;
        ForceEnabled = true;
        HintBroker.Instance.Enabled = true;
    }

    /// <summary>清掉已经不在线的玩家快照(防止长时间运行后字典只增不减)。</summary>
    public static void Prune(IEnumerable<ReferenceHub> alive)
    {
        HashSet<ReferenceHub> set = new HashSet<ReferenceHub>(alive, HubComparer.Instance);
        lock (Sync)
        {
            foreach (ReferenceHub hub in PreOverride.Keys.ToArray())
            {
                if (hub == null || !set.Contains(hub))
                {
                    PreOverride.Remove(hub);
                }
            }
            foreach (ReferenceHub hub in ByPlugin.Keys.ToArray())
            {
                if (hub == null || !set.Contains(hub))
                {
                    ByPlugin.Remove(hub);
                }
            }
        }
    }

    private static string OneLine(string text)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " ⏎ ");
        return flat.Length > 160 ? flat.Substring(0, 160) + "…" : flat;
    }
}
