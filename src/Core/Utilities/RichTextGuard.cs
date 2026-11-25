using System;
using System.Collections.Generic;
using System.Text;

namespace HintChorus.Core.Utilities;

/// <summary>
/// <b>富文本闭合</b>：把文本里未闭合的 Unity 富文本标签补上闭合标签。
///
/// <para>外部插件常常只写 <c>&lt;color=#ff0000&gt;警告</c> 而不写 <c>&lt;/color&gt;</c>，
/// 颜色/字号就会泄漏到同一行后面的内容、控制台以及下一个插件的文本上。
/// 本类在文本进入 UI 之前统一收尾：按出现顺序压栈，结束时倒序补齐闭合标签。</para>
///
/// <para>只处理已知标签（b/i/u/s/color/size/material/quad/mark/sup/sub 等），
/// 位置类标签（voffset/pos/line-height 等）原样放过，不做补齐。</para>
/// </summary>
internal static class RichTextGuard
{
    /// <summary>是否启用闭合（配置项 CloseRichTextTags）。</summary>
    internal static bool Enabled { get; set; } = true;

    private static readonly string[] Known =
    {
        "b", "i", "u", "s", "color", "size", "material", "quad", "mark", "sup", "sub"
    };

    internal static string EnsureClosed(string? text)
    {
        if (!Enabled || string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
        {
            return text ?? string.Empty;
        }

        try
        {
            List<string> open = new List<string>(4);
            int index = 0;
            while (index < text.Length)
            {
                int start = text.IndexOf('<', index);
                if (start < 0)
                {
                    break;
                }
                int end = text.IndexOf('>', start + 1);
                if (end < 0)
                {
                    break;
                }

                string inner = text.Substring(start + 1, end - start - 1).Trim();
                index = end + 1;
                if (inner.Length == 0)
                {
                    continue;
                }

                if (inner[0] == '/')
                {
                    string closing = inner.Substring(1).Trim();
                    for (int i = open.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(TagName(open[i]), closing, StringComparison.OrdinalIgnoreCase))
                        {
                            open.RemoveAt(i);
                            break;
                        }
                    }
                    continue;
                }

                if (inner.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                string name = TagName(inner);
                if (Array.IndexOf(Known, name) < 0)
                {
                    continue;
                }
                open.Add(inner);
            }

            if (open.Count == 0)
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text, text.Length + open.Count * 8);
            for (int i = open.Count - 1; i >= 0; i--)
            {
                builder.Append("</").Append(TagName(open[i])).Append('>');
            }
            return builder.ToString();
        }
        catch (Exception)
        {
            return text;
        }
    }

    private static string TagName(string inner)
    {
        int eq = inner.IndexOf('=');
        string name = eq >= 0 ? inner.Substring(0, eq) : inner;
        return name.Trim().ToLowerInvariant();
    }
}
