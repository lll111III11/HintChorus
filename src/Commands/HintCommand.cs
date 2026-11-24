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
using HintChorus.Core.Transport;
using HintChorus.Core.Updates;

namespace HintChorus.Commands;

/// <summary>
/// <b>服务器控制台命令 <c>Hint</c></b> —— 插件级原生回退 / 配置 / 语言包。
///
/// <para>用途: 某个 Hint 插件与本底层不兼容时, 不必卸载插件、也不必改配置文件,
/// 直接在<b>服务器后台控制台</b>输入 <c>Hint orvs</c> 即可把指定插件切回游戏原版提示。</para>
///
/// <para>命令大小写不敏感(<c>Hint</c> / <c>hint</c> / <c>HINT</c>, <c>orvs</c> / <c>ORVS</c> 均可)。</para>
///
/// <para>支持多个 Hint 插件共存: 只有被选中的插件走原生直通, 其余仍由合并层托管, 互不冲突。
/// 切换只影响本次运行, 不写配置; 重启即全部恢复默认合并。</para>
///
/// <para>15.0.0 起新增 <c>Hint lang</c>: 查看内嵌/外部语言包、热重载外置语言文件。</para>
/// </summary>
[CommandHandler(typeof(GameConsoleCommandHandler))]
internal sealed class HintCommand : ICommand, IUsageProvider
{
    /// <summary>LabAPI 通过 Activator 创建命令实例, 这里显式给一个公开无参构造。</summary>
    public HintCommand()
    {
    }

    public string Command => "hint";

    public string[] Aliases => new[] { "hc" };

    public string Description => Lang.Get("cmd.description");

    public string[] Usage => new[]
    {
        Lang.Get("cmd.helpOrvs"),
        Lang.Get("cmd.helpPick"),
        Lang.Get("cmd.helpMerge"),
        Lang.Get("cmd.helpNative"),
        Lang.Get("cmd.helpAll"),
        Lang.Get("cmd.helpReset"),
        Lang.Get("cmd.helpYield"),
        Lang.Get("cmd.helpTake"),
        Lang.Get("cmd.helpSetup"),
        Lang.Get("cmd.helpUpdate"),
        Lang.Get("cmd.helpLang")
    };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        try
        {
            if (!CanExecute(sender, out string denial))
            {
                response = denial;
                return false;
            }

            if (arguments.Count == 0)
            {
                response = BuildHelp();
                return true;
            }

            string sub = Arg(arguments, 0);

            // 向导进行中允许直接回答当前项: Hc 是 / Hc 否 / Hc on / Hc off
            if (SetupWizard.IsActive)
            {
                bool? answer = SetupWizard.ParseAnswer(sub);
                if (answer != null)
                {
                    return SetupWizard.Handle(arguments, out response);
                }
            }

            if (sub.Equals("setup", StringComparison.OrdinalIgnoreCase))
            {
                return SetupWizard.Handle(Slice(arguments, 1), out response);
            }

            if (sub.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                return UpdateService.HandleCommand(out response);
            }

            if (sub.Equals("lang", StringComparison.OrdinalIgnoreCase))
            {
                return HandleLang(Slice(arguments, 1), out response);
            }

            if (sub.Equals("preop", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePreOp(Slice(arguments, 1), out response);
            }

            if (!sub.Equals("orvs", StringComparison.OrdinalIgnoreCase))
            {
                response = Lang.Format("cmd.unknown", sub, BuildHelp());
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
                response = Lang.Get("cmd.yield");
                UiLog.Detail("COMMAND", "yield by " + SafeSender(sender));
                return true;
            }

            if (target.Equals("take", StringComparison.OrdinalIgnoreCase))
            {
                UiInterception.Instance.AllowExternalTakeover = false;
                UiInterception.Instance.Repair();
                HintBroker.Instance.MarkDirty();
                response = Lang.Get("cmd.take");
                UiLog.Detail("COMMAND", "take by " + SafeSender(sender));
                return true;
            }

            if (target.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                int replayed = HintTakeover.EnterNativeMode(true);
                response = Lang.Format("cmd.native", replayed);
                return true;
            }

            if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<string> candidates = HintFallback.Candidates();
                int changed = HintFallback.SetAllNative(true);
                int replayed = HintTakeover.EnterNativeMode(true);
                response = Lang.Format("cmd.all", changed, candidates.Count, replayed);
                return true;
            }

            if (target.Equals("reset", StringComparison.OrdinalIgnoreCase)
                || target.Equals("merge", StringComparison.OrdinalIgnoreCase))
            {
                HintTakeover.LeaveNativeMode();
                int restored = HintFallback.ResetAll();
                HintBroker.Instance.MarkDirty();
                response = Lang.Format("cmd.reset", restored);
                return true;
            }

            if (!int.TryParse(target, out int index))
            {
                response = Lang.Format("cmd.invalidNumber", BuildHelp());
                return false;
            }

            BuildEntries(out List<MainstreamPlugin> mainstream, out List<string> others);
            bool noMainstream = mainstream.Count == 0;
            int offset = noMainstream ? 1 : 0;

            if (noMainstream && index == 1)
            {
                int replayed = HintTakeover.EnterNativeMode(true);
                response = Lang.Format("cmd.nativeNone", replayed);
                UiLog.Detail("COMMAND", "native mode by " + SafeSender(sender));
                return true;
            }

            int position = index - offset;
            int total = mainstream.Count + others.Count;
            if (position < 1 || position > total)
            {
                response = Lang.Format("cmd.outOfRange", index, 1 + offset, total + offset);
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
                response = Lang.Format("cmd.mergeOne", display);
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
                response = Lang.Format("cmd.nativeOne", display, replayed, index);
                if (mainstreamEntry != null && mainstreamEntry.Native)
                {
                    response += Lang.Get("cmd.alreadyNative");
                }
            }
            UiLog.Detail("COMMAND", (merge ? "merge " : "native ") + string.Join(",", targets) + " by " + SafeSender(sender));
            return true;
        }
        catch (Exception ex)
        {
            UiLog.Report("HintCommand", ex);
            response = Lang.Format("cmd.execFailed", ex.Message);
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
            builder.AppendLine(Lang.Get("cmd.list.noMainstream"));
            builder.AppendLine(Lang.Get("cmd.list.nativeOption"));
            if (others.Count > 0)
            {
                builder.AppendLine(Lang.Get("cmd.list.others"));
                for (int i = 0; i < others.Count; i++)
                {
                    builder.Append("  ").Append(i + 2).Append(") ").Append(others[i])
                        .Append(HintFallback.IsNative(others[i]) ? Lang.Get("cmd.list.stateNative") : Lang.Get("cmd.list.stateMerged"))
                        .AppendLine();
                }
            }
            builder.AppendLine(Lang.Get("cmd.list.noMainstreamHint"));
            builder.Append(Lang.Get("cmd.list.resetHint"));
            response = builder.ToString();
            return true;
        }

        builder.AppendLine(Lang.Format(
            "cmd.list.header",
            mainstream.Count,
            HintTakeover.NativeMode ? Lang.Get("cmd.list.nativeModeSuffix") : string.Empty));
        for (int i = 0; i < mainstream.Count; i++)
        {
            MainstreamPlugin plugin = mainstream[i];
            builder.Append("  ").Append(i + 1).Append(") ").Append(plugin.DisplayName)
                .Append(plugin.Native ? Lang.Get("cmd.list.stateNative") : Lang.Get("cmd.list.stateMerged"))
                .Append("  (").Append(plugin.Source).Append(')')
                .AppendLine();
        }
        if (others.Count > 0)
        {
            builder.AppendLine(Lang.Get("cmd.list.others"));
            for (int i = 0; i < others.Count; i++)
            {
                int number = mainstream.Count + i + 1;
                builder.Append("  ").Append(number).Append(") ").Append(others[i])
                    .Append(HintFallback.IsNative(others[i]) ? Lang.Get("cmd.list.stateNative") : Lang.Get("cmd.list.stateMerged"))
                    .AppendLine();
            }
        }
        builder.AppendLine(Lang.Get("cmd.list.pickHint"));
        builder.Append(Lang.Get("cmd.list.nativeAllHint"));
        response = builder.ToString();
        return true;
    }

    /// <summary><c>Hint lang [reload|path]</c> —— 语言包查看与热重载。</summary>
    private static bool HandleLang(ArraySegment<string> arguments, out string response)
    {
        string action = arguments.Count > 0 ? Arg(arguments, 0).ToLowerInvariant() : string.Empty;
        if (action.Length == 0 || action == "list")
        {
            IReadOnlyList<string> languages = LanguagePack.Languages;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(Lang.Format("lang.header", languages.Count));
            foreach (string code in languages)
            {
                string source = LanguagePack.SourceOf(code);
                string sourceText = source == "builtin+external"
                    ? Lang.Get("lang.sourceBoth")
                    : source == "external"
                        ? Lang.Get("lang.sourceExternal")
                        : Lang.Get("lang.sourceBuiltin");
                builder.AppendLine(Lang.Format("lang.entry", code, LanguagePack.KeyCount(code), sourceText));
            }
            builder.Append(Lang.Format("lang.dir", LanguagePack.DirectoryPath));
            response = builder.ToString();
            return true;
        }

        if (action == "reload")
        {
            LanguagePack.Reload();
            LocaleService.Invalidate();
            response = Lang.Format("lang.reloaded", LanguagePack.Languages.Count, LanguagePack.Report);
            return true;
        }

        if (action == "path" || action == "dir")
        {
            response = Lang.Format("lang.dir", LanguagePack.DirectoryPath);
            return true;
        }

        response = Lang.Format("lang.unknown", action, Lang.Get("lang.usage"));
        return false;
    }

    /// <summary><c>Hint preop [on|off|status]</c> —— 预更新实操作: 摘外部补丁 + 全网络包管理 + 瞬间内存替换。</summary>
    private static bool HandlePreOp(ArraySegment<string> arguments, out string response)
    {
        string action = arguments.Count > 0 ? Arg(arguments, 0).ToLowerInvariant() : "on";

        if (action == "status" || action == "state")
        {
            response = PacketGovernor.Status();
            return true;
        }

        if (action == "off" || action == "stop" || action == "0")
        {
            response = PacketGovernor.Disengage()
                ? Lang.Get("preop.disengaged")
                : Lang.Get("preop.statusOff");
            return true;
        }

        if (action != "on" && action != "start" && action != "1")
        {
            response = Lang.Get("preop.usage");
            return false;
        }

        if (!PacketGovernor.Engage(out int groups, out int players))
        {
            response = Lang.Get("preop.failedShort");
            return false;
        }

        response = Lang.Format("preop.engaged", groups, players);
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

    private string[] UsageLines => new[]
    {
        Lang.Get("cmd.helpOrvs"),
        Lang.Get("cmd.helpPick"),
        Lang.Get("cmd.helpMerge"),
        Lang.Get("cmd.helpNative"),
        Lang.Get("cmd.helpAll"),
        Lang.Get("cmd.helpReset"),
        Lang.Get("cmd.helpYield"),
        Lang.Get("cmd.helpTake"),
        Lang.Get("cmd.helpSetup"),
        Lang.Get("cmd.helpUpdate"),
        Lang.Get("cmd.helpLang"),
        Lang.Get("cmd.helpPreOp")
    };

    private static string BuildHelp()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(Lang.Get("cmd.helpHeader"));
        foreach (string line in new HintCommand().UsageLines)
        {
            builder.Append("  ").AppendLine(line);
        }
        builder.Append(Lang.Get("cmd.helpFooter"));
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
    /// 只允许服务器后台终端控制台。RA 面板、查询接口与玩家客户端一律拒绝。
    /// </summary>
    private static bool CanExecute(ICommandSender? sender, out string denial)
    {
        denial = Lang.Get("cmd.consoleOnly");
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
                && (commandSender.FullPermissions
                    || string.Equals(commandSender.SenderId, "GAME CONSOLE", StringComparison.Ordinal));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
