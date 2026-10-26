using Ew4.Interface.Host;
using Ew4.Platform.Persistence.Layout;

namespace Ew4.Service.Host;

public static class Ew4HostLaunch
{
    public static Ew4HostLaunchOptions Resolve(string[] launchArgs, string? currentDirectory = null)
    {
        string repositoryRoot = ResolveRepositoryRoot(launchArgs, currentDirectory);
        string homeRoot = ResolveInstallHome(launchArgs, repositoryRoot);
        return new Ew4HostLaunchOptions(repositoryRoot, homeRoot);
    }

    internal static InstallLayout ToInstallLayout(Ew4HostLaunchOptions launch)
    {
        var install = new InstallLayout(launch.InstallHome);
        install.EnsureRuntimeDirectories();
        return install;
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
