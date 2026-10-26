using System.IO;
using Ew4.Platform.Persistence.Io;

namespace Ew4.Platform.Persistence.Import;

/// <summary>短时临时目录（如旧版 Host 导入流落盘）。</summary>
public static class TemporaryScratchStore
{
    public static string CreateTempDirectory(string namePrefix)
    {
        string temp = PhysicalFiles.Combine(Path.GetTempPath(), namePrefix + Guid.NewGuid().ToString("N"));
        PhysicalFiles.EnsureDirectory(temp);
        return temp;
    }

    public static string WriteFile(string directory, string fileName, Stream content)
    {
        string path = PhysicalFiles.Combine(directory, fileName);
        using FileStream file = PhysicalFiles.CreateWriteStream(path);
        content.CopyTo(file);
        return path;
    }

    public static void TryDeleteDirectory(string directory) =>
        PhysicalFiles.DeleteDirectoryRecursive(directory);
}
