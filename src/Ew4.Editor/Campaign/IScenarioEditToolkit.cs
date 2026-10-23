using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Editor.Campaign;

/// <summary>
/// 剧本 BTL ↔ JSON 编辑工具库：供 CLI、网页编辑器、单测共用；不依赖 Session/Host。
/// </summary>
public interface IScenarioEditToolkit
{
    ScenarioDocument ImportBtl(string scenarioId, byte[] btlBytes);

    byte[] ExportBtl(ScenarioDocument document, ScenarioExportMode mode = ScenarioExportMode.PreserveSections);

    byte[] SerializeJson(ScenarioDocument document);

    ScenarioDocument DeserializeJson(byte[] jsonBytes);

    ScenarioDocument ReadJsonFile(string jsonPath);

    void WriteJsonFile(string jsonPath, ScenarioDocument document);

    ScenarioDocument ReadBtlFile(string scenarioId, string btlPath);

    void WriteBtlFile(string btlPath, ScenarioDocument document, ScenarioExportMode mode = ScenarioExportMode.ApplyLayers);

    ScenarioBtlRoundtrip.Result ValidateBtlRoundtrip(string scenarioId, byte[] btlBytes);

    ScenarioDocument OpenFromPack(IModSource pack, string scenarioId);

    ScenarioSaveResult SaveToWorkspace(string workspaceRoot, ScenarioDocument document);

    ScenarioDocument PatchArmyCell(ScenarioDocument document, int armyIndex, short newCell);

    ScenarioDocument PatchSiteCell(ScenarioDocument document, int siteIndex, short newCell);

    ScenarioDocument PatchFactionResources(
        ScenarioDocument document,
        int factionIndex,
        long money,
        long industry,
        long food);

    ScenarioDocument PatchArmyFields(ScenarioDocument document, int armyIndex, ArmyFieldPatch patch);

    ScenarioDocument PatchSiteFields(ScenarioDocument document, int siteIndex, SiteFieldPatch patch);

    ScenarioDocument PatchFactionFields(ScenarioDocument document, int factionIndex, FactionFieldPatch patch);

    ScenarioDocument PatchEventFields(ScenarioDocument document, int eventIndex, EventFieldPatch patch);

    ScenarioDocument PatchMapInfo(ScenarioDocument document, MapInfoPatch patch);
}

/// <summary>
/// 导出 BTL 前是否将 <see cref="ScenarioDocument.Layers"/> 写回 sections。
/// </summary>
public enum ScenarioExportMode
{
    /// <summary>沿用已保存的 section 字节（无损往返）。</summary>
    PreserveSections,
    /// <summary>先 <see cref="ScenarioBtlCodec.ApplyLayers"/> 再导出（编辑器保存）。</summary>
    ApplyLayers,
}
