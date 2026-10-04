using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Hints;

namespace HintIsolation.Core.Compat;

public static class NativeHintTranslator
{
	private sealed record TemplatePair(string En, string Zh);

	private const string GameHintsRelativePath = "..\\..\\Translations\\en\\GameHints.txt";

	private static readonly Regex TokenRegex = new Regex("\\[[a-zA-Z_]+]", RegexOptions.Compiled);

	private static readonly Dictionary<byte, TemplatePair> Embedded = new Dictionary<byte, TemplatePair>
	{
		{
			0,
			new TemplatePair("<color=red>Access denied</color>", "<color=red>访问被拒绝</color>")
		},
		{
			1,
			new TemplatePair("Reached the limit of <color=yellow>[type] ammo</color> (<color=yellow>[max_type_count] rounds</color>).", "已达到 <color=yellow>[type] 弹药</color> 的上限（<color=yellow>[max_type_count] 发</color>）。")
		},
		{
			2,
			new TemplatePair("<b>Already</b> reached the limit of <color=yellow>[type] ammo</color> (<color=yellow>[max_type_count] rounds</color>).", "已<b>达到</b> <color=yellow>[type] 弹药</color> 的上限（<color=yellow>[max_type_count] 发</color>）。")
		},
		{
			3,
			new TemplatePair("Reached the limit of <color=yellow>[type]</color> (<color=yellow>[max_type_count] items</color>).", "已达到 <color=yellow>[type]</color> 的可持有上限（<color=yellow>[max_type_count] 个</color>）。")
		},
		{
			4,
			new TemplatePair("<b>Already</b> reached the limit of <color=yellow>[type]</color> (<color=yellow>[max_type_count] items</color>).", "已<b>达到</b> <color=yellow>[type]</color> 的可持有上限（<color=yellow>[max_type_count] 个</color>）。")
		},
		{
			5,
			new TemplatePair("Only <color=yellow>[max_item_count] items</color> can be carried.", "最多只能携带 <color=yellow>[max_item_count] 个</color> 物品。")
		}
	};

	private static readonly Dictionary<byte, string> ServerEnglish = new Dictionary<byte, string>();

	private static bool _loaded;

	private static Func<Hint, HintParameter[]?>? _parametersGetter;

	private static Func<TranslationHint, HintTranslations>? _translationGetter;

	public static bool IsLoaded => _loaded;

	public static void EnsureLoaded()
	{
		if (!_loaded)
		{
			_loaded = true;
			BindGetters();
			LoadServerFile();
		}
	}

	public static bool TryTranslate(TranslationHint hint, string language, out string text)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected I4, but got Unknown
		text = string.Empty;
		EnsureLoaded();
		try
		{
			if (_translationGetter == null)
			{
				return false;
			}
			string text2 = PickTemplate((byte)(int)_translationGetter(hint), language);
			if (text2 == null)
			{
				return false;
			}
			HintParameter[] parameters = _parametersGetter?.Invoke((Hint)(object)hint);
			string[] values = ExtractValues(parameters);
			if (TokenRegex.Matches(text2).Count > values.Length)
			{
				return false;
			}
			int index = 0;
			text = TokenRegex.Replace(text2, (Match match) => (index >= values.Length) ? match.Value : values[index++]);
			return !string.IsNullOrWhiteSpace(text);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string? PickTemplate(byte key, string language)
	{
		if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
		{
			if (ServerEnglish.TryGetValue(key, out string value))
			{
				return value;
			}
			if (!Embedded.TryGetValue(key, out TemplatePair value2))
			{
				return null;
			}
			return value2.En;
		}
		if (Embedded.TryGetValue(key, out TemplatePair value3) && !string.IsNullOrEmpty(value3.Zh))
		{
			return value3.Zh;
		}
		if (ServerEnglish.TryGetValue(key, out string value4))
		{
			return value4;
		}
		if (!Embedded.TryGetValue(key, out TemplatePair value5))
		{
			return null;
		}
		return value5.En;
	}

	private static void LoadServerFile()
	{
		try
		{
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\Translations\\en\\GameHints.txt");
			if (!File.Exists(path))
			{
				return;
			}
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				string value = array[i]?.Trim();
				if (!string.IsNullOrEmpty(value))
				{
					ServerEnglish[(byte)i] = value;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void BindGetters()
	{
		try
		{
			MethodInfo methodInfo = AccessTools.Property(typeof(Hint), "Parameters")?.GetGetMethod(nonPublic: true);
			_parametersGetter = ((methodInfo != null) ? ((Func<Hint, HintParameter[]>)Delegate.CreateDelegate(typeof(Func<Hint, HintParameter[]>), methodInfo)) : null);
			MethodInfo methodInfo2 = AccessTools.Property(typeof(TranslationHint), "Translation")?.GetGetMethod(nonPublic: true);
			_translationGetter = ((methodInfo2 != null) ? ((Func<TranslationHint, HintTranslations>)Delegate.CreateDelegate(typeof(Func<TranslationHint, HintTranslations>), methodInfo2)) : null);
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
		Type type = ((object)parameter).GetType();
		while (type != null && type != typeof(object) && type != typeof(HintParameter))
		{
			PropertyInfo property = type.GetProperty("Value", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
		if (value is string result)
		{
			return result;
		}
		if (value is float num)
		{
			return num.ToString("0.##", CultureInfo.InvariantCulture);
		}
		if (value is IFormattable formattable)
		{
			return formattable.ToString(null, CultureInfo.InvariantCulture);
		}
		return value.ToString() ?? string.Empty;
	}
}
