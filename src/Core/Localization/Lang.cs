namespace HintChorus.Core.Localization;

/// <summary>
/// 运行时文本的短入口: 当前语言由 <see cref="LocaleService"/> 决定。
/// 所有控制台 / 命令 / 向导 / 更新 / 提示文本都从这里取, 不再硬编码。
/// </summary>
internal static class Lang
{
    internal static string Get(string key)
    {
        return LanguagePack.Get(key, LocaleService.Language);
    }

    internal static string For(string key, string? language)
    {
        return LanguagePack.Get(key, language);
    }

    internal static string Format(string key, params object?[] args)
    {
        return LanguagePack.Format(key, LocaleService.Language, args);
    }

    internal static string FormatFor(string key, string? language, params object?[] args)
    {
        return LanguagePack.Format(key, language, args);
    }
}
