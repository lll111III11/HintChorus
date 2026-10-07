using HarmonyLib;
using Hints;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HintChorus.Core.Compat;

/// <summary>
/// <b>原生提示翻译器(多语言)</b> —— 把游戏自己的 <c>TranslationHint</c> 在服务端翻成纯文本,
/// 让它可以并入复合提示, 与插件 UI <b>真共存</b>(不再"原生顶掉插件")。
///
/// <para><b>为什么要翻译:</b> 客户端的 Hint 只有<b>一个槽</b>, 原生提示直接下发会顶掉复合提示;
/// 而 <c>TranslationHint</c> 不带文本, 只带一个 <c>HintTranslations</c> 枚举键 + 参数,
/// 真正的字是客户端按玩家语言翻的。服务端想合并它, 就得自己翻 —— 而且必须按<b>玩家自己的语言</b>翻,
/// 否则中文玩家会看到英文提示。这就是本类的"语言自适应"。</para>
///
/// <para><b>语言覆盖策略:</b></para>
/// <list type="bullet">
///   <item><b>内嵌 5 种联合国常用语</b>(DLL 自带, 永远可用): <c>en / zh / fr / ru / es</c>;</item>
///   <item><b>外部语言目录</b>(可选, 其余语言都从这里来): <c>configs\&lt;端口&gt;\HintChorus\translations\&lt;lang&gt;.txt</c>,
///     每行一条模板(行号 = 枚举下标 + 1, 忽略空行)。GitHub 仓库的
///     <c>docs/translations/</c> 下提供全部分支语言文件, 服主下载放进该目录即生效;</item>
///   <item><b>运行时自动兜底</b>: 加载游戏自带 <c>Translations\en\GameHints.txt</c> 作为英文的运行时刷新;
///     某语言缺失时逐级回退: 外部 → 内嵌 → 英文。</item>
/// </list>
///
/// <para><b>语言名归一化:</b> SCP:SL 客户端的语言代码(如 <c>zh_Hans</c> / <c>zh_Hans-2</c> /
/// <c>zh_Flash_Hans</c> / <c>zh_Hant</c>)会被映射到统一的规范键, 繁体找不到再回退简体。
/// 外部文件按<b>原始语言名</b>精确匹配优先(如 <c>zh_Hant.txt</c>), 再按规范键匹配。</para>
///
/// <para><b>翻译失败时的行为:</b> 枚举未知 / 占位符比参数多(无法保真) / 反射失败,
/// 一律返回 false —— 调用方退回"让路"机制, 绝不以半成品文本污染 UI。</para>
/// </summary>
public static class NativeHintTranslator
{
    /// <summary>服务器自带英文翻译文件的相对路径(相对 Managed 目录)。</summary>
    private const string GameHintsRelativePath = @"..\..\Translations\en\GameHints.txt";

    /// <summary>外部语言目录名(位于本底层的自宿主目录下)。</summary>
    private const string TranslationsFolderName = "translations";

    /// <summary>占位符形如 <c>[type]</c> / <c>[max_item_count]</c>, 按出现顺序与参数一一对应。</summary>
    private static readonly Regex TokenRegex = new(@"\[[a-zA-Z_]+]", RegexOptions.Compiled);

    /// <summary>
    /// 内嵌模板: <c>规范语言键 → (枚举键 → 模板)</c>。
    /// 键 = <c>HintTranslations</c> 枚举值(0..5), 对应官方 <c>GameHints.txt</c> 第 1..6 行。
    /// 英文与官方一致; 其余为本底层自行翻译。
    /// </summary>
    private static readonly Dictionary<string, Dictionary<byte, string>> Embedded = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new()
        {
            { 0, "<color=red>Access denied</color>" },
            { 1, "Reached the limit of <color=yellow>[type] ammo</color> (<color=yellow>[max_type_count] rounds</color>)." },
            { 2, "<b>Already</b> reached the limit of <color=yellow>[type] ammo</color> (<color=yellow>[max_type_count] rounds</color>)." },
            { 3, "Reached the limit of <color=yellow>[type]</color> (<color=yellow>[max_type_count] items</color>)." },
            { 4, "<b>Already</b> reached the limit of <color=yellow>[type]</color> (<color=yellow>[max_type_count] items</color>)." },
            { 5, "Only <color=yellow>[max_item_count] items</color> can be carried." },
        },
        ["zh"] = new()
        {
            { 0, "<color=red>访问被拒绝</color>" },
            { 1, "已达到 <color=yellow>[type] 弹药</color> 的上限（<color=yellow>[max_type_count] 发</color>）。" },
            { 2, "已<b>达到</b> <color=yellow>[type] 弹药</color> 的上限（<color=yellow>[max_type_count] 发</color>）。" },
            { 3, "已达到 <color=yellow>[type]</color> 的可持有上限（<color=yellow>[max_type_count] 个</color>）。" },
            { 4, "已<b>达到</b> <color=yellow>[type]</color> 的可持有上限（<color=yellow>[max_type_count] 个</color>）。" },
            { 5, "最多只能携带 <color=yellow>[max_item_count] 个</color> 物品。" },
        },
        ["fr"] = new()
        {
            { 0, "<color=red>Accès refusé</color>" },
            { 1, "Limite de <color=yellow>munitions [type]</color> atteinte (<color=yellow>[max_type_count] cartouches</color>)." },
            { 2, "<b>Déjà</b> à la limite de <color=yellow>munitions [type]</color> (<color=yellow>[max_type_count] cartouches</color>)." },
            { 3, "Limite de <color=yellow>[type]</color> atteinte (<color=yellow>[max_type_count] objets</color>)." },
            { 4, "<b>Déjà</b> à la limite de <color=yellow>[type]</color> (<color=yellow>[max_type_count] objets</color>)." },
            { 5, "Seuls <color=yellow>[max_item_count] objets</color> peuvent être portés." },
        },
        ["ru"] = new()
        {
            { 0, "<color=red>Доступ запрещён</color>" },
            { 1, "Достигнут предел <color=yellow>боеприпасов: [type]</color> (<color=yellow>[max_type_count] шт.</color>)." },
            { 2, "<b>Уже</b> достигнут предел <color=yellow>боеприпасов: [type]</color> (<color=yellow>[max_type_count] шт.</color>)." },
            { 3, "Достигнут предел: <color=yellow>[type]</color> (<color=yellow>[max_type_count] шт.</color>)." },
            { 4, "<b>Уже</b> достигнут предел: <color=yellow>[type]</color> (<color=yellow>[max_type_count] шт.</color>)." },
            { 5, "Можно нести не более <color=yellow>[max_item_count] предметов</color>." },
        },
        ["es"] = new()
        {
            { 0, "<color=red>Acceso denegado</color>" },
            { 1, "Alcanzado el límite de <color=yellow>municiones [type]</color> (<color=yellow>[max_type_count] cartuchos</color>)." },
            { 2, "<b>Ya</b> alcanzado el límite de <color=yellow>municiones [type]</color> (<color=yellow>[max_type_count] cartuchos</color>)." },
            { 3, "Alcanzado el límite de <color=yellow>[type]</color> (<color=yellow>[max_type_count] objetos</color>)." },
            { 4, "<b>Ya</b> alcanzado el límite de <color=yellow>[type]</color> (<color=yellow>[max_type_count] objetos</color>)." },
            { 5, "Solo se pueden llevar <color=yellow>[max_item_count] objetos</color>." },
        },
    };

    /// <summary>
    /// SCP:SL 客户端语言代码 → 规范键。归一化用于: 内嵌查找 + 外部文件按规范名匹配。
    /// 中文变体全部归到 <c>zh</c>; 繁体 <c>zh_Hant</c> 保留独立键(外部可精确提供繁体)。
    /// </summary>
    private static readonly Dictionary<string, string> LanguageAlias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "en",
        ["zh"] = "zh",
        ["zh_hans"] = "zh",
        ["zh_hans-2"] = "zh",
        ["zh_flash_hans"] = "zh",
        ["zh_hant"] = "zh_hant",
        ["fr"] = "fr",
        ["ru"] = "ru",
        ["es"] = "es",
    };

    /// <summary>外部加载的模板: <c>语言键(小写) → (枚举键 → 模板)</c>。来源: 游戏 Translations + 自宿主 translations 目录。</summary>
    private static readonly Dictionary<string, Dictionary<byte, string>> External = new(StringComparer.OrdinalIgnoreCase);

    private static bool _loaded;

    private static Func<Hint, HintParameter[]?>? _parametersGetter;
    private static Func<TranslationHint, HintTranslations>? _translationGetter;
    private static Func<ReferenceHub, string?>? _playerLanguageGetter;

    /// <summary>模板是否已就绪(内嵌或外部文件)。</summary>
    public static bool IsLoaded => _loaded;

    /// <summary>当前可用的语言键(内嵌 + 外部, 已排序)。</summary>
    public static IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
            foreach (string key in Embedded.Keys)
            {
                keys.Add(key);
            }

            foreach (string key in External.Keys)
            {
                keys.Add(key);
            }

            List<string> list = new(keys);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }
    }

    /// <summary>载入(幂等): 绑定反射读取器 + 加载游戏英文文件 + 扫描外部语言目录。</summary>
    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        BindGetters();
        LoadServerEnglishFile();
        LoadExternalTranslations();
    }

    /// <summary>
    /// 解析<b>单个玩家</b>的语言(真正"自适应")。
    ///
    /// <para>服务端通常<b>拿不到</b>玩家客户端的 UI 语言 —— 翻译在客户端做, 语言设置不同步服务端。
    /// 但部分游戏版本会在 <c>ReferenceHub.playerPreferences.Language</c> 上同步玩家偏好,
    /// 本方法用反射<b>探测一次</b>: 存在就按玩家语言返回, 不存在/失败就回退配置默认语言。</para>
    ///
    /// <para>探测成本只在首次(缓存委托); 翻译本身只在原生提示出现时发生, 频率低, 用 Invoke 可接受。</para>
    /// </summary>
    public static string ResolvePlayerLanguage(ReferenceHub hub, string fallback)
    {
        EnsureLoaded();

        if (hub is null)
        {
            return fallback;
        }

        if (_playerLanguageGetter is null)
        {
            _playerLanguageGetter = TryBuildPlayerLanguageGetter();
        }

        if (_playerLanguageGetter is not null)
        {
            try
            {
                string? language = _playerLanguageGetter(hub);
                if (!string.IsNullOrWhiteSpace(language))
                {
                    return language!;
                }
            }
            catch (Exception)
            {
                // 读取失败回退默认
            }
        }

        return fallback;
    }

    /// <summary>尝试构建"读玩家语言"的委托; 该版本没有该字段时返回 null(永久回退)。</summary>
    private static Func<ReferenceHub, string?>? TryBuildPlayerLanguageGetter()
    {
        try
        {
            PropertyInfo? prefsProperty = AccessTools.Property(typeof(ReferenceHub), "playerPreferences");
            MethodInfo? prefsGetter = prefsProperty?.GetGetMethod();
            if (prefsGetter is null || prefsProperty is null)
            {
                return null;
            }

            PropertyInfo? languageProperty = prefsProperty.PropertyType.GetProperty("Language");
            MethodInfo? languageGetter = languageProperty?.GetGetMethod();
            if (languageGetter is null || languageProperty is null)
            {
                return null;
            }

            return hub =>
            {
                try
                {
                    object? prefs = prefsGetter.Invoke(hub, null);
                    return prefs is null ? null : languageGetter.Invoke(prefs, null) as string;
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 尝试把译文提示翻成纯文本(按玩家语言自适应)。
    /// <para>成功返回 true; 枚举未知 / 参数不足 / 反射失败时返回 false —— 调用方应退回"让路"。</para>
    /// </summary>
    public static bool TryTranslate(TranslationHint hint, string language, out string text)
    {
        text = string.Empty;
        EnsureLoaded();

        try
        {
            if (_translationGetter is null)
            {
                return false;
            }

            // HintTranslations 枚举底层是 byte; 直接取枚举值转 byte 作模板键。
            byte key = (byte)_translationGetter(hint);

            string? template = PickTemplate(key, language);
            if (template is null)
            {
                return false;
            }

            HintParameter[]? parameters = _parametersGetter?.Invoke(hint);
            string[] values = ExtractValues(parameters);

            // 占位符比参数多 → 无法保真插值, 拒绝翻译(退回让路), 绝不输出半成品。
            if (TokenRegex.Matches(template).Count > values.Length)
            {
                return false;
            }

            int index = 0;
            text = TokenRegex.Replace(template, match => index < values.Length ? values[index++] : match.Value);

            return !string.IsNullOrWhiteSpace(text);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 选模板, 优先级逐级回退:
    /// ① 原始语言名精确匹配外部(如 <c>zh_Hant.txt</c>) → ② 规范键外部 → ③ 规范键内嵌
    /// → ④ 繁体回退简体(内嵌 zh) → ⑤ 英文内嵌 → ⑥ 英文外部(游戏文件运行时刷新)。
    /// </summary>
    private static string? PickTemplate(byte key, string language)
    {
        // ① 原始语言名精确匹配外部
        if (External.TryGetValue(language, out Dictionary<byte, string>? exact)
            && exact.TryGetValue(key, out string? t1))
        {
            return t1;
        }

        string norm = NormalizeLanguage(language);

        // ② 规范键外部
        if (External.TryGetValue(norm, out Dictionary<byte, string>? ext)
            && ext.TryGetValue(key, out string? t2))
        {
            return t2;
        }

        // ③ 规范键内嵌
        if (Embedded.TryGetValue(norm, out Dictionary<byte, string>? emb)
            && emb.TryGetValue(key, out string? t3))
        {
            return t3;
        }

        // ④ 繁体没有独立模板 → 回退简体
        if (string.Equals(norm, "zh_hant", StringComparison.OrdinalIgnoreCase)
            && Embedded["zh"].TryGetValue(key, out string? t4))
        {
            return t4;
        }

        // ⑤ 英文内嵌(终极兜底)
        if (Embedded["en"].TryGetValue(key, out string? t5))
        {
            return t5;
        }

        // ⑥ 英文外部(游戏文件运行时刷新, 含游戏新增的键)
        if (External.TryGetValue("en", out Dictionary<byte, string>? en)
            && en.TryGetValue(key, out string? t6))
        {
            return t6;
        }

        return null;
    }

    /// <summary>客户端语言代码 → 规范键(小写、归一化)。未知语言原样小写返回。</summary>
    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en";
        }

        return LanguageAlias.TryGetValue(language, out string? norm) ? norm : language.ToLowerInvariant();
    }

    /// <summary>尽力加载游戏自带 <c>Translations\en\GameHints.txt</c> 作为英文的运行时刷新。</summary>
    private static void LoadServerEnglishFile()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GameHintsRelativePath);
            if (!File.Exists(path))
            {
                return;
            }

            Dictionary<byte, string> templates = new();
            string[] lines = File.ReadAllLines(path);
            // 键 = 枚举下标(0..5), 对应文件第 1..6 行。用独立计数器逐个非空行分配:
            // 若文件中间出现空行/注释行, 不会让后续键整体错位。
            byte key = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string? line = lines[i]?.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                templates[key] = line!;
                key++;
            }

            if (templates.Count > 0)
            {
                External["en"] = templates;
            }
        }
        catch (Exception)
        {
            // 读不到就只用内嵌英文 —— 覆盖范围仍全量。
        }
    }

    /// <summary>
    /// 扫描自宿主目录的 <c>translations\</c> 文件夹, 把每个 <c>&lt;lang&gt;.txt</c> 载入外部模板。
    /// 文件格式与 <c>GameHints.txt</c> 相同: 每行一条, 行号 = 枚举下标 + 1, 空行忽略。
    /// </summary>
    private static void LoadExternalTranslations()
    {
        try
        {
            string folder = Path.Combine(HintChorus.Core.Bootstrap.RuntimeHome.HomePath, TranslationsFolderName);
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(folder, "*.txt", SearchOption.TopDirectoryOnly))
            {
                string lang = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(lang))
                {
                    continue;
                }

                Dictionary<byte, string> templates = new();
                string[] lines = File.ReadAllLines(file);
                byte key = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string? line = lines[i]?.Trim();
                    if (string.IsNullOrEmpty(line) || line!.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    templates[key] = line!;
                    key++;
                }

                if (templates.Count > 0)
                {
                    External[lang] = templates;
                }
            }
        }
        catch (Exception)
        {
            // 外部语言目录读不到不影响内嵌的 5 种联合国常用语。
        }
    }

    /// <summary>把 <c>Hint.Parameters</c> 与 <c>TranslationHint.Translation</c> 的读取器绑成委托(避免每次反射)。</summary>
    private static void BindGetters()
    {
        try
        {
            PropertyInfo? parameters = AccessTools.Property(typeof(Hint), "Parameters");
            MethodInfo? parametersGetter = parameters?.GetGetMethod(true);
            _parametersGetter = parametersGetter != null
                ? (Func<Hint, HintParameter[]?>)Delegate.CreateDelegate(typeof(Func<Hint, HintParameter[]>), parametersGetter)
                : null;

            PropertyInfo? translation = AccessTools.Property(typeof(TranslationHint), "Translation");
            MethodInfo? translationGetter = translation?.GetGetMethod(true);
            // ⚠ 委托返回类型必须与 getter 完全一致(值类型 HintTranslations):
            //   绑定成 Func<TranslationHint, object?> 会因"值类型→object 不做装箱协变"而抛
            //   ArgumentException, 被 catch 吞掉后翻译功能整个静默失效。
            _translationGetter = translationGetter != null
                ? (Func<TranslationHint, HintTranslations>)Delegate.CreateDelegate(typeof(Func<TranslationHint, HintTranslations>), translationGetter)
                : null;
        }
        catch (Exception)
        {
            _parametersGetter = null;
            _translationGetter = null;
        }
    }

    /// <summary>逐个参数取显示值。</summary>
    private static string[] ExtractValues(HintParameter[]? parameters)
    {
        if (parameters is null || parameters.Length == 0)
        {
            return Array.Empty<string>();
        }

        string[] values = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            values[i] = FormatParameter(parameters[i]);
        }

        return values;
    }

    /// <summary>
    /// 把单个参数格式化成显示文本。
    ///
    /// <para>优先让参数<b>自己</b>格式化 —— <c>HintParameter.Update(progress)</c> 会调用
    /// 各自的 <c>UpdateState</c>: <c>PrimitiveHintParameter</c> 走 <c>FormatValue</c> 输出数值,
    /// <c>IdHintParameter</c>(物品/类别/弹药) 走 <c>FormatId</c> 查 <c>InventoryItemLoader</c>
    /// 拿<b>本地化名</b>, 结果填进公开的 <c>Formatted</c>。这样物品名不会变空串,
    /// 也不必自己反射 <c>Value</c>/<c>Id</c> 再去拼名字。</para>
    ///
    /// <para>格式化失败(异常或 <c>Formatted</c> 为空)时回退到反射 <c>Value</c>。</para>
    /// </summary>
    private static string FormatParameter(HintParameter parameter)
    {
        try
        {
            parameter.Update(0f);
            string? formatted = parameter.Formatted;
            if (!string.IsNullOrEmpty(formatted))
            {
                return formatted;
            }
        }
        catch (Exception)
        {
            // 回退到反射读取
        }

        return FormatValue(ReadValue(parameter));
    }

    /// <summary>
    /// 反射读取参数的 <c>Value</c>(仅作 <see cref="FormatParameter"/> 的回退)。
    /// 它声明在泛型基类 <c>PrimitiveHintParameter&lt;TValue&gt;</c> 上且是 protected,
    /// 因此沿继承链向上找(BindingFlags.DeclaredOnly 逐层定位)。
    /// </summary>
    private static object? ReadValue(HintParameter parameter)
    {
        Type? type = parameter.GetType();
        while (type != null && type != typeof(object) && type != typeof(HintParameter))
        {
            PropertyInfo? property = type.GetProperty(
                "Value",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            if (property != null)
            {
                return property.GetValue(parameter);
            }

            type = type.BaseType;
        }

        return null;
    }

    /// <summary>把参数值格式化成可显示的文本。</summary>
    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is float single)
        {
            return single.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }
}
