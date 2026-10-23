using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

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
    /// <summary>用于 <c>enemy_units_remaining</c> / <c>enemy_objectives_remaining</c> 等阈值比较。</summary>
    public string Compare { get; init; } = "le";
    public long Value { get; init; }
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
    public static IEnumerable<VictoryCondition> Conditions(VictoryCondition root)
    {
        var pending = new Stack<(VictoryCondition Condition, int Depth)>();
        pending.Push((root, 0));
        int count = 0;
        while (pending.Count > 0)
        {
            var (c, depth) = pending.Pop();
            if (c == null || c.Conditions == null || c.Factions == null || c.Units == null || c.Cells == null
                || c.Resources == null || depth > 12 || ++count > 1024)
                throw new RuleViolation("extension.victory", "Null or excessive victory condition tree");
            yield return c;
            foreach (var child in c.Conditions.Reverse()) pending.Push((child, depth + 1));
        }
    }

    public static void Validate(ExtensionVictoryRules rules)
    {
        if (rules == null || rules.Priority is not ("victory" or "defeat") || rules.RoundLimit < 1 || rules.DescriptionKey == null)
            throw new RuleViolation("extension.victory", "Invalid outcome priority/round limit");
        var conditions = Conditions(rules.Win).Concat(rules.Lose == null ? Array.Empty<VictoryCondition>() : Conditions(rules.Lose)).ToArray();
        if (conditions.Length > 1024) throw new RuleViolation("extension.victory", "Too many victory conditions");
        foreach (var c in conditions)
        {
            if (c.Type is not ("all" or "any" or "enemy_factions_defeated" or "factions_defeated" or "units_destroyed" or "units_lost"
                    or "cells_owned" or "cells_lost" or "enemy_objectives_cleared" or "round_reached" or "resources" or "script"
                    or "enemy_units_remaining" or "enemy_objectives_remaining")
                || c.Type is "all" or "any" && c.Conditions.Count == 0 || c.Round < 1
                || c.Type is "cells_owned" or "cells_lost" && c.Cells.Count == 0
                || c.Type is "units_destroyed" or "units_lost" && c.Units.Count == 0
                || c.Type == "factions_defeated" && c.Factions.Count == 0
                || c.Factions.Any(string.IsNullOrWhiteSpace) || c.Units.Any(string.IsNullOrWhiteSpace) || c.Cells.Any(i => i < 0)
                || c.Resources.Money < 0 || c.Resources.Industry < 0 || c.Resources.Food < 0
                || c.Type is "enemy_units_remaining" or "enemy_objectives_remaining"
                    && (c.Value < 0 || c.Compare is not ("eq" or "ne" or "gt" or "ge" or "lt" or "le"))
                || c.Type == "script" && (c.Script == null || !c.Script.StartsWith("rewrite/", StringComparison.Ordinal)
                    || !c.Script.EndsWith(".lua", StringComparison.Ordinal) || c.Script.Contains('\\') || c.Script.Contains(':')
                    || c.Script.Split('/').Any(p => p is "" or "." or "..") || c.Function == null
                    || !System.Text.RegularExpressions.Regex.IsMatch(c.Function, "^[A-Za-z_][A-Za-z0-9_]*$")))
                throw new RuleViolation("extension.victory", "Invalid victory condition: " + c.Type);
            if (c.Type is not "script" && (c.Script != null || c.Function != null))
                throw new RuleViolation("extension.victory", "Script handlers are only valid for type 'script'");
            c.Resources.Validate();
        }
    }

    public static void ValidateTargets(ExtensionVictoryRules rules, WorldState world, bool initial, Func<string, bool>? knownFaction = null)
    {
        foreach (var c in Conditions(rules.Win).Concat(rules.Lose == null ? Array.Empty<VictoryCondition>() : Conditions(rules.Lose)))
            if (c.Cells.Any(i => i < 0 || i >= world.Cells.Count || !world.Cells[i].Terrain.Active)
                || c.Factions.Any(id => !(knownFaction?.Invoke(id) ?? world.Factions.Any(f => f.Id == id)))
                || initial && c.Units.Any(id => world.Units.All(u => u.Id != id)))
                throw new RuleViolation("extension.victory_reference", "Victory condition references an unknown unit/faction/cell");
    }

    public static ExtensionVictoryRules Copy(ExtensionVictoryRules rules)
    {
        VictoryCondition Clone(VictoryCondition c) => c with { Conditions = c.Conditions.Select(Clone).ToArray(),
            Cells = c.Cells.ToArray(), Units = c.Units.ToArray(), Factions = c.Factions.ToArray() };
        return rules with { Win = Clone(rules.Win), Lose = rules.Lose == null ? null : Clone(rules.Lose) };
    }

    public static BattleOutcome Evaluate(WorldState state, string camp, IReadOnlyCollection<string> neutral, ExtensionVictoryRules rules, IRuleScriptRuntime? scripts)
    {
        bool win = Match(state, camp, neutral, rules.Win, scripts);
        bool lose = rules.Lose != null && Match(state, camp, neutral, rules.Lose, scripts);
        return lose && (!win || rules.Priority == "defeat") ? BattleOutcome.Defeat : win ? BattleOutcome.Victory : BattleOutcome.Continue;
    }
    private static bool Compare(long actual, string op, long threshold) => op switch
    {
        "eq" => actual == threshold,
        "ne" => actual != threshold,
        "gt" => actual > threshold,
        "ge" => actual >= threshold,
        "lt" => actual < threshold,
        "le" => actual <= threshold,
        _ => throw new RuleViolation("extension.victory_compare", "Unknown comparison: " + op),
    };

    private static long HostileUnitCount(WorldState state, string camp, IReadOnlyCollection<string> neutral) =>
        state.Units.LongCount(u => state.DiplomaticRelations.Count > 0 && state.ScenarioPlayerFaction is string observer
            ? WorldDiplomacy.Hostile(state, observer, u.Owner, neutral)
            : state.Factions.Single(f => f.Id == u.Owner).Camp != camp);

    private static long EnemyObjectiveCount(WorldState state, string camp, IReadOnlyCollection<string> neutral) =>
        WorldVictory.Facts(state, neutral).Where(f => f.Combatant && f.Camp != camp).Sum(f => (long)f.Objectives);

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
            case "factions_defeated": return c.Factions.All(id => state.Factions.Any(f => f.Id == id && f.Defeated));
            case "units_destroyed": return c.Units.All(id => state.Units.All(u => u.Id != id));
            case "units_lost": return c.Units.Any(id => state.Units.All(u => u.Id != id));
            case "cells_owned": return c.Cells.All(Owned);
            case "cells_lost": return c.Cells.Any(cell => !Owned(cell));
            case "enemy_objectives_cleared": return !WorldVictory.Facts(state, neutral).Any(f => !f.Defeated && f.Combatant && f.Camp != camp && f.Objectives > 0);
            case "round_reached": return state.Round >= c.Round;
            case "resources": return state.Factions.Where(f => f.Human && f.Camp == camp).Any(f => f.Resources.Money >= c.Resources.Money
                && f.Resources.Industry >= c.Resources.Industry && f.Resources.Food >= c.Resources.Food);
            case "enemy_units_remaining":
                return Compare(HostileUnitCount(state, camp, neutral), c.Compare, c.Value);
            case "enemy_objectives_remaining":
                return Compare(EnemyObjectiveCount(state, camp, neutral), c.Compare, c.Value);
            case "script":
                if (scripts == null)
                {
                    throw new RuleViolation("extension.script", "Victory script conditions require API 1 legacy script runtime");
                }

                var values = new Dictionary<string, long>(state.ExtensionState)
                {
                    ["round"] = state.Round,
                    ["enemyUnits"] = HostileUnitCount(state, camp, neutral),
                    ["enemyObjectives"] = EnemyObjectiveCount(state, camp, neutral),
                };
                return scripts.Run(c.Script!, c.Function!, new("scenario.victory", null, null, 1, state.Round, 0, values, state, camp), new Ew4Random(state.RandomState)).Value != 0;
            default: throw new RuleViolation("extension.victory_type", "Unknown victory condition: " + c.Type);
        }
    }
}
