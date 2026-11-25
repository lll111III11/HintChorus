using System;

namespace HintChorus.Core.Transport;

/// <summary>
/// <b>网络包安全策略</b>：决定 HC 是否放行某一类出站包。
///
/// <para>默认关闭（只统计不拦截）。开启后按四个开关分别放行 提示 / 广播 / CASSIE / RPC。
/// 本层自己合成的复合提示永远放行，避免把 HintChorus 自己的画面也拦掉。</para>
/// </summary>
internal static class NetworkSecurity
{
    internal static bool Enabled { get; set; }

    internal static bool AllowHintPackets { get; set; } = true;

    internal static bool AllowBroadcastPackets { get; set; } = true;

    internal static bool AllowCassiePackets { get; set; } = true;

    internal static bool AllowRpcPackets { get; set; } = true;

    internal static long BlockedHints;

    internal static long BlockedBroadcasts;

    internal static long BlockedCassie;

    internal static long BlockedRpc;

    internal static bool HintAllowed => !Enabled || AllowHintPackets;

    internal static bool BroadcastAllowed => !Enabled || (AllowBroadcastPackets && AllowRpcPackets);

    internal static bool CassieAllowed => !Enabled || AllowCassiePackets;

    internal static void Apply(HintChorus.HintChorusPlugin.PluginConfig config)
    {
        Enabled = config.EnableNetworkSecurity;
        AllowHintPackets = config.AllowHintPackets;
        AllowBroadcastPackets = config.AllowBroadcastPackets;
        AllowCassiePackets = config.AllowCassiePackets;
        AllowRpcPackets = config.AllowRpcPackets;
    }

    internal static string Status()
    {
        if (!Enabled)
        {
            return "off";
        }
        return string.Format(
            "hint={0} broadcast={1} cassie={2} rpc={3} | blocked {4}/{5}/{6}/{7}",
            AllowHintPackets ? "on" : "off",
            AllowBroadcastPackets ? "on" : "off",
            AllowCassiePackets ? "on" : "off",
            AllowRpcPackets ? "on" : "off",
            BlockedHints,
            BlockedBroadcasts,
            BlockedCassie,
            BlockedRpc);
    }

    internal static void ResetCounters()
    {
        BlockedHints = 0;
        BlockedBroadcasts = 0;
        BlockedCassie = 0;
        BlockedRpc = 0;
    }
}
