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
/// 首次运行配置向导（逐项询问）。
///
/// <para>触发时机: 本次版本的首次启动, 等所有启动文本输出完之后进入。
/// 每次只显示当前一项, 回答“是/否”后再显示下一项, 不再一次列完。</para>
///
/// <para>只接受服务器后台终端控制台: <c>Hc setup</c> 查看当前项,
/// <c>Hc setup 是</c> / <c>Hc setup 否</c> 回答当前项;
/// 也可用 <c>Hc 是</c> / <c>Hc 否</c> 直接回答, 或
/// <c>Hc setup 1 on|off</c> 这类写法指定某一项。</para>
///
/// <para>状态保存在 <c>configs/&lt;端口&gt;/HintChorus/setup.state</c>,
/// 每个版本只会引导一次。所有文案来自语言包 <c>setup.*</c>。</para>
/// </summary>
internal static class SetupWizard
{
    private const int TotalSteps = 4;

    private static HintChorus.HintChorusPlugin.PluginConfig? _config;

    private static Action? _save;

    private static ServerLocale? _locale;

    private static bool _active;

    private static int _step = 1;

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
        _step = 1;
        PrintHeader();
        Logger.Info(BuildQuestion());
        Logger.Info("==================================================");
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
            // 只显示当前这一项, 等待回答。
            response = BuildQuestion();
            return true;
        }

        string first = Arg(args, 0).Trim();
        string lower = first.ToLowerInvariant();

        if (lower == "done")
        {
            return Complete(true, out response);
        }
        if (lower == "skip")
        {
            return Complete(false, out response);
        }

        try
        {
            if (int.TryParse(lower, NumberStyles.Integer, CultureInfo.InvariantCulture, out int step)
                && step >= 1
                && step <= TotalSteps)
            {
                bool? value = ParseToggle(Arg(args, 1));
                if (value == null)
                {
                    response = Lang.For("setup.usage", Language);
                    return false;
                }
                return Apply(step, value.Value, out response);
            }

            bool? answer = ParseToggle(lower);
            if (answer == null)
            {
                response = Lang.For("setup.usage", Language);
                return false;
            }
            return Apply(_step, answer.Value, out response);
        }
        catch (Exception ex)
        {
            response = Lang.FormatFor("setup.failed", Language, ex.Message);
            return false;
        }
    }

    /// <summary>把“是/否/开/关/on/off/yes/no”解析成布尔; 不是回答词则返回 null。</summary>
    internal static bool? ParseAnswer(string value)
    {
        return ParseToggle(value);
    }

    /// <summary>写入某一项; 如果回答的正是当前项, 就推进到下一项并显示新问题。</summary>
    private static bool Apply(int step, bool value, out string response)
    {
        if (_config == null)
        {
            response = Lang.Get("setup.notInitialized");
            return false;
        }

        switch (step)
        {
            case 1:
                _config.AutoUpdate = value;
                break;
            case 2:
                _config.EnableDetailedUiLog = value;
                break;
            case 3:
                _config.RnrAfterUpdate = value;
                break;
            default:
                // 第 4 项: 是否写入配置文件。
                return Complete(value, out response);
        }

        if (step == _step && _step < TotalSteps)
        {
            _step++;
        }

        response = BuildQuestion();
        return true;
    }

    private static bool Complete(bool save, out string response)
    {
        if (save)
        {
            ApplyLocale();
            Save();
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

    private static void PrintHeader()
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
    }

    private static string BuildQuestion()
    {
        if (_config == null)
        {
            return Lang.Get("setup.notInitialized");
        }
        ServerLocale locale = _locale ?? ServerLocale.Detect(_config.Language);
        _locale = locale;
        string language = locale.Language;
        string state = OnOff(ValueOf(_step), language);

        string question;
        string description;
        switch (_step)
        {
            case 2:
                question = Lang.FormatFor("setup.q2", language, state);
                description = Lang.For("setup.desc2", language);
                break;
            case 3:
                question = Lang.FormatFor("setup.q3", language, state);
                description = Lang.For("setup.desc3", language);
                break;
            case 4:
                question = Lang.FormatFor("setup.q4", language, state);
                description = Lang.For("setup.desc4", language);
                break;
            default:
                question = Lang.FormatFor("setup.q1", language, state);
                description = Lang.For("setup.desc1", language);
                break;
        }

        return Lang.FormatFor("setup.step", language, _step, TotalSteps)
            + "\n" + question
            + "\n    " + description
            + "\n" + Lang.For("setup.answer", language)
            + "\n" + Lang.For("setup.commands", language);
    }

    private static bool ValueOf(int step)
    {
        if (_config == null)
        {
            return false;
        }
        switch (step)
        {
            case 1:
                return _config.AutoUpdate;
            case 2:
                return _config.EnableDetailedUiLog;
            case 3:
                return _config.RnrAfterUpdate;
            default:
                return false;
        }
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
        if (text == "on" || text == "yes" || text == "1" || text == "true"
            || text == "开" || text == "是" || text == "oui" || text == "да" || text == "si" || text == "sí")
        {
            return true;
        }
        if (text == "off" || text == "no" || text == "0" || text == "false"
            || text == "关" || text == "否" || text == "non" || text == "нет")
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
