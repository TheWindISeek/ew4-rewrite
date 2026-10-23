using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Editor;

public abstract record ScenarioEdit;

public sealed record ScenarioEditArmyCell(int Index, short Cell) : ScenarioEdit;

public sealed record ScenarioEditSiteCell(int Index, short Cell) : ScenarioEdit;

public sealed record ScenarioEditFactionResources(int Index, long Money, long Industry, long Food) : ScenarioEdit;

public sealed record ScenarioEditResult(string ScenarioId, ScenarioSaveResult Saved);

public interface IScenarioEditor
{
    ScenarioDocument Open(string workspaceId, string scenarioId);

    ScenarioEditResult Apply(string workspaceId, string scenarioId, ScenarioEdit edit);
}
