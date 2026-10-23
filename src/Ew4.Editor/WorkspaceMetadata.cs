using System.Text.Json;
using Ew4.Platform.Content;

namespace Ew4.Editor;

internal sealed class WorkspaceMetadata
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastValidatedAt { get; set; }
    public ValidationLevel? LastValidationLevel { get; set; }
    public List<WorkspaceExportRecord> Exports { get; set; } = [];

    public static WorkspaceMetadata Load(string workspaceRoot)
    {
        string path = MetaPath(workspaceRoot);
        if (!File.Exists(path))
        {
            return new WorkspaceMetadata();
        }

        return JsonSerializer.Deserialize<WorkspaceMetadata>(File.ReadAllText(path), JsonOptions)
            ?? new WorkspaceMetadata();
    }

    public void Save(string workspaceRoot)
    {
        string path = MetaPath(workspaceRoot);
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string MetaPath(string workspaceRoot) => Path.Combine(workspaceRoot, ".workspace.json");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}

internal sealed record WorkspaceExportRecord(DateTimeOffset ExportedAt, string ArchivePath, ValidationLevel Level);
