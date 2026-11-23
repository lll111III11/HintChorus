using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HintChorus.Core.Bootstrap;

namespace HintChorus.Core.Localization;

/// <summary>
/// <b>运行时语言包</b>。
///
/// <para>五个内嵌语言文件 (<c>HintChorus.lang.en.lang</c> 等) 编进主 DLL,
/// 外部语言包放在 <c>configs/&lt;端口&gt;/HintChorus/lang/&lt;code&gt;.lang</c>,
/// 逐 key 覆盖内嵌内容 —— 改文案不需要重新编译, 新增语言也不需要改代码。</para>
///
/// <para>回退顺序: 请求语言 → 语言主代码 (pt_BR → pt) → en → zh → <c>[key]</c>。
/// 任何文件解析失败都只影响它自己, 不会让插件启动失败。</para>
///
/// <para>文件格式: 每行 <c>key = value</c>; <c>#</c> / <c>;</c> / <c>//</c> 开头为注释;
/// value 支持 <c>\n</c> <c>\r</c> <c>\t</c> <c>\\</c> 转义与 <c>{0}</c> 占位符。</para>
/// </summary>
internal static class LanguagePack
{
    internal const string DefaultLanguage = "en";

    internal const string FallbackLanguage = "zh";

    private const string ResourcePrefix = "HintChorus.lang.";

    private const string ResourceSuffix = ".lang";

    private const string FileExtension = ".lang";

    private static readonly object Sync = new object();

    private static readonly string[] BuiltInLanguages = { "en", "zh", "fr", "ru", "es" };

    private static Dictionary<string, Dictionary<string, string>> _packs =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Sources =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static bool _loaded;

    private static string _report = "language packs: not loaded";

    /// <summary>外部语言包目录。</summary>
    internal static string DirectoryPath
    {
        get
        {
            try
            {
                return Path.Combine(RuntimeHome.HomePath, "lang");
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }

    /// <summary>最后一次加载的摘要(启动日志 / <c>Hint lang</c> 用)。</summary>
    internal static string Report
    {
        get
        {
            EnsureLoaded();
            return _report;
        }
    }

    /// <summary>当前已加载的语言代码(含外部包), 按字母序。</summary>
    internal static IReadOnlyList<string> Languages
    {
        get
        {
            EnsureLoaded();
            List<string> list = new List<string>(_packs.Keys);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }
    }

    /// <summary>重新读取内嵌与外部语言包(命令 <c>Hint lang reload</c> 与插件 Enable 时调用)。</summary>
    internal static void Reload()
    {
        lock (Sync)
        {
            LoadPackages();
        }
    }

    /// <summary>
    /// 把配置里的语言解析成实际使用的语言代码。
    /// 显式配置且存在对应语言包(含外部)时优先用它; 否则走服务器语言探测。
    /// </summary>
    internal static string ResolveLanguage(string? configured)
    {
        EnsureLoaded();
        string explicitCode = NormalizeCode(configured);
        if (explicitCode.Length > 0 && explicitCode != "auto")
        {
            string resolved = MatchPack(explicitCode);
            if (resolved.Length > 0)
            {
                return resolved;
            }
            string normalized = ServerLocale.Normalize(configured);
            return normalized.Length > 0 ? normalized : DefaultLanguage;
        }

        string detected = ServerLocale.Detect(configured).Language;
        string detectedPack = MatchPack(detected);
        return detectedPack.Length > 0 ? detectedPack : DefaultLanguage;
    }

    internal static string Get(string key, string? language)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }
        EnsureLoaded();

        foreach (string candidate in Candidates(language))
        {
            Dictionary<string, string>? table;
            string value;
            if (_packs.TryGetValue(candidate, out table) && table.TryGetValue(key, out value))
            {
                return value;
            }
        }
        return "[" + key + "]";
    }

    internal static string Format(string key, string? language, params object?[] args)
    {
        string template = Get(key, language);
        if (args == null || args.Length == 0)
        {
            return template;
        }
        try
        {
            return string.Format(template, args);
        }
        catch (Exception)
        {
            return template;
        }
    }

    /// <summary>语言包的 key 数量(诊断用)。</summary>
    internal static int KeyCount(string language)
    {
        EnsureLoaded();
        Dictionary<string, string>? table;
        return _packs.TryGetValue(NormalizeCode(language), out table) ? table.Count : 0;
    }

    /// <summary>某语言包的来源: builtin / external / builtin+external。</summary>
    internal static string SourceOf(string language)
    {
        EnsureLoaded();
        string source;
        return Sources.TryGetValue(NormalizeCode(language), out source) ? source : "missing";
    }

    /// <summary>语言规则: 小写, '-' → '_'; <c>auto</c> 与空白返回空串。</summary>
    internal static string NormalizeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        string code = value.Trim().ToLowerInvariant().Replace('-', '_');
        return code == "auto" ? string.Empty : code;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }
        lock (Sync)
        {
            if (_loaded)
            {
                return;
            }
            LoadPackages();
        }
    }

    private static void LoadPackages()
    {
        Dictionary<string, Dictionary<string, string>> packs =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string code in BuiltInLanguages)
        {
            Dictionary<string, string>? table = ReadEmbedded(code);
            if (table == null)
            {
                continue;
            }
            packs[code] = table;
            sources[code] = "builtin";
        }

        string directory = DirectoryPath;
        if (directory.Length > 0)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory, "*" + FileExtension, SearchOption.TopDirectoryOnly))
                    {
                        string code = NormalizeCode(Path.GetFileNameWithoutExtension(file));
                        if (code.Length == 0)
                        {
                            continue;
                        }
                        Dictionary<string, string>? external = ReadFile(file);
                        if (external == null)
                        {
                            continue;
                        }
                        Dictionary<string, string>? merged;
                        if (packs.TryGetValue(code, out merged))
                        {
                            foreach (KeyValuePair<string, string> pair in external)
                            {
                                merged[pair.Key] = pair.Value;
                            }
                            sources[code] = "builtin+external";
                        }
                        else
                        {
                            packs[code] = external;
                            sources[code] = "external";
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 目录不可读时保持内嵌语言包可用。
            }
        }

        _packs = packs;
        Sources.Clear();
        foreach (KeyValuePair<string, string> pair in sources)
        {
            Sources[pair.Key] = pair.Value;
        }
        _loaded = true;
        _report = BuildReport(packs, sources, directory);
    }

    private static Dictionary<string, string>? ReadEmbedded(string code)
    {
        try
        {
            Assembly assembly = typeof(LanguagePack).Assembly;
            string name = ResourcePrefix + code + ResourceSuffix;
            using Stream? stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                return null;
            }
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Dictionary<string, string>? ReadFile(string path)
    {
        try
        {
            using StreamReader reader = new StreamReader(path, Encoding.UTF8, true);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Dictionary<string, string> Parse(string text)
    {
        Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(text))
        {
            return table;
        }
        using StringReader reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0
                || trimmed.StartsWith("#", StringComparison.Ordinal)
                || trimmed.StartsWith(";", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }
            int split = trimmed.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }
            string key = trimmed.Substring(0, split).Trim();
            string value = trimmed.Substring(split + 1).Trim();
            if (key.Length == 0)
            {
                continue;
            }
            table[key] = Unescape(value);
        }
        return table;
    }

    private static string Unescape(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }
        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c != '\\' || i + 1 >= value.Length)
            {
                builder.Append(c);
                continue;
            }
            char next = value[++i];
            switch (next)
            {
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case '\\':
                    builder.Append('\\');
                    break;
                default:
                    builder.Append('\\').Append(next);
                    break;
            }
        }
        return builder.ToString();
    }

    private static string MatchPack(string code)
    {
        if (code.Length == 0)
        {
            return string.Empty;
        }
        if (_packs.ContainsKey(code))
        {
            return code;
        }
        int underscore = code.IndexOf('_');
        if (underscore > 0)
        {
            string baseCode = code.Substring(0, underscore);
            if (_packs.ContainsKey(baseCode))
            {
                return baseCode;
            }
            return string.Empty;
        }
        foreach (string key in _packs.Keys)
        {
            if (key.StartsWith(code + "_", StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }
        return string.Empty;
    }

    private static IEnumerable<string> Candidates(string? language)
    {
        string code = NormalizeCode(language);
        if (code.Length > 0 && code != "auto")
        {
            yield return code;
            int underscore = code.IndexOf('_');
            if (underscore > 0)
            {
                yield return code.Substring(0, underscore);
            }
        }
        if (!code.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            yield return DefaultLanguage;
        }
        if (!code.Equals(FallbackLanguage, StringComparison.OrdinalIgnoreCase))
        {
            yield return FallbackLanguage;
        }
    }

    private static string BuildReport(
        Dictionary<string, Dictionary<string, string>> packs,
        Dictionary<string, string> sources,
        string directory)
    {
        List<string> parts = new List<string>();
        List<string> codes = new List<string>(packs.Keys);
        codes.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string code in codes)
        {
            string source;
            sources.TryGetValue(code, out source);
            parts.Add(code + "=" + packs[code].Count + " keys (" + (source ?? "?") + ")");
        }
        string external = directory.Length > 0 ? directory : "(unavailable)";
        return string.Join(", ", parts) + " | external: " + external;
    }
}
