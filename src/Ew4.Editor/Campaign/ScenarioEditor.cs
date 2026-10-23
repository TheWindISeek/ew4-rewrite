using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Editor.Campaign;

public sealed class ScenarioEditor : IScenarioEditor
{
    private readonly IWorkspaceService _workspaces;
    private readonly IScenarioEditToolkit _toolkit;

    public ScenarioEditor(IWorkspaceService workspaces, IScenarioEditToolkit toolkit)
    {
        _workspaces = workspaces;
        _toolkit = toolkit;
    }

    public ScenarioDocument Open(string workspaceId, string scenarioId) =>
        _toolkit.OpenFromPack(_workspaces.OpenMergedMod(workspaceId), scenarioId);

    public ScenarioEditResult Apply(string workspaceId, string scenarioId, ScenarioEdit edit)
    {
        ScenarioDocument document = Open(workspaceId, scenarioId);
        if (!string.Equals(document.Id, scenarioId, StringComparison.Ordinal))
        {
            throw new ContentError(
                "scenario.id",
                $"Opened document id '{document.Id}' does not match '{scenarioId}'.");
        }

        ScenarioDocument patched = edit switch
        {
            ScenarioEditArmyCell army => _toolkit.PatchArmyCell(document, army.Index, army.Cell),
            ScenarioEditSiteCell site => _toolkit.PatchSiteCell(document, site.Index, site.Cell),
            ScenarioEditFactionResources faction => _toolkit.PatchFactionResources(
                document,
                faction.Index,
                faction.Money,
                faction.Industry,
                faction.Food),
            ScenarioEditArmyFields armyFields => _toolkit.PatchArmyFields(document, armyFields.Index, armyFields.Fields),
            ScenarioEditSiteFields siteFields => _toolkit.PatchSiteFields(document, siteFields.Index, siteFields.Fields),
            ScenarioEditFactionFields factionFields => _toolkit.PatchFactionFields(
                document,
                factionFields.Index,
                factionFields.Fields),
            ScenarioEditEventFields eventFields => _toolkit.PatchEventFields(document, eventFields.Index, eventFields.Fields),
            ScenarioEditMapInfo map => _toolkit.PatchMapInfo(document, map.Fields),
            _ => throw new ContentError("scenario.edit", "Unsupported scenario edit type."),
        };

        ScenarioSaveResult saved = _toolkit.SaveToWorkspace(_workspaces.ResolveWorkspaceRoot(workspaceId), patched);
        return new ScenarioEditResult(scenarioId, saved);
    }
}
