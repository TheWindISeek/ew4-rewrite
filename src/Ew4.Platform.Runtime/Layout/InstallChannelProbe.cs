namespace Ew4.Platform.Runtime.Layout;

/// <summary>
/// 从当前工作目录或可执行文件位置推断渠道安装根（<c>…/mods</c> + <c>…/user</c>）。
/// 用于仅携带 <c>install/</c> 发行包、无需仓库 <c>src/</c> 时启动宿主。
/// </summary>
public static class InstallChannelProbe
{
    public static bool LooksLikeChannelHome(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        string root = Path.GetFullPath(directory);
        return Directory.Exists(Path.Combine(root, InstallLayout.Install.Mods))
            && Directory.Exists(Path.Combine(root, InstallLayout.Install.User.Root));
    }

    /// <summary>
    /// 自 <paramref name="startDirectory"/> 向上查找渠道安装根；优先 <paramref name="preferredChannel"/>（如 <c>web</c>）。
    /// </summary>
    public static string? TryFindChannelHome(string? startDirectory, string preferredChannel = InstallLayout.Channels.Web)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return null;
        }

        string? dir = Path.GetFullPath(startDirectory);
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
