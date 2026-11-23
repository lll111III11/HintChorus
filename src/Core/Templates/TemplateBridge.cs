using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using HintChorus.Core.Diagnostics;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Templates;

/// <summary>
/// 主插件与映射组件（HintChorus.Mapping）之间的桥。
///
/// <para>刻意全部用反射：映射组件只在下一次重启时才会被 LabAPI 预加载，
/// 首次运行时主插件不能硬引用它，否则会因找不到程序集而加载失败。</para>
///
/// <para>默认不启用：配置里 <c>Template</c> 为空就完全不碰映射组件，
/// 保持现有排版与行为。</para>
/// </summary>
internal static class TemplateBridge
{
    private const string AssemblyName = "HintChorus.Mapping";

    private const string HostTypeName = "HintChorus.Mapping.MappingHost";

    private const string DllFileName = "HintChorus.Mapping.dll";

    private static Assembly? _assembly;

    private static Type? _host;

    internal static bool IsAvailable => _host != null;

    internal static string Status { get; private set; } = "未启用(未配置模板)";

    internal static string TemplateName { get; private set; } = string.Empty;

    internal static void Install(string templateName)
    {
        TemplateName = templateName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            Status = "未启用(未配置模板)";
            return;
        }

        try
        {
            _assembly = FindOrLoad();
            if (_assembly == null)
            {
                Status = "映射组件未找到(检查 dependencies\\global\\" + DllFileName + ")";
                Logger.Warn("[HintChorus] 已配置模板 " + TemplateName + ", 但" + Status);
                return;
            }

            _host = _assembly.GetType(HostTypeName, false);
            if (_host == null)
            {
                Status = "映射组件缺少入口类型 " + HostTypeName;
                Logger.Warn("[HintChorus] " + Status);
                return;
            }

            string customDirectory = Path.Combine(
                PathManager.Configs.FullName,
                Server.Port.ToString(CultureInfo.InvariantCulture),
                "HintChorus",
                "templates");

            Invoke<string>("Initialize", new object?[] { customDirectory });
            Invoke<object>("Warmup", null);
            string resolved = Invoke<string>("Resolve", new object?[] { TemplateName }) ?? ("MISS|" + TemplateName);
            string describe = Invoke<string>("Describe", null) ?? string.Empty;

            bool found = resolved.StartsWith("OK|", StringComparison.Ordinal);
            Status = (found ? "已解析 " : "未找到 ") + resolved
                + (describe.Length > 0 ? "\n" + describe : string.Empty);

            if (found)
            {
                Logger.Info("[HintChorus] 模板: " + Status);
            }
            else
            {
                Logger.Warn("[HintChorus] 模板 " + TemplateName + " 不存在, 保持现有排版。可用: "
                    + (Invoke<string>("ListTemplates", null) ?? "(无)"));
            }
        }
        catch (Exception ex)
        {
            Status = "模板初始化失败: " + ex.GetType().Name + ": " + ex.Message;
            Logger.Error("[HintChorus] " + Status);
            UiLog.Report("TemplateBridge", ex);
        }
    }

    private static Assembly? FindOrLoad()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic
                && string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.Ordinal))
            {
                return assembly;
            }
        }

        try
        {
            string path = Path.Combine(PathManager.Dependencies.FullName, "global", DllFileName);
            if (File.Exists(path))
            {
                return Assembly.Load(File.ReadAllBytes(path));
            }
        }
        catch (Exception ex)
        {
            UiLog.Report("TemplateBridgeLoad", ex);
        }
        return null;
    }

    private static T? Invoke<T>(string methodName, object?[]? args)
    {
        if (_host == null)
        {
            return default;
        }
        MethodInfo? method = _host.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            return default;
        }
        object? result = method.Invoke(null, args);
        return result is T value ? value : default;
    }
}
