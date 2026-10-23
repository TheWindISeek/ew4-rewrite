using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Ew4.Platform.Content;

/// <summary>
/// 从 <c>.ew4mod</c> / zip 流读取 Pack。
/// </summary>
public sealed class ZipPackSource : IPackSource
{
    private readonly Dictionary<string, byte[]> _files;

    public ZipPackSource(Stream archive, bool leaveOpen = false)
    {
        _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            string path = PackPath.Normalize(entry.FullName);
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _files[path] = memory.ToArray();
        }

        Manifest = PackManifestReader.ReadFromSource(this);
    }

    public ZipPackSource(string archivePath)
        : this(File.OpenRead(archivePath), leaveOpen: false)
    {
    }

    public PackManifest Manifest { get; }

    public bool Exists(string path) => _files.ContainsKey(PackPath.Normalize(path));

    public byte[] Read(string path)
    {
        path = PackPath.Normalize(path);
        if (!_files.TryGetValue(path, out byte[]? data))
        {
            throw new ContentError("pack.missing-file", "Pack file not found: " + path);
        }

        return data;
    }

    public IEnumerable<string> List(string prefix)
    {
        prefix = string.IsNullOrWhiteSpace(prefix) ? "" : PackPath.Normalize(prefix);
        return _files.Keys.Where(p => string.IsNullOrEmpty(prefix) || p.StartsWith(prefix, StringComparison.Ordinal));
    }

    public IReadOnlyDictionary<string, byte[]> Files => _files;
}
