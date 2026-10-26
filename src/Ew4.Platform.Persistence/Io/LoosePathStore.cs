using System.IO;

namespace Ew4.Platform.Persistence.Io;

/// <summary>工具/CLI 等场景下的绝对路径读写（非 workspace / mod 安装布局）。</summary>
public sealed class LoosePathStore
{
    public static LoosePathStore Default { get; } = new();

    public string Normalize(string path) => PhysicalFiles.GetFullPath(path);

    public string Combine(string basePath, params string[] segments) =>
        Path.Combine([basePath, .. segments]);

    public byte[] ReadBytes(string path) => PhysicalFiles.ReadAllBytes(Normalize(path));

    public void WriteBytes(string path, byte[] data) => PhysicalFiles.WriteAllBytes(Normalize(path), data);
}
