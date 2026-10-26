using Ew4.Platform.Persistence.Layout;

namespace Ew4.Service.Host;

internal static class WebHostPaths
{
    public const string WwwRootFolderName = "wwwroot";

    public static string ResolveWwwRoot(InstallLayout install, string repositoryRoot)
    {
        string published = Path.Combine(install.AppDirectory, WwwRootFolderName);
        if (Directory.Exists(published))
        {
            return published;
        }

        string besideHost = Path.Combine(AppContext.BaseDirectory, WwwRootFolderName);
        if (Directory.Exists(besideHost))
        {
            return besideHost;
        }

        string dev = RepositoryPaths.FrontendWwwRoot(repositoryRoot);
        return Directory.Exists(dev) ? dev : published;
    }
}
