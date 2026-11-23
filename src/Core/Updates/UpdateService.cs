using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HintChorus.Core.Localization;
using LabApi.Loader.Features.Paths;
using ServerOutput;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Updates;

/// <summary>
/// 安全更新检查。
///
/// <para>只读检查 GitHub 最新 Release; 开启自动更新时也只下载到
/// <c>plugins/global/HintChorus.dll.new</c>, <b>绝不覆盖正在运行的 DLL</b>。</para>
/// </summary>
internal static class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/lll111III11/HintChorus/releases/latest";

    private static HintChorus.HintChorusPlugin.PluginConfig? _config;

    internal static void Initialize(HintChorus.HintChorusPlugin.PluginConfig config)
    {
        _config = config;
    }

    internal static void CheckInBackground()
    {
        Task.Run(delegate
        {
            Check();
        });
    }

    internal static bool HandleCommand(out string response)
    {
        CheckInBackground();
        response = SetupText.Get("updateStarted", Language());
        return true;
    }

    private static void Check()
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string json = DownloadText(LatestReleaseUrl);
            string tag = Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            string asset = Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*HintChorus\\.dll)\"");
            if (tag.Length == 0)
            {
                Logger.Warn("[HintChorus] 更新检查: 返回内容里没有 tag_name");
                return;
            }

            Version latest = ParseVersion(tag);
            Version current = ParseVersion(CurrentVersion());
            string language = Language();
            if (latest <= current)
            {
                Logger.Info("[HintChorus] " + SetupText.Format("updateNone", language, current));
                return;
            }

            Logger.Warn("[HintChorus] " + SetupText.Format("updateFound", language, latest, current));
            if (_config != null && _config.AutoUpdate && asset.Length > 0)
            {
                string target = Path.Combine(PathManager.Plugins.FullName, "global", "HintChorus.dll.new");
                DownloadTo(asset, target);

                if (_config.RnrAfterUpdate)
                {
                    TryReplaceAndScheduleRestart(target, language);
                }
                else
                {
                    Logger.Warn("[HintChorus] " + SetupText.Format("updateDownloaded", language, target));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + SetupText.Format(
                "updateFailed",
                Language(),
                ex.GetType().Name + ": " + ex.Message));
        }
    }

    private static void TryReplaceAndScheduleRestart(string stagingPath, string language)
    {
        string livePath = Path.Combine(PathManager.Plugins.FullName, "global", "HintChorus.dll");
        try
        {
            File.Copy(stagingPath, livePath, true);
            try
            {
                File.Delete(stagingPath);
            }
            catch (Exception)
            {
            }
            Logger.Warn("[HintChorus] " + SetupText.Get("updateReplaced", language));
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + SetupText.Get("updateReplaceFailed", language) + " " + ex.Message);
            return;
        }

        try
        {
            ServerStatic.StopNextRound = ServerStatic.NextRoundAction.Restart;
            try
            {
                ServerConsole.AddOutputEntry(default(ExitActionRestartEntry));
            }
            catch (Exception)
            {
            }
            Logger.Warn("[HintChorus] " + SetupText.Get("rnrScheduled", language));
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] 安排回合结束重启失败: " + ex.Message);
        }
    }

    private static string DownloadText(string url)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.UserAgent = "HintChorus-UpdateCheck";
        request.Timeout = 10000;
        request.ReadWriteTimeout = 10000;
        using (WebResponse response = request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    private static void DownloadTo(string url, string targetPath)
    {
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        using (WebClient client = new WebClient())
        {
            client.Headers.Add("User-Agent", "HintChorus-UpdateCheck");
            client.DownloadFile(url, targetPath);
        }
    }

    private static string Match(string text, string pattern)
    {
        Match match = Regex.Match(text, pattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static Version ParseVersion(string text)
    {
        Match match = Regex.Match(text ?? string.Empty, "(\\d+(?:\\.\\d+){1,3})");
        Version version;
        if (match.Success && Version.TryParse(match.Groups[1].Value, out version))
        {
            return version;
        }
        return new Version(0, 0);
    }

    private static string CurrentVersion()
    {
        return typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string Language()
    {
        string normalized = ServerLocale.Normalize(_config?.Language);
        if (normalized.Length > 0)
        {
            return normalized;
        }
        return ServerLocale.Detect(_config?.Language).Language;
    }
}
