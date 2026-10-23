namespace Ew4.Platform.Content.Layout;

/// <summary>
/// 仓库根（含 <c>src/</c>、<c>maps/</c>）下的相对路径分段。
/// 文档习惯写作 <c>repo.build</c>、<c>repo.data.import_seed</c>；C# 类型名为 PascalCase。
/// </summary>
public static class RepositoryPaths
{
    public static class Repo
    {
        public const string Build = "build";

        public static class Data
        {
            public const string ImportSeed = "data/import-seed";
        }

        public static class Maps
        {
            public const string EastAsiaRewrite = "maps/eastasia_rewrite";
            public const string MingguoCityPlan = "maps/mingguo/city_plan";
        }

        public static class Src
        {
            public static class Web
            {
                public const string WwwRoot = "src/Ew4.Front.Web/wwwroot";
            }

        }

        public static class Reports
        {
            public const string Root = "build/reports";
        }
    }

    public static string Combine(string repositoryRoot, string relativePath) =>
        Path.Combine(Path.GetFullPath(repositoryRoot), relativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string BuildRoot(string repositoryRoot) => Combine(repositoryRoot, Repo.Build);

    public static string ImportSeedDirectory(string repositoryRoot)
    {
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        foreach (string relative in new[] { Repo.Data.ImportSeed })
        {
            string candidate = Combine(repositoryRoot, relative);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new ContentError(
            "repo.import-seed",
            "找不到 data/import-seed（用于生成 mods/base 的原料，不进 Git）。");
    }

    public static string ReportsCategory(string repositoryRoot, string category) =>
        Combine(repositoryRoot, $"{Repo.Reports.Root}/{category}");

    public static string EastAsiaAuthoring(string repositoryRoot) =>
        Combine(repositoryRoot, Repo.Maps.EastAsiaRewrite);

    public static string MingguoCityPlan(string repositoryRoot) =>
        Combine(repositoryRoot, Repo.Maps.MingguoCityPlan);

    public static string FrontendWwwRoot(string repositoryRoot) =>
        Combine(repositoryRoot, Repo.Src.Web.WwwRoot);

}
