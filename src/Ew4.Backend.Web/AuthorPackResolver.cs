using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Runtime;

namespace Ew4.Backend.Web;

internal static class AuthorModResolver
{
    public static IModSource OpenMergedMod(FileModRepository repository, string modId) =>
        ModMerge.OpenMerged(repository, modId);

    public static IModSource OpenWorkspacePack(FileModRepository repository, string workspaceRoot) =>
        WorkspaceModSources.OpenMerged(workspaceRoot, repository);

    public static IModSource OpenActivePack(FileModRepository repository, HostApp host)
    {
        string modId = host.Status().ActiveModId ?? ModId.Base;
        return OpenMergedMod(repository, modId);
    }
}
