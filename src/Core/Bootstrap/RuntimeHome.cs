using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;

namespace HintIsolation.Core.Bootstrap;

public static class RuntimeHome
{
	public const string HomeFolderName = "HintIsolation";

	public const string ModulesFolderName = "modules";

	public const string RuntimeFolderName = "runtime";

	public const string ManifestFileName = "home.yml";

	public static bool IsReady { get; private set; }

	public static IReadOnlyList<string> DiscoveredModules { get; private set; } = Array.Empty<string>();

	public static bool ResolverInstalled { get; private set; }

	public static string HomePath => Path.Combine(PathManager.Configs.FullName, Server.Port.ToString(CultureInfo.InvariantCulture), "HintIsolation");

	public static string ModulesPath => Path.Combine(HomePath, "modules");

	public static string RuntimePath => Path.Combine(HomePath, "runtime");

	public static string ManifestPath => Path.Combine(HomePath, "home.yml");

	public static bool EnsureCreated()
	{
		try
		{
			bool result = !Directory.Exists(HomePath);
			Directory.CreateDirectory(HomePath);
			Directory.CreateDirectory(ModulesPath);
			Directory.CreateDirectory(RuntimePath);
			if (!File.Exists(ManifestPath))
			{
				File.WriteAllText(ManifestPath, BuildManifest(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
			IsReady = true;
			return result;
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 创建自宿主目录失败: " + ex.Message));
			return false;
		}
	}

	public static void InstallResolver()
	{
		if (ResolverInstalled)
		{
			return;
		}
		try
		{
			AppDomain.CurrentDomain.AssemblyResolve += ResolveFromHome;
			ResolverInstalled = true;
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 挂装配解析器失败: " + ex.Message));
		}
	}

	public static void UninstallResolver()
	{
		if (ResolverInstalled)
		{
			try
			{
				AppDomain.CurrentDomain.AssemblyResolve -= ResolveFromHome;
			}
			catch (Exception)
			{
			}
			ResolverInstalled = false;
		}
	}

	public static IReadOnlyList<string> DiscoverModules()
	{
		try
		{
			if (!Directory.Exists(ModulesPath))
			{
				DiscoveredModules = Array.Empty<string>();
				return DiscoveredModules;
			}
			DiscoveredModules = (from n in Directory.EnumerateFiles(ModulesPath, "*.dll", SearchOption.TopDirectoryOnly).Select(Path.GetFileName)
				where !string.IsNullOrEmpty(n)
				select (n)).OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase).ToArray();
			return DiscoveredModules;
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 扫描模块目录失败: " + ex.Message));
			DiscoveredModules = Array.Empty<string>();
			return DiscoveredModules;
		}
	}

	public static Assembly? LoadModule(string fileName)
	{
		try
		{
			string text = Path.Combine(ModulesPath, fileName);
			if (!File.Exists(text))
			{
				return null;
			}
			return Assembly.LoadFrom(text);
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintIsolation] 加载模块 " + fileName + " 失败: " + ex.Message));
			return null;
		}
	}

	private static Assembly? ResolveFromHome(object? sender, ResolveEventArgs args)
	{
		try
		{
			AssemblyName assemblyName = new AssemblyName(args.Name);
			if (string.IsNullOrEmpty(assemblyName.Name))
			{
				return null;
			}
			string path = assemblyName.Name + ".dll";
			string[] array = new string[3] { ModulesPath, RuntimePath, HomePath };
			for (int i = 0; i < array.Length; i++)
			{
				string text = Path.Combine(array[i], path);
				if (File.Exists(text))
				{
					return Assembly.LoadFrom(text);
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static string BuildManifest()
	{
		return string.Join("\n", "# ══════════════════════════════════════════════════════════", "# HintIsolation 自宿主目录清单", "#", "# 这个文件夹是本底层自己的加载根 —— 地位等同于 LabAPI 的 plugins\\global。", "# 它由前驱引导器在启动时创建, 由装配解析器从底层接管依赖查找。", "# ══════════════════════════════════════════════════════════", "", "layout:", "  home: .", "  modules: modules       # 本底层的模块; 引用到的依赖放这里就能被解析到", "  runtime: runtime       # 运行时文件(状态、清单、缓存)", "", "bootstrap:", "  # 前驱引导器名(带 0 前缀, 在 LabAPI 插件目录里抢最先加载)", "  precursor: 0HintIsolation.Bootstrap.dll", "", "assembly_resolution:", "  # 解析顺序: modules -> runtime -> home", "  search_order: [modules, runtime, home]", "", "created_by: HintIsolation", "home: " + HomePath, "");
	}
}
