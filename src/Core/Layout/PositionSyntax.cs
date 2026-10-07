using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HintChorus.Core.Layout;

/// <summary>
/// <b>位置写法解析器</b> —— 同时承担「自有写法」与「兼容原有写法」两件事。
///
/// <list type="number">
///   <item>
///     <b>自有写法(文本标记)</b>: <c>{{hc:pos=top-right,offset=-90}}</c> / <c>{{hc:middle}}</c>。
///     解析后<b>从文本里剥掉</b>, 玩家绝对看不到; 与其它框架的 <c>{0}</c> 模板不冲突(本标记是双大括号)。
///   </item>
///   <item>
///     <b>兼容原有写法</b>: 插件文本里已经自带的 TMP 位置标签
///     (<c>&lt;voffset&gt;</c> / <c>&lt;pos&gt;</c> / <c>&lt;align&gt;</c> / <c>&lt;line-height&gt;</c> /
///     <c>&lt;margin&gt;</c> / <c>&lt;indent&gt;</c>) —— 一律判为「自带位置」,
///     由 <see cref="HintPosition.SelfPositioned"/> 原样放行, 不被本底层重排。
///   </item>
/// </list>
/// </summary>
public static class PositionSyntax
{
	/// <summary>自有写法标记: <c>{{hc:...}}</c>。</summary>
	private static readonly Regex MarkerRegex = new Regex(
		@"\{\{\s*hc\s*:\s*(?<body>[^{}]*)\}\}",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>兼容检测: 外来位置标签(含自闭合与成对)。</summary>
	private static readonly Regex ForeignTagRegex = new Regex(
		@"<\s*/?\s*(voffset|pos|align|line-height|margin|indent)\b[^>]*>",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>文本里有没有自有写法标记。</summary>
	public static bool HasMarker(string? text)
	{
		return !string.IsNullOrEmpty(text) && MarkerRegex.IsMatch(text);
	}

	/// <summary>文本里有没有外来位置标签(有 → 说明插件自己摆了位)。</summary>
	public static bool HasForeignPositionTags(string? text)
	{
		return !string.IsNullOrEmpty(text) && ForeignTagRegex.IsMatch(text);
	}

	/// <summary>
	/// 剥离自有写法标记并给出解析结果。
	/// </summary>
	/// <param name="text">原始提示文本。</param>
	/// <param name="parsed">
	/// 命中的位置(多个标记时以<b>最后一个</b>为准); 无标记时为 <c>null</c>。
	/// </param>
	/// <returns>剥掉标记后的可显示文本。</returns>
	public static string StripMarkers(string? text, out HintPosition? parsed)
	{
		parsed = null;
		if (string.IsNullOrEmpty(text))
		{
			return text ?? string.Empty;
		}

		if (!MarkerRegex.IsMatch(text))
		{
			return text;
		}

		foreach (Match match in MarkerRegex.Matches(text))
		{
			if (TryParseMarker(match.Groups["body"].Value, out HintPosition position))
			{
				parsed = position;
			}
		}

		return MarkerRegex.Replace(text, string.Empty);
	}

	/// <summary>
	/// 解析标记体。可写:
	/// <list type="bullet">
	///   <item><c>top-right</c> / <c>middle</c> / <c>右上</c>(直接写锚点);</item>
	///   <item><c>pos=top-right,offset=-90</c>(键值对, 偏移正=上移)。</item>
	/// </list>
	/// </summary>
	public static bool TryParseMarker(string? body, out HintPosition position)
	{
		position = HintPosition.Default;
		if (string.IsNullOrWhiteSpace(body))
		{
			return false;
		}

		HintAnchor? anchor = null;
		float offset = 0f;

		foreach (string rawPart in body.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string part = rawPart.Trim();
			if (part.Length == 0)
			{
				continue;
			}

			int eq = part.IndexOf('=');
			if (eq > 0)
			{
				string key = part.Substring(0, eq).Trim().ToLowerInvariant();
				string val = part.Substring(eq + 1).Trim();

				if ((key == "pos" || key == "anchor" || key == "位置") && HintPosition.TryParseAnchor(val, out HintAnchor a))
				{
					anchor = a;
				}
				else if ((key == "offset" || key == "voffset" || key == "y" || key == "偏移")
					&& float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float o))
				{
					offset = o;
				}

				continue;
			}

			if (HintPosition.TryParseAnchor(part, out HintAnchor direct))
			{
				anchor = direct;
			}
		}

		if (anchor is null)
		{
			return false;
		}

		position = new HintPosition(anchor.Value, offset, managed: true);
		return true;
	}
}
