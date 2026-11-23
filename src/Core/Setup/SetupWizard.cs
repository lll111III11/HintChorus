using System;
using System.Globalization;
using System.IO;
using HintChorus.Core.Localization;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Setup;

/// <summary>
/// 首次运行配置向导。
///
/// <para>触发时机: 本次版本的首次启动, 等所有启动文本输出完之后进入。
/// 只在服务器终端控制台交互, 通过 <c>Hc setup ...</c> 逐项设置。</para>
///
/// <para>状态保存在 <c>configs/&lt;端口&gt;/HintChorus/setup.state</c>,
/// 每个版本只会引导一次。所有文案来自语言包 <c>setup.*</c>。</para>
/// </summary>
internal static class SetupWizard
{
    private static HintChorus.HintChorusPlugin.PluginConfig? _config;

    private static Action? _save;

    private static ServerLocale? _locale;

    private static bool _active;

    internal static bool IsActive => _active;

    internal static ServerLocale? Locale => _locale;

    internal static string Language => _locale?.Language ?? "en";

    internal static void Initialize(HintChorus.HintChorusPlugin.PluginConfig config, Action save)
    {
        _config = config;
        _save = save;
    }

    internal static bool ShouldRun()
    {
        try
        {
            string path = StatePath();
            if (!File.Exists(path))
            {
                return true;
            }
            string current = CurrentVersion();
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("CompletedVersion=", StringComparison.OrdinalIgnoreCase))
                {
                    string completed = line.Substring("CompletedVersion=".Length).Trim();
                    return !string.Equals(completed, current, StringComparison.OrdinalIgnoreCase);
                }
            }
            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    internal static void Start()
    {
        if (_config == null)
        {
            return;
        }
        _locale = ServerLocale.Detect(_config.Language);
        _active = true;
        PrintQuestions();
    }

    internal static bool Handle(ArraySegment<string> args, out string response)
    {
        if (_config == null)
        {
            response = Lang.Get("setup.notInitialized");
            return false;
        }
        if (!_active)
        {
            response = Lang.For("setup.notActive", Language);
            return false;
        }

        if (args.Count == 0)
        {
            PrintQuestions();
            response = string.Empty;
            return true;
        }

        string first = Arg(args, 0).ToLowerInvariant();
        try
        {
            switch (first)
            {
                case "1":
                case "2":
                case "4":
                {
                    bool? value = ParseToggle(Arg(args, 1));
                    if (value == null)
                    {
                        response = Lang.For("setup.usage", Language);
                        return false;
                    }
                    if (first == "1")
                    {
                        _config.AutoUpdate = value.Value;
                        Logger.Info("[HintChorus] " + Lang.For("setup.desc1", Language));
                    }
                    else if (first == "2")
                    {
                        _config.EnableDetailedUiLog = value.Value;
                        Logger.Info("[HintChorus] " + Lang.For("setup.desc2", Language));
                    }
                    else
                    {
                        _config.RnrAfterUpdate = value.Value;
                        Logger.Info("[HintChorus] " + Lang.For("setup.desc4", Language));
                    }
                    PrintQuestions();
                    response = string.Empty;
                    return true;
                }
                case "3":
                {
                    bool? write = ParseToggle(Arg(args, 1));
                    if (write == null)
                    {
                        response = Lang.For("setup.usage", Language);
                        return false;
                    }
                    if (write.Value)
                    {
                        ApplyLocale();
                        Save();
                        Logger.Info("[HintChorus] " + Lang.For("setup.desc3", Language));
                        response = Lang.FormatFor("setup.saved", Language, ConfigPath());
                    }
                    else
                    {
                        response = Lang.For("setup.skipped", Language);
                    }
                    MarkCompleted();
                    Logger.Info("[HintChorus] " + response);
                    return true;
                }
                case "done":
                {
                    ApplyLocale();
                    Save();
                    response = Lang.FormatFor("setup.saved", Language, ConfigPath());
                    MarkCompleted();
                    Logger.Info("[HintChorus] " + response);
                    return true;
                }
                case "skip":
                    response = Lang.For("setup.skipped", Language);
                    MarkCompleted();
                    Logger.Info("[HintChorus] " + response);
                    return true;
                default:
                    response = Lang.For("setup.usage", Language);
                    return false;
            }
        }
        catch (Exception ex)
        {
            response = Lang.FormatFor("setup.failed", Language, ex.Message);
            return false;
        }
    }

    private static void PrintQuestions()
    {
        if (_config == null)
        {
            return;
        }
        ServerLocale locale = _locale ?? ServerLocale.Detect(_config.Language);
        _locale = locale;
        string language = locale.Language;
        Logger.Info("==================================================");
        Logger.Info("[HintChorus] " + Lang.For("setup.banner", language));
        Logger.Info(Lang.FormatFor(
            "setup.detected",
            language,
            string.IsNullOrEmpty(locale.CultureName) ? language : locale.CultureName,
            string.IsNullOrEmpty(locale.TimeZoneId) ? "unknown" : locale.TimeZoneId,
            locale.RegionDetected ? locale.Region : "en (fallback)"));
        string source = locale.TimeZoneSource == "network"
            ? Lang.For("setup.srcNetwork", language)
            : Lang.For("setup.srcLocal", language);
        string clock = locale.ClockTrusted
            ? Lang.For("setup.clockOk", language)
            : Lang.FormatFor("setup.clockSkew", language, locale.ClockSkewSeconds);
        Logger.Info(Lang.FormatFor("setup.detectedExtra", language, source, clock));
        Logger.Info(Lang.FormatFor("setup.q1", language, OnOff(_config.AutoUpdate, language)));
        Logger.Info("    " + Lang.For("setup.desc1", language));
        Logger.Info(Lang.FormatFor("setup.q2", language, OnOff(_config.EnableDetailedUiLog, language)));
        Logger.Info("    " + Lang.For("setup.desc2", language));
        Logger.Info(Lang.FormatFor("setup.q3", language, Lang.For("setup.no", language)));
        Logger.Info("    " + Lang.For("setup.desc3", language));
        Logger.Info(Lang.FormatFor("setup.q4", language, OnOff(_config.RnrAfterUpdate, language)));
        Logger.Info("    " + Lang.For("setup.desc4", language));
        Logger.Info(Lang.For("setup.commands", language));
        Logger.Info("==================================================");
    }

    private static void ApplyLocale()
    {
        if (_config == null)
        {
            return;
        }
        if (_locale == null)
        {
            _locale = ServerLocale.Detect(_config.Language);
        }
        _config.Language = _locale.Language;
        _config.Timezone = string.IsNullOrEmpty(_locale.TimeZoneId) ? "auto" : _locale.TimeZoneId;
        _config.Region = _locale.RegionDetected && _locale.Region.Length > 0 ? _locale.Region : "en";
    }

    private static void Save()
    {
        try
        {
            _save?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + Lang.FormatFor("setup.writeConfigFailed", Language, ex.Message));
        }
    }

    private static void MarkCompleted()
    {
        _active = false;
        try
        {
            string path = StatePath();
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(
                path,
                "CompletedVersion=" + CurrentVersion() + Environment.NewLine
                + "CompletedUtc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + Lang.FormatFor("setup.writeStateFailed", Language, ex.Message));
        }
        Logger.Info("[HintChorus] " + Lang.For("setup.done", Language));
        Logger.Info("[HintChorus] " + Lang.For("setup.restart", Language));
    }

    private static bool? ParseToggle(string value)
    {
        string text = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (text == "on" || text == "yes" || text == "1" || text == "true" || text == "开" || text == "是")
        {
            return true;
        }
        if (text == "off" || text == "no" || text == "0" || text == "false" || text == "关" || text == "否")
        {
            return false;
        }
        return null;
    }

    private static string OnOff(bool value, string language)
    {
        return Lang.For(value ? "setup.on" : "setup.off", language);
    }

    private static string Arg(ArraySegment<string> args, int index)
    {
        if (args.Array == null || index < 0 || index >= args.Count)
        {
            return string.Empty;
        }
        return args.Array[args.Offset + index] ?? string.Empty;
    }

    private static string CurrentVersion()
    {
        return typeof(SetupWizard).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    }

    private static string StatePath()
    {
        return Path.Combine(
            PathManager.Configs.FullName,
            Server.Port.ToString(CultureInfo.InvariantCulture),
            "HintChorus",
            "setup.state");
    }

    private static string ConfigPath()
    {
        return Path.Combine(
            PathManager.Configs.FullName,
            Server.Port.ToString(CultureInfo.InvariantCulture),
            "HintChorus",
            "config.yml");
    }
}
