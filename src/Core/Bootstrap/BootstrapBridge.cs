using System;
using HintChorus.Core.Interception;
using HintChorus.Core.Layout;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Bootstrap;

public static class BootstrapBridge
{
	private static int InstallOrder;

	public static bool EarlyInstalled { get; private set; }

	public static int EarlyInstallOrder { get; private set; }

	public static string EarlyInstall()
	{
		if (EarlyInstalled)
		{
			return "抢先安装此前已完成, 本次跳过";
		}
		try
		{
			StartupLog.Detect();
			string text = EnsureHome();
			TextMetrics.Load();
			HintChorusPlugin.PluginConfig pluginConfig = BootstrapConfigurator.TryLoadFromDisk();
			if (pluginConfig == null)
			{
				EarlyInstalled = true;
				return Finish(text + " | 尚未生成 config.yml, 交由主插件自行安装");
			}
			if (!pluginConfig.InterceptThirdPartyUi)
			{
				EarlyInstalled = true;
				return Finish(text + " | 配置中拦截层未开启, 无需抢先安装");
			}
			BootstrapConfigurator.Apply(pluginConfig);
			UiInterception.Instance.Install();
			EarlyInstalled = true;
			EarlyInstallOrder = ++InstallOrder;
			StartupLog.Info($"[抢先加载] 已在其它插件 Enable 之前装好 UI 拦截补丁(顺序 #{EarlyInstallOrder}): " + "提示条 + 屏幕广播 + 玩家控制台 + CASSIE + 管理端聊天 + 命中标记");
			return Finish($"{text} | 已在其它插件之前安装 UI 拦截补丁(顺序 #{EarlyInstallOrder})");
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintChorus] 抢先安装失败(主插件随后仍会正常安装): " + ex));
			return "抢先安装异常: " + ex.Message;
		}
	}

	private static string Finish(string detail)
	{
		StartupLog.MarkStarted();
		StartupLog.PrintSummary("[HintChorus] UI 通道已就绪 —— 提示条 / 广播 / 控制台 / CASSIE / 管理端聊天 / 命中标记 + 设置页");
		return detail;
	}

	public static string EnsureHome()
	{
		if (RuntimeHome.IsReady)
		{
			return "自宿主目录已就绪";
		}
		bool flag = RuntimeHome.EnsureCreated();
		RuntimeHome.InstallResolver();
		int count = RuntimeHome.DiscoverModules().Count;
		if (!RuntimeHome.IsReady)
		{
			return "自宿主目录创建失败";
		}
		StartupLog.Info("[HintChorus] 自宿主目录" + (flag ? "已创建" : "已就绪") + ": " + RuntimeHome.HomePath + $" (模块 {count} 个, 装配解析已挂载)");
		if (!flag)
		{
			return $"自宿主目录已就绪(模块 {count} 个)";
		}
		return $"已创建自宿主目录(模块 {count} 个)";
	}
}
