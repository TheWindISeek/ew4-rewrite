using System;
using System.Linq;

namespace Ew4.Platform.Engine;

internal static class EngineCommandHandlers
{
    public static void RegisterAll(EngineCommandDispatcher dispatcher)
    {
        dispatcher.Register<MoveCommand>(Move);
        dispatcher.Register<AttackCommand>(Attack);
        dispatcher.Register<EndTurnCommand>(EndTurn);
        dispatcher.Register<TrainCommand>(Train);
        dispatcher.Register<UpgradeBuildingCommand>(UpgradeBuilding);
        dispatcher.Register<BuyTransportCommand>(BuyTransport);
        dispatcher.Register<BuildInstallationCommand>(BuildInstallation);
        dispatcher.Register<RecruitCommand>(Recruit);
        dispatcher.Register<DisbandCommand>(Disband);
        dispatcher.Register<DialogueAckCommand>(DialogueAck);
        dispatcher.Register<EventChoiceCommand>(EventChoice);
        dispatcher.Register<HoldCommand>(Hold);
        dispatcher.Register<DeployCommanderCommand>(DeployCommander);
    }

    private static SessionChange Move(GameSession session, RuleSet rules, MoveCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session, WorldActions.Move(session.World, session.World.Revision, command.UnitId, command.DestinationCell, rules.Movement), context);

    private static SessionChange Attack(GameSession session, RuleSet rules, AttackCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session, WorldBattle.Attack(session.World, session.World.Revision, command.UnitId, command.TargetId, rules.Battle), context);

    private static SessionChange EndTurn(GameSession session, RuleSet rules, EndTurnCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session, WorldTurns.EndTurn(session.World, session.World.Revision, rules.Turns), context);

    private static SessionChange Train(GameSession session, RuleSet rules, TrainCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session, WorldImprovements.Train(session.World, session.World.Revision, command.UnitId, rules.Training), context);

    private static SessionChange UpgradeBuilding(GameSession session, RuleSet rules, UpgradeBuildingCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session, WorldImprovements.UpgradeBuilding(session.World, session.World.Revision, command.Cell, rules.Upgrades), context);

    private static SessionChange BuyTransport(GameSession session, RuleSet rules, BuyTransportCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session, WorldSupportActions.BuyTransport(session.World, session.World.Revision, command.UnitId, rules.Transport), context);

    private static SessionChange BuildInstallation(GameSession session, RuleSet rules, BuildInstallationCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session,
            WorldSupportActions.BuildInstallation(session.World, session.World.Revision, command.UnitId, rules.Installations[command.InstallationKind]),
            context);

    private static SessionChange Recruit(GameSession session, RuleSet rules, RecruitCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session,
            WorldActions.Recruit(session.World, session.World.Revision, command.Cell, command.UnitIdSeed,
                rules.Recruitment[session.World.ActiveFaction][command.OfferId]),
            context);

    private static SessionChange Disband(GameSession session, RuleSet rules, DisbandCommand command, EngineApplyContext? context, EngineCommandDispatcher host)
    {
        DisbandRules disband = context?.DisbandOverride ?? rules.Disband;
        return host.CommitWorld(session, WorldDisband.Execute(session.World, session.World.Revision, command.UnitId, disband), context);
    }

    private static SessionChange DialogueAck(GameSession session, RuleSet rules, DialogueAckCommand command, EngineApplyContext? context, EngineCommandDispatcher host)
    {
        WorldState world = FlowExtraction.Hydrate(session.World, command.Waiting);
        WorldChange change = world.PendingDialogue == null
            ? throw new RuleViolation("client.dialogue", "No dialogue is waiting")
            : world.Phase == TurnPhase.Active
                ? new(WorldEvents.Acknowledge(world, world.PendingDialogue) with { Revision = checked(world.Revision + 1) }, Array.Empty<WorldNotice>())
                : WorldTurns.Resume(world, world.Revision, rules.Turns, world.PendingDialogue);
        return host.CommitWorld(session, change, context);
    }

    private static SessionChange EventChoice(GameSession session, RuleSet rules, EventChoiceCommand command, EngineApplyContext? context, EngineCommandDispatcher host)
    {
        WorldState world = FlowExtraction.Hydrate(session.World, command.Waiting);
        WorldChange chosen = ScenarioRules.Choose(world, world.Revision, command.EventId, command.ChoiceId);
        if (chosen.State.PendingDialogue == null && chosen.State.Phase is not (TurnPhase.Active or TurnPhase.Finished))
        {
            WorldChange resumed = WorldTurns.Resume(chosen.State, chosen.State.Revision, rules.Turns);
            chosen = new(resumed.State, chosen.Notices.Concat(resumed.Notices).ToArray());
        }

        return host.CommitWorld(session, chosen, context);
    }

    private static SessionChange Hold(GameSession session, RuleSet rules, HoldCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session, WorldHold.Hold(session.World, session.World.Revision, command.UnitId), context);

    private static SessionChange DeployCommander(GameSession session, RuleSet rules, DeployCommanderCommand command, EngineApplyContext? context, EngineCommandDispatcher host) =>
        host.CommitWorld(session,
            WorldCommanders.Deploy(session.World, session.World.Revision, command.UnitId, command.OfferId, rules.Commanders),
            context);
}
