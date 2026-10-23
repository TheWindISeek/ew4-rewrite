using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Content;

/// <summary>
/// 以主 Pack 覆盖父 Pack 的虚拟文件系统（整文件级补全）。
/// </summary>
public sealed class MergedPackSource : IPackSource
{
    private readonly IPackSource _primary;
    private readonly IPackSource? _parent;
    private readonly HashSet<string> _paths;

    public MergedPackSource(IPackSource primary, IPackSource? parent)
    {
        _primary = primary;
        _parent = parent;
        _paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in primary.List(""))
        {
            _paths.Add(path);
        }

        if (parent != null)
        {
            foreach (string path in parent.List(""))
            {
                _paths.Add(path);
            }
        }
    }

    public IPackSource Primary => _primary;

    public IPackSource? Parent => _parent;

    public PackManifest Manifest => _primary.Manifest;

    public bool Exists(string path)
    {
        path = PackPath.Normalize(path);
        return _primary.Exists(path) || (_parent?.Exists(path) ?? false);
    }

    public byte[] Read(string path)
    {
        path = PackPath.Normalize(path);
        if (_primary.Exists(path))
        {
            return _primary.Read(path);
        }

        if (_parent != null && _parent.Exists(path))
        {
            return _parent.Read(path);
        }

        throw new ContentError("pack.missing-file", "Pack file not found: " + path);
    }

    public IEnumerable<string> List(string prefix) =>
        _paths.Where(p => string.IsNullOrEmpty(prefix) || p.StartsWith(PackPath.Normalize(prefix), StringComparison.Ordinal));
}
