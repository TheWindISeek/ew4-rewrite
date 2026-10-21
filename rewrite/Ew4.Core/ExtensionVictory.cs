using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record VictoryCondition
{
    public string Type { get; init; } = "enemy_factions_defeated";
    public IReadOnlyList<VictoryCondition> Conditions { get; init; } = Array.Empty<VictoryCondition>();
    public IReadOnlyList<string> Factions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Units { get; init; } = Array.Empty<string>();
    public IReadOnlyList<int> Cells { get; init; } = Array.Empty<int>();
    public int Round { get; init; } = 1;
    public Resources Resources { get; init; } = new(0, 0, 0);
    public bool IncludeProvokedNeutrals { get; init; } = true;
    public string? Script { get; init; }
    public string? Function { get; init; }
}
public sealed record ExtensionVictoryRules
{
    public VictoryCondition Win { get; init; } = new();
    public VictoryCondition? Lose { get; init; }
    public string Priority { get; init; } = "defeat";
    public string DescriptionKey { get; init; } = "";
    public int? RoundLimit { get; init; }
    public bool RequirePlayerSurvival { get; init; } = true;
}
public static class ExtensionVictory
{
    public static BattleOutcome Evaluate(WorldState state, string camp, IReadOnlyCollection<string> neutral, ExtensionVictoryRules rules, IRuleScriptRuntime? scripts)
    {
        bool win = Match(state, camp, neutral, rules.Win, scripts);
        bool lose = rules.Lose != null && Match(state, camp, neutral, rules.Lose, scripts);
        return lose && (!win || rules.Priority == "defeat") ? BattleOutcome.Defeat : win ? BattleOutcome.Victory : BattleOutcome.Continue;
    }
    private static bool Match(WorldState state, string camp, IReadOnlyCollection<string> neutral, VictoryCondition c, IRuleScriptRuntime? scripts)
    {
        bool Owned(int cell) => state.Cells[cell].Owner is string owner && (state.ScenarioPlayerFaction is string player
            ? WorldDiplomacy.Allied(state, owner, player) : state.Factions.Single(f => f.Id == owner).Camp == camp);
        switch (c.Type)
        {
            case "all": return c.Conditions.All(v => Match(state, camp, neutral, v, scripts));
            case "any": return c.Conditions.Any(v => Match(state, camp, neutral, v, scripts));
            case "enemy_factions_defeated": return !WorldVictory.Facts(state, neutral).Any(f => !f.Defeated && f.Camp != camp && f.Combatant
                && (c.IncludeProvokedNeutrals ? f.Combatant : !neutral.Contains(state.Factions.Single(s => s.Id == f.Id).Camp)
                    || state.ScenarioPlayerFaction is string player && WorldDiplomacy.Override(state, player, f.Id) == "hostile"));
            case "factions_defeated": return c.Factions.All(id => state.Factions.Single(f => f.Id == id).Defeated);
            case "units_destroyed": return c.Units.All(id => state.Units.All(u => u.Id != id));
            case "units_lost": return c.Units.Any(id => state.Units.All(u => u.Id != id));
            case "cells_owned": return c.Cells.All(Owned);
            case "cells_lost": return c.Cells.Any(cell => !Owned(cell));
            case "enemy_objectives_cleared": return !WorldVictory.Facts(state, neutral).Any(f => !f.Defeated && f.Combatant && f.Camp != camp && f.Objectives > 0);
            case "round_reached": return state.Round >= c.Round;
            case "resources": return state.Factions.Where(f => f.Human && f.Camp == camp).Any(f => f.Resources.Money >= c.Resources.Money
                && f.Resources.Industry >= c.Resources.Industry && f.Resources.Food >= c.Resources.Food);
            case "script":
                var values = new Dictionary<string, long>(state.ExtensionState) { ["round"] = state.Round,
                    ["enemyUnits"] = state.Units.Count(u => state.DiplomaticRelations.Count > 0 && state.ScenarioPlayerFaction is string observer
                        ? WorldDiplomacy.Hostile(state, observer, u.Owner, neutral) : state.Factions.Single(f => f.Id == u.Owner).Camp != camp),
                    ["enemyObjectives"] = WorldVictory.Facts(state, neutral).Where(f => f.Combatant && f.Camp != camp).Sum(f => (long)f.Objectives) };
                return scripts!.Run(c.Script!, c.Function!, new("scenario.victory", null, null, 1, state.Round, 0, values, state, camp), new Ew4Random(state.RandomState)).Value != 0;
            default: throw new RuleViolation("extension.victory_type", "Unknown victory condition: " + c.Type);
        }
    }
}
