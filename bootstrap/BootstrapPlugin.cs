using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using System;
using Logger = LabApi.Features.Console.Logger;

namespace HintIsolation.Bootstrap;

/// <summary>
/// <b>HintIsolation 抢先引导器(0 前缀)。</b>
///
/// <para>本插件由主 DLL <c>HintIsolation.dll</c> 从内嵌资源释放为
/// <c>plugins\global\0HintIsolation.Bootstrap.dll</c>。文件名以 <c>0</c> 开头,
/// 在 LabAPI "同优先级按枚举顺序"的规则下排在前面, 加上
/// <see cref="LoadPriority.Highest"/>, 从而<b>最先 Enable</b>。</para>
///
/// <para>三步流程(与需求一一对应):</para>
/// <list type="number">
///   <item><b>优先加载</b> —— 释放 + 0 前缀 + Highest 优先级, 抢在最前;</item>
///   <item><b>提示与改写</b> —— 首次运行弹出"请重启服务器"大号横幅, 并把参数
///     <c>Prompted</c> 由 0 改写为 1(此后不再提醒);</item>
///   <item><b>驱动辅助</b> —— 反射驱动主 DLL 的抢先安装入口, 把 UI 拦截补丁
///     装在其它插件 Enable 之前。</item>
/// </list>
/// </summary>
public sealed class BootstrapPlugin : Plugin
{
    public override string Name => "HintIsolation.Bootstrap";

    public override string Description => "HintIsolation 抢先引导器: 0 前缀 + Highest 优先级, 抢在其它插件前装载 UI 拦截";

    public override string Author => "LabAPI-Docs";

    public override Version RequiredApiVersion => LabApiProperties.CurrentVersion;

    /// <summary>最高优先级 —— 与 <c>0</c> 前缀双保险, 确保最先 Enable。</summary>
    public override LoadPriority Priority => LoadPriority.Highest;

    /// <summary>引导器本身不改变游戏行为, 声明为透明。</summary>
    public override bool IsTransparent => true;

    public override void Enable()
    {
        BootstrapState state = BootstrapState.Load();
        state.MarkRun();

        // ── 第 2 步: 提示与改写参数 ──
        if (state.Prompted == 0)
        {
            BootstrapBanner.PrintFirstRun();
            state.Prompted = 1;
            state.Save();
        }

        // ── 第 3 步: 驱动辅助主 DLL ──
        string detail;
        try
        {
            detail = MainBridge.InvokeEarlyInstall();
        }
        catch (Exception e)
        {
            detail = "抢先安装入口调用失败(主插件自身仍会正常安装): " + e.Message;
        }

        BootstrapBanner.PrintActive(detail, state.Runs);
        state.Save();
    }

    public override void Disable()
    {
        // 引导器不持有资源; 拦截层的卸载由主插件负责。
    }
}
