// CLI / Tool 用的薄局内编排：Content 开局 + IEngine.Apply + Persistence 槽位。
// 不经 IGameOperations；Web 游玩主路径是 PlayHost + PlaySession（/api/play/*）。
// 磁盘 IO 仅经 Ew4.Platform.Persistence（SaveDirectoryLayout、EngineSlotPersistence、SlotSaveEnvelope）。

using System.Collections.Generic;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence;
using Ew4.Platform.Persistence.Serialization;

namespace Ew4.Service;

public sealed record CliStartRequest(string ScenarioId, long ExpectedRevision = 0);

public sealed record CliSaveResult(bool Ok, string? Code, string? Message);

public sealed record CliGameView(long Revision, object Payload);

public sealed record CliSaveSlotInfo(SaveSlotId Slot, string Label, long Revision);

/// <summary>
/// 绑定单个 <see cref="ResolvedContent"/>，在进程内持有一局 <see cref="GameSession"/>。
/// 供 <c>Ew4.Tool</c> 冒烟与将来 CLI；与 <see cref="Play.PlaySession"/> 并行，不替代 Web RPC。
/// </summary>
public sealed class CliEngineGame
{
    private readonly ResolvedContent _content;
    private readonly IEngine _engine;
    private readonly IScenarioBuilder _builder;
    private readonly string? _saveRoot;
    private GameSession? _session;
    private RuleSet? _rules;
    private long _revision;

    public CliEngineGame(ResolvedContent content, IEngine engine, IScenarioBuilder builder, string? saveRootDirectory = null)
    {
        _content = content;
        _engine = engine;
        _builder = builder;
        _saveRoot = string.IsNullOrWhiteSpace(saveRootDirectory) ? null : Path.GetFullPath(saveRootDirectory);
    }

    public ResolvedContent Content => _content;

    public CliGameView Catalog() =>
        new(_revision, CliCatalog.BuildPayload(_content));

    public CliGameView Start(CliStartRequest request)
    {
        BuiltScenario built = _builder.Build(_content, request.ScenarioId, new ScenarioSelection(request.ScenarioId));
        _rules = built.Rules;
        var account = new PlayerAccount
        {
            Id = "local",
            ContentRevision = built.InitialWorld.ContentRevision,
            Medals = 20_000_000,
        };
        _session = _engine.NewGame(built.InitialWorld, built.Rules, account, new NewGameOptions());
        if (built.Session != null)
        {
            _session = _session with
            {
                ContentRevision = built.Session.SessionContentRevision ?? _session.ContentRevision,
                RosterDeployments = built.Session.RosterDeployments,
                Markets = built.Session.Markets,
                TavernCandidates = built.Session.TavernCandidates,
            };
            _session.Validate();
        }

        _revision = _session.Revision;
        return new CliGameView(_revision, new { started = true, scenarioId = request.ScenarioId });
    }

    public CliGameView Apply(EngineCommand command)
    {
        EnsureInGame();
        EngineResult result = _engine.Apply(_session!, _rules!, command);
        _session = result.Session;
        _revision = _session.Revision;
        return new CliGameView(_revision, new { notices = result.Notices.Count });
    }

    public CliGameView Leave(long expectedRevision)
    {
        if (_session != null && _session.Revision != expectedRevision)
        {
            throw new RuleViolation("session.revision", "Session revision mismatch.");
        }

        _session = null;
        _rules = null;
        return new CliGameView(_revision, new { left = true });
    }

    public CliSaveResult Save(SaveSlotId slot)
    {
        if (_saveRoot == null)
        {
            return new CliSaveResult(false, "session.save", "Save directory is not configured.");
        }

        try
        {
            EnsureInGame();
            string path = SaveDirectoryLayout.SlotPath(_saveRoot, slot);
            EngineSlotPersistence.Write(path, _session!, CreateEnvelope());
            return new CliSaveResult(true, null, null);
        }
        catch (Exception error) when (error is RuleViolation or IOException or ContentError or PersistenceError)
        {
            string code = error switch
            {
                RuleViolation rule => rule.Code,
                ContentError content => content.Code,
                PersistenceError persistence => persistence.Code,
                _ => "session.save",
            };
            return new CliSaveResult(false, code, error.Message);
        }
    }

    public CliGameView Load(SaveSlotId slot)
    {
        if (_saveRoot == null)
        {
            throw new ContentError("session.load", "Save directory is not configured.");
        }

        if (!SaveDirectoryLayout.SlotFileExists(_saveRoot, slot))
        {
            throw new ContentError("session.load", "This save slot is empty.");
        }

        string path = SaveDirectoryLayout.SlotPath(_saveRoot, slot);
        SlotSaveEnvelope.Binding envelope = CreateEnvelope();
        byte[] inner = SlotSaveEnvelope.ReadSlotJson(path, envelope);
        string scenarioId = EngineSlotPersistence.ReadScenarioId(inner);
        BuiltScenario built = _builder.Build(_content, scenarioId, new ScenarioSelection(scenarioId));
        _rules = built.Rules;
        string accountRevision = ResolvedContentBootstrap.Get(_content).Catalogs.Accounts.SourceRevision;
        var contentBinding = new SessionContentBinding(
            built.Session?.SessionContentRevision ?? built.InitialWorld.ContentRevision,
            built.InitialWorld.ContentRevision,
            accountRevision);
        _session = EngineSlotPersistence.ReadInner(inner, contentBinding);
        _revision = _session.Revision;
        return new CliGameView(_revision, new { loaded = true, scenarioId });
    }

    public IReadOnlyList<CliSaveSlotInfo> Slots(int mode)
    {
        if (_saveRoot == null)
        {
            return Array.Empty<CliSaveSlotInfo>();
        }

        var list = new List<CliSaveSlotInfo>();
        for (int index = 0; index < 7; index++)
        {
            var slot = new SaveSlotId(mode, index);
            string path = SaveDirectoryLayout.SlotPath(_saveRoot, slot);
            list.Add(new CliSaveSlotInfo(slot, Path.GetFileName(path), SaveDirectoryLayout.SlotFileExists(_saveRoot, slot) ? 1 : 0));
        }

        return list;
    }

    private SlotSaveEnvelope.Binding CreateEnvelope() =>
        new(_content.Manifest.Id, _content.Fingerprint, _engine.Info);

    private void EnsureInGame()
    {
        if (_session == null || _rules == null)
        {
            throw new ContentError("session.state", "No active game session.");
        }
    }

    private static class CliCatalog
    {
        public static object BuildPayload(ResolvedContent content)
        {
            var scenarios = content.Source.List("scenarios/")
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(path => path["scenarios/".Length..^".json".Length])
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            return new Dictionary<string, object?>
            {
                ["modId"] = content.Manifest.Id,
                ["fingerprint"] = content.Fingerprint,
                ["scenarios"] = scenarios,
            };
        }
    }
}
