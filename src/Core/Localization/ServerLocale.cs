using System;
using System.Globalization;

namespace HintChorus.Core.Localization;

/// <summary>
/// 服务器本地语言 / 时区 / 地区探测。
///
/// <para>顺序: 配置里显式指定的语言优先; 否则按系统 UI 语言映射;
/// 地区探测失败时回退英文。支持 zh / en / fr / ru / es。</para>
/// </summary>
internal sealed class ServerLocale
{
    internal string Language { get; private set; } = "en";

    internal string CultureName { get; private set; } = string.Empty;

    internal string Region { get; private set; } = string.Empty;

    internal bool RegionDetected { get; private set; }

    internal string TimeZoneId { get; private set; } = string.Empty;

    internal string TimeZoneDisplay { get; private set; } = string.Empty;

    internal string TimeZoneSource { get; private set; } = "local";

    internal bool ClockTrusted { get; private set; } = true;

    internal long ClockSkewSeconds { get; private set; }

    internal bool NetworkAvailable { get; private set; }

    internal string NetworkCountry { get; private set; } = string.Empty;

    internal static ServerLocale Detect(string? configuredLanguage)
    {
        ServerLocale locale = new ServerLocale();
        NetworkLocaleProbe.WaitReady(800);
        NetworkLocaleResult? network = NetworkLocaleProbe.Enabled ? NetworkLocaleProbe.Current : null;
        bool online = network != null && network.Available;
        locale.NetworkAvailable = online;
        locale.NetworkCountry = network?.CountryCode ?? string.Empty;
        locale.ClockTrusted = network?.ClockTrusted ?? true;
        locale.ClockSkewSeconds = network?.ClockSkewSeconds ?? 0;

        CultureInfo culture = CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
        locale.CultureName = culture?.Name ?? string.Empty;

        try
        {
            RegionInfo region = RegionInfo.CurrentRegion;
            string code = region?.TwoLetterISORegionName ?? string.Empty;
            locale.Region = code;
            locale.RegionDetected = code.Length > 0;
        }
        catch (Exception)
        {
            locale.RegionDetected = false;
        }

        if (online && network != null && network.TimeZoneId.Length > 0)
        {
            locale.TimeZoneId = network.TimeZoneId;
            locale.TimeZoneDisplay = network.TimeZoneId;
            locale.TimeZoneSource = "network";
        }
        else
        {
            try
            {
                TimeZoneInfo zone = TimeZoneInfo.Local;
                locale.TimeZoneId = zone.Id;
                locale.TimeZoneDisplay = zone.DisplayName;
            }
            catch (Exception)
            {
            }
            locale.TimeZoneSource = "local";
        }

        string explicitLanguage = Normalize(configuredLanguage);
        if (explicitLanguage.Length > 0)
        {
            locale.Language = explicitLanguage;
        }
        else if (online && network != null && network.Language.Length > 0)
        {
            locale.Language = network.Language;
        }
        else if (!locale.RegionDetected)
        {
            // 地区探测不到: 先尝试用本地时区映射, 仍失败则回退英文
            string zoneLanguage = NetworkLocaleProbe.MapTimeZoneLanguage(locale.TimeZoneId);
            locale.Language = zoneLanguage.Length > 0 ? zoneLanguage : "en";
        }
        else
        {
            locale.Language = MapLanguage(culture);
        }
        return locale;
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        string text = value.Trim().ToLowerInvariant();
        if (text == "auto")
        {
            return string.Empty;
        }
        if (text.StartsWith("zh", StringComparison.Ordinal))
        {
            return "zh";
        }
        if (text.StartsWith("fr", StringComparison.Ordinal))
        {
            return "fr";
        }
        if (text.StartsWith("ru", StringComparison.Ordinal))
        {
            return "ru";
        }
        if (text.StartsWith("es", StringComparison.Ordinal))
        {
            return "es";
        }
        if (text.StartsWith("en", StringComparison.Ordinal))
        {
            return "en";
        }
        // 不认识的显式语言: 回退英文
        return "en";
    }

    private static string MapLanguage(CultureInfo? culture)
    {
        string twoLetter = culture?.TwoLetterISOLanguageName ?? "en";
        switch (twoLetter.ToLowerInvariant())
        {
            case "zh":
                return "zh";
            case "fr":
                return "fr";
            case "ru":
                return "ru";
            case "es":
                return "es";
            default:
                return "en";
        }
    }
}
