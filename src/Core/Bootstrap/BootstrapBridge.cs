using System;
using HintChorus.Core.Interception;
using HintChorus.Core.Layout;
using HintChorus.Core.Localization;
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
			return Lang.Get("early.skipped");
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
				return Finish(text + " | " + Lang.Get("early.noConfig"));
			}
			if (!pluginConfig.InterceptThirdPartyUi)
			{
				EarlyInstalled = true;
				return Finish(text + " | " + Lang.Get("early.disabled"));
			}
			BootstrapConfigurator.Apply(pluginConfig);
			UiInterception.Instance.Install();
			EarlyInstalled = true;
			EarlyInstallOrder = ++InstallOrder;
			StartupLog.Info(Lang.Format("early.installed", EarlyInstallOrder));
			return Finish(text + " | " + Lang.Format("early.done", EarlyInstallOrder));
		}
		catch (Exception ex)
		{
			Logger.Error((object)Lang.Format("early.failed", ex));
			return Lang.Format("early.exception", ex.Message);
		}
	}

	private static string Finish(string detail)
	{
		StartupLog.MarkStarted();
		StartupLog.PrintSummary(Lang.Get("early.uiReady"));
		return detail;
	}

	public static string EnsureHome()
	{
		if (RuntimeHome.IsReady)
		{
			return Lang.Get("home.ready");
		}
		bool flag = RuntimeHome.EnsureCreated();
		RuntimeHome.InstallResolver();
		int count = RuntimeHome.DiscoverModules().Count;
		if (!RuntimeHome.IsReady)
		{
			return Lang.Get("home.failed");
		}
		StartupLog.Info(Lang.Format(
			"home.status",
			flag ? Lang.Get("home.createdState") : Lang.Get("home.readyState"),
			RuntimeHome.HomePath,
			count));
		if (!flag)
		{
			return Lang.Format("home.readyModules", count);
		}
		return Lang.Format("home.createdModules", count);
	}
}
