using Ew4.Platform.Content;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.CatalogLoad;

/// <summary>开局后应用 BTL 内嵌自定义胜负等（不读取 Mod 规则扩展文件）。</summary>
internal static class ScenarioExtensionPostProcessor
{
    internal static BuiltScenario Apply(
        StaticDataScenarioBuild built,
        StaticDataBattle battle,
        string scenarioId,
        int mode,
        ModRuntimeBootstrap bootstrap)
    {
        string playerFaction = built.PlayerFaction;

        WorldState world = built.InitialWorld with { ScenarioPlayerFaction = playerFaction };
        StaticDataScenarioRules rules = built.Rules with
        {
            Battle = built.Rules.Battle with
            {
                Combat = built.Rules.Battle.Combat with { LethalStrikePreventsCounterattack = true },
            },
        };

        if (mode is 1 or 2)
        {
            world = world with
            {
                Jurisdictions = battle.Jurisdictions?.Apply(world) ?? TerritorialJurisdictionBuilder.Create(world),
            };
        }

        ExtensionVictoryRules? customVictory = battle.Victory;
        if (customVictory == null)
        {
            return Finish(built, rules, world, scenarioId, mode, bootstrap);
        }

        ExtensionVictory.ValidateTargets(customVictory, world, initial: true);

        VictoryRules victory = rules.Turns.Victory with { Custom = customVictory };
        WorldMoveRules movement = rules.Movement with { Victory = victory };
        WorldTurnRules turns = rules.Turns with { Victory = victory };
        WorldBattleRules battleRules = rules.Battle with { Victory = victory };
        rules = rules with
        {
            Movement = movement,
            Turns = turns,
            Battle = battleRules,
        };

        WorldChange scenarioChange = ScenarioRules.Evaluate(world);
        world = scenarioChange.State;

        if (world.PendingDialogue == null && world.Outcome == BattleOutcome.Continue)
        {
            BattleOutcome outcome = WorldVictory.Evaluate(
                world,
                turns.PlayerCamp!,
                victory,
                turns.NeutralCamps);
            if (outcome != BattleOutcome.Continue)
            {
                world = world with { Outcome = outcome, Phase = TurnPhase.Finished };
            }
        }

        return Finish(built, rules, world, scenarioId, mode, bootstrap);
    }

    private static BuiltScenario Finish(
        StaticDataScenarioBuild built,
        StaticDataScenarioRules rules,
        WorldState world,
        string scenarioId,
        int mode,
        ModRuntimeBootstrap bootstrap)
    {
        BuiltSessionAttach session = ScenarioSessionBootstrap.Create(
            built,
            rules,
            scenarioId,
            bootstrap.Catalogs.Accounts.SourceRevision,
            world.ContentRevision);
        return new BuiltScenario(world, RuleSetMapping.FromLegacy(rules, mode, built.PlayerFaction), session);
    }
}
