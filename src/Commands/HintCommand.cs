using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandSystem;
using HintChorus.Core.Broker;
using HintChorus.Core.Compat;
using HintChorus.Core.Diagnostics;
using HintChorus.Core.Interception;
using HintChorus.Core.Localization;
using HintChorus.Core.Setup;
using HintChorus.Core.Updates;
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
///
/// <para>Beta 1.1.1 起: 无主流插件时不再只回一句"没有插件", 而是明确提示, 并提供编号 1 =
/// 整体退回原生 + 恢复覆盖前数据; 有主流插件时按编号切换, 并把该插件最后一条数据热应用回去。</para>
/// </summary>
internal sealed class HintCommand : ICommand, IUsageProvider
{
    internal static readonly HintCommand Instance = new HintCommand();

    private static bool _registered;

    private static DateTime _nextAttemptUtc = DateTime.MinValue;

    private static int _failedAttempts;

    public string Command => "hint";

    public string[] Aliases => new[] { "hc" };

    public string Description => "HintChorus 服务器控制台: Hint orvs 检测主流插件并切回原生提示";

    public string[] Usage => new[]
    {
        SetupText.Get("helpOrvs", LocaleService.Language),
        SetupText.Get("helpPick", LocaleService.Language),
        SetupText.Get("helpMerge", LocaleService.Language),
        SetupText.Get("helpNative", LocaleService.Language),
        SetupText.Get("helpAll", LocaleService.Language),
        SetupText.Get("helpReset", LocaleService.Language),
        SetupText.Get("helpYield", LocaleService.Language),
        SetupText.Get("helpTake", LocaleService.Language),
        SetupText.Get("helpSetup", LocaleService.Language),
        SetupText.Get("helpUpdate", LocaleService.Language)
    };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        try
        {
            if (!IsServerConsole(sender))
            {
                response = "[HintChorus] 该命令只允许在服务器终端控制台执行。";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = BuildHelp();
                return true;
            }

            string sub = Arg(arguments, 0);

            if (sub.Equals("setup", StringComparison.OrdinalIgnoreCase))
            {
                return SetupWizard.Handle(Slice(arguments, 1), out response);
            }

            if (sub.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                return UpdateService.HandleCommand(out response);
            }

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

            if (target.Equals("yield", StringComparison.OrdinalIgnoreCase))
            {
                UiInterception.Instance.AllowExternalTakeover = true;
                response = "[HintChorus] 已让出提示通道: 外部 Hint 插件现在可以接管。\n输入 Hint orvs take 收回。";
                UiLog.Detail("COMMAND", "yield by " + SafeSender(sender));
                return true;
            }

            if (target.Equals("take", StringComparison.OrdinalIgnoreCase))
            {
                UiInterception.Instance.AllowExternalTakeover = false;
                UiInterception.Instance.Repair();
                HintBroker.Instance.MarkDirty();
                response = "[HintChorus] 已收回提示通道: 本底层重新接管, 并重发 UI。";
                UiLog.Detail("COMMAND", "take by " + SafeSender(sender));
                return true;
            }

            if (target.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                int replayed = HintTakeover.EnterNativeMode(true);
                response = "[HintChorus] 已整体退回原生提示, 恢复 " + replayed
                    + " 个玩家的覆盖前数据。\n输入 Hint orvs reset 可恢复合并渲染。";
                return true;
            }

            if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<string> candidates = HintFallback.Candidates();
                int changed = HintFallback.SetAllNative(true);
                int replayed = HintTakeover.EnterNativeMode(true);
                response = "[HintChorus] 已把 " + changed + " 个插件切回原生直通(共发现 " + candidates.Count
                    + " 个), 并恢复 " + replayed + " 个玩家的覆盖前数据。\n输入 Hint orvs reset 可全部恢复合并。";
                return true;
            }

            if (target.Equals("reset", StringComparison.OrdinalIgnoreCase)
                || target.Equals("merge", StringComparison.OrdinalIgnoreCase))
            {
                HintTakeover.LeaveNativeMode();
                int restored = HintFallback.ResetAll();
                HintBroker.Instance.MarkDirty();
                response = "[HintChorus] 已退出原生模式, 恢复 " + restored + " 个插件为合并渲染。";
                return true;
            }

            if (!int.TryParse(target, out int index))
            {
                response = "[HintChorus] 编号必须是数字。\n" + BuildHelp();
                return false;
            }

            BuildEntries(out List<MainstreamPlugin> mainstream, out List<string> others);
            bool noMainstream = mainstream.Count == 0;
            int offset = noMainstream ? 1 : 0;

            if (noMainstream && index == 1)
            {
                int replayed = HintTakeover.EnterNativeMode(true);
                response = "[HintChorus] 未检测到主流插件, 已按编号 1 整体退回原生提示, 恢复 " + replayed
                    + " 个玩家的覆盖前数据。\n输入 Hint orvs reset 可恢复合并渲染。";
                UiLog.Detail("COMMAND", "native mode by " + SafeSender(sender));
                return true;
            }

            int position = index - offset;
            int total = mainstream.Count + others.Count;
            if (position < 1 || position > total)
            {
                response = "[HintChorus] 编号超出范围: " + index + " (有效范围 " + (1 + offset) + "-" + (total + offset) + ")。";
                return false;
            }

            string pluginId;
            string display;
            string[] targets;
            MainstreamPlugin? mainstreamEntry = null;
            if (position <= mainstream.Count)
            {
                mainstreamEntry = mainstream[position - 1];
                display = mainstreamEntry.DisplayName;
                targets = mainstreamEntry.RawIds.Length > 0 ? mainstreamEntry.RawIds : new[] { mainstreamEntry.Id };
                pluginId = mainstreamEntry.Id;
            }
            else
            {
                pluginId = others[position - mainstream.Count - 1];
                targets = new[] { pluginId };
                display = pluginId;
            }

            bool merge = arguments.Count >= 3
                && Arg(arguments, 2).Equals("merge", StringComparison.OrdinalIgnoreCase);

            if (merge)
            {
                foreach (string id in targets)
                {
                    HintFallback.SetNative(id, false);
                }
                HintTakeover.LeaveNativeMode();
                HintBroker.Instance.MarkDirty();
                response = "[HintChorus] 已把 " + display + " 恢复为合并渲染。";
            }
            else
            {
                // 如果当前是整体原生模式, 先回到合并, 再单独把目标插件切原生。
                HintTakeover.LeaveNativeMode();
                foreach (string id in targets)
                {
                    HintFallback.SetNative(id, true);
                }
                int replayed = 0;
                foreach (string id in targets)
                {
                    replayed += HintTakeover.ReplayPlugin(id);
                }
                response = "[HintChorus] 已把 " + display + " 切回原生直通, 并热应用 "
                    + replayed + " 个玩家的最后一条数据。\n其它插件仍由合并层托管; 恢复: Hint orvs "
                    + index + " merge";
                if (mainstreamEntry != null && mainstreamEntry.Native)
                {
                    response += "\n(该插件此前已是原生直通)";
                }
            }
            UiLog.Detail("COMMAND", (merge ? "merge " : "native ") + string.Join(",", targets) + " by " + SafeSender(sender));
            return true;
        }
        catch (Exception ex)
        {
            UiLog.Report("HintCommand", ex);
            response = "[HintChorus] 命令执行异常: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 无编号时:
    /// <para>0 个主流插件 → 明确告诉服主"未检测到主流插件", 并提供编号 1 = 退回原生;</para>
    /// <para>有主流插件 → 列出编号, 等待输入。</para>
    /// </summary>
    private static bool ListOrPick(out string response)
    {
        BuildEntries(out List<MainstreamPlugin> mainstream, out List<string> others);
        StringBuilder builder = new StringBuilder();

        if (mainstream.Count == 0)
        {
            builder.AppendLine("[HintChorus] 未检测到主流 Hint 插件。");
            builder.AppendLine("  1) 直接退回原生（恢复覆盖前的数据）");
            if (others.Count > 0)
            {
                builder.AppendLine("其他已发现插件:");
                for (int i = 0; i < others.Count; i++)
                {
                    builder.Append("  ").Append(i + 2).Append(") ").Append(others[i])
                        .Append(HintFallback.IsNative(others[i]) ? "  [原生直通]" : "  [合并中]")
                        .AppendLine();
                }
            }
            builder.AppendLine("输入 Hint orvs 1 退回原生; 输入 Hint orvs <编号> 单独切换某个插件。");
            builder.Append("恢复合并: Hint orvs reset");
            response = builder.ToString();
            return true;
        }

        builder.AppendLine("[HintChorus] 已检测到 " + mainstream.Count + " 个主流 Hint 插件"
            + (HintTakeover.NativeMode ? "（当前处于整体原生模式）" : string.Empty) + ":");
        for (int i = 0; i < mainstream.Count; i++)
        {
            MainstreamPlugin plugin = mainstream[i];
            builder.Append("  ").Append(i + 1).Append(") ").Append(plugin.DisplayName)
                .Append(plugin.Native ? "  [原生直通]" : "  [合并中]")
                .Append("  (").Append(plugin.Source).Append(')')
                .AppendLine();
        }
        if (others.Count > 0)
        {
            builder.AppendLine("其他已发现插件:");
            for (int i = 0; i < others.Count; i++)
            {
                int number = mainstream.Count + i + 1;
                builder.Append("  ").Append(number).Append(") ").Append(others[i])
                    .Append(HintFallback.IsNative(others[i]) ? "  [原生直通]" : "  [合并中]")
                    .AppendLine();
            }
        }
        builder.AppendLine("输入 Hint orvs <编号> 切换为原生直通并热应用其最后数据; 加 merge 恢复合并。");
        builder.Append("整体退回原生: Hint orvs native | 全部恢复: Hint orvs reset");
        response = builder.ToString();
        return true;
    }

    private static void BuildEntries(out List<MainstreamPlugin> mainstream, out List<string> others)
    {
        mainstream = MainstreamHintPlugins.Detect().ToList();
        HashSet<string> mainstreamIds = new HashSet<string>(
            mainstream.Select((MainstreamPlugin p) => p.Id),
            StringComparer.OrdinalIgnoreCase);

        others = HintFallback.Candidates()
            .Where((string id) => !mainstreamIds.Contains(id) && !MainstreamHintPlugins.IsMainstream(id))
            .ToList();
    }

    private static string BuildHelp()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(SetupText.Get("helpHeader", LocaleService.Language));
        foreach (string line in Instance.Usage)
        {
            builder.Append("  ").AppendLine(line);
        }
        builder.Append(SetupText.Get("helpFooter", LocaleService.Language));
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

    private static ArraySegment<string> Slice(ArraySegment<string> args, int start)
    {
        if (args.Array == null || start >= args.Count)
        {
            return new ArraySegment<string>(Array.Empty<string>());
        }
        return new ArraySegment<string>(args.Array, args.Offset + start, args.Count - start);
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

    /// <summary>
    /// 只允许服务器终端控制台。RA 面板的 <c>RemoteAdmin.PlayerCommandSender</c>、
    /// 查询接口的 <c>Query.QueryCommandSender</c> 以及玩家客户端控制台一律拒绝。
    /// </summary>
    private static bool IsServerConsole(ICommandSender? sender)
    {
        if (sender == null)
        {
            return false;
        }
        try
        {
            if (string.Equals(sender.GetType().FullName, "GameCore.ConsoleCommandSender", StringComparison.Ordinal))
            {
                return true;
            }
            CommandSender? commandSender = sender as CommandSender;
            return commandSender != null
                && string.Equals(commandSender.SenderId, "GAME CONSOLE", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
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
