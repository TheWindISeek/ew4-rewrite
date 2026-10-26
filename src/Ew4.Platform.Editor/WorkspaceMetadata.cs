using System.Text.Json;
using Ew4.Platform.Content;
using Ew4.Platform.Persistence.Workspaces;

namespace Ew4.Platform.Editor;

internal sealed class WorkspaceMetadata
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastValidatedAt { get; set; }
    public ValidationLevel? LastValidationLevel { get; set; }
    public string Mode { get; set; } = "baseTweak";
    public List<WorkspaceExportRecord> Exports { get; set; } = [];

    public static WorkspaceMetadata Load(WorkspaceStore store, string workspaceRoot)
    {
        if (!store.TryReadMetadataJson(workspaceRoot, out string json))
        {
            return new WorkspaceMetadata();
        }

        return JsonSerializer.Deserialize<WorkspaceMetadata>(json, JsonOptions)
            ?? new WorkspaceMetadata();
    }

    public void Save(WorkspaceStore store, string workspaceRoot)
    {
        string serialized = JsonSerializer.Serialize(this, JsonOptions);
        store.WriteMetadataJson(workspaceRoot, serialized);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}

internal sealed record WorkspaceExportRecord(DateTimeOffset ExportedAt, string ArchivePath, ValidationLevel Level);
