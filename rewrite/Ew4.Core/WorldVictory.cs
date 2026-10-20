using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public static class WorldVictory
{
    public static IReadOnlyCollection<FactionFacts> Facts(WorldState state, IReadOnlyCollection<string> neutralCamps,
        ObjectiveUnitOwnership ownership = ObjectiveUnitOwnership.UnitOwner)
    {
        if (!Enum.IsDefined(typeof(ObjectiveUnitOwnership), ownership))
            throw new RuleViolation("victory.configuration", "Unknown unit objective ownership policy");
        var counts = state.Factions.ToDictionary(f => f.Id, _ => 0, StringComparer.Ordinal);
        foreach (var cell in state.Cells)
            if (cell.Objective && cell.Owner != null) counts[cell.Owner] = checked(counts[cell.Owner] + 1);
        foreach (var unit in state.Units.Where(u => u.Objective))
        {
            string? owner = ownership == ObjectiveUnitOwnership.UnitOwner ? unit.Owner : state.Cells[unit.Cell].Owner;
            if (owner != null) counts[owner] = checked(counts[owner] + 1);
        }
        return state.Factions.Select(f => new FactionFacts(f.Id, f.Camp, f.Human, f.Defeated,
            counts[f.Id], !neutralCamps.Contains(f.Camp) || state.Factions.Any(p => p.Human && WorldDiplomacy.Provoked(state, f.Id, p.Id)))).ToArray();
    }

    public static BattleOutcome Evaluate(WorldState state, string playerCamp, VictoryRules rules,
        IReadOnlyCollection<string> neutralCamps)
    {
        if (rules.Custom == null) return Victory.Evaluate(playerCamp, state.Round,
            Facts(state, neutralCamps, rules.UnitObjectiveOwnership), rules);
        if (rules.RoundLimit.HasValue && state.Round > rules.RoundLimit.Value
            || !state.Factions.Any(f => f.Human && f.Camp == playerCamp && !f.Defeated)) return BattleOutcome.Defeat;
        return ExtensionVictory.Evaluate(state, playerCamp, neutralCamps, rules.Custom, rules.Scripts);
    }
}
