namespace Ew4.Platform.Runtime.Layout;

/// <summary>
/// 安装根（<c>EW4_HOME</c>）下的相对路径；仓库内按渠道分 <c>install/web</c>、<c>install/apk</c>。
/// 分段命名对齐 Java 风格：<c>install.app</c>、<c>install.user.saves</c>。
/// </summary>
public sealed class InstallLayout
{
    public static class Channels
    {
        public const string Web = "web";
        public const string Apk = "apk";
    }

    public static class Install
    {
        public const string Root = "install";
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

    public static string ChannelHomeUnderRepository(string repositoryRoot, string channel) =>
        Path.Combine(Path.GetFullPath(repositoryRoot), Install.Root, channel);

    public static InstallLayout WebUnderRepository(string repositoryRoot) =>
        new(ChannelHomeUnderRepository(repositoryRoot, Channels.Web));

    public static InstallLayout ApkUnderRepository(string repositoryRoot) =>
        new(ChannelHomeUnderRepository(repositoryRoot, Channels.Apk));

    /// <summary>
    /// 默认 Web 渠道安装根：<c>&lt;repo&gt;/install/web</c>。
    /// </summary>
    public static InstallLayout DefaultUnderRepository(string repositoryRoot) =>
        WebUnderRepository(repositoryRoot);

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
