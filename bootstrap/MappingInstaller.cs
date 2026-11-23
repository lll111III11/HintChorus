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
///
/// <para>dependencies 里的 DLL 可能被 LabAPI 占用；因此按<b>程序集版本</b>判断是否需要更新，
/// 同版本重建产物不会反复尝试覆盖。确实需要更新但文件被占用时，写
/// <c>HintChorus.Mapping.dll.new</c> 并提示停服后手动替换。</para>
/// </summary>
internal static class MappingInstaller
{
    private const string DllResource = "HintChorus.Mapping.dll";

    private const string MapResource = "HintChorus.Mapping.template-map.ini";

    private const string DllFileName = "HintChorus.Mapping.dll";

    private const string MapFileName = "template-map.ini";

    private enum WriteResult
    {
        Unchanged,
        Written,
        Staged,
        Failed
    }

    internal static string EnsureReleased()
    {
        try
        {
            string directory = Path.Combine(PathManager.Dependencies.FullName, "global");
            Directory.CreateDirectory(directory);

            WriteResult dllResult = WriteIfChanged(
                DllResource,
                Path.Combine(directory, DllFileName),
                out string dllDetail);
            WriteResult mapResult = WriteIfChanged(
                MapResource,
                Path.Combine(directory, MapFileName),
                out string mapDetail);

            if (dllResult == WriteResult.Failed || mapResult == WriteResult.Failed)
            {
                return "释放映射组件失败: " + dllDetail + " " + mapDetail;
            }
            if (dllResult == WriteResult.Staged || mapResult == WriteResult.Staged)
            {
                return "映射组件更新已暂存: " + dllDetail + " " + mapDetail
                    + " (当前版本仍可用; 停服后手动替换 .new 文件)";
            }
            if (dllResult == WriteResult.Written || mapResult == WriteResult.Written)
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

    private static WriteResult WriteIfChanged(string resourceName, string targetPath, out string detail)
    {
        detail = string.Empty;
        byte[]? data = ReadResource(resourceName);
        if (data == null)
        {
            return WriteResult.Unchanged;
        }

        bool exists = File.Exists(targetPath);
        if (exists)
        {
            if (SameContent(targetPath, data))
            {
                return WriteResult.Unchanged;
            }

            // 同版本重建产物: 不因为字节变化去覆盖可能正在使用的 DLL。
            Version? installed = ReadVersion(targetPath);
            Version? embedded = ReadVersion(data);
            if (installed != null && embedded != null && installed >= embedded)
            {
                return WriteResult.Unchanged;
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
            return WriteResult.Written;
        }
        catch (Exception ex)
        {
            return TryStage(targetPath, data, ex.Message, out detail);
        }
    }

    private static WriteResult TryStage(string targetPath, byte[] data, string reason, out string detail)
    {
        string stagedPath = targetPath + ".new";
        try
        {
            File.WriteAllBytes(stagedPath, data);
            detail = stagedPath + " (" + reason + ")";
            return WriteResult.Staged;
        }
        catch (Exception ex)
        {
            detail = reason + " / " + ex.Message;
            return WriteResult.Failed;
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

    private static bool SameContent(string path, byte[] right)
    {
        try
        {
            byte[] left = File.ReadAllBytes(path);
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
        catch (Exception)
        {
            return false;
        }
    }

    private static Version? ReadVersion(string path)
    {
        try
        {
            return AssemblyName.GetAssemblyName(path).Version;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Version? ReadVersion(byte[] data)
    {
        string temp = Path.Combine(Path.GetTempPath(), "HintChorus.Mapping." + Guid.NewGuid().ToString("N") + ".dll");
        try
        {
            File.WriteAllBytes(temp, data);
            return ReadVersion(temp);
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
