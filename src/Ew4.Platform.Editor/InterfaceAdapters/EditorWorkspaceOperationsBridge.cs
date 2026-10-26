using System.Text.Json;
using Ew4.Interface.Common;
using Ew4.Interface.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Maps;
using Ew4.Platform.Editor.Campaign;
using Ew4.Platform.Mod;

namespace Ew4.Platform.Editor.InterfaceAdapters;

public sealed class EditorWorkspaceOperationsBridge : IEditorWorkspaceOperations
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceService _workspaces;

    private readonly MapCatalogProvider _maps;

    public EditorWorkspaceOperationsBridge(IWorkspaceService workspaces, MapCatalogProvider maps)
    {
        _workspaces = workspaces;
        _maps = maps;
    }

    public string ListWorkspacesJson() => JsonSerializer.Serialize(_workspaces.List(), Json);

    public string CreateWorkspaceJson(string id, string name, string? fromModId, string? mode = null)
    {
        WorkspaceInfo info = _workspaces.Create(fromModId ?? ModId.Base, id, name, EditorModes.Parse(mode));
        return JsonSerializer.Serialize(info, Json);
    }

    public string CapabilitiesJson(string workspaceId)
    {
        EditorMode mode = _workspaces.GetMode(workspaceId);
        return JsonSerializer.Serialize(new { mode = EditorModes.Format(mode), capabilities = Capabilities.For(mode) }, Json);
    }

    public string MapListJson(string workspaceId) =>
        JsonSerializer.Serialize(Catalog(workspaceId).List(), Json);

    public string MapDescribeJson(string workspaceId, int mapId) =>
        JsonSerializer.Serialize(Catalog(workspaceId).Describe(mapId)
            ?? throw new ContentError("map.unknown", $"Base has no map {mapId}."), Json);

    public string MapPreviewJson(string workspaceId, int mapId, MapCropQuery? crop) =>
        JsonSerializer.Serialize(Catalog(workspaceId).Preview(mapId, crop == null ? null : new MapCrop(crop.X, crop.Y, crop.W, crop.H))
            ?? throw new ContentError("map.crop", "Unknown map or crop outside the map."), Json);

    private IMapCatalog Catalog(string workspaceId) => _maps.Get(_workspaces.OpenMergedMod(workspaceId));

    public void DeleteWorkspace(string workspaceId) => _workspaces.Delete(workspaceId);

    public string ValidateJson(string workspaceId, ValidationLevel level) =>
        JsonSerializer.Serialize(_workspaces.Validate(workspaceId, level).Select(ToDiagnosticDto), Json);

    public string ExportJson(string workspaceId, ValidationLevel level, string? fileName, bool standalone = false)
    {
        ExportResult result = _workspaces.Export(workspaceId, new ExportOptions(level, fileName, standalone));
        return JsonSerializer.Serialize(new
        {
            result.Success,
            result.ArchivePath,
            diagnostics = result.Diagnostics.Select(ToDiagnosticDto),
        }, Json);
    }

    public string ListCampaignsJson(string workspaceId) => _workspaces.ListCampaignsJson(workspaceId);

    public byte[] ReadFile(string workspaceId, string relativePath) =>
        _workspaces.ReadFile(workspaceId, relativePath);

    public string WriteFileJson(string workspaceId, string relativePath, byte[] content)
    {
        WriteResult result = _workspaces.WriteFile(workspaceId, relativePath, content);
        return JsonSerializer.Serialize(new
        {
            result.Path,
            diagnostics = result.Diagnostics.Select(ToDiagnosticDto),
        }, Json);
    }

    private static object ToDiagnosticDto(Diagnostic diagnostic) => new
    {
        severity = diagnostic.Severity.ToString(),
        code = diagnostic.Code,
        path = diagnostic.Path,
        message = diagnostic.Message,
        hint = diagnostic.Hint,
    };
}
