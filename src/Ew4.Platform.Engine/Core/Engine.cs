using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

/// <summary>
/// <see cref="IEngine"/> 实现：经 <see cref="EngineCommandDispatcher"/> 转发至规则系统。
/// </summary>
public sealed class Engine : IEngine
{
    private readonly EngineCommandDispatcher _commands = new();

    public static EngineInfo DefaultInfo { get; } = new(
        EngineVersion: "0.3.0",
        BehaviorVersion: 1,
        ContentFormats: new Dictionary<string, int>
        {
            ["scenario"] = 1,
            ["rules"] = 1,
            ["map"] = 1,
            ["commander"] = 1,
            ["unit"] = 1,
            ["country"] = 1,
            ["text"] = 1,
            ["script"] = 1,
            ["asset"] = 1,
            ["source"] = 1,
        },
        Capabilities: new[] { "rule-api-2" },
        KnownCompatFlags: Array.Empty<string>());

    public Engine()
    {
        EngineCommandHandlers.RegisterAll(_commands);
    }

    public EngineInfo Info => DefaultInfo;

    public GameSession NewGame(WorldState initial, RuleSet rules, PlayerAccount account, NewGameOptions options)
    {
        initial.Validate();
        account.Validate();
        rules.Validate();
        WorldChange entered = WorldTurns.Resume(initial, initial.Revision, rules.Turns);
        string player = initial.ScenarioPlayerFaction ?? initial.ActiveFaction;
        var session = new GameSession
        {
            ContentRevision = initial.ContentRevision,
            Revision = 0,
            PlayerFaction = player,
            World = entered.State,
            Account = account.Copy(),
        };
        session.Validate();
        return session.Copy();
    }

    public EngineResult Apply(GameSession session, RuleSet rules, EngineCommand command, EngineApplyContext? context = null)
    {
        if (session.Revision != command.ExpectedRevision)
        {
            throw new RuleViolation("session.revision", "Stale session command");
        }

        IReadOnlyList<EngineEvent> events = Describe(session.World, rules, command);
        SessionChange change = _commands.Dispatch(session, rules, command, context);
        (WorldState world, IReadOnlyList<FlowRequest> flow) = FlowExtraction.Take(change.State.World);
        GameSession state = flow.Count == 0 ? change.State : change.State with { World = world };
        return new EngineResult(state, change.Notices) { Flow = flow, Events = events };
    }

    // 展示用事件在执行前按同一规则预先计算；命令本身非法时留给命令处理器报错。
    private static IReadOnlyList<EngineEvent> Describe(WorldState world, RuleSet rules, EngineCommand command)
    {
        try
        {
            switch (command)
            {
                case MoveCommand move when WorldActions.Reachable(world, move.UnitId, rules.Movement).TryGetValue(move.DestinationCell, out ReachableCell cell):
                    return new EngineEvent[] { new UnitMovedEvent(move.UnitId, cell.Path.ToArray()) };
                case AttackCommand attack:
                    return new EngineEvent[] { new AttackResolvedEvent(WorldBattle.Preview(world, world.Revision, attack.UnitId, attack.TargetId, rules.Battle)) };
            }
        }
        catch (RuleViolation)
        {
        }

        return Array.Empty<EngineEvent>();
    }

    public IReadOnlyDictionary<int, ReachableCell> Reachable(WorldState world, RuleSet rules, string unitId) =>
        WorldActions.Reachable(world, unitId, rules.Movement);

    public BattlePreview PreviewAttack(WorldState world, RuleSet rules, string attackerId, string targetId) =>
        WorldBattle.Preview(world, world.Revision, attackerId, targetId, rules.Battle);
}
