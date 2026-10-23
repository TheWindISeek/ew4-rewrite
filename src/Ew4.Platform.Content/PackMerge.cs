using System;

namespace Ew4.Platform.Content;

/// <summary>
/// 按 <c>mod.json</c> 的 <c>parent</c> 合并已安装的 Pack（整文件覆盖）。
/// </summary>
public static class ModMerge
{
    /// <summary>
    /// 差异 Mod 的 overlay 目录（仅 primary 内的 <c>scenarios/*.json</c> 等），合并后的 vfs 仍用 <see cref="Apply"/> 结果读取。
    /// </summary>
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
