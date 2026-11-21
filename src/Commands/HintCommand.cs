using System;
using System.Collections.Generic;
using System.Text;
using CommandSystem;
using HintChorus.Core.Broker;
using HintChorus.Core.Diagnostics;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Commands;

/// <summary>
/// <b>服务器控制台命令 <c>Hint orvs</c></b> —— 插件级原生回退。
///
/// <para>用途: 某个 Hint 插件与本底层不兼容时, 不必卸载插件、也不必改配置文件,
/// 直接在<b>服务器后台控制台</b>输入 <c>Hint orvs</c> 即可把指定插件切回游戏原版提示。</para>
///
/// <para>命令大小写不敏感(<c>Hint</c> / <c>hint</c> / <c>HINT</c>, <c>orvs</c> / <c>ORVS</c> 均可)。</para>
///
/// <para>支持多个 Hint 插件共存: 只有被选中的插件走原生直通, 其余仍由合并层托管, 互不冲突。
/// 切换只影响本次运行, 不写配置; 重启即全部恢复默认合并。</para>
/// </summary>
internal sealed class HintCommand : ICommand, IUsageProvider
{
    internal static readonly HintCommand Instance = new HintCommand();

    private static bool _registered;

    private static DateTime _nextAttemptUtc = DateTime.MinValue;

    private static int _failedAttempts;

    public string Command => "hint";

    public string[] Aliases => new[] { "hc" };

    public string Description => "HintChorus 控制台: Hint orvs 把指定 Hint 插件切回游戏原生提示";

    public string[] Usage => new[]
    {
        "Hint orvs              —— 列出已发现的 Hint 插件并选择编号",
        "Hint orvs <编号>       —— 把该插件切回原生直通",
        "Hint orvs <编号> merge —— 把该插件恢复为合并渲染",
        "Hint orvs all          —— 全部切回原生",
        "Hint orvs reset        —— 全部恢复合并"
    };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        try
        {
            if (arguments.Count == 0)
            {
                response = BuildHelp();
                return true;
            }

            string sub = Arg(arguments, 0);
            if (!sub.Equals("orvs", StringComparison.OrdinalIgnoreCase))
            {
                response = "[HintChorus] 未知子命令: " + sub + "\n" + BuildHelp();
                return false;
            }

            if (arguments.Count == 1)
            {
                return ListOrPick(out response);
            }

            string target = Arg(arguments, 1);
            if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<string> candidates = HintFallback.Candidates();
                int changed = HintFallback.SetAllNative(true);
                response = "[HintChorus] 已把 " + changed + " 个插件切回原生直通(共发现 " + candidates.Count
                    + " 个)。输入 Hint orvs reset 可全部恢复合并。";
                return true;
            }

            if (target.Equals("reset", StringComparison.OrdinalIgnoreCase)
                || target.Equals("merge", StringComparison.OrdinalIgnoreCase))
            {
                int restored = HintFallback.ResetAll();
                response = "[HintChorus] 已恢复 " + restored + " 个插件为合并渲染。";
                return true;
            }

            if (!int.TryParse(target, out int index))
            {
                response = "[HintChorus] 编号必须是数字。\n" + BuildHelp();
                return false;
            }

            IReadOnlyList<string> list = HintFallback.Candidates();
            if (list.Count == 0)
            {
                response = "[HintChorus] 当前没有发现任何第三方 Hint 插件(尚未有插件发过提示)。";
                return false;
            }
            if (index < 1 || index > list.Count)
            {
                response = "[HintChorus] 编号超出范围: " + index + " (有效范围 1-" + list.Count + ")。";
                return false;
            }

            string pluginId = list[index - 1];
            bool merge = arguments.Count >= 3
                && Arg(arguments, 2).Equals("merge", StringComparison.OrdinalIgnoreCase);

            if (merge)
            {
                HintFallback.SetNative(pluginId, false);
                response = "[HintChorus] 已把 " + pluginId + " 恢复为合并渲染。";
            }
            else
            {
                HintFallback.SetNative(pluginId, true);
                response = "[HintChorus] 已把 " + pluginId + " 切回原生直通 —— 它的提示将像原版一样直接显示, "
                    + "其它插件仍由合并层托管。\n如需恢复: Hint orvs " + index + " merge";
            }
            UiLog.Detail("COMMAND", (merge ? "merge " : "native ") + pluginId + " by " + SafeSender(sender));
            return true;
        }
        catch (Exception ex)
        {
            UiLog.Report("HintCommand", ex);
            response = "[HintChorus] 命令执行异常: " + ex.Message;
            return false;
        }
    }

    /// <summary>无编号时: 0 个 → 提示; 1 个 → 直接切换; 多个 → 列出并要求输入编号。</summary>
    private static bool ListOrPick(out string response)
    {
        IReadOnlyList<string> list = HintFallback.Candidates();
        if (list.Count == 0)
        {
            response = "[HintChorus] 当前没有发现任何第三方 Hint 插件(尚未有插件发过提示)。"
                + "\n本插件支持多个 Hint 插件同时存在且互不冲突; 若某个插件出问题, 用 Hint orvs <编号> 单独回退。";
            return false;
        }

        if (list.Count == 1)
        {
            string only = list[0];
            HintFallback.SetNative(only, true);
            response = "[HintChorus] 只发现 1 个 Hint 插件, 已自动切回原生直通: " + only
                + "\n恢复合并: Hint orvs 1 merge";
            UiLog.Detail("COMMAND", "auto native " + only);
            return true;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[HintChorus] 已发现 " + list.Count + " 个 Hint 插件, 请选择要切回原生的编号:");
        for (int i = 0; i < list.Count; i++)
        {
            builder.Append("  ").Append(i + 1).Append(") ").Append(list[i])
                .Append(HintFallback.IsNative(list[i]) ? "  [原生直通]" : "  [合并中]")
                .AppendLine();
        }
        builder.AppendLine("输入 Hint orvs <编号> 切换; 已切原生的可加 merge 恢复, 例如: Hint orvs 2 merge");
        builder.Append("全部恢复: Hint orvs reset");
        response = builder.ToString();
        return true;
    }

    private static string BuildHelp()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[HintChorus] 控制台命令(大小写不敏感):");
        foreach (string line in Instance.Usage)
        {
            builder.Append("  ").AppendLine(line);
        }
        builder.Append("说明: 本插件支持多个 Hint 插件共存; 某个插件出问题时可用 orvs 单独回退, 其余插件不受影响。");
        return builder.ToString();
    }

    private static string Arg(ArraySegment<string> args, int index)
    {
        if (args.Array == null || index < 0 || index >= args.Count)
        {
            return string.Empty;
        }
        return args.Array[args.Offset + index] ?? string.Empty;
    }

    private static string SafeSender(ICommandSender? sender)
    {
        try
        {
            return sender?.LogName ?? "(unknown)";
        }
        catch (Exception)
        {
            return "(unknown)";
        }
    }

    /// <summary>把命令注册到服务器控制台(幂等; 控制台尚未就绪时由维护循环重试)。</summary>
    internal static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        // 控制台可能在 LabAPI 插件加载阶段尚未就绪: 5 秒重试一次, 且只在前 3 次失败时打印告警, 避免刷屏。
        DateTime now = DateTime.UtcNow;
        if (now < _nextAttemptUtc)
        {
            return;
        }
        _nextAttemptUtc = now.AddSeconds(5);

        try
        {
            GameCore.Console.ConsoleCommandHandler.RegisterCommand(Instance);
            _registered = true;
            _failedAttempts = 0;
            UiLog.Detail("COMMAND", "已注册控制台命令 Hint / hc");
            StartupLog.Info("[HintChorus] 控制台命令已就绪: Hint orvs (大小写不敏感)");
        }
        catch (Exception ex)
        {
            _failedAttempts++;
            if (_failedAttempts <= 3)
            {
                Logger.Warn("[HintChorus] 注册控制台命令 Hint 失败(每 5 秒重试): " + ex.Message);
            }
            UiLog.Report("CommandRegister", ex);
        }
    }

    /// <summary>从服务器控制台注销命令。</summary>
    internal static void Unregister()
    {
        if (!_registered)
        {
            return;
        }
        try
        {
            GameCore.Console.ConsoleCommandHandler.UnregisterCommand(Instance);
        }
        catch (Exception ex)
        {
            UiLog.Report("CommandUnregister", ex);
        }
        finally
        {
            _registered = false;
        }
    }
}
