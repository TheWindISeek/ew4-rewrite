using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

/// <summary>
/// 内容包安装、启用、删除时的 <c>parent</c> 与清单规则（architecture/11 §4.3 P1–P8）。
/// </summary>
public static class PackInstallGuard
{
    public static void ValidateInstall(IPackSource source, FilePackRepository repository, IEngine engine)
    {
        PackManifest manifest = source.Manifest;
        AssertNotBaseOverwrite(manifest.Id);
        EngineRequirement.AssertCompatible(manifest.Requires, engine.Info);
        IPackSource? parent = ResolveInstalledParent(manifest, repository);
        if (parent != null)
        {
            ValidateParentChain(parent);
            AssertFormatsCompatible(manifest, parent.Manifest);
            EngineRequirement.AssertCompatible(parent.Manifest.Requires, engine.Info);
        }
    }

    public static void ValidateActivation(IPackSource pack, FilePackRepository repository, IEngine engine)
    {
        EngineRequirement.AssertCompatible(pack.Manifest.Requires, engine.Info);
        IPackSource? parent = ResolveInstalledParent(pack.Manifest, repository);
        if (parent != null)
        {
            ValidateParentChain(parent);
            AssertFormatsCompatible(pack.Manifest, parent.Manifest);
            EngineRequirement.AssertCompatible(parent.Manifest.Requires, engine.Info);
        }
    }

    public static void ValidateRemove(string packId, FilePackRepository repository)
    {
        packId = PackId.Validate(packId);
        if (string.Equals(packId, PackId.Base, StringComparison.Ordinal))
        {
            throw new ContentError("pack.remove", "The base pack cannot be removed.");
        }

        IReadOnlyList<PackInfo> dependents = repository.List()
            .Where(p => string.Equals(p.Parent, packId, StringComparison.Ordinal))
            .ToList();
        if (dependents.Count > 0)
        {
            string names = string.Join(", ", dependents.Select(p => p.Id));
            throw new ContentError(
                "pack.remove.dependents",
                "Cannot remove pack '" + packId + "' because these packs depend on it: " + names + ".");
        }
    }

    private static void AssertNotBaseOverwrite(string id)
    {
        if (string.Equals(PackId.Validate(id), PackId.Base, StringComparison.Ordinal))
        {
            throw new ContentError("pack.install", "The base pack cannot be imported or overwritten.");
        }
    }

    private static IPackSource? ResolveInstalledParent(PackManifest manifest, FilePackRepository repository)
    {
        if (string.IsNullOrWhiteSpace(manifest.Parent))
        {
            return null;
        }

        string parentId = PackId.Validate(manifest.Parent);
        string parentDir = Path.Combine(repository.RootDirectory, parentId);
        if (!Directory.Exists(parentDir))
        {
            throw new ContentError(
                "pack.parent",
                "Parent pack '" + parentId + "' is not installed. Install it before this pack.");
        }

        return repository.Open(parentId);
    }

    private static void ValidateParentChain(IPackSource parent)
    {
        if (!string.IsNullOrWhiteSpace(parent.Manifest.Parent))
        {
            throw new ContentError(
                "pack.parent.chain",
                "Parent pack '" + parent.Manifest.Id + "' cannot itself have a parent (only one overlay layer is allowed).");
        }
    }

    private static void AssertFormatsCompatible(PackManifest child, PackManifest parent)
    {
        foreach (KeyValuePair<string, int> pair in child.Formats)
        {
            if (parent.Formats.TryGetValue(pair.Key, out int parentVersion) && parentVersion != pair.Value)
            {
                throw new ContentError(
                    "pack.formats",
                    $"Format '{pair.Key}' version {pair.Value} does not match parent '{parent.Id}' version {parentVersion}.");
            }
        }
    }
}
