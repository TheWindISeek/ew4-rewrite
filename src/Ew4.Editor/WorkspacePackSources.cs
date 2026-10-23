using Ew4.Platform.Content;

namespace Ew4.Editor;

public static class WorkspacePackSources
{
    public static IPackSource OpenMerged(string workspaceRoot, FilePackRepository packs)
    {
        var primary = new DirectoryPackSource(workspaceRoot);
        IPackSource? parent = OpenParent(primary, packs);
        return parent == null ? primary : new MergedPackSource(primary, parent);
    }

    public static ResolvedContent Resolve(
        string workspaceRoot,
        FilePackRepository packs,
        ContentResolver resolver)
    {
        var primary = new DirectoryPackSource(workspaceRoot);
        IPackSource? parent = OpenParent(primary, packs);
        return resolver.Resolve(primary, parent);
    }

    private static IPackSource? OpenParent(IPackSource primary, FilePackRepository packs)
    {
        string? parentId = primary.Manifest.Parent;
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return null;
        }

        return packs.Open(parentId);
    }
}
