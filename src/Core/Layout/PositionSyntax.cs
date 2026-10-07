using System;
using System.Text.RegularExpressions;

namespace HintChorus.Core.Layout;

/// <summary>
/// <b>位置写法解析器</b> —— 同时承担「自有写法」与「兼容原有写法」两件事。
///
/// <list type="number">
///   <item>
///     <b>自有写法(文本标记)</b>: <c>{{hc:pos=top-right,offset=-90}}</c> / <c>{{hc:750}}</c>。
///     标记体交由 <see cref="HintPosition.TryParse"/> 解析 —— 与 C# 字符串 API <b>完全同一套语法</b>。
///     解析后<b>从文本里剥掉</b>, 玩家绝对看不到; 与其它框架的 <c>{0}</c> 模板不冲突(本标记是双大括号)。
///   </item>
///   <item>
///     <b>兼容原有写法</b>: 插件文本里已经自带的 TMP 位置标签
///     (<c>&lt;voffset&gt;</c> / <c>&lt;pos&gt;</c> / <c>&lt;align&gt;</c> / <c>&lt;line-height&gt;</c> /
///     <c>&lt;line-indent&gt;</c> / <c>&lt;indent&gt;</c> / <c>&lt;margin&gt;</c>) —— 一律判为「自带位置」,
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

	/// <summary>
	/// 兼容检测: 外来<b>位置</b>标签(含自闭合与成对)。
	/// <para>这份名单取自生态实况 —— 与 <b>HintServiceMeow</b> 的标签白名单
	/// (<c>align/indent/line-height/line-indent/margin/pos/voffset/…</c>) 中"会改变落点"的那些对齐;
	/// <c>size</c> / <c>color</c> / <c>alpha</c> 等纯样式标签<b>不算</b>位置(用了它们仍由本底层摆位)。</para>
	/// </summary>
	private static readonly Regex ForeignTagRegex = new Regex(
		@"<\s*/?\s*(voffset|pos|align|line-height|line-indent|indent|margin)\b[^>]*>",
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
	/// 命中的位置(多个标记时以<b>最后一个</b>为准); 无标记或标记无效时为 <c>null</c>。
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
	/// 解析标记体 —— <b>直接复用自有写法的统一语法</b>(见 <see cref="HintPosition.TryParse"/>)。
	///
	/// <para>可写: <c>top-right</c> / <c>middle</c> / <c>右上</c> / <c>750</c>(0–1000 标尺) /
	/// <c>pos=750,align=left,offset=-90</c>。</para>
	/// </summary>
	public static bool TryParseMarker(string? body, out HintPosition position)
	{
		return HintPosition.TryParse(body, out position);
	}
}
