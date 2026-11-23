using System;
using System.Collections.Generic;
using System.Linq;
using HintChorus.Core.Broker;

namespace HintChorus.Core.Compat;

/// <summary>一个被识别到的主流 Hint 插件。</summary>
public sealed class MainstreamPlugin
{
    /// <summary>用于切换回退的插件标识(优先取真实调用方 ID)。</summary>
    public string Id = string.Empty;

    /// <summary>界面上显示的主流名称(如 HintServiceMeow)。</summary>
    public string DisplayName = string.Empty;

    /// <summary>识别来源: 已加载程序集 / 已发过提示。</summary>
    public string Source = string.Empty;

    /// <summary>该主流插件名下所有真实调用方 ID(切换时全部生效)。</summary>
    public string[] RawIds = Array.Empty<string>();

    /// <summary>当前是否已被切到原生直通(任一 RawId 命中即为真)。</summary>
    public bool Native;
}

/// <summary>
/// <b>主流 Hint 插件识别</b>。
///
/// <para>用于 <c>Hint orvs</c>: 先看当前服务器有没有装主流 Hint 插件;
/// 一个都没有时不再只回一句"没有插件", 而是明确告诉服主, 并提供编号 1 —— 直接退回原生。</para>
///
/// <para>识别分两路: 已经实际发过提示的插件(优先, 因为它的 ID 与归因一致) + 已加载程序集名。
/// 只认 Hint 生态里的主流名字, 不把任何未知插件硬说成主流。</para>
/// </summary>
public static class MainstreamHintPlugins
{
    private static readonly (string Prefix, string Name)[] Known =
    {
        ("HintServiceMeow", "HintServiceMeow"),
        ("RueI", "RueI"),
        ("HintService", "HintService"),
        ("ScpSlHints", "ScpSlHints"),
        ("HintSystem", "HintSystem")
    };

    /// <summary>当前识别到的主流 Hint 插件(按名称排序)。</summary>
    public static IReadOnlyList<MainstreamPlugin> Detect()
    {
        Dictionary<string, MainstreamPlugin> found =
            new Dictionary<string, MainstreamPlugin>(StringComparer.OrdinalIgnoreCase);

        // ① 已经发过提示的插件: 它的 ID 就是归因用的 ID, 回退时必须用它, 否则切了不生效。
        try
        {
            foreach (string candidate in HintFallback.Candidates())
            {
                if (string.IsNullOrWhiteSpace(candidate) || IsSelf(candidate))
                {
                    continue;
                }

                string display = ResolveDisplay(candidate);
                if (display.Length == 0)
                {
                    continue;
                }

                if (!found.TryGetValue(display, out MainstreamPlugin plugin))
                {
                    plugin = new MainstreamPlugin
                    {
                        Id = candidate,
                        DisplayName = display,
                        Source = "已发过提示"
                    };
                    found[display] = plugin;
                }
                plugin.RawIds = plugin.RawIds.Append(candidate).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
        catch (Exception)
        {
        }

        // ② 只加载、还没发过提示的插件: 按程序集名识别, 作为兜底信息展示。
        try
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name ?? string.Empty;
                if (name.Length == 0 || IsSelf(name))
                {
                    continue;
                }

                string display = ResolveDisplay(name);
                if (display.Length == 0)
                {
                    continue;
                }

                if (!found.TryGetValue(display, out MainstreamPlugin plugin))
                {
                    plugin = new MainstreamPlugin
                    {
                        Id = name,
                        DisplayName = display,
                        Source = "程序集 " + name
                    };
                    found[display] = plugin;
                }
                plugin.RawIds = plugin.RawIds.Append(name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
        catch (Exception)
        {
        }

        foreach (MainstreamPlugin plugin in found.Values)
        {
            plugin.Native = plugin.RawIds.Any(HintFallback.IsNative);
        }

        return found.Values
            .OrderBy((MainstreamPlugin p) => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>这个名字是否属于已知主流 Hint 插件。</summary>
    public static bool IsMainstream(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }
        for (int i = 0; i < Known.Length; i++)
        {
            if (id.StartsWith(Known[i].Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>把插件 ID 归到主流名称; 不是主流则返回空串。</summary>
    private static string ResolveDisplay(string id)
    {
        for (int i = 0; i < Known.Length; i++)
        {
            if (id.StartsWith(Known[i].Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Known[i].Name;
            }
        }
        return string.Empty;
    }

    private static bool IsSelf(string name)
    {
        return name.Equals("HintChorus", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("HintChorus.", StringComparison.OrdinalIgnoreCase);
    }
}
