using LabApi.Features.Console;
using System;
using System.Linq;
using System.Reflection;
using Logger = LabApi.Features.Console.Logger;

namespace HintIsolation.Bootstrap;

/// <summary>
/// 主 DLL 桥接器 —— <b>刻意用反射</b>, 不引用主程序集。
///
/// <para>原因: LabAPI 用 <c>Assembly.Load(bytes)</c> 逐个载入插件, 插件目录不在
/// 程序集探测路径上。若引导器直接引用主 DLL 的类型, 运行时会因解析不到而抛异常。
/// 反射查找"已载入的程序集"既绕开这个坑, 又不受加载顺序影响。</para>
///
/// <para>关键前提(已对 LabAPI 源码核实): <c>PluginLoader.LoadAllPlugins</c>
/// 先对**所有**插件文件执行 <c>Assembly.Load</c>, 之后才逐个 <c>Enable()</c>。
/// 所以引导器抢先 <c>Enable</c> 时, 主 DLL 的程序集已经在内存里了 —— 这正是
/// "抢在其它插件之前把补丁装好" 能成立的技术基础。</para>
/// </summary>
internal static class MainBridge
{
    private const string MainAssemblyName = "HintIsolation";
    private const string BridgeTypeName = "HintIsolation.Core.Bootstrap.BootstrapBridge";
    private const string EntryMethodName = "EarlyInstall";

    /// <summary>主程序集是否已载入。</summary>
    public static bool IsMainLoaded => FindMainAssembly() != null;

    /// <summary>
    /// 反射调用主 DLL 的抢先安装入口。
    /// </summary>
    /// <returns>执行结果说明(用于日志)。</returns>
    public static string InvokeEarlyInstall()
    {
        Assembly? main = FindMainAssembly();
        if (main is null)
        {
            return "主程序集尚未载入, 本轮由主插件自身完成安装";
        }

        Type? bridge = main.GetType(BridgeTypeName, throwOnError: false);
        if (bridge is null)
        {
            return $"主程序集内未找到 {BridgeTypeName}";
        }

        MethodInfo? entry = bridge.GetMethod(EntryMethodName,
            BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        if (entry is null)
        {
            return $"主程序集内未找到 {EntryMethodName} 入口";
        }

        object? result = entry.Invoke(null, null);
        return result as string ?? "已调用抢先安装入口";
    }

    private static Assembly? FindMainAssembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => !a.IsDynamic
                                 && string.Equals(a.GetName().Name, MainAssemblyName, StringComparison.Ordinal));
    }
}
