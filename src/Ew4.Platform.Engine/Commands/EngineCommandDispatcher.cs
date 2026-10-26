using System;
using System.Collections.Generic;

namespace Ew4.Platform.Engine;

internal delegate SessionChange EngineCommandHandler(
    GameSession session,
    RuleSet rules,
    EngineCommand command,
    EngineApplyContext? context,
    EngineCommandDispatcher host);

internal sealed class EngineCommandDispatcher
{
    private readonly Dictionary<Type, EngineCommandHandler> _handlers = new();

    public void Register<T>(Func<GameSession, RuleSet, T, EngineApplyContext?, EngineCommandDispatcher, SessionChange> handler)
        where T : EngineCommand
    {
        _handlers[typeof(T)] = (session, rules, command, context, host) =>
            handler(session, rules, (T)command, context, host);
    }

    public SessionChange Dispatch(GameSession session, RuleSet rules, EngineCommand command, EngineApplyContext? context)
    {
        if (!_handlers.TryGetValue(command.GetType(), out EngineCommandHandler? handler))
        {
            throw new RuleViolation("engine.command", "Unsupported engine command");
        }

        return handler(session, rules, command, context, this);
    }

    public SessionChange CommitWorld(GameSession session, WorldChange change, EngineApplyContext? context)
    {
        if (context?.Commit != null)
        {
            change = WorldCommitPipeline.Enrich(change, context.Commit);
        }

        return SessionActions.CommitWorld(session, session.Revision, change);
    }

    public SessionChange CommitSessionChange(SessionChange change, EngineApplyContext? context)
    {
        if (context?.Commit == null)
        {
            change.State.Validate();
            return change;
        }

        WorldChange enriched = WorldCommitPipeline.Enrich(
            new WorldChange(change.State.World, change.Notices),
            context.Commit);
        GameSession synced = SessionActions.SyncPlayer(change.State with { World = enriched.State });
        synced.Validate();
        return new SessionChange(synced, enriched.Notices);
    }
}
