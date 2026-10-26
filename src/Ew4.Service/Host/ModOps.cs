using System.Text.Json;
using Ew4.Interface.Mod;
using Ew4.Platform.Mod.Catalog;
using Ew4.Platform.Mod.Import;

namespace Ew4.Service.Host;

public sealed class ModOps : IModOperations
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ModCatalogService _catalog;
    private readonly ModImportService _imports;

    public ModOps(ModCatalogService catalog, ModImportService imports)
    {
        _catalog = catalog;
        _imports = imports;
    }

    public string GetPanelStateJson() =>
        JsonSerializer.Serialize(ModPanelApi.Serialize(GetPanelState()), Json);

    public void Activate(string? modId) => _catalog.Activate(modId);

    public void Remove(string modId) => _catalog.Remove(modId);

    public ModImportPrepared StageUploadedFile(string sourcePath, string fileName)
    {
        PreparedModImport prepared = _imports.StageUploadedFile(sourcePath, fileName);
        string identityJson = JsonSerializer.Serialize(prepared.Identity, Json);
        return new ModImportPrepared(prepared.Token, identityJson);
    }

    public void Discard(string? token) => _imports.Discard(token);

    public void Commit(string token, string id, string name) => _imports.Commit(token, id, name);

    private ModPanelState GetPanelState() => _catalog.GetPanelState();
}
