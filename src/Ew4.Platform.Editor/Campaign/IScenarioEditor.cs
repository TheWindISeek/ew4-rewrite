using Ew4.Platform.Content.Maps;

namespace Ew4.Platform.Editor.Campaign;

public abstract record ScenarioEdit;

public sealed record ScenarioEditArmyCell(int Index, short Cell) : ScenarioEdit;

public sealed record ScenarioEditSiteCell(int Index, short Cell) : ScenarioEdit;

public sealed record ScenarioEditFactionResources(int Index, long Money, long Industry, long Food) : ScenarioEdit;

/// <summary>改用 base 的某张地图（及其中一块裁剪，省略为整图）。</summary>
public sealed record ScenarioEditMapSelect(int MapId, MapCrop? Crop = null) : ScenarioEdit;

public sealed record ScenarioEditResult(string ScenarioId, ScenarioSaveResult Saved);

public interface IScenarioEditor
{
    /// <summary>读取 workspace 合并视图里的场景 JSON（schema 3）。</summary>
    byte[] OpenJson(string workspaceId, string scenarioId);

    ScenarioEditResult Apply(string workspaceId, string scenarioId, ScenarioEdit edit);

    /// <summary>自由编辑：在 workspace 里新建只有地图与归属的空场景（需 CreateScenario 能力）。</summary>
    ScenarioSaveResult Create(string workspaceId, string scenarioId, string name, int mapId, Ew4.Platform.Content.Maps.MapCrop? crop = null);
}
