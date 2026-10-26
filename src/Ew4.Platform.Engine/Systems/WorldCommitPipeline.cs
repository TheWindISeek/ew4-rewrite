using System.Linq;

namespace Ew4.Platform.Engine;

/// <summary>局内世界变更提交前的剧本/胜利与本地多人收尾。</summary>
public sealed record WorldCommitContext(WorldState PreviousWorld, WorldTurnRules TurnRules);

public static class WorldCommitPipeline
{
    public static WorldChange Enrich(WorldChange change, WorldCommitContext context)
    {
        var scenarioChange = ScenarioRules.Evaluate(change.State);
        change = new(scenarioChange.State, change.Notices.Concat(scenarioChange.Notices).ToArray());
        if (context.TurnRules.Victory.Custom != null || change.State.ScenarioEvents.Count > 0)
        {
            WorldState world = change.State;
            if ((context.TurnRules.Victory.Custom != null || world.ScenarioEvents.Count > 0)
                && world.Outcome == BattleOutcome.Continue
                && world.PendingDialogue == null)
            {
                BattleOutcome outcome = WorldVictory.Evaluate(
                    world,
                    context.TurnRules.PlayerCamp!,
                    context.TurnRules.Victory,
                    context.TurnRules.NeutralCamps);
                if (outcome != BattleOutcome.Continue)
                {
                    world = world with { Outcome = outcome, Phase = TurnPhase.Finished };
                }
            }

            change = change with { State = world };
        }

        return change with
        {
            State = LocalMultiplayerGame.Resolve(change.State, context.TurnRules.Victory)
                with { Revision = checked(context.PreviousWorld.Revision + 1) }
        };
    }
}
