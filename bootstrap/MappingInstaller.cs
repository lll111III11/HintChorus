using System;
using System.IO;
using System.Reflection;
using LabApi.Loader.Features.Paths;

namespace HintChorus.Bootstrap;

/// <summary>
/// 把内嵌的映射组件（HintChorus.Mapping.dll + template-map.ini）释放到
/// <c>LabAPI\dependencies\global</c>。
///
/// <para>dependencies 在进程启动时、所有插件之前由 LabAPI 预加载，
/// 所以这里是"两阶段"：本次运行释放，下一次重启才真正享受到预加载。</para>
/// </summary>
internal static class MappingInstaller
{
    private const string DllResource = "HintChorus.Mapping.dll";

    private const string MapResource = "HintChorus.Mapping.template-map.ini";

    private const string DllFileName = "HintChorus.Mapping.dll";

    private const string MapFileName = "template-map.ini";

    internal static string EnsureReleased()
    {
        try
        {
            string directory = Path.Combine(PathManager.Dependencies.FullName, "global");
            Directory.CreateDirectory(directory);
            bool wroteDll = WriteIfChanged(DllResource, Path.Combine(directory, DllFileName));
            bool wroteMap = WriteIfChanged(MapResource, Path.Combine(directory, MapFileName));
            if (wroteDll || wroteMap)
            {
                return "映射组件已释放到 dependencies\\global (下一次重启由 LabAPI 预加载)";
            }
            return "映射组件已是最新: " + directory;
        }
        catch (Exception ex)
        {
            return "释放映射组件失败: " + ex.Message;
        }
    }

    private static bool WriteIfChanged(string resourceName, string targetPath)
    {
        byte[]? data = ReadResource(resourceName);
        if (data == null)
        {
            return false;
        }
        try
        {
            if (File.Exists(targetPath))
            {
                byte[] existing = File.ReadAllBytes(targetPath);
                if (SameContent(existing, data))
                {
                    return false;
                }
            }
            File.WriteAllBytes(targetPath, data);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static byte[]? ReadResource(string resourceName)
    {
        using Stream? stream = typeof(MappingInstaller).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }
        using MemoryStream memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool SameContent(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }
        return true;
    }
}
