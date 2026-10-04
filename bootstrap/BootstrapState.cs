using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using System;
using System.Globalization;
using System.IO;

namespace HintIsolation.Bootstrap;

/// <summary>
/// 引导器状态文件 —— 即需求里的"参数"。
///
/// <para><see cref="Prompted"/> = 0 表示还没提示过(首次引导后需要提示重启);
/// 提示过一次就改写为 1, 此后不再提醒。</para>
///
/// <para>落盘位置: <c>configs\&lt;端口&gt;\HintIsolation\bootstrap.state</c>,
/// 纯键值文本, 刻意不依赖任何 YAML 库, 保证引导阶段零负担。</para>
/// </summary>
internal sealed class BootstrapState
{
    private const string FileName = "bootstrap.state";

    /// <summary>是否已经提示过(0 = 未提示, 1 = 不再提醒)。</summary>
    public int Prompted { get; set; }

    /// <summary>引导器启动次数。</summary>
    public int Runs { get; set; }

    /// <summary>最后一次引导时间(UTC, ISO8601)。</summary>
    public string LastRunUtc { get; set; } = string.Empty;

    /// <summary>已释放的引导器版本(用于升级后重新提示)。</summary>
    public string ReleasedVersion { get; set; } = string.Empty;

    /// <summary>状态文件路径。</summary>
    public static string FilePath =>
        Path.Combine(PathManager.Configs.FullName, Server.Port.ToString(CultureInfo.InvariantCulture),
            "HintIsolation", FileName);

    /// <summary>读取状态(不存在则返回全新状态)。</summary>
    public static BootstrapState Load()
    {
        BootstrapState state = new();

        try
        {
            string path = FilePath;
            if (!File.Exists(path))
            {
                return state;
            }

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                int split = line.IndexOf(':');
                if (split <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, split).Trim();
                string value = line.Substring(split + 1).Trim().Trim('"');

                switch (key.ToLowerInvariant())
                {
                    case "prompted":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int prompted))
                        {
                            state.Prompted = prompted;
                        }

                        break;

                    case "runs":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int runs))
                        {
                            state.Runs = runs;
                        }

                        break;

                    case "lastrunutc":
                        state.LastRunUtc = value;
                        break;

                    case "releasedversion":
                        state.ReleasedVersion = value;
                        break;
                }
            }
        }
        catch (Exception)
        {
            // 引导阶段绝不因状态文件问题中断启动
        }

        return state;
    }

    /// <summary>写回状态。</summary>
    public bool Save()
    {
        try
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            File.WriteAllLines(path, new[]
            {
                "# HintIsolation 引导器状态 —— Prompted: 0=下次仍提示, 1=不再提醒",
                $"Prompted: {Prompted}",
                $"Runs: {Runs}",
                $"LastRunUtc: \"{LastRunUtc}\"",
                $"ReleasedVersion: \"{ReleasedVersion}\"",
            });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>标记本次运行(次数 + 时间)。</summary>
    public void MarkRun()
    {
        Runs++;
        LastRunUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }
}
