namespace Ew4.Tool.Export;

/// <summary>导出请求。契约见 docs/task/plan/apk-export.md。</summary>
public sealed record ApkExportRequest(
    string OutPath,
    string? ShellApkPath = null,
    string? ModId = null,
    string? WorkspacePath = null,
    string? Ew4ModPath = null,
    long? ExportId = null,
    bool DryRun = false,
    bool AllowLossy = false);

/// <summary>兼容性问题：某个文件/字段在原版 APK 格式里表达不了。</summary>
public sealed record ApkCompatIssue(string Path, string Field, string Reason, string Impact);

public sealed record ApkExportResult(
    string? OutPath,
    long ExportId,
    int FilesPatched,
    IReadOnlyList<ApkCompatIssue> Compat);
