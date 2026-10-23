namespace Ew4.Platform.Runtime.Layout;

/// <summary>
/// 安装根（<c>EW4_HOME</c> / <c>install/</c>）下的相对路径。
/// 分段命名对齐 Java 风格：<c>install.app</c>、<c>install.user.saves</c>。
/// </summary>
public sealed class InstallLayout
{
    public static class Install
    {
        public const string App = "app";
        public const string Mods = "mods";

        public static class User
        {
            public const string Root = "user";
            public const string Active = "active.json";
            public const string Saves = "saves";
            public const string Workspaces = "workspaces";
            public const string Cache = "cache";
            public const string Logs = "logs";
            public const string Tmp = "tmp";
            public const string PendingImports = "pending-imports";
        }
    }

    private readonly string _home;

    public InstallLayout(string homeRoot)
    {
        if (string.IsNullOrWhiteSpace(homeRoot))
        {
            throw new ArgumentException("Install home is required.", nameof(homeRoot));
        }

        _home = Path.GetFullPath(homeRoot);
    }

    public string Home => _home;

    public string AppDirectory => Segment(Install.App);

    public string ModsDirectory => Segment(Install.Mods);

    public string UserDirectory => Segment(Install.User.Root);

    public string ActivePackFile => Path.Combine(UserDirectory, Install.User.Active);

    public string SavesDirectory => Path.Combine(UserDirectory, Install.User.Saves);

    public string WorkspacesDirectory => Path.Combine(UserDirectory, Install.User.Workspaces);

    public string CacheDirectory => Path.Combine(UserDirectory, Install.User.Cache);

    public string LogsDirectory => Path.Combine(UserDirectory, Install.User.Logs);

    public string TmpDirectory => Path.Combine(UserDirectory, Install.User.Tmp);

    public string PendingImportsDirectory => Path.Combine(UserDirectory, Install.User.PendingImports);

    public string Segment(string relative) =>
        Path.Combine(_home, relative.Replace('/', Path.DirectorySeparatorChar));

    public static string? HomeFromEnvironment()
    {
        string? env = Environment.GetEnvironmentVariable("EW4_HOME");
        return string.IsNullOrWhiteSpace(env) ? null : Path.GetFullPath(env.Trim());
    }

    /// <summary>
    /// 默认安装目录：仓库下的 <c>install/</c>（与 <see cref="Ew4.Platform.Content.Layout.RepositoryPaths.Repo.Build"/> 同级）。
    /// </summary>
    public static InstallLayout DefaultUnderRepository(string repositoryRoot) =>
        new(Path.Combine(Path.GetFullPath(repositoryRoot), "install"));

    public void EnsureRuntimeDirectories()
    {
        Directory.CreateDirectory(AppDirectory);
        Directory.CreateDirectory(ModsDirectory);
        Directory.CreateDirectory(UserDirectory);
        Directory.CreateDirectory(WorkspacesDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(TmpDirectory);
        Directory.CreateDirectory(PendingImportsDirectory);
    }
}
