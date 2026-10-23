using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ew4.Platform.Content;

public sealed class FilePackRepository : IPackRepository
{
    private readonly string _root;

    public FilePackRepository(string packsRootDirectory)
    {
        _root = Path.GetFullPath(packsRootDirectory);
        Directory.CreateDirectory(_root);
    }

    public IReadOnlyList<PackInfo> List()
    {
        if (!Directory.Exists(_root))
        {
            return Array.Empty<PackInfo>();
        }

        var list = new List<PackInfo>();
        foreach (string directory in Directory.EnumerateDirectories(_root))
        {
            if (!PackManifestPaths.DirectoryHasManifest(directory))
            {
                continue;
            }

            var source = new DirectoryPackSource(directory);
            list.Add(new PackInfo(source.Manifest.Id, source.Manifest.Name, source.Manifest.Version, source.Manifest.Parent));
        }

        return list.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    }

    public PackInfo Install(IPackSource source, InstallOptions options)
    {
        string id = PackId.Validate(source.Manifest.Id);
        string destination = Path.Combine(_root, id);
        if (Directory.Exists(destination) && !options.ReplaceExisting)
        {
            throw new ContentError("pack.install", $"Pack '{id}' is already installed.");
        }

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.CreateDirectory(destination);
        foreach (string path in source.List(""))
        {
            string target = Path.Combine(destination, path.Replace('/', Path.DirectorySeparatorChar));
            string? parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllBytes(target, source.Read(path));
        }

        return new PackInfo(source.Manifest.Id, source.Manifest.Name, source.Manifest.Version, source.Manifest.Parent);
    }

    public void Remove(string packId)
    {
        PackInstallGuard.ValidateRemove(packId, this);
        packId = PackId.Validate(packId);
        string destination = Path.Combine(_root, packId);
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    public IPackSource Open(string packId)
    {
        packId = PackId.Validate(packId);
        string destination = Path.Combine(_root, packId);
        return new DirectoryPackSource(destination);
    }

    public string RootDirectory => _root;
}
