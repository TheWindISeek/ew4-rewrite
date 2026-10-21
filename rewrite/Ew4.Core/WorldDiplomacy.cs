using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record NeutralHostility(string First, string Second);
public sealed record DiplomaticRelation(string First, string Second, string Relation);

public static class WorldDiplomacy
{
    public static string? Override(WorldState state, string first, string second) => state.DiplomaticRelations
        .FirstOrDefault(p => p.First == first && p.Second == second || p.First == second && p.Second == first)?.Relation;

    public static bool Allied(WorldState state, string first, string second) => first == second
        || (Override(state, first, second) is string relation ? relation == "allied"
            : state.Factions.Single(f => f.Id == first).Camp == state.Factions.Single(f => f.Id == second).Camp);

    public static string Relation(WorldState state, string first, string second, IReadOnlyCollection<string>? camps = null)
    {
        if (first == second) return "allied";
        if (Override(state, first, second) is string relation) return relation;
        if (Allied(state, first, second)) return "allied";
        return Hostile(state, first, second, camps ?? state.DiplomacyNeutralCamps) ? "hostile" : "neutral";
    }

    public static WorldState Set(WorldState state, string first, string second, string relation)
    {
        if (first == second || !state.Factions.Any(f => f.Id == first) || !state.Factions.Any(f => f.Id == second)
            || relation is not ("allied" or "neutral" or "hostile" or "default"))
            throw new RuleViolation("world.diplomacy", "Invalid bilateral relation");
        if (string.CompareOrdinal(first, second) > 0) (first, second) = (second, first);
        var pairs = state.DiplomaticRelations.Where(p => p.First != first || p.Second != second);
        return state with { DiplomaticRelations = (relation == "default" ? pairs : pairs.Append(new(first, second, relation))).ToArray(),
            NeutralHostilities = state.NeutralHostilities.Where(p => p.First != first || p.Second != second).ToArray() };
    }

    public static bool IsNeutral(WorldState state, string faction, IReadOnlyCollection<string> camps) =>
        camps.Contains(state.Factions.Single(f => f.Id == faction).Camp);

    public static bool Provoked(WorldState state, string first, string second) =>
        state.NeutralHostilities.Any(h => h.First == first && h.Second == second || h.Second == first && h.First == second);

    public static bool NeutralTo(WorldState state, string faction, string observer, IReadOnlyCollection<string> camps) =>
        Override(state, faction, observer) is string relation ? relation == "neutral"
            : IsNeutral(state, faction, camps) && !Provoked(state, faction, observer);

    public static bool Dormant(WorldState state, string faction, IReadOnlyCollection<string> camps) =>
        IsNeutral(state, faction, camps) && (state.DiplomaticRelations.Count == 0
            ? !state.NeutralHostilities.Any(h => h.First == faction || h.Second == faction)
            : !state.Factions.Any(f => f.Id != faction && !f.Defeated && Hostile(state, faction, f.Id, camps)));

    public static bool Hostile(WorldState state, string first, string second, IReadOnlyCollection<string> camps) =>
        first != second && (Override(state, first, second) is string relation ? relation == "hostile"
            : !Allied(state, first, second) && (!(IsNeutral(state, first, camps) || IsNeutral(state, second, camps)) || Provoked(state, first, second)));

    public static WorldState Attacked(WorldState state, string attacker, string defender, IReadOnlyCollection<string> camps)
    {
        if (Override(state, attacker, defender) is string relation)
            return relation == "neutral" ? Set(state, attacker, defender, "hostile") : state;
        if (!(IsNeutral(state, attacker, camps) || IsNeutral(state, defender, camps)) || attacker == defender || Provoked(state, attacker, defender)) return state;
        var pair = string.CompareOrdinal(attacker, defender) < 0 ? new NeutralHostility(attacker, defender) : new NeutralHostility(defender, attacker);
        return state with { NeutralHostilities = state.NeutralHostilities.Append(pair).ToArray() };
    }
}
