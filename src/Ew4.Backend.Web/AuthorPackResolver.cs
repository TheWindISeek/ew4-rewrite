using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Runtime;

namespace Ew4.Backend.Web;

internal static class AuthorPackResolver
{
    public static IPackSource OpenMergedPack(FilePackRepository repository, string packId)
    {
        IPackSource pack = repository.Open(packId);
        return MergeWithParent(repository, pack);
    }

    public static IPackSource OpenWorkspacePack(FilePackRepository repository, string workspaceRoot) =>
        WorkspacePackSources.OpenMerged(workspaceRoot, repository);

    public static IPackSource OpenActivePack(FilePackRepository repository, HostApp host)
    {
        string packId = host.Status().ActivePackId ?? PackId.Base;
        return OpenMergedPack(repository, packId);
    }

    private static IPackSource MergeWithParent(FilePackRepository repository, IPackSource pack)
    {
        string? parentId = pack.Manifest.Parent;
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return pack;
        }

        IPackSource parent = repository.Open(parentId);
        return new MergedPackSource(pack, parent);
    }
}
