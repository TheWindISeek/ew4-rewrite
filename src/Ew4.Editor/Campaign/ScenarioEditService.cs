using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Editor.Campaign;

/// <summary>
/// 兼容旧接口名；新代码请使用 <see cref="IScenarioEditToolkit"/> / <see cref="ScenarioEditToolkit"/>.
/// </summary>
public sealed class ScenarioEditService : IScenarioEditService
{
    private readonly IScenarioEditToolkit _toolkit;

    public ScenarioEditService(IScenarioEditToolkit? toolkit = null) => _toolkit = toolkit ?? ScenarioEditToolkit.Default;

    public ScenarioDocument OpenFromPack(IModSource pack, string scenarioId) => _toolkit.OpenFromPack(pack, scenarioId);

    public ScenarioSaveResult SaveDocument(string workspaceRoot, ScenarioDocument document) =>
        _toolkit.SaveToWorkspace(workspaceRoot, document);

    public RoundtripReport ValidateDocumentRoundtrip(byte[] originalBtl, string scenarioId)
    {
        ScenarioBtlRoundtrip.Result result = _toolkit.ValidateBtlRoundtrip(scenarioId, originalBtl);
        bool match = result.Document;
        return new(match, originalBtl.Length, match ? originalBtl.Length : 0);
    }
}
