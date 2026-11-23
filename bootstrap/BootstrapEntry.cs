using System;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Bootstrap;

/// <summary>
/// <b>引导 payload 入口</b>。
///
/// <para>本程序集不注册成 LabAPI 插件，而是由永久 shim（<c>0.dll</c>）从
/// <c>HintChorus.Bootstrap.payload</c> 以字节载入后调用 <see cref="Run"/>。</para>
///
/// <para>三步流程：</para>
/// <list type="number">
///   <item>释放映射组件到 <c>dependencies\global</c>；</item>
///   <item>首次运行时提示并改写 <c>Prompted</c>；</item>
///   <item>反射驱动主 DLL 的抢先安装入口，把 UI 拦截补丁装在其它插件 Enable 之前。</item>
/// </list>
/// </summary>
public static class BootstrapEntry
{
    private static bool _ran;

    public static void Run()
    {
        if (_ran)
        {
            return;
        }
        _ran = true;

        BootstrapState state = BootstrapState.Load();
        state.MarkRun();

        Version version = typeof(BootstrapEntry).Assembly.GetName().Version ?? new Version(0, 0);
        Logger.Info("[HintChorus] 引导 payload v" + version + " 已载入");

        // ── 释放映射组件到 dependencies(下一次重启由 LabAPI 预加载) ──
        string mappingDetail = MappingInstaller.EnsureReleased();
        Logger.Info("[HintChorus] " + mappingDetail);

        // ── 首次运行提示与参数改写 ──
        if (state.Prompted == 0)
        {
            BootstrapBanner.PrintFirstRun();
            state.Prompted = 1;
            state.Save();
        }

        // ── 驱动辅助主 DLL ──
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
}
