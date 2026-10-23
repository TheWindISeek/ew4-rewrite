using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ew4.Platform.Content;

public sealed class FileModRepository : IModRepository
{
    private readonly string _root;

    public FileModRepository(string packsRootDirectory)
    {
        _root = Path.GetFullPath(packsRootDirectory);
        Directory.CreateDirectory(_root);
    }

    public IReadOnlyList<ModInfo> List()
    {
        if (!Directory.Exists(_root))
        {
            return Array.Empty<ModInfo>();
        }

        var list = new List<ModInfo>();
        foreach (string directory in Directory.EnumerateDirectories(_root))
        {
            if (!ModManifestPaths.DirectoryHasManifest(directory))
            {
                continue;
            }

            var source = new DirectoryModSource(directory);
            list.Add(new ModInfo(source.Manifest.Id, source.Manifest.Name, source.Manifest.Version, source.Manifest.Parent));
        }

        return list.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    }

    public ModInfo Install(IModSource source, InstallOptions options)
    {
        string id = ModId.Validate(source.Manifest.Id);
        string destination = Path.Combine(_root, id);
        if (Directory.Exists(destination) && !options.ReplaceExisting)
        {
            throw new ContentError("mod.install", $"Pack '{id}' is already installed.");
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

        return new ModInfo(source.Manifest.Id, source.Manifest.Name, source.Manifest.Version, source.Manifest.Parent);
    }

    public void Remove(string modId)
    {
        ModInstallGuard.ValidateRemove(modId, this);
        modId = ModId.Validate(modId);
        string destination = Path.Combine(_root, modId);
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    public IModSource Open(string modId)
    {
        modId = ModId.Validate(modId);
        string destination = Path.Combine(_root, modId);
        return new DirectoryModSource(destination);
    }

    public string RootDirectory => _root;
}
