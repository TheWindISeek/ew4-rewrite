namespace Ew4.Tool.Export;

/// <summary>
/// 兼容性检查：找出原版 APK 格式表达不了的内容（如 schema 3 剧本里的新字段）。
/// 原版不认识的内容（<c>client/lang/</c>、<c>client/music*</c>）属于"自动忽略"，不进清单。
/// 尚未实现。
/// </summary>
public sealed class ApkCompatibilityChecker
{
    public IReadOnlyList<ApkCompatIssue> Check(ApkExportRequest request) =>
        throw new NotImplementedException("APK compatibility check is not implemented yet.");
}
