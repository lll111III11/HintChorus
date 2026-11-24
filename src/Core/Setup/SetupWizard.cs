using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
/// <para>第 5 项可进入“详细配置”: 把除前 4 项以外的所有配置参数逐个过一遍。
/// 详细配置中可随时 <c>Hc setup default</c> 把剩余项全部设为默认、
/// <c>Hc setup skip</c> 跳过剩余项、<c>Hc setup save</c> 保存进度并在下次启动继续。</para>
///
/// <para>状态保存在 <c>configs/&lt;端口&gt;/HintChorus/setup.state</c>,
/// 每个版本只会引导一次。所有文案来自语言包 <c>setup.*</c>。</para>
/// </summary>
internal static class SetupWizard
{
    private const int TotalSteps = 5;

    private enum Mode
    {
        Basic,
        Detailed
    }

    /// <summary>前面 4 项里已经问过的配置项, 详细配置不再重复询问。</summary>
    private static readonly HashSet<string> BasicKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(HintChorus.HintChorusPlugin.PluginConfig.AutoUpdate),
        nameof(HintChorus.HintChorusPlugin.PluginConfig.EnableDetailedUiLog),
        nameof(HintChorus.HintChorusPlugin.PluginConfig.RnrAfterUpdate)
    };

    private static readonly HintChorus.HintChorusPlugin.PluginConfig DefaultConfig =
        new HintChorus.HintChorusPlugin.PluginConfig();

    private static HintChorus.HintChorusPlugin.PluginConfig? _config;

    private static Action? _save;

    private static ServerLocale? _locale;

    private static bool _active;

    private static Mode _mode = Mode.Basic;

    private static int _step = 1;

    private static int _detailIndex;

    private static PropertyInfo[]? _detailProperties;

    private static bool _writeConfigOnFinish;

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
            string schema = ConfigSchema();
            bool resume = false;
            bool hasCompleted = false;
            bool completedMatches = false;
            bool schemaMatches = false;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("ResumeMode=", StringComparison.OrdinalIgnoreCase))
                {
                    resume = true;
                }
                else if (line.StartsWith("CompletedVersion=", StringComparison.OrdinalIgnoreCase))
                {
                    hasCompleted = true;
                    string completed = line.Substring("CompletedVersion=".Length).Trim();
                    completedMatches = string.Equals(completed, current, StringComparison.OrdinalIgnoreCase);
                }
                else if (line.StartsWith("ConfigSchema=", StringComparison.OrdinalIgnoreCase))
                {
                    string stored = line.Substring("ConfigSchema=".Length).Trim();
                    schemaMatches = string.Equals(stored, schema, StringComparison.OrdinalIgnoreCase);
                }
            }
            // 版本变化 或 配置项增删(键名/类型变化) 都会重新进入向导。
            return resume || !hasCompleted || !completedMatches || !schemaMatches;
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
        PrintHeader();

        if (TryLoadResume(out int resumeIndex) && DetailCount > 0)
        {
            _mode = Mode.Detailed;
            _detailIndex = Math.Max(0, Math.Min(resumeIndex, DetailCount - 1));
            Logger.Info(Lang.FormatFor("setup.detailed.resumed", Language, _detailIndex + 1, DetailCount));
            Logger.Info(BuildDetailedQuestion());
        }
        else
        {
            _mode = Mode.Basic;
            _step = 1;
            Logger.Info(BuildQuestion());
        }
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

        if (_mode == Mode.Detailed)
        {
            return HandleDetailed(args, out response);
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
            return Complete(_writeConfigOnFinish, out response);
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
            case 4:
                // 第 4 项只记录“结束时是否写配置”, 第 5 项问完后才真正结束。
                _writeConfigOnFinish = value;
                break;
            default:
                if (value)
                {
                    if (DetailCount == 0)
                    {
                        return Complete(_writeConfigOnFinish, out response);
                    }
                    _mode = Mode.Detailed;
                    _detailIndex = 0;
                    response = BuildDetailedQuestion();
                    return true;
                }
                return Complete(_writeConfigOnFinish, out response);
        }

        if (step == _step && _step < TotalSteps)
        {
            _step++;
        }

        response = BuildQuestion();
        return true;
    }

    private static bool HandleDetailed(ArraySegment<string> args, out string response)
    {
        if (_config == null)
        {
            response = Lang.Get("setup.notInitialized");
            return false;
        }

        if (args.Count == 0)
        {
            response = BuildDetailedQuestion();
            return true;
        }

        string text = JoinArgs(args).Trim();
        string lower = text.ToLowerInvariant();

        if (lower == "default" || lower == "defaults" || lower == "默认" || lower == "全部默认")
        {
            int remaining = Math.Max(0, DetailCount - _detailIndex);
            for (int i = _detailIndex; i < DetailCount; i++)
            {
                object? defaultValue = _detailProperties![i].GetValue(DefaultConfig);
                if (defaultValue is Dictionary<string, string> map)
                {
                    // 不把 DefaultConfig 的字典实例直接挂到运行配置上, 避免后续改动污染默认值。
                    defaultValue = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
                }
                _detailProperties[i].SetValue(_config, defaultValue);
            }
            ApplyLocale();
            Save();
            MarkCompleted();
            response = Lang.FormatFor("setup.detailed.defaultsApplied", Language, remaining)
                + "\n" + Lang.FormatFor("setup.saved", Language, ConfigPath());
            Logger.Info("[HintChorus] " + response);
            return true;
        }

        if (lower == "skip" || lower == "跳过")
        {
            int remaining = Math.Max(0, DetailCount - _detailIndex);
            response = Lang.FormatFor("setup.detailed.skipped", Language, remaining);
            if (_writeConfigOnFinish)
            {
                ApplyLocale();
                Save();
                response += "\n" + Lang.FormatFor("setup.saved", Language, ConfigPath());
            }
            MarkCompleted();
            Logger.Info("[HintChorus] " + response);
            return true;
        }

        if (lower == "save" || lower == "保存" || lower == "保存进度")
        {
            ApplyLocale();
            Save();
            SaveProgress(_detailIndex);
            _active = false;
            response = Lang.FormatFor("setup.detailed.savedProgress", Language, _detailIndex + 1, DetailCount);
            Logger.Info("[HintChorus] " + response);
            return true;
        }

        if (lower == "done")
        {
            return Complete(true, out response);
        }

        PropertyInfo property = _detailProperties![_detailIndex];
        if (!TrySetProperty(property, text, out string error))
        {
            response = Lang.FormatFor("setup.detailed.invalid", Language, text, property.Name, error);
            return false;
        }

        _detailIndex++;
        if (_detailIndex >= DetailCount)
        {
            return Complete(true, out response);
        }

        response = BuildDetailedQuestion();
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
            case 5:
                question = Lang.FormatFor("setup.q5", language, state);
                description = Lang.For("setup.desc5", language);
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

    private static string BuildDetailedQuestion()
    {
        if (_config == null || DetailCount == 0)
        {
            return Lang.Get("setup.notInitialized");
        }
        ServerLocale locale = _locale ?? ServerLocale.Detect(_config.Language);
        _locale = locale;
        string language = locale.Language;
        PropertyInfo property = _detailProperties![_detailIndex];
        string description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;

        return Lang.FormatFor(
                "setup.detailed.item",
                language,
                _detailIndex + 1,
                DetailCount,
                property.Name,
                FriendlyType(property.PropertyType))
            + "\n" + Lang.FormatFor("setup.detailed.current", language, FormatValue(property.GetValue(_config)))
            + "\n" + Lang.FormatFor("setup.detailed.default", language, FormatValue(property.GetValue(DefaultConfig)))
            + (description.Length > 0 ? "\n    " + description : string.Empty)
            + "\n" + Lang.For("setup.detailed.usage", language);
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

    private static int DetailCount
    {
        get
        {
            EnsureDetailProperties();
            return _detailProperties?.Length ?? 0;
        }
    }

    private static void EnsureDetailProperties()
    {
        if (_detailProperties != null)
        {
            return;
        }
        _detailProperties = typeof(HintChorus.HintChorusPlugin.PluginConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !BasicKeys.Contains(p.Name))
            .OrderBy(p => p.MetadataToken)
            .ToArray();
    }

    private static bool TrySetProperty(PropertyInfo property, string text, out string error)
    {
        error = string.Empty;
        try
        {
            Type type = property.PropertyType;

            if (type == typeof(bool))
            {
                bool? value = ParseToggle(text);
                if (value == null)
                {
                    error = "bool (on/off/yes/no/是/否)";
                    return false;
                }
                property.SetValue(_config, value.Value);
                return true;
            }

            if (type == typeof(string))
            {
                if (text.Equals("default", StringComparison.OrdinalIgnoreCase) || text == "默认")
                {
                    property.SetValue(_config, property.GetValue(DefaultConfig));
                }
                else
                {
                    property.SetValue(_config, text);
                }
                return true;
            }

            if (type == typeof(int))
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    error = "int";
                    return false;
                }
                property.SetValue(_config, value);
                return true;
            }

            if (type == typeof(float))
            {
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    error = "float";
                    return false;
                }
                property.SetValue(_config, value);
                return true;
            }

            if (type == typeof(byte))
            {
                if (!byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte value))
                {
                    error = "byte (0-255)";
                    return false;
                }
                property.SetValue(_config, value);
                return true;
            }

            if (type == typeof(ushort))
            {
                if (!ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort value))
                {
                    error = "ushort (0-65535)";
                    return false;
                }
                property.SetValue(_config, value);
                return true;
            }

            if (type.IsEnum)
            {
                try
                {
                    object value = Enum.Parse(type, text, true);
                    property.SetValue(_config, value);
                    return true;
                }
                catch (Exception)
                {
                    error = FriendlyType(type);
                    return false;
                }
            }

            if (type == typeof(Dictionary<string, string>))
            {
                if (text.Length == 0
                    || text.Equals("default", StringComparison.OrdinalIgnoreCase)
                    || text == "默认")
                {
                    property.SetValue(_config, new Dictionary<string, string>());
                    return true;
                }
                Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string part in text.Split(new[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int split = part.IndexOf('=');
                    if (split <= 0)
                    {
                        error = "key=value;key2=value2";
                        return false;
                    }
                    map[part.Substring(0, split).Trim()] = part.Substring(split + 1).Trim();
                }
                property.SetValue(_config, map);
                return true;
            }

            error = FriendlyType(type);
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
        {
            return Lang.For("setup.detailed.empty", Language);
        }
        if (value is string text)
        {
            return text.Length == 0 ? Lang.For("setup.detailed.empty", Language) : text;
        }
        if (value is Dictionary<string, string> map)
        {
            if (map.Count == 0)
            {
                return Lang.For("setup.detailed.empty", Language);
            }
            return string.Join(";", map.Select(pair => pair.Key + "=" + pair.Value));
        }
        if (value is float single)
        {
            return single.ToString("0.###", CultureInfo.InvariantCulture);
        }
        return value.ToString() ?? string.Empty;
    }

    private static string FriendlyType(Type type)
    {
        if (type == typeof(bool))
        {
            return "bool";
        }
        if (type == typeof(string))
        {
            return "string";
        }
        if (type == typeof(int))
        {
            return "int";
        }
        if (type == typeof(float))
        {
            return "float";
        }
        if (type == typeof(byte))
        {
            return "byte";
        }
        if (type == typeof(ushort))
        {
            return "ushort";
        }
        if (type == typeof(Dictionary<string, string>))
        {
            return "key=value;key2=value2";
        }
        if (type.IsEnum)
        {
            return string.Join("/", Enum.GetNames(type));
        }
        return type.Name;
    }

    private static string JoinArgs(ArraySegment<string> args)
    {
        if (args.Array == null || args.Count == 0)
        {
            return string.Empty;
        }
        string[] parts = new string[args.Count];
        for (int i = 0; i < args.Count; i++)
        {
            parts[i] = args.Array[args.Offset + i] ?? string.Empty;
        }
        return string.Join(" ", parts);
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
        _mode = Mode.Basic;
        _step = 1;
        _detailIndex = 0;
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
                + "ConfigSchema=" + ConfigSchema() + Environment.NewLine
                + "CompletedUtc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + Lang.FormatFor("setup.writeStateFailed", Language, ex.Message));
        }
        Logger.Info("[HintChorus] " + Lang.For("setup.done", Language));
        Logger.Info("[HintChorus] " + Lang.For("setup.restart", Language));
    }

    private static void SaveProgress(int index)
    {
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
                "ResumeMode=detailed" + Environment.NewLine
                + "ResumeIndex=" + index.ToString(CultureInfo.InvariantCulture) + Environment.NewLine
                + "ResumeUtc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + Lang.FormatFor("setup.writeStateFailed", Language, ex.Message));
        }
    }

    private static bool TryLoadResume(out int index)
    {
        index = 0;
        try
        {
            string path = StatePath();
            if (!File.Exists(path))
            {
                return false;
            }
            bool resume = false;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("ResumeMode=", StringComparison.OrdinalIgnoreCase))
                {
                    resume = true;
                }
                else if (line.StartsWith("ResumeIndex=", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(
                        line.Substring("ResumeIndex=".Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out index);
                }
            }
            return resume;
        }
        catch (Exception)
        {
            return false;
        }
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

    /// <summary>
    /// 配置结构指纹: 属性名 + 类型。新增/删除/改名/改类型都会变化,
    /// 从而让向导在“增加了新配置项”的版本重新弹出。
    /// </summary>
    private static string ConfigSchema()
    {
        PropertyInfo[] properties = typeof(HintChorus.HintChorusPlugin.PluginConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

        uint hash = 2166136261u;
        foreach (PropertyInfo property in properties)
        {
            string token = property.Name + ":" + property.PropertyType.Name + ";";
            for (int i = 0; i < token.Length; i++)
            {
                hash ^= token[i];
                hash *= 16777619u;
            }
        }
        return hash.ToString("X8", CultureInfo.InvariantCulture);
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
