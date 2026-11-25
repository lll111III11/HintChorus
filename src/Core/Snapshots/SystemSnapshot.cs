using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HintChorus.Core.Localization;
using LabApi.Loader.Features.Paths;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Snapshots;

/// <summary>
/// <b>系统快照（Beta）</b>：定时把「插件配置 + 插件主体 + 依赖」复制到
/// <c>configs/&lt;端口&gt;/HintChorus/snapshots/&lt;yyyyMMdd-HHmmss&gt;/</c>，
/// 可用控制台 <c>Hint snap</c> 列出 / 立即创建 / 回溯。
///
/// <para>默认每小时一份、最多保留 72 份、启动后先存一份。
/// 回溯只负责把文件写回原位，<b>需要重启服务器</b>才会生效；
/// 命令会提示是否立即重启。</para>
///
/// <para>正在运行的 <c>HintChorus.dll</c> 无法被覆盖，回溯时会被跳过并单独提示。</para>
/// </summary>
internal static class SystemSnapshot
{
    /// <summary>单份快照的体积上限（超过后停止复制插件 DLL，只保留已复制的部分）。</summary>
    private const long MaxSnapshotBytes = 1024L * 1024 * 1024;

    private static readonly object Sync = new object();

    private static bool _enabled;
    private static float _intervalHours = 1f;
    private static int _maxCount = 72;
    private static bool _includePlugins = true;
    private static bool _onStartup = true;
    private static int _port = 7777;
    private static DateTime _nextUtc;
    private static bool _busy;
    private static string _lastName = string.Empty;

    internal static bool Enabled => _enabled;

    internal static int MaxCount => _maxCount;

    internal static float IntervalHours => _intervalHours;

    internal static string LastName => _lastName;

    internal static string Root => Path.Combine(
        PathManager.Configs.FullName,
        _port.ToString(CultureInfo.InvariantCulture),
        "HintChorus",
        "snapshots");

    internal static void Configure(HintChorus.HintChorusPlugin.PluginConfig config, int port)
    {
        _enabled = config.EnableSystemSnapshot;
        _intervalHours = Math.Max(0.05f, config.SnapshotIntervalHours);
        _maxCount = Math.Max(1, Math.Min(1000, config.SnapshotMaxCount));
        _includePlugins = config.SnapshotIncludePlugins;
        _onStartup = config.SnapshotOnStartup;
        _port = port;
        _nextUtc = DateTime.UtcNow.AddSeconds(_onStartup ? 20 : _intervalHours * 3600);
    }

    /// <summary>由每秒维护循环调用：到点就在后台创建一份快照。</summary>
    internal static void Tick()
    {
        if (!_enabled)
        {
            return;
        }
        try
        {
            if (DateTime.UtcNow < _nextUtc)
            {
                return;
            }
            _nextUtc = DateTime.UtcNow.AddHours(Math.Max(0.05, _intervalHours));
            if (_busy)
            {
                return;
            }
            string reason = _lastName.Length == 0 ? "startup" : "auto";
            Task.Run(() =>
            {
                if (Create(reason, out string detail))
                {
                    Logger.Info("[HintChorus] " + Lang.Format("snap.created", detail));
                }
                else
                {
                    Logger.Warn("[HintChorus] " + Lang.Format("snap.createFailed", detail));
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + Lang.Format("snap.createFailed", ex.Message));
        }
    }

    /// <summary>立即创建一份快照。</summary>
    internal static bool Create(string reason, out string detail)
    {
        lock (Sync)
        {
            if (_busy)
            {
                detail = "busy";
                return false;
            }
            _busy = true;
        }

        string name = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string target = Path.Combine(Root, name);
        try
        {
            Directory.CreateDirectory(target);
            long bytes = 0;
            int files = 0;
            bool truncated = false;

            string configsSource = Path.Combine(PathManager.Configs.FullName, _port.ToString(CultureInfo.InvariantCulture));
            CopyTree(
                configsSource,
                Path.Combine(target, "configs"),
                delegate (string relative, string file)
                {
                    string normalized = relative.Replace('\\', '/');
                    return normalized.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)
                        || normalized.StartsWith("snapshots/", StringComparison.OrdinalIgnoreCase)
                        || normalized.IndexOf("/snapshots/", StringComparison.OrdinalIgnoreCase) >= 0;
                },
                ref bytes,
                ref files,
                ref truncated);

            if (_includePlugins)
            {
                string pluginsSource = Path.Combine(PathManager.Plugins.FullName, "global");
                CopyTree(
                    pluginsSource,
                    Path.Combine(target, "plugins"),
                    delegate (string relative, string file)
                    {
                        return !file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                    },
                    ref bytes,
                    ref files,
                    ref truncated);

                string labRoot = Directory.GetParent(PathManager.Configs.FullName)?.FullName ?? string.Empty;
                if (labRoot.Length > 0)
                {
                    CopyTree(
                        Path.Combine(labRoot, "dependencies", "global"),
                        Path.Combine(target, "dependencies"),
                        delegate (string relative, string file)
                        {
                            return !file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                        },
                        ref bytes,
                        ref files,
                        ref truncated);
                }
            }

            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("created=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            manifest.AppendLine("reason=" + reason);
            manifest.AppendLine("port=" + _port.ToString(CultureInfo.InvariantCulture));
            manifest.AppendLine("files=" + files.ToString(CultureInfo.InvariantCulture));
            manifest.AppendLine("bytes=" + bytes.ToString(CultureInfo.InvariantCulture));
            manifest.AppendLine("includePlugins=" + (_includePlugins ? "true" : "false"));
            manifest.AppendLine("truncated=" + (truncated ? "true" : "false"));
            File.WriteAllText(Path.Combine(target, "manifest.txt"), manifest.ToString(), Encoding.UTF8);

            PruneLocked();
            _lastName = name;
            detail = name + " | " + files + " files | " + FormatSize(bytes);

            // 磁盘提醒: 按单份体积估算满额占用, 超过 2GB 时明确提示服主, 避免服务器磁盘被快照吃满。
            long projected = bytes * _maxCount;
            if (_includePlugins && projected > 2L * 1024 * 1024 * 1024)
            {
                Logger.Warn("[HintChorus] " + Lang.Format("snap.spaceWarn", FormatSize(projected), _maxCount));
            }
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }
            }
            catch (Exception)
            {
            }
            detail = ex.Message;
            Logger.Warn("[HintChorus] " + Lang.Format("snap.createFailed", ex.Message));
            return false;
        }
        finally
        {
            lock (Sync)
            {
                _busy = false;
            }
        }
    }

    /// <summary>列出全部快照（新 -> 旧）。</summary>
    internal static List<SnapshotEntry> List()    {
        List<SnapshotEntry> list = new List<SnapshotEntry>();
        try
        {
            if (!Directory.Exists(Root))
            {
                return list;
            }
            foreach (string dir in Directory.GetDirectories(Root))
            {
                list.Add(ReadEntry(dir));
            }
            list.Sort((SnapshotEntry a, SnapshotEntry b) => string.CompareOrdinal(b.Name, a.Name));
        }
        catch (Exception ex)
        {
            Logger.Warn("[HintChorus] " + ex.Message);
        }
        return list;
    }

    /// <summary>回溯到指定快照；<paramref name="name"/> 支持 latest。</summary>
    internal static bool Restore(string name, out string detail)
    {
        detail = string.Empty;
        List<SnapshotEntry> all = List();
        if (all.Count == 0)
        {
            detail = Lang.Get("snap.empty");
            return false;
        }

        SnapshotEntry target = null;
        if (string.IsNullOrWhiteSpace(name) || name.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            target = all[0];
        }
        else
        {
            foreach (SnapshotEntry entry in all)
            {
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    target = entry;
                    break;
                }
            }
        }
        if (target == null)
        {
            detail = Lang.Format("snap.notFound", name);
            return false;
        }

        string source = Path.Combine(Root, target.Name);
        List<string> locked = new List<string>();
        int copied = 0;

        copied += RestoreTree(
            Path.Combine(source, "configs"),
            Path.Combine(PathManager.Configs.FullName, _port.ToString(CultureInfo.InvariantCulture)),
            delegate (string relative, string file)
            {
                return false;
            },
            locked);

        if (Directory.Exists(Path.Combine(source, "plugins")))
        {
            copied += RestoreTree(
                Path.Combine(source, "plugins"),
                Path.Combine(PathManager.Plugins.FullName, "global"),
                delegate (string relative, string file)
                {
                    return false;
                },
                locked);
        }

        if (Directory.Exists(Path.Combine(source, "dependencies")))
        {
            string labRoot = Directory.GetParent(PathManager.Configs.FullName)?.FullName ?? string.Empty;
            if (labRoot.Length > 0)
            {
                copied += RestoreTree(
                    Path.Combine(source, "dependencies"),
                    Path.Combine(labRoot, "dependencies", "global"),
                    delegate (string relative, string file)
                    {
                        return false;
                    },
                    locked);
            }
        }

        detail = Lang.Format("snap.restored", target.Name, copied, locked.Count)
            + (locked.Count > 0 ? " | " + Lang.Get("snap.lockedHint") + " " + string.Join(", ", locked.ToArray()) : string.Empty);
        return true;
    }

    private static SnapshotEntry ReadEntry(string dir)
    {
        SnapshotEntry entry = new SnapshotEntry
        {
            Name = Path.GetFileName(dir),
            CreatedLocal = Directory.GetLastWriteTime(dir),
            Files = 0,
            Bytes = 0
        };
        string manifest = Path.Combine(dir, "manifest.txt");
        try
        {
            if (File.Exists(manifest))
            {
                foreach (string line in File.ReadAllLines(manifest, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq);
                    string value = line.Substring(eq + 1);
                    if (key == "files")
                    {
                        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out entry.Files);
                    }
                    else if (key == "bytes")
                    {
                        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out entry.Bytes);
                    }
                    else if (key == "created")
                    {
                        DateTime parsed;
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                        {
                            entry.CreatedLocal = parsed.ToLocalTime();
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        return entry;
    }

    private static void PruneLocked()
    {
        try
        {
            List<SnapshotEntry> all = List();
            for (int i = all.Count - 1; i >= 0 && all.Count > _maxCount; i--)
            {
                string dir = Path.Combine(Root, all[i].Name);
                try
                {
                    Directory.Delete(dir, true);
                    all.RemoveAt(i);
                }
                catch (Exception)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static void CopyTree(
        string source,
        string target,
        Func<string, string, bool> skip,
        ref long bytes,
        ref int files,
        ref bool truncated)
    {
        if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
        {
            return;
        }
        string root = source.TrimEnd('\\', '/');
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (bytes > MaxSnapshotBytes)
            {
                truncated = true;
                return;
            }
            string relative = file.Substring(root.Length).TrimStart('\\', '/');
            if (skip != null && skip(relative, file))
            {
                continue;
            }
            string destination = Path.Combine(target, relative);
            string parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
            File.Copy(file, destination, true);
            bytes += new FileInfo(file).Length;
            files++;
        }
    }

    private static int RestoreTree(string source, string target, Func<string, string, bool> skip, List<string> locked)
    {
        if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
        {
            return 0;
        }
        int copied = 0;
        string root = source.TrimEnd('\\', '/');
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(root.Length).TrimStart('\\', '/');
            if (skip != null && skip(relative, file))
            {
                continue;
            }
            string destination = Path.Combine(target, relative);
            string parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
            try
            {
                File.Copy(file, destination, true);
                copied++;
            }
            catch (Exception)
            {
                locked.Add(relative.Replace('\\', '/'));
            }
        }
        return copied;
    }

    internal static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
        {
            return (bytes / (1024d * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        }
        if (bytes >= 1024L * 1024)
        {
            return (bytes / (1024d * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }
        if (bytes >= 1024L)
        {
            return (bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        }
        return bytes.ToString(CultureInfo.InvariantCulture) + " B";
    }
}

/// <summary>一份系统快照的摘要。</summary>
internal sealed class SnapshotEntry
{
    internal string Name = string.Empty;

    internal DateTime CreatedLocal;

    internal int Files;

    internal long Bytes;
}
