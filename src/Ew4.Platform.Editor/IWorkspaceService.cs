using Ew4.Interface.Common;

namespace Ew4.Platform.Editor;

public sealed record WorkspaceInfo(
    string Id,
    string Name,
    string ParentModId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastValidatedAt,
    ValidationLevel? LastValidationLevel,
    string Mode = "baseTweak");

public sealed record WriteResult(string Path, IReadOnlyList<Diagnostic> Diagnostics);

/// <remarks>Standalone 为假时只导出 workspace 自身的 overlay（依赖 parent）；为真时合并 parent 内容并去掉 parent 引用。</remarks>
public sealed record ExportOptions(
    ValidationLevel ValidationLevel = ValidationLevel.SmokeStart,
    string? FileName = null,
    bool Standalone = false);

public sealed record ExportResult(
    bool Success,
    string? ArchivePath,
    IReadOnlyList<Diagnostic> Diagnostics);

public interface IWorkspaceService
{
    string WorkspacesRoot { get; }

    IReadOnlyList<WorkspaceInfo> List();

    WorkspaceInfo Create(string fromModId, string newId, string name, Ew4.Platform.Editor.Campaign.EditorMode mode = Ew4.Platform.Editor.Campaign.EditorMode.BaseTweak);

    Ew4.Platform.Editor.Campaign.EditorMode GetMode(string workspaceId);

    void Delete(string workspaceId);

    byte[] ReadFile(string workspaceId, string logicalPath);

    WriteResult WriteFile(string workspaceId, string logicalPath, byte[] data);

    IReadOnlyList<Diagnostic> Validate(string workspaceId, ValidationLevel upTo);

    ExportResult Export(string workspaceId, ExportOptions options);

    IModSource OpenMergedMod(string workspaceId);

    string ResolveWorkspaceRoot(string workspaceId);

    /// <summary>按模式分组的战役 id 列表（JSON 对象字符串，供 <see cref="IEditorWorkspaceOperations"/>）。</summary>
    string ListCampaignsJson(string workspaceId);
}
