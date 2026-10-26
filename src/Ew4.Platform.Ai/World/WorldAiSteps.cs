using System;
using System.Collections.Generic;

namespace Ew4.Platform.Ai;

using Ew4.Platform.Engine;

/// <summary>
/// AI 的一个决策：一组与玩家完全相同的 Engine 命令，加上推进后的 AI 随机状态。
/// 命令的 <see cref="EngineCommand.ExpectedRevision"/> 取规划时的快照版本，由执行方逐条改写为当前版本。
/// </summary>
public sealed record AiStep(IReadOnlyList<EngineCommand> Commands, ulong RandomState);

/// <summary>AI 规划入口：读取当前世界，产出玩家同款命令；自身不修改任何状态。</summary>
public static class AiPlanner
{
    public static AiStep Next(WorldState state, RuleSet rules, WorldAiRules ai, WorldProductionRules production, ulong random)
    {
        // 移动规则以 Engine 规则集为准，避免 AI 持有一份会过期的副本。
        ai = ai with { Movement = rules.Movement };
        long revision = state.Revision;
        WorldActions.Ready(state, revision);
        if (WorldDiplomacy.Dormant(state, state.ActiveFaction, ai.NeutralCamps))
            return Single(new EndTurnCommand(revision), random);

        var attempt = WorldAi.PlanProduction(state, random, ai, production, rules.Turns);
        if (attempt.Command != null) return new(Production(attempt.Command, revision), attempt.FinalRandomState);

        var commander = production.Commanders == null ? null : WorldAi.PlanCommander(state, production.Commanders, production.CommanderScoring);
        if (commander != null)
            return Single(new DeployCommanderCommand(revision, commander.UnitId, commander.OfferId), attempt.FinalRandomState);

        var unit = WorldAi.PlanNextUnit(state, attempt.FinalRandomState, ai);
        if (unit == null) return Single(new EndTurnCommand(revision), attempt.FinalRandomState);
        return new(Unit(unit.Candidates[AiPlanning.Best(unit.Candidates)].Action, revision), unit.FinalRandomState);
    }

    private static AiStep Single(EngineCommand command, ulong random) => new(new[] { command }, random);

    private static IReadOnlyList<EngineCommand> Production(WorldProductionCommand command, long revision)
    {
        switch (command)
        {
            case AiRecruitCommand recruit:
                var recruitment = new RecruitCommand(revision, recruit.Cell, recruit.OfferId, recruit.NewUnitId);
                return recruit.RelocateUnitId == null
                    ? new EngineCommand[] { recruitment }
                    : new EngineCommand[] { new MoveCommand(revision, recruit.RelocateUnitId, recruit.Relocation!.Value), recruitment };
            case AiInstallationCommand installation:
                return new EngineCommand[] { new BuildInstallationCommand(revision, installation.UnitId, installation.RecipeId) };
            case AiUpgradeCommand upgrade:
                return new EngineCommand[] { new UpgradeBuildingCommand(revision, upgrade.Cell) };
            case AiTransportCommand transport:
                return new EngineCommand[] { new BuyTransportCommand(revision, transport.UnitId) };
            default:
                throw new RuleViolation("ai.production_selection", "Unsupported production command");
        }
    }

    private static IReadOnlyList<EngineCommand> Unit(WorldAiCommand command, long revision)
    {
        switch (command)
        {
            case AiStrikeCommand strike:
                return new EngineCommand[] { new AttackCommand(revision, strike.UnitId, strike.TargetId) };
            case AiMoveCommand move:
                return move.TargetId == null
                    ? new EngineCommand[] { new MoveCommand(revision, move.UnitId, move.Destination) }
                    : new EngineCommand[] { new MoveCommand(revision, move.UnitId, move.Destination), new AttackCommand(revision, move.UnitId, move.TargetId) };
            case AiStrategicCommand strategic:
                return strategic.BuyTransport
                    ? new EngineCommand[] { new BuyTransportCommand(revision, strategic.UnitId), new MoveCommand(revision, strategic.UnitId, strategic.Destination) }
                    : new EngineCommand[] { new MoveCommand(revision, strategic.UnitId, strategic.Destination) };
            case AiHoldCommand:
                return new EngineCommand[] { new HoldCommand(revision, command.UnitId) };
            default:
                throw new RuleViolation("ai.command", "Unsupported unit AI command");
        }
    }
}
