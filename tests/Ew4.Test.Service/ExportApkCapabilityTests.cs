using Ew4.Service.Tooling;
using Xunit;

namespace Ew4.Test.Service;

public sealed class ExportApkCapabilityTests
{
    [Fact]
    public void Supported_when_channel_unset_or_web()
    {
        string? previous = Environment.GetEnvironmentVariable("EW4_CHANNEL");
        try
        {
            Environment.SetEnvironmentVariable("EW4_CHANNEL", null);
            Assert.True(ExportApkCapability.Supported);
            Environment.SetEnvironmentVariable("EW4_CHANNEL", "web");
            Assert.True(ExportApkCapability.Supported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EW4_CHANNEL", previous);
        }
    }

    [Fact]
    public void Not_supported_on_apk_channel()
    {
        string? previous = Environment.GetEnvironmentVariable("EW4_CHANNEL");
        try
        {
            Environment.SetEnvironmentVariable("EW4_CHANNEL", "apk");
            Assert.False(ExportApkCapability.Supported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EW4_CHANNEL", previous);
        }
    }
}
