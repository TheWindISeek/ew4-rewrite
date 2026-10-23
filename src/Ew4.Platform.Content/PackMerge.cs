using System;

namespace Ew4.Platform.Content;

/// <summary>
/// 按 <c>mod.json</c> 的 <c>parent</c> 合并已安装的 Pack（整文件覆盖）。
/// </summary>
public static class PackMerge
{
    /// <summary>
    /// 差异 Mod 的 overlay 目录（仅 primary 内的 <c>scenarios/*.json</c> 等），合并后的 vfs 仍用 <see cref="Apply"/> 结果读取。
    /// </summary>
    public static IPackSource OverlayCatalog(IPackSource source) =>
        source is MergedPackSource { Parent: not null } merged ? merged.Primary : source;

    public static IPackSource? OpenParent(IPackSource primary, IPackRepository repository)
    {
        string? parentId = primary.Manifest.Parent;
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return null;
        }

        return repository.Open(parentId);
    }

    public static IPackSource Apply(IPackSource primary, IPackSource? parent) =>
        parent == null ? primary : new MergedPackSource(primary, parent);

    public static IPackSource OpenMerged(IPackRepository repository, string packId)
    {
        IPackSource primary = repository.Open(packId);
        return Apply(primary, OpenParent(primary, repository));
    }
}
