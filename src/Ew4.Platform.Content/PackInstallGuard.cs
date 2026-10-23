using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

/// <summary>
/// 内容包安装、启用、删除时的 <c>parent</c> 与清单规则（见 docs/arch/README.md、task/done/refactor.md）。
/// </summary>
public static class ModInstallGuard
{
    public static void ValidateInstall(IModSource source, FileModRepository repository, IEngine engine)
    {
        ModManifest manifest = source.Manifest;
        AssertNotBaseOverwrite(manifest.Id);
        EngineRequirement.AssertCompatible(manifest.Requires, engine.Info);
        IModSource? parent = ResolveInstalledParent(manifest, repository);
        if (parent != null)
        {
            ValidateParentChain(parent);
            AssertFormatsCompatible(manifest, parent.Manifest);
            EngineRequirement.AssertCompatible(parent.Manifest.Requires, engine.Info);
        }
    }

    public static void ValidateActivation(IModSource pack, FileModRepository repository, IEngine engine)
    {
        EngineRequirement.AssertCompatible(pack.Manifest.Requires, engine.Info);
        IModSource? parent = ResolveInstalledParent(pack.Manifest, repository);
        if (parent != null)
        {
            ValidateParentChain(parent);
            AssertFormatsCompatible(pack.Manifest, parent.Manifest);
            EngineRequirement.AssertCompatible(parent.Manifest.Requires, engine.Info);
        }
    }

    public static void ValidateRemove(string modId, FileModRepository repository)
    {
        modId = ModId.Validate(modId);
        if (string.Equals(modId, ModId.Base, StringComparison.Ordinal))
        {
            throw new ContentError("mod.remove", "The base pack cannot be removed.");
        }

        IReadOnlyList<ModInfo> dependents = repository.List()
            .Where(p => string.Equals(p.Parent, modId, StringComparison.Ordinal))
            .ToList();
        if (dependents.Count > 0)
        {
            string names = string.Join(", ", dependents.Select(p => p.Id));
            throw new ContentError(
                "mod.remove.dependents",
                "Cannot remove pack '" + modId + "' because these packs depend on it: " + names + ".");
        }
    }

    private static void AssertNotBaseOverwrite(string id)
    {
        if (string.Equals(ModId.Validate(id), ModId.Base, StringComparison.Ordinal))
        {
            throw new ContentError("mod.install", "The base pack cannot be imported or overwritten.");
        }
    }

    private static IModSource? ResolveInstalledParent(ModManifest manifest, FileModRepository repository)
    {
        if (string.IsNullOrWhiteSpace(manifest.Parent))
        {
            return null;
        }

        string parentId = ModId.Validate(manifest.Parent);
        string parentDir = Path.Combine(repository.RootDirectory, parentId);
        if (!Directory.Exists(parentDir))
        {
            throw new ContentError(
                "mod.parent",
                "Parent pack '" + parentId + "' is not installed. Install it before this pack.");
        }

        return repository.Open(parentId);
    }

    private static void ValidateParentChain(IModSource parent)
    {
        if (!string.IsNullOrWhiteSpace(parent.Manifest.Parent))
        {
            throw new ContentError(
                "mod.parent.chain",
                "Parent pack '" + parent.Manifest.Id + "' cannot itself have a parent (only one overlay layer is allowed).");
        }
    }

    private static void AssertFormatsCompatible(ModManifest child, ModManifest parent)
    {
        foreach (KeyValuePair<string, int> pair in child.Formats)
        {
            if (parent.Formats.TryGetValue(pair.Key, out int parentVersion) && parentVersion != pair.Value)
            {
                throw new ContentError(
                    "mod.formats",
                    $"Format '{pair.Key}' version {pair.Value} does not match parent '{parent.Id}' version {parentVersion}.");
            }
        }
    }
}
