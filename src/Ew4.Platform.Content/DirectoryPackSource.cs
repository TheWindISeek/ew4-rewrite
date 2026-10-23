using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ew4.Platform.Content;

/// <summary>
/// 从磁盘目录读取 Pack；根目录必须包含 <c>mod.json</c>（或过渡期的 <c>pack.json</c>）。
/// </summary>
public sealed class DirectoryPackSource : IPackSource
{
    private readonly string _root;

    public DirectoryPackSource(string rootDirectory)
    {
        _root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(_root))
        {
            throw new ContentError("pack.directory", "Pack directory does not exist: " + rootDirectory);
        }

        Manifest = PackManifestReader.ReadFromSource(this);
    }

    public PackManifest Manifest { get; }

    public bool Exists(string path)
    {
        path = PackPath.Normalize(path);
        return File.Exists(PhysicalPath(path));
    }

    public byte[] Read(string path)
    {
        path = PackPath.Normalize(path);
        string physical = PhysicalPath(path);
        if (!File.Exists(physical))
        {
            throw new ContentError("pack.missing-file", "Pack file not found: " + path);
        }

        return File.ReadAllBytes(physical);
    }

    public IEnumerable<string> List(string prefix)
    {
        prefix = string.IsNullOrWhiteSpace(prefix) ? "" : PackPath.Normalize(prefix);
        if (prefix.Length > 0 && !prefix.EndsWith("/", StringComparison.Ordinal))
        {
            prefix += "/";
        }

        foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(_root, file).Replace('\\', '/');
            if (prefix.Length == 0 || relative.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return relative;
            }
        }
    }

    internal string PhysicalRoot => _root;

    private string PhysicalPath(string logicalPath) =>
        Path.GetFullPath(Path.Combine(_root, logicalPath.Replace('/', Path.DirectorySeparatorChar)));
}
