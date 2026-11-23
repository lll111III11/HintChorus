using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HintChorus.Core.Localization;

/// <summary>公网 IP / 网络时区探测结果。</summary>
internal sealed class NetworkLocaleResult
{
    internal bool Available;

    internal string CountryCode = string.Empty;

    internal string TimeZoneId = string.Empty;

    internal string Language = string.Empty;

    internal bool ClockTrusted = true;

    internal long ClockSkewSeconds;

    internal string Error = string.Empty;
}

/// <summary>
/// 有网络时的语言/时区判定:
/// 公网 IP 国家 + 网络时区; 同时用 HTTP Date 校验本地时钟。
/// 无网络或探测失败时, 由 <see cref="ServerLocale"/> 回退本地时区/区域。
/// </summary>
internal static class NetworkLocaleProbe
{
    private static readonly object Gate = new object();

    private static readonly string[] Endpoints =
    {
        "https://ipapi.co/json/",
        "https://ipinfo.io/json"
    };

    private static Task? _task;

    internal static bool Enabled { get; set; } = true;

    internal static NetworkLocaleResult? Current { get; private set; }

    internal static void Start()
    {
        if (!Enabled || _task != null)
        {
            return;
        }
        _task = Task.Run(delegate
        {
            Probe();
        });
    }

    internal static void WaitReady(int milliseconds)
    {
        Task? task = _task;
        if (task == null)
        {
            return;
        }
        try
        {
            task.Wait(milliseconds);
        }
        catch (Exception)
        {
        }
    }

    private static void Probe()
    {
        NetworkLocaleResult? result = null;
        foreach (string endpoint in Endpoints)
        {
            try
            {
                result = TryEndpoint(endpoint);
                if (result != null && result.Available)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                result = new NetworkLocaleResult
                {
                    Available = false,
                    Error = ex.GetType().Name + ": " + ex.Message
                };
            }
        }
        lock (Gate)
        {
            Current = result ?? new NetworkLocaleResult { Available = false, Error = "unreachable" };
        }
    }

    private static NetworkLocaleResult? TryEndpoint(string url)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.UserAgent = "HintChorus-Locale";
        request.Timeout = 2500;
        request.ReadWriteTimeout = 2500;
        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        {
            string json = reader.ReadToEnd();
            string country = Match(json, "\"country_code\"\\s*:\\s*\"([^\"]+)\"");
            if (country.Length == 0)
            {
                country = Match(json, "\"country\"\\s*:\\s*\"([^\"]+)\"");
            }
            string timeZone = Match(json, "\"timezone\"\\s*:\\s*\"([^\"]+)\"");
            if (timeZone.Length == 0)
            {
                timeZone = Match(json, "\"tz\"\\s*:\\s*\"([^\"]+)\"");
            }

            string language = MapCountry(country);
            if (language.Length == 0)
            {
                language = MapTimeZone(timeZone);
            }

            long skew = 0;
            bool hasNetworkTime = false;
            string? dateHeader = response.Headers["Date"];
            DateTime networkUtc;
            if (!string.IsNullOrEmpty(dateHeader)
                && DateTime.TryParse(
                    dateHeader,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out networkUtc))
            {
                hasNetworkTime = true;
                skew = (long)(networkUtc - DateTime.UtcNow).TotalSeconds;
            }

            return new NetworkLocaleResult
            {
                Available = country.Length > 0 || timeZone.Length > 0,
                CountryCode = country.ToUpperInvariant(),
                TimeZoneId = timeZone,
                Language = language,
                ClockTrusted = !hasNetworkTime || Math.Abs(skew) <= 300,
                ClockSkewSeconds = skew
            };
        }
    }

    private static string Match(string text, string pattern)
    {
        Match match = Regex.Match(text, pattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string MapCountry(string country)
    {
        switch ((country ?? string.Empty).ToUpperInvariant())
        {
            case "CN":
            case "TW":
            case "HK":
            case "MO":
                return "zh";
            case "FR":
            case "BE":
            case "CA":
            case "CH":
            case "LU":
            case "MC":
                return "fr";
            case "RU":
            case "BY":
            case "KZ":
            case "KG":
                return "ru";
            case "ES":
            case "MX":
            case "AR":
            case "CO":
            case "CL":
            case "PE":
            case "VE":
            case "UY":
            case "EC":
            case "BO":
            case "PY":
            case "CR":
            case "DO":
            case "GT":
            case "HN":
            case "NI":
            case "PA":
            case "PR":
            case "SV":
            case "CU":
                return "es";
            default:
                return string.Empty;
        }
    }

    internal static string MapTimeZoneLanguage(string timeZone)
    {
        return MapTimeZone(timeZone);
    }

    private static string MapTimeZone(string timeZone)
    {
        string value = timeZone ?? string.Empty;
        if (value.Length == 0)
        {
            return string.Empty;
        }
        if (value.StartsWith("Asia/Shanghai", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Asia/Taipei", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Asia/Hong_Kong", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Asia/Macau", StringComparison.OrdinalIgnoreCase)
            || value.Equals("China Standard Time", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Taipei Standard Time", StringComparison.OrdinalIgnoreCase))
        {
            return "zh";
        }
        if (value.StartsWith("Europe/Paris", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Romance Standard Time", StringComparison.OrdinalIgnoreCase))
        {
            return "fr";
        }
        if (value.StartsWith("Europe/Moscow", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Asia/Yekaterinburg", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Russian Standard Time", StringComparison.OrdinalIgnoreCase))
        {
            return "ru";
        }
        if (value.StartsWith("Europe/Madrid", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("America/Mexico_City", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("America/Argentina", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("America/Bogota", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("America/Lima", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("America/Santiago", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Central Standard Time (Mexico)", StringComparison.OrdinalIgnoreCase))
        {
            return "es";
        }
        return string.Empty;
    }
}
