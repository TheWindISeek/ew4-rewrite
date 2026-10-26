namespace Ew4.Tool.Export;

/// <summary>把合并后的 Mod 写回原版壳 APK。CLI 与未来的 HTTP 入口共用同一实现。</summary>
public interface IApkExporter
{
    /// <summary>只做兼容性检查，不产出文件。</summary>
    IReadOnlyList<ApkCompatIssue> Check(ApkExportRequest request);

    /// <summary>导出。有兼容性问题且未设 <see cref="ApkExportRequest.AllowLossy"/> 时应失败（退出码 3）。</summary>
    ApkExportResult Export(ApkExportRequest request);
}
