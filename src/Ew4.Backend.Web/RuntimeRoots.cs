using Ew4.Platform.Runtime.Layout;

namespace Ew4.Backend.Web;

public sealed record RuntimeRoots(InstallLayout Install, string RepositoryRoot)
{
    public static RuntimeRoots Resolve(string[] launchArgs, string? currentDirectory = null)
    {
        string repositoryRoot = ResolveRepositoryRoot(launchArgs, currentDirectory);
        string homeRoot = ResolveInstallHome(launchArgs, repositoryRoot);
        var install = new InstallLayout(homeRoot);
        install.EnsureRuntimeDirectories();
        return new RuntimeRoots(install, repositoryRoot);
    }

    private static string ResolveInstallHome(string[] launchArgs, string repositoryRoot)
    {
        for (int i = 0; i < launchArgs.Length - 1; i++)
        {
            if (string.Equals(launchArgs[i], "--home", StringComparison.Ordinal))
            {
                return Path.GetFullPath(launchArgs[i + 1]);
            }
        }

        string? envHome = InstallLayout.HomeFromEnvironment();
        if (!string.IsNullOrWhiteSpace(envHome))
        {
            return envHome;
        }

        string? inferred = InstallChannelProbe.TryFindFromProcess();
        if (!string.IsNullOrWhiteSpace(inferred))
        {
            return inferred;
        }

        string defaultInstall = InstallLayout.DefaultUnderRepository(repositoryRoot).Home;
        if (Directory.Exists(defaultInstall))
        {
            return defaultInstall;
        }

        return defaultInstall;
    }

    private static string ResolveRepositoryRoot(string[] launchArgs, string? currentDirectory)
    {
        for (int i = 0; i < launchArgs.Length - 1; i++)
        {
            if (string.Equals(launchArgs[i], "--root", StringComparison.Ordinal))
            {
                return Path.GetFullPath(launchArgs[i + 1]);
            }
        }

        currentDirectory ??= Directory.GetCurrentDirectory();
        string cwd = Path.GetFullPath(currentDirectory);

        string installSibling = Path.Combine(cwd, "install");
        if (Directory.Exists(installSibling) && File.Exists(Path.Combine(cwd, "Ew4.sln")))
        {
            return cwd;
        }

        if (string.Equals(Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "install", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(cwd) ?? cwd;
        }

        return cwd;
    }
}
