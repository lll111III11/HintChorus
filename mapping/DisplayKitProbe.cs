using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace HintChorus.Mapping;

/// <summary>
/// 反射探测 DisplayKit / 旧 Hint 的显示类型。
/// 不编译期引用任何游戏程序集, 14.2.x 探测不到时保持沉默。
/// </summary>
internal static class DisplayKitProbe
{
    private static readonly List<string> FoundTypes = new List<string>();

    internal static bool Found { get; private set; }

    internal static long ElapsedMilliseconds { get; private set; }

    internal static IReadOnlyList<string> TypeNames => FoundTypes;

    internal static void Scan()
    {
        FoundTypes.Clear();
        Stopwatch watch = Stopwatch.StartNew();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!ShouldScan(assembly))
            {
                continue;
            }
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch (Exception)
            {
                continue;
            }
            foreach (Type type in types)
            {
                if (type == null)
                {
                    continue;
                }
                string? full = type.FullName;
                if (string.IsNullOrEmpty(full))
                {
                    continue;
                }
                if (full.IndexOf("DisplayCanvas", StringComparison.Ordinal) < 0
                    && full.IndexOf("DisplayText", StringComparison.Ordinal) < 0
                    && full.IndexOf("DisplayKit", StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                FoundTypes.Add(full);
            }
        }
        watch.Stop();
        ElapsedMilliseconds = watch.ElapsedMilliseconds;
        Found = FoundTypes.Count > 0;
    }

    private static bool ShouldScan(Assembly assembly)
    {
        if (assembly.IsDynamic)
        {
            return false;
        }
        string name = assembly.GetName().Name ?? string.Empty;
        if (string.Equals(name, "HintChorus.Mapping", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Mono", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Unity", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }
}
