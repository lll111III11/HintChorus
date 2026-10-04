using LabApi.Features.Console;
using System;

namespace HintIsolation.Bootstrap;

/// <summary>
/// 引导提示横幅 —— 需求要求的"弹出提示"。
///
/// <para>专用服务器没有 GUI, 所谓"弹出"只能落在服务器控制台。这里用
/// <b>大号 ASCII 标题 + 额外 API 横幅 + 高对比框体</b>渲染,
/// 确保管理员一眼看到, 不被刷屏淹没。</para>
///
/// <para>注意: 中文是双宽字符, 不能靠空格补齐右边框(会参差不齐), 因此中文正文行
/// 只保留左边框 + 缩进; 纯 ASCII 的横幅则用等宽字符补齐右边框, 保证整齐。</para>
/// </summary>
internal static class BootstrapBanner
{
    /// <summary>提示正文。</summary>
    public const string Message = "请重启服务器, 以便让底层启动";

    /// <summary>额外横幅的文案。</summary>
    public const string ApiBadge = "HintIso API !";

    private const ConsoleColor Title = ConsoleColor.Cyan;
    private const ConsoleColor BadgeColor = ConsoleColor.Green;
    private const ConsoleColor Border = ConsoleColor.Yellow;
    private const ConsoleColor Body = ConsoleColor.White;

    /// <summary>
    /// 大号标题字: <c>HINTISO</c>(连写, 与品牌名一致)。
    /// 每个字母按该字体最大宽度补齐, 保证跨行对齐。
    /// </summary>
    private static readonly string[] TitleArt =
    {
        "  ██╗  ██╗██╗███╗   ██╗████████╗██╗███████╗ ██████╗ ",
        "  ██║  ██║██║████╗  ██║╚══██╔══╝██║██╔════╝██╔═══██╗",
        "  ███████║██║██╔██╗ ██║   ██║   ██║███████╗██║   ██║",
        "  ██╔══██║██║██║╚██╗██║   ██║   ██║╚════██║██║   ██║",
        "  ██║  ██║██║██║ ╚████║   ██║   ██║███████║╚██████╔╝",
    };

    private const string TopBorder = "  ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓";
    private const string BottomBorder = "  ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛";
    private const string LeftBorder = "  ┃ ";

    /// <summary>打印首次引导提示(重启提醒 + 参数改写说明)。</summary>
    public static void PrintFirstRun()
    {
        Logger.Raw(string.Empty, Title);

        foreach (string line in TitleArt)
        {
            Logger.Raw(line, Title);
        }

        Logger.Raw(string.Empty, Title);

        // ── 额外横幅 ──
        DrawAsciiBox(ApiBadge, BadgeColor);

        Logger.Raw(string.Empty, Border);

        // ── 重启提示框(中文正文, 只保留左边框以免右边参差) ──
        Logger.Raw(TopBorder, Border);
        Logger.Raw(LeftBorder, Border);
        Logger.Raw(LeftBorder + Message, Body);
        Logger.Raw(LeftBorder, Border);
        Logger.Raw(LeftBorder + "引导器已就位: 本次已把参数 Prompted 由 0 改写为 1", Body);
        Logger.Raw(LeftBorder + "(此后不再提醒; 下次重启即由引导器抢在其它插件之前加载)", Body);
        Logger.Raw(LeftBorder, Border);
        Logger.Raw(BottomBorder, Border);
        Logger.Raw(string.Empty, Border);
    }

    /// <summary>打印后续运行时的简短状态行(不再刷大横幅)。</summary>
    public static void PrintActive(string detail, int runs)
    {
        Logger.Raw($"[抢先加载] 引导器已激活 (第 {runs} 次运行) —— {detail}", Title);
    }

    /// <summary>
    /// 画一个纯 ASCII 的等宽框体横幅(内容为 ASCII 时才能精确补齐右边框)。
    /// </summary>
    private static void DrawAsciiBox(string text, ConsoleColor color)
    {
        int inner = text.Length + 4;

        Logger.Raw("  ┏" + new string('━', inner) + "┓", color);
        Logger.Raw("  ┃  " + text + "  ┃", color);
        Logger.Raw("  ┗" + new string('━', inner) + "┛", color);
    }
}
