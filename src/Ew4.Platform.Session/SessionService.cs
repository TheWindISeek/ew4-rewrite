using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Session.Persistence;

namespace Ew4.Platform.Session;

/// <summary>
/// 绑定单个 <see cref="ResolvedContent"/> 的局内服务。
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly ResolvedContent _content;
    private readonly IEngine _engine;
    private readonly IScenarioBuilder _builder;
    private readonly string? _saveRoot;
    private GameSession? _session;
    private RuleSet? _rules;
    private long _revision;

    public SessionService(ResolvedContent content, IEngine engine, IScenarioBuilder builder, string? saveRootDirectory = null)
    {
        _content = content;
        _engine = engine;
        _builder = builder;
        _saveRoot = string.IsNullOrWhiteSpace(saveRootDirectory) ? null : Path.GetFullPath(saveRootDirectory);
    }

    public ResolvedContent Content => _content;

    public SessionView Catalog()
    {
        return new SessionView(_revision, SessionCatalog.BuildCatalogPayload(_content));
    }

    public SessionView Start(StartRequest request)
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
        return new SessionView(_revision, new { started = true, scenarioId = request.ScenarioId });
    }

    public SessionView Apply(EngineCommand command)
    {
        EnsureInGame();
        EngineResult result = _engine.Apply(_session!, _rules!, command);
        _session = result.Session;
        _revision = _session.Revision;
        return new SessionView(_revision, new { notices = result.Notices.Count });
    }

    public SessionView Leave(long expectedRevision)
    {
        if (_session != null && _session.Revision != expectedRevision)
        {
            throw new RuleViolation("session.revision", "Session revision mismatch.");
        }

        _session = null;
        _rules = null;
        return new SessionView(_revision, new { left = true });
    }

    public SaveResult Save(SaveSlotId slot)
    {
        if (_saveRoot == null)
        {
            return new SaveResult(false, "session.save", "Save directory is not configured.");
        }

        try
        {
            EnsureInGame();
            string path = SaveDirectoryLayout.SlotPath(_saveRoot, slot);
            EngineSlotPersistence.Write(path, _session!, CreateEnvelope());
            return new SaveResult(true, null, null);
        }
        catch (Exception error) when (error is RuleViolation or IOException or ContentError)
        {
            string code = error is RuleViolation rule ? rule.Code : error is ContentError content ? content.Code : "session.save";
            return new SaveResult(false, code, error.Message);
        }
    }

    public SessionView Load(SaveSlotId slot)
    {
        if (_saveRoot == null)
        {
            throw new ContentError("session.load", "Save directory is not configured.");
        }

        string path = SaveDirectoryLayout.SlotPath(_saveRoot, slot);
        if (!File.Exists(path))
        {
            throw new ContentError("session.load", "This save slot is empty.");
        }

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
        return new SessionView(_revision, new { loaded = true, scenarioId });
    }

    public IReadOnlyList<SaveSlotInfo> Slots(int mode)
    {
        if (_saveRoot == null)
        {
            return Array.Empty<SaveSlotInfo>();
        }

        var list = new List<SaveSlotInfo>();
        for (int index = 0; index < 7; index++)
        {
            var slot = new SaveSlotId(mode, index);
            string path = SaveDirectoryLayout.SlotPath(_saveRoot, slot);
            list.Add(new SaveSlotInfo(slot, Path.GetFileName(path), File.Exists(path) ? 1 : 0));
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
}
