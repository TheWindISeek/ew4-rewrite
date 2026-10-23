using Ew4.Platform.Content.Layout;

namespace Ew4.Platform.Content;

/// <summary>
/// 兼容入口：新代码请用 <see cref="RepositoryPaths"/> 与 <see cref="Ew4.Platform.Runtime.Layout.InstallLayout"/>。
/// </summary>
public static class RepositoryLayout
{
    public const string BuildRootRelative = RepositoryPaths.Repo.Build;

    public const string InstallRootRelative = "install";

    public const string ModsRootRelative = "mods";

    public const string ImportSeedRelative = RepositoryPaths.Repo.Data.ImportSeed;

    public const string EastAsiaAuthoringRelative = RepositoryPaths.Repo.Maps.EastAsiaRewrite;

    public const string MingguoCityPlanRelative = RepositoryPaths.Repo.Maps.MingguoCityPlan;

    public const string FrontendWwwRelative = RepositoryPaths.Repo.Src.Web.WwwRoot;

    public static string ResolveBuildRoot(string contentRoot) =>
        RepositoryPaths.BuildRoot(contentRoot);

    public static string ResolveInstallRoot(string contentRoot) =>
        ResolveInstallHome(contentRoot);

    public static string ResolveImportSeedDirectory(string contentRoot) =>
        RepositoryPaths.ImportSeedDirectory(contentRoot);

    public static string FrontendWwwRoot(string contentRoot) =>
        RepositoryPaths.FrontendWwwRoot(contentRoot);

    public static string ReportsRoot(string contentRoot, string category) =>
        RepositoryPaths.ReportsCategory(contentRoot, category);

    public static string EastAsiaAuthoringDirectory(string contentRoot) =>
        RepositoryPaths.EastAsiaAuthoring(contentRoot);

    public static string MingguoCityPlanDirectory(string contentRoot) =>
        RepositoryPaths.MingguoCityPlan(contentRoot);

    /// <summary>内容包目录：<c>EW4_HOME/mods</c>（默认 <c>install/web/mods</c>）。</summary>
    public static string ModsDirectory(string repositoryRoot) =>
        Path.Combine(ResolveInstallHome(repositoryRoot), ModsRootRelative);

    public static string UserStateDirectory(string repositoryRoot) =>
        Path.Combine(ResolveInstallHome(repositoryRoot), "user");

    private static string ResolveInstallHome(string repositoryRoot)
    {
        string? env = Environment.GetEnvironmentVariable("EW4_HOME");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return Path.GetFullPath(env.Trim());
        }

        return Path.Combine(Path.GetFullPath(repositoryRoot), InstallRootRelative);
    }
}
