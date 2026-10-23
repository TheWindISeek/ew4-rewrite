using System;
using System.Collections.Generic;

namespace Ew4.Platform.Engine;

/// <summary>
/// <see cref="IEngine"/> 实现：转调现有静态规则 API。
/// </summary>
public sealed class Engine : IEngine
{
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

    public EngineInfo Info => DefaultInfo;

    public GameSession NewGame(WorldState initial, RuleSet rules, PlayerAccount account, NewGameOptions options)
    {
        initial.Validate();
        account.Validate();
        rules.Turns.Validate();
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

    public EngineResult Apply(GameSession session, RuleSet rules, EngineCommand command)
    {
        if (session.Revision != command.ExpectedRevision)
        {
            throw new RuleViolation("session.revision", "Stale session command");
        }

        SessionChange change = command switch
        {
            MoveCommand move => CommitWorld(session, WorldActions.Move(session.World, session.World.Revision, move.UnitId, move.DestinationCell, rules.Movement)),
            AttackCommand attack => CommitWorld(session, WorldBattle.Attack(session.World, session.World.Revision, attack.UnitId, attack.TargetId, rules.Battle)),
            EndTurnCommand => CommitWorld(session, WorldTurns.EndTurn(session.World, session.World.Revision, rules.Turns)),
            TrainCommand train => CommitWorld(session, WorldImprovements.Train(session.World, session.World.Revision, train.UnitId, rules.Training)),
            UpgradeBuildingCommand upgrade => CommitWorld(session, WorldImprovements.UpgradeBuilding(session.World, session.World.Revision, upgrade.Cell, rules.Upgrades)),
            BuyTransportCommand transport => CommitWorld(session, WorldSupportActions.BuyTransport(session.World, session.World.Revision, transport.UnitId, rules.Transport)),
            BuildInstallationCommand install => CommitWorld(session,
                WorldSupportActions.BuildInstallation(session.World, session.World.Revision, install.UnitId, rules.Installations[install.InstallationKind])),
            RecruitCommand recruit => CommitWorld(session,
                WorldActions.Recruit(session.World, session.World.Revision, recruit.Cell, recruit.UnitIdSeed,
                    rules.Recruitment[session.World.ActiveFaction][recruit.OfferId])),
            DisbandCommand disband => CommitWorld(session,
                WorldDisband.Execute(session.World, session.World.Revision, disband.UnitId, rules.Disband)),
            DeployCommand deploy => RosterDeployment.Deploy(session, session.Revision, deploy.UnitId, deploy.CommanderId, rules.Roster),
            DialogueAckCommand => CommitWorld(session, session.World.PendingDialogue == null
                ? throw new RuleViolation("client.dialogue", "No dialogue is waiting")
                : session.World.Phase == TurnPhase.Active
                    ? new WorldChange(WorldEvents.Acknowledge(session.World, session.World.PendingDialogue) with
                    {
                        Revision = checked(session.World.Revision + 1),
                    }, Array.Empty<WorldNotice>())
                    : WorldTurns.Resume(session.World, session.World.Revision, rules.Turns, session.World.PendingDialogue)),
            EventChoiceCommand choice => CommitWorld(session,
                ScenarioRules.Choose(session.World, session.World.Revision, choice.EventId, choice.ChoiceId, null)),
            AiStepCommand => ApplyAiStep(session, rules),
            _ => throw new RuleViolation("engine.command", "Unsupported engine command"),
        };

        return new EngineResult(change.State, change.Notices);
    }

    public IReadOnlyDictionary<int, ReachableCell> Reachable(WorldState world, RuleSet rules, string unitId) =>
        WorldActions.Reachable(world, unitId, rules.Movement);

    public BattlePreview PreviewAttack(WorldState world, RuleSet rules, string attackerId, string targetId) =>
        WorldBattle.Preview(world, world.Revision, attackerId, targetId, rules.Battle);

    public AiStepPlan PlanAiStep(WorldState world, RuleSet rules) =>
        WorldAi.PlanStep(world, rules.Ai, rules.Production[world.ActiveFaction], rules.Turns);

    private static SessionChange CommitWorld(GameSession session, WorldChange change) =>
        SessionActions.CommitWorld(session, session.Revision, change);

    private static SessionChange ApplyAiStep(GameSession session, RuleSet rules)
    {
        WorldState world = session.World;
        WorldProductionRules production = rules.Production[world.ActiveFaction];
        AiStepPlan plan = WorldAi.PlanStep(world, rules.Ai, production, rules.Turns);
        WorldChange change = WorldAi.ExecuteStep(world, plan, rules.Ai, production, rules.Turns, rules.Battle);
        return CommitWorld(session, change);
    }
}
