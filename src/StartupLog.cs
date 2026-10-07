using System;
using System.IO;
using HintChorus.Core.Bootstrap;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus;

internal static class StartupLog
{
	private const string FlagFileName = "started.flag";

	public static bool Quiet { get; private set; }

	public static string FlagPath => Path.Combine(RuntimeHome.RuntimePath, "started.flag");

	public static bool HasStartedBefore()
	{
		try
		{
			return File.Exists(FlagPath);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool Detect()
	{
		Quiet = HasStartedBefore();
		return Quiet;
	}

	public static void MarkStarted()
	{
		try
		{
			Directory.CreateDirectory(RuntimeHome.RuntimePath);
			File.WriteAllText(FlagPath, DateTime.UtcNow.ToString("O"));
		}
		catch (Exception)
		{
		}
	}

	public static void ClearFlag()
	{
		try
		{
			if (File.Exists(FlagPath))
			{
				File.Delete(FlagPath);
			}
		}
		catch (Exception)
		{
		}
		Quiet = false;
	}

	public static void Info(string message)
	{
		if (!Quiet)
		{
			Logger.Info((object)message);
		}
	}

	public static void Warn(string message)
	{
		Logger.Warn((object)message);
	}

	public static void PrintSummary(string detail)
	{
		Logger.Raw(detail, ConsoleColor.Gray);
	}
}
