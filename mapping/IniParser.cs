using System;
using System.Collections.Generic;

namespace HintChorus.Mapping;

/// <summary>极小的 INI 读取器: 只支持 [section] 与 key=value, 避免依赖任何 JSON 库。</summary>
internal static class IniParser
{
    internal static Dictionary<string, Dictionary<string, string>> Parse(string text)
    {
        Dictionary<string, Dictionary<string, string>> result =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        Dictionary<string, string>? current = null;
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (string raw in normalized.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';')
            {
                continue;
            }
            if (line[0] == '[')
            {
                int end = line.IndexOf(']');
                if (end <= 1)
                {
                    continue;
                }
                string section = line.Substring(1, end - 1).Trim();
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[section] = current;
                continue;
            }
            if (current == null)
            {
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            string key = line.Substring(0, eq).Trim();
            string value = line.Substring(eq + 1).Trim();
            if (key.Length > 0)
            {
                current[key] = value;
            }
        }
        return result;
    }
}
