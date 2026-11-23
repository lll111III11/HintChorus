using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HintChorus.Core.Localization;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;

namespace HintChorus.Core.Bootstrap;

/// <summary>引导资产释放结果。</summary>
internal enum BootstrapReleaseResult
{
	/// <summary>磁盘上的 shim / payload / mapping 已经是最新。</summary>
	Current,

	/// <summary>本次写入了新资产, 需要重启才能生效。</summary>
	Released,

	/// <summary>有文件被占用, 已把新版本暂存为 .new。</summary>
	Staged,

	/// <summary>连释放/暂存都失败。</summary>
	Failed
}

/// <summary>
/// <b>单文件发放的释放器</b>。
///
/// <para>主 DLL 内嵌三类资产:</para>
/// <list type="bullet">
///   <item><b>永久 shim</b> → <c>plugins\global\0.dll</c>, 只负责载入 payload;</item>
///   <item><b>引导 payload</b> → <c>plugins\global\HintChorus.Bootstrap.payload</c>, 可随时替换;</item>
///   <item><b>映射组件</b> → <c>dependencies\global\HintChorus.Mapping.dll</c> + 程序表。</item>
/// </list>
///
/// <para>首次运行全部释放, 提示重启一次; 重启后 shim 载入 payload, payload 再做抢先安装。
/// 以后更新只替换 payload / mapping, <c>0.dll</c> 永远不动。</para>
///
/// <para>Windows 会映射正在运行的 DLL, 所以释放器按<b>程序集版本</b>判断是否需要更新;
/// 同版本重建不重写; 真需要更新但文件被占用时暂存为 <c>.new</c>。</para>
/// </summary>
internal static class BootstrapInstaller
{
	private const string ShimResource = "HintChorus.Shim.dll";

	private const string PayloadResource = "HintChorus.Bootstrap.Payload.dll";

	private const string MappingResource = "HintChorus.Mapping.dll";

	private const string MappingMapResource = "HintChorus.Mapping.template-map.ini";

	private const string ShimFileName = "0.dll";

	private const string PayloadFileName = "HintChorus.Bootstrap.payload";

	private const string MappingFileName = "HintChorus.Mapping.dll";

	private const string MappingMapFileName = "template-map.ini";

	/// <summary>旧版引导器文件名, 释放新版时顺手清掉。</summary>
	private const string LegacyFileName = "0HintChorus.Bootstrap.dll";

	private enum FileResult
	{
		Current,
		Written,
		Staged,
		Failed
	}

	internal static string TargetPath => Path.Combine(PathManager.Plugins.FullName, "global", ShimFileName);

	internal static BootstrapReleaseResult EnsureReleased(out string detail)
	{
		detail = string.Empty;
		try
		{
			string pluginsDir = Path.Combine(PathManager.Plugins.FullName, "global");
			string dependenciesDir = Path.Combine(PathManager.Dependencies.FullName, "global");
			Directory.CreateDirectory(pluginsDir);
			Directory.CreateDirectory(dependenciesDir);
			DeleteLegacy(Path.Combine(pluginsDir, ShimFileName));

			bool written = false;
			List<string> staged = new List<string>();
			List<string> errors = new List<string>();

			FileResult shim = ReleaseFile(
				ShimResource,
				Path.Combine(pluginsDir, ShimFileName),
				"HintChorus.Shim",
				out string shimDetail);
			Accumulate(shim, shimDetail, ref written, staged, errors);

			FileResult payload = ReleaseFile(
				PayloadResource,
				Path.Combine(pluginsDir, PayloadFileName),
				"HintChorus.Bootstrap.Payload",
				out string payloadDetail);
			Accumulate(payload, payloadDetail, ref written, staged, errors);

			FileResult mapping = ReleaseFile(
				MappingResource,
				Path.Combine(dependenciesDir, MappingFileName),
				"HintChorus.Mapping",
				out string mappingDetail);
			Accumulate(mapping, mappingDetail, ref written, staged, errors);

			FileResult map = ReleaseFile(
				MappingMapResource,
				Path.Combine(dependenciesDir, MappingMapFileName),
				null,
				out string mapDetail);
			Accumulate(map, mapDetail, ref written, staged, errors);

			if (errors.Count > 0)
			{
				detail = Lang.Format("bootstrap.assetsFailed", string.Join("; ", errors));
				return BootstrapReleaseResult.Failed;
			}
			if (staged.Count > 0)
			{
				detail = Lang.Format("bootstrap.assetsStaged", string.Join(", ", staged));
				return BootstrapReleaseResult.Staged;
			}
			if (written)
			{
				// 主 DLL 已经替玩家提示重启; 标记 Prompted, 避免重启后 payload 再弹一次"请重启"。
				MarkPrompted();
				detail = Lang.Get("bootstrap.assetsUpdated");
				return BootstrapReleaseResult.Released;
			}

			detail = Lang.Get("bootstrap.assetsCurrent");
			return BootstrapReleaseResult.Current;
		}
		catch (Exception ex)
		{
			detail = Lang.Format("bootstrap.assetsFailed", ex.Message);
			Logger.Error((object)("[HintChorus] " + detail));
			return BootstrapReleaseResult.Failed;
		}
	}

	private static void Accumulate(
		FileResult result,
		string detail,
		ref bool written,
		List<string> staged,
		List<string> errors)
	{
		switch (result)
		{
			case FileResult.Written:
				written = true;
				break;
			case FileResult.Staged:
				staged.Add(detail);
				break;
			case FileResult.Failed:
				errors.Add(detail);
				break;
		}
	}

	private static FileResult ReleaseFile(
		string resourceName,
		string targetPath,
		string? expectedAssemblyName,
		out string detail)
	{
		detail = string.Empty;
		byte[]? data = ReadResource(resourceName);
		if (data == null)
		{
			detail = "missing embedded resource " + resourceName;
			return FileResult.Failed;
		}

		if (File.Exists(targetPath))
		{
			if (IsSameContent(targetPath, data))
			{
				return FileResult.Current;
			}

			AssemblyName? installed = expectedAssemblyName == null ? null : ReadAssemblyName(targetPath);
			Version? embeddedVersion = expectedAssemblyName == null ? null : ReadVersion(data);

			if (installed != null && expectedAssemblyName != null)
			{
				// 旧架构的 0.dll 叫 HintChorus.Bootstrap; 看到名字不同就强制迁移。
				if (!string.Equals(installed.Name, expectedAssemblyName, StringComparison.Ordinal))
				{
					// fall through: 强制写入
				}
				else if (embeddedVersion != null && installed.Version != null
					&& installed.Version >= embeddedVersion)
				{
					return FileResult.Current;
				}
			}
		}

		try
		{
			string? directory = Path.GetDirectoryName(targetPath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
			File.WriteAllBytes(targetPath, data);
			return FileResult.Written;
		}
		catch (Exception ex)
		{
			return TryStage(targetPath, data, ex.Message, out detail);
		}
	}

	private static FileResult TryStage(string targetPath, byte[] data, string reason, out string detail)
	{
		string stagedPath = targetPath + ".new";
		try
		{
			File.WriteAllBytes(stagedPath, data);
			detail = stagedPath + " (" + reason + ")";
			return FileResult.Staged;
		}
		catch (Exception ex)
		{
			detail = targetPath + ": " + reason + " / " + ex.Message;
			return FileResult.Failed;
		}
	}

	private static void DeleteLegacy(string targetPath)
	{
		try
		{
			string? directory = Path.GetDirectoryName(targetPath);
			if (directory == null)
			{
				return;
			}
			string legacy = Path.Combine(directory, LegacyFileName);
			if (File.Exists(legacy))
			{
				File.Delete(legacy);
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>标记首次重启提示已经由主 DLL 发出, 避免 payload 再次弹横幅。</summary>
	private static void MarkPrompted()
	{
		try
		{
			string path = Path.Combine(
				PathManager.Configs.FullName,
				Server.Port.ToString(CultureInfo.InvariantCulture),
				"HintChorus",
				"bootstrap.state");
			string? directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			if (!File.Exists(path))
			{
				File.WriteAllLines(path, new[]
				{
					"# HintChorus 引导器状态 —— Prompted: 0=下次仍提示, 1=不再提醒",
					"Prompted: 1"
				});
				return;
			}

			List<string> lines = File.ReadAllLines(path).ToList();
			bool found = false;
			for (int i = 0; i < lines.Count; i++)
			{
				if (lines[i].TrimStart().StartsWith("Prompted", StringComparison.OrdinalIgnoreCase))
				{
					lines[i] = "Prompted: 1";
					found = true;
				}
			}
			if (!found)
			{
				lines.Add("Prompted: 1");
			}
			File.WriteAllLines(path, lines);
		}
		catch (Exception)
		{
		}
	}

	private static byte[]? ReadResource(string name)
	{
		using Stream? stream = typeof(BootstrapInstaller).Assembly.GetManifestResourceStream(name);
		if (stream == null)
		{
			return null;
		}
		using MemoryStream memoryStream = new MemoryStream();
		stream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	private static bool IsSameContent(string path, byte[] expected)
	{
		try
		{
			byte[] array = File.ReadAllBytes(path);
			if (array.Length != expected.Length)
			{
				return false;
			}
			using MD5 mD = MD5.Create();
			return mD.ComputeHash(array).SequenceEqual(mD.ComputeHash(expected));
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static AssemblyName? ReadAssemblyName(string path)
	{
		try
		{
			return AssemblyName.GetAssemblyName(path);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static Version? ReadVersion(byte[] data)
	{
		string temp = Path.Combine(Path.GetTempPath(), "HintChorus.Asset." + Guid.NewGuid().ToString("N") + ".dll");
		try
		{
			File.WriteAllBytes(temp, data);
			return ReadAssemblyName(temp)?.Version;
		}
		catch (Exception)
		{
			return null;
		}
		finally
		{
			try
			{
				if (File.Exists(temp))
				{
					File.Delete(temp);
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
