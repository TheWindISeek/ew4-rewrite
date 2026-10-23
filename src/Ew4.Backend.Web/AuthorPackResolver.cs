using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Runtime;

namespace Ew4.Backend.Web;

internal static class AuthorPackResolver
{
    public static IPackSource OpenMergedPack(FilePackRepository repository, string packId) =>
        PackMerge.OpenMerged(repository, packId);

    public static IPackSource OpenWorkspacePack(FilePackRepository repository, string workspaceRoot) =>
        WorkspacePackSources.OpenMerged(workspaceRoot, repository);

    public static IPackSource OpenActivePack(FilePackRepository repository, HostApp host)
    {
        string packId = host.Status().ActivePackId ?? PackId.Base;
        return OpenMergedPack(repository, packId);
    }
}
