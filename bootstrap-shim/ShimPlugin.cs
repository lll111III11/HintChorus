using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LabApi.Features;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Shim;

/// <summary>
/// <b>永久引导 shim</b>（释放为 <c>0.dll</c>）。
///
/// <para>它不包含任何业务逻辑，只负责在 LabAPI 的插件目录里抢到第一个位置，
/// 然后把 <c>HintChorus.Bootstrap.payload</c> 从字节载入并调用其入口。</para>
///
/// <para>这样 <c>0.dll</c> 自身永远不需要更新：Windows 会映射正在运行的插件文件，
/// 但 payload 不是以插件文件形式加载的，所以可以随时替换。</para>
/// </summary>
public sealed class ShimPlugin : Plugin
{
    private const string PayloadFileName = "HintChorus.Bootstrap.payload";

    private const string PayloadAssemblyName = "HintChorus.Bootstrap.Payload";

    private const string EntryTypeName = "HintChorus.Bootstrap.BootstrapEntry";

    private static bool _ran;

    public override string Name => "HintChorus.Bootstrap";

    public override string Description => "HintChorus 永久引导 shim: 加载外部 payload, 抢在其它插件前安装 UI 拦截";

    public override string Author => "LabAPI-Docs";

    public override Version RequiredApiVersion => LabApiProperties.CurrentVersion;

    /// <summary>最高优先级 —— 与 <c>0</c> 前缀双保险, 确保最先 Enable。</summary>
    public override LoadPriority Priority => LoadPriority.Highest;

    public override bool IsTransparent => true;

    public override void Enable()
    {
        if (_ran)
        {
            return;
        }
        _ran = true;

        try
        {
            string path = Path.Combine(PathManager.Plugins.FullName, "global", PayloadFileName);
            if (!File.Exists(path))
            {
                Logger.Error("[HintChorus] 引导 payload 不存在: " + path + " (主插件会在下次启动时重新释放)");
                return;
            }

            Assembly payload = FindLoadedPayload() ?? Assembly.Load(File.ReadAllBytes(path));
            Type? entry = payload.GetType(EntryTypeName, false);
            MethodInfo? run = entry?.GetMethod(
                "Run",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (run == null)
            {
                Logger.Error("[HintChorus] 引导 payload 缺少入口 " + EntryTypeName + ".Run");
                return;
            }
            run.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Logger.Error("[HintChorus] 加载引导 payload 失败: " + ex);
        }
    }

    public override void Disable()
    {
        // shim 不持有资源; 拦截层的卸载由主插件负责。
    }

    private static Assembly? FindLoadedPayload()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => !a.IsDynamic
                && string.Equals(a.GetName().Name, PayloadAssemblyName, StringComparison.Ordinal));
    }
}
