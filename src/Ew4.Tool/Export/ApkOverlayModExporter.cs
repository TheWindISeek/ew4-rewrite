namespace Ew4.Tool.Export;

/// <summary>
/// <see cref="IApkExporter"/> 的占位实现：合并 → 逆映射 → btl → 补丁 assets → Manifest → zipalign → 签名。
/// 尚未实现，见 docs/task/plan/apk-export.md。
/// </summary>
public sealed class ApkOverlayModExporter : IApkExporter
{
    public IReadOnlyList<ApkCompatIssue> Check(ApkExportRequest request) =>
        throw new NotImplementedException("APK export is not implemented yet.");

    public ApkExportResult Export(ApkExportRequest request) =>
        throw new NotImplementedException("APK export is not implemented yet.");
}
