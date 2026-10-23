using Ew4.Platform.Content;

namespace Ew4.Editor;

public sealed record WorkspaceInfo(
    string Id,
    string Name,
    string ParentModId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastValidatedAt,
    ValidationLevel? LastValidationLevel);

public sealed record WriteResult(string Path, IReadOnlyList<Diagnostic> Diagnostics);

public sealed record ExportOptions(
    ValidationLevel ValidationLevel = ValidationLevel.SmokeStart,
    string? FileName = null);

public sealed record ExportResult(
    bool Success,
    string? ArchivePath,
    IReadOnlyList<Diagnostic> Diagnostics);

public interface IWorkspaceService
{
    string WorkspacesRoot { get; }

    IReadOnlyList<WorkspaceInfo> List();

    WorkspaceInfo Create(string fromModId, string newId, string name);

    void Delete(string workspaceId);

    byte[] ReadFile(string workspaceId, string logicalPath);

    WriteResult WriteFile(string workspaceId, string logicalPath, byte[] data);

    IReadOnlyList<Diagnostic> Validate(string workspaceId, ValidationLevel upTo);

    ExportResult Export(string workspaceId, ExportOptions options);

    IModSource OpenMergedMod(string workspaceId);

    string ResolveWorkspaceRoot(string workspaceId);
}
