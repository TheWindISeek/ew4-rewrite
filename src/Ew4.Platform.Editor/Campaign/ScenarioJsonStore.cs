using Ew4.Platform.Persistence.Io;
using Ew4.Platform.Persistence.Workspaces;

namespace Ew4.Platform.Editor.Campaign;

public sealed record ScenarioSaveResult(string DocumentPath);

/// <summary>把场景 JSON 写进 workspace 的 <c>scenarios/&lt;id&gt;.json</c>。</summary>
public sealed class ScenarioJsonStore
{
    private readonly LoosePathStore _paths;
    private readonly WorkspaceStore? _workspaces;

    public ScenarioJsonStore(LoosePathStore paths, WorkspaceStore? workspaces)
    {
        _paths = paths;
        _workspaces = workspaces;
    }

    public ScenarioSaveResult Save(string workspaceRoot, string scenarioId, byte[] json)
    {
        string root = _paths.Normalize(workspaceRoot);
        if (_workspaces != null) _workspaces.SaveScenarioDocument(root, scenarioId, json);
        else _paths.WriteBytes(_paths.Combine(root, "scenarios", scenarioId + ".json"), json);
        return new("scenarios/" + scenarioId + ".json");
    }
}
