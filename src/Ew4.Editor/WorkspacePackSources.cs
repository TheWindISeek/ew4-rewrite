using Ew4.Platform.Content;

namespace Ew4.Editor;

public static class WorkspacePackSources
{
    public static IPackSource OpenMerged(string workspaceRoot, FilePackRepository packs)
    {
        var primary = new DirectoryPackSource(workspaceRoot);
        return PackMerge.Apply(primary, PackMerge.OpenParent(primary, packs));
    }

    public static ResolvedContent Resolve(
        string workspaceRoot,
        FilePackRepository packs,
        ContentResolver resolver)
    {
        var primary = new DirectoryPackSource(workspaceRoot);
        return resolver.Resolve(primary, PackMerge.OpenParent(primary, packs));
    }
}
