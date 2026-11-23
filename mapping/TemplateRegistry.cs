using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace HintChorus.Mapping;

/// <summary>
/// 模板程序表: 内置 + 自定义目录合并, 自定义同名覆盖内置。
/// 只记录名字/语言/布局/说明, 具体样式由后续版本释放。
/// </summary>
internal sealed class TemplateRegistry
{
    private const string EmbeddedResource = "HintChorus.Mapping.template-map.ini";

    private readonly Dictionary<string, TemplateDescriptor> _templates =
        new Dictionary<string, TemplateDescriptor>(StringComparer.OrdinalIgnoreCase);

    internal string DefaultName { get; private set; } = "zh";

    internal int Count => _templates.Count;

    internal int CustomCount { get; private set; }

    internal void LoadEmbedded()
    {
        Assembly assembly = typeof(TemplateRegistry).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(EmbeddedResource);
        if (stream == null)
        {
            return;
        }
        using StreamReader reader = new StreamReader(stream);
        LoadText(reader.ReadToEnd(), "(builtin)", false);
    }

    internal void LoadDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }
        foreach (string file in Directory.GetFiles(directory, "*.ini", SearchOption.TopDirectoryOnly))
        {
            try
            {
                LoadText(File.ReadAllText(file), file, true);
            }
            catch (Exception)
            {
                // 单个自定义模板坏掉不影响其它模板
            }
        }
    }

    private void LoadText(string text, string source, bool custom)
    {
        Dictionary<string, Dictionary<string, string>> sections = IniParser.Parse(text);
        Dictionary<string, string>? meta;
        if (sections.TryGetValue("meta", out meta))
        {
            string value;
            if (!custom && meta.TryGetValue("default", out value) && value.Length > 0)
            {
                DefaultName = value;
            }
        }

        foreach (KeyValuePair<string, Dictionary<string, string>> pair in sections)
        {
            if (string.Equals(pair.Key, "meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            Dictionary<string, string> values = pair.Value;
            if (!custom && _templates.ContainsKey(pair.Key))
            {
                continue;
            }
            if (custom && !_templates.ContainsKey(pair.Key))
            {
                CustomCount++;
            }
            _templates[pair.Key] = new TemplateDescriptor
            {
                Name = pair.Key,
                Language = Get(values, "language", DefaultName),
                Layout = Get(values, "layout", "rows"),
                Description = Get(values, "description", ""),
                SourceFile = source,
                IsCustom = custom
            };
        }
    }

    internal TemplateDescriptor? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        TemplateDescriptor? descriptor;
        return _templates.TryGetValue(name.Trim(), out descriptor) ? descriptor : null;
    }

    internal string Names()
    {
        List<string> names = new List<string>(_templates.Keys);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", names);
    }

    private static string Get(Dictionary<string, string> values, string key, string fallback)
    {
        string value;
        return values.TryGetValue(key, out value) && value.Length > 0 ? value : fallback;
    }
}
