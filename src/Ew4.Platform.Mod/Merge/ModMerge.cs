using Ew4.Platform.Persistence.Packages;

namespace Ew4.Platform.Mod.Merge;

/// <summary>按 <c>mod.json</c> 的 <c>parent</c> 合并已安装的 Pack（整文件覆盖）。</summary>
public static class ModMerge
{
    public static IModSource OverlayCatalog(IModSource source) =>
        source is MergedModSource { Parent: not null } merged ? merged.Primary : source;

    public static IModSource? OpenParent(IModSource primary, IModRepository repository)
    {
        string? parentId = primary.Manifest.Parent;
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return null;
        }

        return repository.Open(parentId);
    }

    public static IModSource Apply(IModSource primary, IModSource? parent) =>
        parent == null ? primary : new MergedModSource(primary, parent);

    public static IModSource OpenMerged(IModRepository repository, string modId)
    {
        IModSource primary = repository.Open(modId);
        return Apply(primary, OpenParent(primary, repository));
    }
}
