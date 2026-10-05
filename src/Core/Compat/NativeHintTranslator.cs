using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Hints;

namespace HintIsolation.Core.Compat;

/// <summary>
/// 把游戏自身的 <see cref="TranslationHint"/> 翻成纯文本, 好并入统一排版。
///
/// <para><b>语言来源(按优先级)</b>:</para>
/// <list type="number">
///   <item>服务器自己的 <c>Translations\&lt;语言&gt;\GameHints.txt</c>(专用服务器通常只有 en);</item>
///   <item>本插件内嵌的 22 份官方译文 <c>HintIsolation.lang.&lt;语言代码&gt;</c>;</item>
///   <item>内嵌的 en;</item>
///   <item>最后兜底: 代码里硬编码的 6 条(英/中)。</item>
/// </list>
///
/// <para><b>为什么可以内嵌</b>: 客户端带 22 种语言, 但专用服务器只带 en —— 想让服务端也能用别的语言,
/// 只能自己带一份。这些译文权利归 Northwood Studios, 见仓库 THIRD-PARTY.md。</para>
///
/// <para><b>一个诚实的限制</b>: 原生提示原本是<b>客户端按玩家自己设的语言</b>渲染的; 一旦被本插件吸收进
/// 统一排版, 就只能统一成一种语言(服务端并不逐玩家知道对方的客户端语言)。所以这里是用配置选一个语言。</para>
/// </summary>
public static class NativeHintTranslator
{
	private sealed record TemplatePair(string En, string Zh);

	/// <summary>与游戏客户端 <c>Translations\</c> 目录一致的 22 个语言代码。</summary>
	public static readonly string[] SupportedLanguages = new string[22]
	{
		"ca", "cs", "de", "en", "es", "fr", "gl", "it", "ko", "pl", "pt_BR", "ru",
		"sk", "sr_CYRL-BA", "sr_LATN-BA", "tr", "uk", "vi", "zh_Flash_Hans", "zh_Hans", "zh_Hans-2", "zh_Hant"
	};

	private static readonly Regex TokenRegex = new Regex("\\[[a-zA-Z_]+\\]", RegexOptions.Compiled);

	/// <summary>最后兜底用的硬编码模板(内嵌资源缺失或该语言缺这一条时才用)。</summary>
	private static readonly Dictionary<byte, TemplatePair> Embedded = new Dictionary<byte, TemplatePair>
	{
		{ 0, new TemplatePair("<color=red>Access denied</color>", "<color=red>访问被拒绝</color>") },
		{ 1, new TemplatePair("Reached the limit of <color=yellow>[type] ammo</color> (<color=yellow>[max_type_count] rounds</color>).", "已达到 <color=yellow>[type] 弹药</color> 的上限（<color=yellow>[max_type_count] 发</color>）。") },
		{ 2, new TemplatePair("<b>Already</b> reached the limit of <color=yellow>[type] ammo</color> (<color=yellow>[max_type_count] rounds</color>).", "已<b>达到</b> <color=yellow>[type] 弹药</color> 的上限（<color=yellow>[max_type_count] 发</color>）。") },
		{ 3, new TemplatePair("Reached the limit of <color=yellow>[type]</color> (<color=yellow>[max_type_count] items</color>).", "已达到 <color=yellow>[type]</color> 的可持有上限（<color=yellow>[max_type_count] 个</color>）。") },
		{ 4, new TemplatePair("<b>Already</b> reached the limit of <color=yellow>[type]</color> (<color=yellow>[max_type_count] items</color>).", "已<b>达到</b> <color=yellow>[type]</color> 的可持有上限（<color=yellow>[max_type_count] 个</color>）。") },
		{ 5, new TemplatePair("Only <color=yellow>[max_item_count] items</color> can be carried.", "最多只能携带 <color=yellow>[max_item_count] 个</color> 物品。") },
	};

	private static readonly Dictionary<string, Dictionary<byte, string>> Tables =
		new Dictionary<string, Dictionary<byte, string>>(StringComparer.OrdinalIgnoreCase);

	private static readonly object Sync = new object();

	private static bool _gettersBound;

	private static Func<Hint, HintParameter[]?>? _parametersGetter;

	private static Func<TranslationHint, HintTranslations>? _translationGetter;

	/// <summary>是否已完成(反射绑定)初始化。</summary>
	public static bool IsLoaded => _gettersBound;

	public static void EnsureLoaded()
	{
		if (_gettersBound)
		{
			return;
		}
		lock (Sync)
		{
			if (_gettersBound)
			{
				return;
			}
			BindGetters();
			_gettersBound = true;
		}
	}

	/// <summary>
	/// 把各种常见写法归一化到 <see cref="SupportedLanguages"/> 里的代码。
	/// <para>例: zh / cn / chs / zh-cn → zh_Hans; cht / tw / zh-tw → zh_Hant; pt → pt_BR; sr → sr_LATN-BA。</para>
	/// </summary>
	public static string NormalizeLanguage(string? code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return "en";
		}
		string c = code.Trim().Replace('-', '_').ToLowerInvariant();
		switch (c)
		{
			case "zh":
			case "cn":
			case "chs":
			case "zh_cn":
			case "zh_chs":
			case "zh_hans":
			case "zh_zh_hans":
				return "zh_Hans";
			case "zh_flash_hans":
			case "zh_flash":
				return "zh_Flash_Hans";
			case "zh_hant":
			case "zh_tw":
			case "tw":
			case "cht":
				return "zh_Hant";
			case "zh_hans_2":
				return "zh_Hans-2";
			case "pt":
			case "pt_br":
			case "ptb":
				return "pt_BR";
			case "sr":
			case "sr_latn_ba":
			case "sr_latn":
				return "sr_LATN-BA";
			case "sr_cyrl_ba":
			case "sr_cyrl":
				return "sr_CYRL-BA";
		}
		foreach (string known in SupportedLanguages)
		{
			if (string.Equals(known, c, StringComparison.OrdinalIgnoreCase))
			{
				return known;
			}
		}
		return "en";
	}

	/// <summary>该语言是不是本插件认得的那 22 个之一。</summary>
	public static bool IsSupportedLanguage(string? code)
	{
		return !string.Equals(NormalizeLanguage(code), "en", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(code?.Trim(), "en", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>取某语言已加载的模板表(懒加载 + 缓存), 拿不到就返回英文表。</summary>
	private static Dictionary<byte, string> GetTable(string code)
	{
		string key = NormalizeLanguage(code);
		lock (Sync)
		{
			if (Tables.TryGetValue(key, out Dictionary<byte, string> cached))
			{
				return cached;
			}
			Dictionary<byte, string> table = LoadFromServerFile(key) ?? LoadFromEmbedded(key);
			if (table == null && !string.Equals(key, "en", StringComparison.OrdinalIgnoreCase))
			{
				table = LoadFromServerFile("en") ?? LoadFromEmbedded("en");
			}
			table ??= new Dictionary<byte, string>(0);
			Tables[key] = table;
			return table;
		}
	}

	private static Dictionary<byte, string>? LoadFromServerFile(string code)
	{
		try
		{
			// 服务器根目录下的 Translations\<语言>\GameHints.txt
			// (旧版这里多退了一层 ..\.., 结果永远读不到 —— 已修)
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translations", code, "GameHints.txt");
			if (!File.Exists(path))
			{
				return null;
			}
			return ParseLines(File.ReadAllLines(path));
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static Dictionary<byte, string>? LoadFromEmbedded(string code)
	{
		try
		{
			using Stream? stream = typeof(NativeHintTranslator).Assembly
				.GetManifestResourceStream("HintIsolation.lang." + NormalizeLanguage(code));
			if (stream == null)
			{
				return null;
			}
			using StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8);
			List<string> lines = new List<string>();
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				lines.Add(line);
			}
			return ParseLines(lines.ToArray());
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static Dictionary<byte, string> ParseLines(string[] lines)
	{
		Dictionary<byte, string> table = new Dictionary<byte, string>(lines.Length);
		for (int i = 0; i < lines.Length; i++)
		{
			string value = lines[i]?.Trim() ?? string.Empty;
			if (value.Length > 0 && i <= byte.MaxValue)
			{
				table[(byte)i] = value;
			}
		}
		return table;
	}

	public static bool TryTranslate(TranslationHint hint, string language, out string text)
	{
		text = string.Empty;
		EnsureLoaded();
		try
		{
			if (_translationGetter == null)
			{
				return false;
			}
			byte key = (byte)(int)_translationGetter(hint);
			string? template = PickTemplate(key, language);
			if (template == null)
			{
				return false;
			}
			HintParameter[]? parameters = _parametersGetter?.Invoke(hint);
			string[] values = ExtractValues(parameters);
			if (TokenRegex.Matches(template).Count > values.Length)
			{
				return false;
			}
			int index = 0;
			text = TokenRegex.Replace(template, (Match match) => (index >= values.Length) ? match.Value : values[index++]);
			return !string.IsNullOrWhiteSpace(text);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>按顺序取模板: 指定语言 → 英文 → 硬编码兜底。</summary>
	private static string? PickTemplate(byte key, string language)
	{
		if (GetTable(language).TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		if (GetTable("en").TryGetValue(key, out string english) && !string.IsNullOrWhiteSpace(english))
		{
			return english;
		}
		if (Embedded.TryGetValue(key, out TemplatePair pair))
		{
			return pair.En;
		}
		return null;
	}

	private static void BindGetters()
	{
		try
		{
			MethodInfo? parametersProperty = AccessTools.Property(typeof(Hint), "Parameters")?.GetGetMethod(nonPublic: true);
			_parametersGetter = ((parametersProperty != null) ? ((Func<Hint, HintParameter[]>)Delegate.CreateDelegate(typeof(Func<Hint, HintParameter[]>), parametersProperty)) : null);
			MethodInfo? translationProperty = AccessTools.Property(typeof(TranslationHint), "Translation")?.GetGetMethod(nonPublic: true);
			_translationGetter = ((translationProperty != null) ? ((Func<TranslationHint, HintTranslations>)Delegate.CreateDelegate(typeof(Func<TranslationHint, HintTranslations>), translationProperty)) : null);
		}
		catch (Exception)
		{
			_parametersGetter = null;
			_translationGetter = null;
		}
	}

	private static string[] ExtractValues(HintParameter[]? parameters)
	{
		if (parameters == null || parameters.Length == 0)
		{
			return Array.Empty<string>();
		}
		string[] array = new string[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
		{
			array[i] = FormatParameter(parameters[i]);
		}
		return array;
	}

	private static string FormatParameter(HintParameter parameter)
	{
		try
		{
			parameter.Update(0f);
			string formatted = parameter.Formatted;
			if (!string.IsNullOrEmpty(formatted))
			{
				return formatted;
			}
		}
		catch (Exception)
		{
		}
		return FormatValue(ReadValue(parameter));
	}

	private static object? ReadValue(HintParameter parameter)
	{
		Type type = parameter.GetType();
		while (type != null && type != typeof(object) && type != typeof(HintParameter))
		{
			PropertyInfo? property = type.GetProperty("Value", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null)
			{
				return property.GetValue(parameter);
			}
			type = type.BaseType;
		}
		return null;
	}

	private static string FormatValue(object? value)
	{
		if (value == null)
		{
			return string.Empty;
		}
		if (value is string text)
		{
			return text;
		}
		if (value is float number)
		{
			return number.ToString("0.##", CultureInfo.InvariantCulture);
		}
		if (value is IFormattable formattable)
		{
			return formattable.ToString(null, CultureInfo.InvariantCulture);
		}
		return value.ToString() ?? string.Empty;
	}
}
