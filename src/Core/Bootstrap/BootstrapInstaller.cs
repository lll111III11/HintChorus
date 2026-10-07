using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;

namespace HintChorus.Core.Bootstrap;

internal static class BootstrapInstaller
{
	internal const string ResourceName = "HintChorus.Bootstrap.dll";

	internal const string FileName = "0HintChorus.Bootstrap.dll";

	internal static string TargetPath => Path.Combine(PathManager.Plugins.FullName, "global", "0HintChorus.Bootstrap.dll");

	internal static bool EnsureReleased(out string detail)
	{
		detail = string.Empty;
		try
		{
			byte[] array = ReadResource();
			if (array == null)
			{
				detail = "未找到内嵌资源 HintChorus.Bootstrap.dll(构建时未嵌入引导器?)";
				return false;
			}
			string targetPath = TargetPath;
			if (File.Exists(targetPath) && IsSameContent(targetPath, array))
			{
				detail = "引导器已是当前版本: " + targetPath;
				return false;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
			File.WriteAllBytes(targetPath, array);
			ResetPromptFlag();
			detail = "已释放引导器 → " + targetPath + " (下次重启生效)";
			return true;
		}
		catch (Exception ex)
		{
			detail = "释放引导器失败: " + ex.Message;
			Logger.Error((object)("[HintChorus] " + detail));
			return false;
		}
	}

	private static void ResetPromptFlag()
	{
		try
		{
			string path = Path.Combine(PathManager.Configs.FullName, Server.Port.ToString(CultureInfo.InvariantCulture), "HintChorus", "bootstrap.state");
			if (!File.Exists(path))
			{
				return;
			}
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].TrimStart().StartsWith("Prompted", StringComparison.OrdinalIgnoreCase))
				{
					array[i] = "Prompted: 0";
				}
			}
			File.WriteAllLines(path, array);
		}
		catch (Exception)
		{
		}
	}

	private static byte[]? ReadResource()
	{
		using Stream stream = typeof(BootstrapInstaller).Assembly.GetManifestResourceStream("HintChorus.Bootstrap.dll");
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
}
