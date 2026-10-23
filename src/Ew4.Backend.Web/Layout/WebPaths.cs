using Ew4.Platform.Content.Layout;
using Ew4.Platform.Runtime.Layout;

namespace Ew4.Backend.Web.Layout;

/// <summary>
/// Web 宿主与静态前端路径（<c>install.app</c> 与开发态 <c>repo.src.host.web</c>）。
/// </summary>
public static class WebPaths
{
    public static class App
    {
        public const string WwwRoot = "wwwroot";
    }

    public static string ResolveWwwRoot(InstallLayout install, string repositoryRoot)
    {
        string published = Path.Combine(install.AppDirectory, App.WwwRoot);
        if (Directory.Exists(published))
        {
            return published;
        }

        string dev = RepositoryPaths.FrontendWwwRoot(repositoryRoot);
        return Directory.Exists(dev) ? dev : published;
    }
}
