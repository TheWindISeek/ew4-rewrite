using Ew4.Platform.Persistence.Io;

namespace Ew4.Platform.Persistence.Layout;

/// <summary>从 CWD 或可执行文件位置推断渠道安装根。</summary>
public static class InstallChannelProbe
{
    public static bool LooksLikeChannelHome(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !PhysicalFiles.DirectoryExists(directory))
        {
            return false;
        }

        string root = PhysicalFiles.GetFullPath(directory);
        return PhysicalFiles.DirectoryExists(Path.Combine(root, InstallLayout.Install.Mods))
            && PhysicalFiles.DirectoryExists(Path.Combine(root, InstallLayout.Install.User.Root));
    }

    public static string? TryFindChannelHome(string? startDirectory, string preferredChannel = InstallLayout.Channels.Web)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return null;
        }

        string? dir = PhysicalFiles.GetFullPath(startDirectory);
        for (int depth = 0; depth < 12 && dir != null; depth++)
        {
            if (LooksLikeChannelHome(dir))
            {
                return dir;
            }

            if (string.Equals(Path.GetFileName(dir), InstallLayout.Install.App, StringComparison.OrdinalIgnoreCase))
            {
                string? parent = Path.GetDirectoryName(dir);
                if (parent != null && LooksLikeChannelHome(parent))
                {
                    return parent;
                }
            }

            string installWeb = Path.Combine(dir, InstallLayout.Install.Root, preferredChannel);
            if (LooksLikeChannelHome(installWeb))
            {
                return installWeb;
            }

            if (string.Equals(Path.GetFileName(dir), InstallLayout.Install.Root, StringComparison.OrdinalIgnoreCase))
            {
                string channel = Path.Combine(dir, preferredChannel);
                if (LooksLikeChannelHome(channel))
                {
                    return channel;
                }
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    public static string? TryFindFromProcess(string preferredChannel = InstallLayout.Channels.Web)
    {
        string? fromCwd = TryFindChannelHome(Directory.GetCurrentDirectory(), preferredChannel);
        if (fromCwd != null)
        {
            return fromCwd;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        string? processDir = Path.GetDirectoryName(processPath);
        return TryFindChannelHome(processDir, preferredChannel);
    }
}
