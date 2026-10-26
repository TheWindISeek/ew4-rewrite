using Ew4.Platform.Persistence.Layout;
using Xunit;

namespace Ew4.Test.Service;

public sealed class InstallChannelProbeTests
{
    [Fact]
    public void Finds_channel_home_from_app_subdirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "ew4-probe-" + Guid.NewGuid().ToString("N"));
        string home = Path.Combine(root, "install", "web");
        Directory.CreateDirectory(Path.Combine(home, "app"));
        Directory.CreateDirectory(Path.Combine(home, "mods"));
        Directory.CreateDirectory(Path.Combine(home, "user"));

        string? found = InstallChannelProbe.TryFindChannelHome(Path.Combine(home, "app"));
        Assert.Equal(home, found);
    }

    [Fact]
    public void Finds_web_under_install_directory()
    {
        string root = Path.Combine(Path.GetTempPath(), "ew4-probe-" + Guid.NewGuid().ToString("N"));
        string home = Path.Combine(root, "install", "web");
        Directory.CreateDirectory(Path.Combine(home, "mods"));
        Directory.CreateDirectory(Path.Combine(home, "user"));

        string? found = InstallChannelProbe.TryFindChannelHome(Path.Combine(root, "install"));
        Assert.Equal(home, found);
    }
}
