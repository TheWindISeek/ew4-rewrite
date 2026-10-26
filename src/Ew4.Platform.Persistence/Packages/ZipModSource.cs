using Ew4.Platform.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Ew4.Platform.Persistence.Packages;

/// <summary>
/// 从 <c>.ew4mod</c> / zip 流读取 Pack。
/// </summary>
public sealed class ZipModSource : IModSource
{
    private readonly Dictionary<string, byte[]> _files;

    public ZipModSource(Stream archive, bool leaveOpen = false)
    {
        _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            string path = ModPath.Normalize(entry.FullName);
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _files[path] = memory.ToArray();
        }

        Manifest = ModManifestReader.ReadFromSource(this);
    }

    public ZipModSource(string archivePath)
        : this(File.OpenRead(archivePath), leaveOpen: false)
    {
    }

    public ModManifest Manifest { get; }

    public bool Exists(string path) => _files.ContainsKey(ModPath.Normalize(path));

    public byte[] Read(string path)
    {
        path = ModPath.Normalize(path);
        if (!_files.TryGetValue(path, out byte[]? data))
        {
            throw new PersistenceError("mod.missing-file", "Pack file not found: " + path);
        }

        return data;
    }

    public IEnumerable<string> List(string prefix)
    {
        prefix = string.IsNullOrWhiteSpace(prefix) ? "" : ModPath.Normalize(prefix);
        return _files.Keys.Where(p => string.IsNullOrEmpty(prefix) || p.StartsWith(prefix, StringComparison.Ordinal));
    }

    public IReadOnlyDictionary<string, byte[]> Files => _files;
}
