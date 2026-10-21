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
        var player = state.Factions.SingleOrDefault(f => f.Id == state.ScenarioPlayerFaction) ?? state.Factions.FirstOrDefault(f => f.Human);
        return state.Factions.Select(f =>
        {
            if (state.DiplomaticRelations.Count == 0 || player == null)
                return new FactionFacts(f.Id, f.Camp, f.Human, f.Defeated, counts[f.Id],
                    !neutralCamps.Contains(f.Camp) || state.Factions.Any(p => p.Human && WorldDiplomacy.Provoked(state, f.Id, p.Id)));
            string relation = WorldDiplomacy.Relation(state, player.Id, f.Id, neutralCamps);
            // Victory's camp-based evaluator receives the current player's bilateral viewpoint.
            return new FactionFacts(f.Id, relation == "allied" ? player.Camp : player.Camp + "/enemy/" + f.Id,
                f.Human, f.Defeated, counts[f.Id], relation != "neutral");
        }).ToArray();
    }

    public static BattleOutcome Evaluate(WorldState state, string playerCamp, VictoryRules rules,
        IReadOnlyCollection<string> neutralCamps)
    {
        if (state.ScenarioPlayerFaction != null)
            playerCamp = state.Factions.Single(f => f.Id == state.ScenarioPlayerFaction).Camp;
        if (rules.Custom == null) return Victory.Evaluate(playerCamp, state.Round,
            Facts(state, neutralCamps, rules.UnitObjectiveOwnership), rules);
        if (rules.Custom.RoundLimit.HasValue && state.Round > rules.Custom.RoundLimit.Value
            || rules.Custom.RequirePlayerSurvival && !state.Factions.Any(f => f.Human && f.Camp == playerCamp && !f.Defeated)) return BattleOutcome.Defeat;
        return ExtensionVictory.Evaluate(state, playerCamp, neutralCamps, rules.Custom, rules.Scripts);
    }
}
