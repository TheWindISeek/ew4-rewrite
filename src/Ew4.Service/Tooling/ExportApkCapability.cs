using Ew4.Platform.Persistence.Layout;

namespace Ew4.Service.Tooling;

/// <summary>当前宿主是否具备 Mod → 侧载 APK 导出（开发机 Docker / Web 渠道）；Android 游戏 APK 内不可用。</summary>
public static class ExportApkCapability
{
    public static bool Supported =>
        !string.Equals(
            Environment.GetEnvironmentVariable("EW4_CHANNEL"),
            InstallLayout.Channels.Apk,
            StringComparison.OrdinalIgnoreCase);
}
