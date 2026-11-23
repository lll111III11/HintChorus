using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace HintChorus.Mapping;

/// <summary>
/// 映射组件对外入口。全部方法都是静态、无参数类型依赖, 方便主插件用反射调用。
///
/// <para>职责只有三件: 读程序表、探测显示层、预热缓存。
/// 具体模板样式在后续版本释放, 这里不实现渲染。</para>
/// </summary>
public static class MappingHost
{
    public const string ComponentVersion = "0.1.0";

    private static readonly TemplateRegistry Registry = new TemplateRegistry();

    private static string _customDirectory = string.Empty;

    private static long _warmupMilliseconds;

    public static bool Initialized { get; private set; }

    public static bool DisplayKitFound => DisplayKitProbe.Found;

    public static int TemplateCount => Registry.Count;

    public static int CustomTemplateCount => Registry.CustomCount;

    public static string DefaultTemplate => Registry.DefaultName;

    public static long WarmupMilliseconds => _warmupMilliseconds;

    /// <summary>读内置程序表 + 自定义模板目录, 返回状态说明。</summary>
    public static string Initialize(string? customDirectory)
    {
        try
        {
            _customDirectory = customDirectory ?? string.Empty;
            Registry.LoadEmbedded();
            if (_customDirectory.Length > 0)
            {
                Directory.CreateDirectory(_customDirectory);
                Registry.LoadDirectory(_customDirectory);
            }
            Initialized = true;
            return Describe();
        }
        catch (Exception ex)
        {
            Initialized = false;
            return "映射组件初始化失败: " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    /// <summary>预热: 扫描显示层类型, 把结果缓存下来, 第一次显示不走冷启动。</summary>
    public static void Warmup()
    {
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            DisplayKitProbe.Scan();
        }
        catch (Exception)
        {
        }
        watch.Stop();
        _warmupMilliseconds = watch.ElapsedMilliseconds;
    }

    /// <summary>返回 "OK|名字|语言|布局|custom/builtin|说明", 找不到返回 "MISS|名字"。</summary>
    public static string Resolve(string? name)
    {
        TemplateDescriptor? descriptor = Registry.Resolve(name ?? string.Empty);
        if (descriptor == null)
        {
            return "MISS|" + (name ?? string.Empty);
        }
        return "OK|" + descriptor.Name + "|" + descriptor.Language + "|" + descriptor.Layout
            + "|" + (descriptor.IsCustom ? "custom" : "builtin") + "|" + descriptor.Description;
    }

    public static string ListTemplates()
    {
        return Registry.Names();
    }

    public static string Describe()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("组件 v").Append(ComponentVersion)
            .Append(" | 模板 ").Append(Registry.Count)
            .Append(" (自定义 ").Append(Registry.CustomCount).Append(')')
            .Append(" | 默认 ").Append(Registry.DefaultName)
            .Append(" | 列表 ").Append(Registry.Names())
            .Append(" | DisplayKit ").Append(DisplayKitProbe.Found ? "已找到" : "未找到")
            .Append(" | 预热 ").Append(_warmupMilliseconds).Append("ms")
            .Append(" | 自定义目录 ").Append(_customDirectory.Length == 0 ? "(未设置)" : _customDirectory);
        if (DisplayKitProbe.Found && DisplayKitProbe.TypeNames.Count > 0)
        {
            builder.Append("\nDisplayKit 类型: ").Append(string.Join(", ", DisplayKitProbe.TypeNames));
        }
        return builder.ToString();
    }
}
