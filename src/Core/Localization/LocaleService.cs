using System;

namespace HintChorus.Core.Localization;

/// <summary>
/// 插件运行时语言入口: 配置 > 公网 IP/网络时区 > 本地时区/区域 > 英文回退。
/// 只用于控制台/向导/命令等运行时文本, 与文档语言相互独立。
///
/// <para>实际文案来自 <see cref="LanguagePack"/>: 内嵌五个语言包,
/// 外部 <c>configs/&lt;端口&gt;/HintChorus/lang/&lt;code&gt;.lang</c> 逐 key 覆盖。</para>
/// </summary>
internal static class LocaleService
{
    private static HintChorus.HintChorusPlugin.PluginConfig? _config;

    private static string? _language;

    internal static void Initialize(HintChorus.HintChorusPlugin.PluginConfig config)
    {
        _config = config;
        _language = null;
        LanguagePack.Reload();
        _language = LanguagePack.ResolveLanguage(config.Language);
    }

    internal static string Language
    {
        get
        {
            string? cached = _language;
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }
            string resolved = LanguagePack.ResolveLanguage(_config?.Language);
            _language = resolved;
            return resolved;
        }
    }

    /// <summary>语言被外部包/配置改变后调用, 下一次取文案时重新解析。</summary>
    internal static void Invalidate()
    {
        _language = null;
    }
}
