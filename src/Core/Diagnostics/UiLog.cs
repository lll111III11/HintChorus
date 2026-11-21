using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HintChorus.Core.Broker;

namespace HintChorus.Core.Diagnostics;

/// <summary>
/// <b>HintChorus 报告 / UI 日志</b>。
///
/// <para>两级日志, 默认只开第一级:</para>
/// <list type="number">
///   <item><b>崩溃 / 异常报告</b>(<c>EnableCrashReportLog</c>, 默认 <b>开</b>) ——
///     插件自身异常、补丁自愈失败、命令异常等写入 <c>logs/report-yyyyMMdd.log</c>。
///     这一级开销极小, 保留它是为了"出问题时有据可查"。</item>
///   <item><b>详细 UI 日志</b>(<c>EnableDetailedUiLog</c>, 默认 <b>关</b>) ——
///     记录每个插件的提示内容、最终屏幕位置、每个玩家的合成结果, 写入 <c>logs/ui-yyyyMMdd.log</c>。
///     打开后 IO 开销明显, 只在排障时开启。</item>
/// </list>
///
/// <para>日志目录: <c>configs/&lt;端口&gt;/HintChorus/logs/</c>。所有写入都做异常隔离,
/// 日志系统本身永远不会把插件拖挂。</para>
/// </summary>
public static class UiLog
{
    private static readonly object Sync = new object();

    private static string _directory = string.Empty;

    private static bool _crashEnabled = true;

    private static bool _detailedEnabled;

    private static bool _perPlayerEnabled;

    private static readonly Dictionary<string, int> LastWriteTicks =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>详细 UI 日志是否开启(热路径用这个做最外层判断)。</summary>
    public static bool Detailed => _detailedEnabled;

    /// <summary>崩溃报告是否开启。</summary>
    public static bool CrashReportEnabled => _crashEnabled;

    /// <summary>由配置装配处调用。重复调用只更新开关, 不重建目录。</summary>
    public static void Initialize(bool crashReport, bool detailedUi, bool perPlayer, string directory)
    {
        lock (Sync)
        {
            _crashEnabled = crashReport;
            _detailedEnabled = detailedUi;
            _perPlayerEnabled = perPlayer && detailedUi;
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _directory = directory;
            }
            if (_crashEnabled || _detailedEnabled)
            {
                TryEnsureDirectory();
            }
        }
    }

    /// <summary>启动报告 —— 默认日志功能, 记录版本、端口、开关与拦截状态。</summary>
    public static void WriteStartupReport(string report)
    {
        if (!_crashEnabled || string.IsNullOrEmpty(report))
        {
            return;
        }
        Append("report", "STARTUP", report);
    }

    /// <summary>异常报告 —— 默认日志功能。只落盘, 控制台的错误打印由调用方负责。</summary>
    public static void Report(string context, Exception? exception)
    {
        if (!_crashEnabled || exception == null)
        {
            return;
        }
        StringBuilder builder = new StringBuilder();
        builder.Append("上下文: ").Append(context).AppendLine();
        builder.Append("类型: ").Append(exception.GetType().FullName).AppendLine();
        builder.Append("消息: ").AppendLine(exception.Message);
        builder.Append("堆栈:").AppendLine();
        builder.Append(exception);
        Append("report", "ERROR", builder.ToString());
    }

    /// <summary>普通报告条目(非异常), 例如命令操作、回退切换。</summary>
    public static void ReportEvent(string context, string message)
    {
        if (!_crashEnabled || string.IsNullOrEmpty(message))
        {
            return;
        }
        Append("report", context, message);
    }

    /// <summary>详细 UI 日志(默认关)。</summary>
    public static void Detail(string category, string message)
    {
        if (!_detailedEnabled || string.IsNullOrEmpty(message))
        {
            return;
        }
        Append("ui", category, message);
    }

    /// <summary>详细 UI 日志 · 带节流(同一 key 在 interval 秒内只写一次)。</summary>
    public static void DetailThrottled(string key, float intervalSeconds, string category, string message)
    {
        if (!_detailedEnabled)
        {
            return;
        }
        if (!ShouldWrite(key, intervalSeconds))
        {
            return;
        }
        Append("ui", category, message);
    }

    /// <summary>
    /// 详细 UI 日志 · <b>一条插件提示被拦截 / 放行</b>。
    /// 默认关闭时这里只做一次 bool 判断, 不上锁也不拼字符串。
    /// </summary>
    public static void Hint(string pluginId, string surface, string text, float duration, string action = "并入")
    {
        if (!_detailedEnabled)
        {
            return;
        }
        string line = "[提示] " + action
            + " | 表面: " + surface
            + " | 插件: " + Safe(pluginId)
            + " | 时长: " + duration.ToString("0.0") + "s"
            + " | 内容: " + OneLine(text);
        Append("ui", "HINT", line);
    }

    /// <summary>
    /// 详细 UI 日志 · <b>某个插件/归属这一帧占用的屏幕位置</b>。
    /// 这是「各个 UI 文字显示位置」的核心记录; 同一归属默认 2 秒最多一条。
    /// </summary>
    public static void Layout(string owner, string position, IReadOnlyList<string> lines)
    {
        if (!_detailedEnabled || lines == null || lines.Count == 0)
        {
            return;
        }
        if (!ShouldWrite("layout:" + owner, 2f))
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("[位置] 插件: ").Append(Safe(owner))
            .Append(" | 位置: ").Append(position)
            .Append(" | 行数: ").Append(lines.Count)
            .AppendLine();
        for (int i = 0; i < lines.Count; i++)
        {
            builder.Append("  ").Append(i + 1).Append(") ").Append(OneLine(lines[i])).AppendLine();
        }
        Append("ui", "LAYOUT", builder.ToString());
    }

    /// <summary>详细 UI 日志 · 每个玩家最终收到的合成文本(需额外打开 <c>DetailedUiLogPerPlayer</c>)。</summary>
    public static void PlayerRender(object? player, string text)
    {
        if (!_detailedEnabled || !_perPlayerEnabled || string.IsNullOrEmpty(text))
        {
            return;
        }
        string key = "player:" + ((player != null) ? player.GetHashCode() : 0);
        if (!ShouldWrite(key, 2f))
        {
            return;
        }
        Append("ui", "PLAYER", "[下发] 玩家: " + Safe(Convert.ToString(player)) + "\n" + Indent(text));
    }

    /// <summary>强制刷新节流表(配置刚切换时调用, 避免旧 key 抑制首条日志)。</summary>
    public static void ResetThrottle()
    {
        lock (Sync)
        {
            LastWriteTicks.Clear();
        }
    }

    private static bool ShouldWrite(string key, float intervalSeconds)
    {
        if (string.IsNullOrEmpty(key))
        {
            return true;
        }
        int now = Environment.TickCount;
        int interval = (int)(Math.Max(0f, intervalSeconds) * 1000f);
        lock (Sync)
        {
            if (LastWriteTicks.TryGetValue(key, out int last))
            {
                int delta = unchecked(now - last);
                if (delta >= 0 && delta < interval)
                {
                    return false;
                }
            }
            LastWriteTicks[key] = now;
            return true;
        }
    }

    private static void Append(string kind, string category, string message)
    {
        try
        {
            lock (Sync)
            {
                TryEnsureDirectory();
                if (string.IsNullOrEmpty(_directory))
                {
                    return;
                }
                string path = Path.Combine(_directory, kind + "-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                string block = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] [" + category + "] "
                    + message.Replace("\r\n", "\n").Replace("\n", "\n    ")
                    + Environment.NewLine;
                File.AppendAllText(path, block, new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // 日志系统自身永不抛异常。
        }
    }

    private static void TryEnsureDirectory()
    {
        try
        {
            if (string.IsNullOrEmpty(_directory))
            {
                return;
            }
            Directory.CreateDirectory(_directory);
        }
        catch (Exception)
        {
        }
    }

    private static string Safe(string? value)
    {
        return string.IsNullOrEmpty(value) ? "(未知)" : value;
    }

    private static string OneLine(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(空)";
        }
        string flat = text.Replace("\r", " ").Replace("\n", " ⏎ ");
        return (flat.Length > 400) ? flat.Substring(0, 400) + "…" : flat;
    }

    private static string Indent(string text)
    {
        string flat = text.Replace("\r\n", "\n");
        return "    " + flat.Replace("\n", "\n    ");
    }
}
