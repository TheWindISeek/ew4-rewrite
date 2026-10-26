using Ew4.Platform.Persistence;
using Ew4.Platform.Persistence.Io;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ew4.Platform.Persistence.Packages;

public sealed class FileModRepository : IModRepository
{
    private readonly string _root;

    public FileModRepository(string packsRootDirectory)
    {
        _root = PhysicalFiles.GetFullPath(packsRootDirectory);
        PhysicalFiles.EnsureDirectory(_root);
    }

    public IReadOnlyList<ModInfo> List()
    {
        if (!PhysicalFiles.DirectoryExists(_root))
        {
            return Array.Empty<ModInfo>();
        }

        var list = new List<ModInfo>();
        foreach (string directory in PhysicalFiles.EnumerateDirectories(_root))
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
        if (PhysicalFiles.DirectoryExists(destination) && !options.ReplaceExisting)
        {
            throw new PersistenceError("mod.install", $"Pack '{id}' is already installed.");
        }

        if (PhysicalFiles.DirectoryExists(destination))
        {
            PhysicalFiles.DeleteDirectoryRecursive(destination);
        }

        PhysicalFiles.EnsureDirectory(destination);
        foreach (string path in source.List(""))
        {
            string target = Path.Combine(destination, path.Replace('/', Path.DirectorySeparatorChar));
            PhysicalFiles.WriteAllBytes(target, source.Read(path));
        }

        return new ModInfo(source.Manifest.Id, source.Manifest.Name, source.Manifest.Version, source.Manifest.Parent);
    }

    public void Remove(string modId)
    {
        modId = ModId.Validate(modId);
        if (string.Equals(modId, ModId.Base, StringComparison.Ordinal))
        {
            throw new PersistenceError("mod.remove", "The base pack cannot be removed.");
        }
        string destination = Path.Combine(_root, modId);
        if (PhysicalFiles.DirectoryExists(destination))
        {
            PhysicalFiles.DeleteDirectoryRecursive(destination);
        }
    }

    public long MeasureInstalledBytes(string modId)
    {
        modId = ModId.Validate(modId);
        return PhysicalFiles.MeasureDirectoryBytes(Path.Combine(_root, modId));
    }

    public IModSource Open(string modId)
    {
        modId = ModId.Validate(modId);
        string destination = Path.Combine(_root, modId);
        return new DirectoryModSource(destination);
    }

    public string RootDirectory => _root;
}
