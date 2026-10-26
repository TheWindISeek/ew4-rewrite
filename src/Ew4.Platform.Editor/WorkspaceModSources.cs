using Ew4.Interface.Mod;
using Ew4.Platform.Content;

namespace Ew4.Platform.Editor;

public static class WorkspaceModSources
{
    public static IModSource OpenMerged(string workspaceRoot, FileModRepository packs)
    {
        var primary = new DirectoryModSource(workspaceRoot);
        return ModMerge.Apply(primary, ModMerge.OpenParent(primary, packs));
    }

    public static ResolvedContent Resolve(
        string workspaceRoot,
        FileModRepository packs,
        IModSnapshotLoader loader)
    {
        var primary = new DirectoryModSource(workspaceRoot);
        return loader.ResolveView(primary, ModMerge.OpenParent(primary, packs)).AsResolvedContent();
    }
}
