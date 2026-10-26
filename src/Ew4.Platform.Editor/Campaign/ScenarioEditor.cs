using Ew4.Platform.Content;
using Ew4.Platform.Content.Maps;

namespace Ew4.Platform.Editor.Campaign;

public sealed class ScenarioEditor : IScenarioEditor
{
    private readonly IWorkspaceService _workspaces;
    private readonly ScenarioJsonStore _store;
    private readonly MapCatalogProvider _maps;

    public ScenarioEditor(IWorkspaceService workspaces, ScenarioJsonStore store, MapCatalogProvider maps)
    {
        _workspaces = workspaces;
        _store = store;
        _maps = maps;
    }

    public byte[] OpenJson(string workspaceId, string scenarioId) => Open(workspaceId, scenarioId).ToUtf8();

    public ScenarioEditResult Apply(string workspaceId, string scenarioId, ScenarioEdit edit)
    {
        EditSession session = Open(workspaceId, scenarioId);
        session.Apply(edit);
        ScenarioSaveResult saved = _store.Save(_workspaces.ResolveWorkspaceRoot(workspaceId), scenarioId, session.ToUtf8());
        return new ScenarioEditResult(scenarioId, saved);
    }

    public ScenarioSaveResult Create(string workspaceId, string scenarioId, string name, int mapId, MapCrop? crop = null)
    {
        if (_workspaces.GetMode(workspaceId) != EditorMode.Free) throw new ContentError("editor.capability", "This editor session may not create scenarios.");
        IModSource merged = _workspaces.OpenMergedMod(workspaceId);
        if (merged.Exists(ScenarioModAccess.ScenarioJsonPath(scenarioId)))
            throw new ContentError("scenario.exists", "Scenario already exists: " + scenarioId);
        EditSession session = EditSession.CreateFree(scenarioId, name, mapId, crop, _maps.Get(merged));
        return _store.Save(_workspaces.ResolveWorkspaceRoot(workspaceId), scenarioId, session.ToUtf8());
    }

    private EditSession Open(string workspaceId, string scenarioId)
    {
        IModSource merged = _workspaces.OpenMergedMod(workspaceId);
        return EditSession.Open(merged, scenarioId, _workspaces.GetMode(workspaceId), _maps.Get(merged));
    }
}
