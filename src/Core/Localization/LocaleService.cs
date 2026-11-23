namespace HintChorus.Core.Localization;

/// <summary>
/// 插件运行时语言入口: 配置 > 公网 IP/网络时区 > 本地时区/区域 > 英文回退。
/// 只用于控制台/向导/命令等运行时文本, 与文档语言相互独立。
/// </summary>
internal static class LocaleService
{
    private static HintChorus.HintChorusPlugin.PluginConfig? _config;

    internal static void Initialize(HintChorus.HintChorusPlugin.PluginConfig config)
    {
        _config = config;
    }

    internal static string Language => ServerLocale.Detect(_config?.Language).Language;
}
