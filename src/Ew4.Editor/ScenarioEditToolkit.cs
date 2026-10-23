using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Editor;

/// <summary>
/// <see cref="IScenarioEditToolkit"/> 默认实现；CLI 与未来的 Author API 应只依赖本类型。
/// </summary>
public sealed class ScenarioEditToolkit : IScenarioEditToolkit
{
    public static ScenarioEditToolkit Default { get; } = new();

    public ScenarioDocument ImportBtl(string scenarioId, byte[] btlBytes) =>
        ScenarioBtlCodec.Import(scenarioId, btlBytes);

    public byte[] ExportBtl(ScenarioDocument document, ScenarioExportMode mode = ScenarioExportMode.PreserveSections)
    {
        ScenarioDocument export = mode == ScenarioExportMode.ApplyLayers
            ? ScenarioBtlCodec.ApplyLayers(document)
            : document;
        return ScenarioBtlCodec.Export(export, reencodeLayers: false);
    }

    public byte[] SerializeJson(ScenarioDocument document) => ScenarioDocumentJson.Serialize(document);

    public ScenarioDocument DeserializeJson(byte[] jsonBytes) => ScenarioDocumentJson.Deserialize(jsonBytes);

    public ScenarioDocument ReadJsonFile(string jsonPath) =>
        DeserializeJson(File.ReadAllBytes(Path.GetFullPath(jsonPath)));

    public void WriteJsonFile(string jsonPath, ScenarioDocument document)
    {
        string path = Path.GetFullPath(jsonPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, SerializeJson(document));
    }

    public ScenarioDocument ReadBtlFile(string scenarioId, string btlPath) =>
        ImportBtl(scenarioId, File.ReadAllBytes(Path.GetFullPath(btlPath)));

    public void WriteBtlFile(string btlPath, ScenarioDocument document, ScenarioExportMode mode = ScenarioExportMode.ApplyLayers)
    {
        string path = Path.GetFullPath(btlPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, ExportBtl(document, mode));
    }

    public ScenarioBtlRoundtrip.Result ValidateBtlRoundtrip(string scenarioId, byte[] btlBytes) =>
        ScenarioBtlRoundtrip.Validate(scenarioId, btlBytes);

    public ScenarioDocument OpenFromPack(IPackSource pack, string scenarioId) =>
        ScenarioPackAccess.OpenScenario(pack, scenarioId);

    public ScenarioSaveResult SaveToWorkspace(string workspaceRoot, ScenarioDocument document)
    {
        string root = Path.GetFullPath(workspaceRoot);
        ScenarioDocument saved = ScenarioBtlCodec.ApplyLayers(document);
        string documentPath = Path.Combine(root, "scenarios", saved.Id + ".json");
        WriteJsonFile(documentPath, saved);
        return new("", "scenarios/" + saved.Id + ".json");
    }

    public ScenarioDocument PatchArmyCell(ScenarioDocument document, int armyIndex, short newCell) =>
        ScenarioEditPatch.PatchArmyCell(document, armyIndex, newCell);

    public ScenarioDocument PatchSiteCell(ScenarioDocument document, int siteIndex, short newCell) =>
        ScenarioEditPatch.PatchSiteCell(document, siteIndex, newCell);

    public ScenarioDocument PatchFactionResources(
        ScenarioDocument document,
        int factionIndex,
        long money,
        long industry,
        long food) =>
        ScenarioEditPatch.PatchFactionResources(document, factionIndex, money, industry, food);

    public ScenarioDocument PatchArmyFields(ScenarioDocument document, int armyIndex, ArmyFieldPatch patch) =>
        ScenarioEditPatch.PatchArmyFields(document, armyIndex, patch);

    public ScenarioDocument PatchSiteFields(ScenarioDocument document, int siteIndex, SiteFieldPatch patch) =>
        ScenarioEditPatch.PatchSiteFields(document, siteIndex, patch);

    public ScenarioDocument PatchFactionFields(ScenarioDocument document, int factionIndex, FactionFieldPatch patch) =>
        ScenarioEditPatch.PatchFactionFields(document, factionIndex, patch);

    public ScenarioDocument PatchEventFields(ScenarioDocument document, int eventIndex, EventFieldPatch patch) =>
        ScenarioEditPatch.PatchEventFields(document, eventIndex, patch);

    public ScenarioDocument PatchMapInfo(ScenarioDocument document, MapInfoPatch patch) =>
        ScenarioEditPatch.PatchMapInfo(document, patch);
}
