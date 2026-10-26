using Ew4.Interface.Mod;
using Ew4.Platform.Content;
using Ew4.Platform.Mod.Scenario;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Editor.Campaign;

public sealed record CampaignSaveOutcome(
    ScenarioSaveResult Saved,
    IReadOnlyList<Diagnostic>? SmokeDiagnostics);

public sealed class CampaignDocumentService
{
    private readonly IWorkspaceService _workspaces;
    private readonly ScenarioJsonStore _store;
    private readonly IScenarioBtlConverter _converter;
    private readonly MapCatalogProvider _maps;
    private readonly IModSnapshotLoader _loader;
    private readonly FileModRepository _repository;
    private readonly IEngine _engine;
    private readonly IScenarioBuilder _builder;

    public CampaignDocumentService(
        IWorkspaceService workspaces,
        ScenarioJsonStore store,
        IScenarioBtlConverter converter,
        MapCatalogProvider maps,
        IModSnapshotLoader loader,
        FileModRepository repository,
        IEngine engine,
        IScenarioBuilder builder)
    {
        _workspaces = workspaces;
        _store = store;
        _converter = converter;
        _maps = maps;
        _loader = loader;
        _repository = repository;
        _engine = engine;
        _builder = builder;
    }

    public CampaignSaveOutcome SaveDocument(string workspaceId, string campaignId, byte[] jsonUtf8, bool smoke)
    {
        EditSession session = EditSession.FromJson(jsonUtf8, campaignId, maps: _maps.Get(_workspaces.OpenMergedMod(workspaceId)));
        ScenarioSaveResult saved = _store.Save(_workspaces.ResolveWorkspaceRoot(workspaceId), campaignId, session.ToUtf8());
        IReadOnlyList<Diagnostic>? diagnostics = null;
        if (smoke)
        {
            ResolvedContent resolved = CampaignSmokeValidator.ResolveWorkspace(
                _loader,
                _repository,
                _workspaces.ResolveWorkspaceRoot(workspaceId));
            diagnostics = CampaignSmokeValidator.SmokeStart(resolved, campaignId, _engine, _builder);
        }

        return new CampaignSaveOutcome(saved, diagnostics);
    }

    public RoundtripResult RoundtripWorkspace(string workspaceId, string campaignId) =>
        ScenarioRoundtrip.CheckJson(_converter, campaignId,
            EditSession.Open(_workspaces.OpenMergedMod(workspaceId), campaignId, maps: _maps.Get(_workspaces.OpenMergedMod(workspaceId))).ToUtf8());
}
