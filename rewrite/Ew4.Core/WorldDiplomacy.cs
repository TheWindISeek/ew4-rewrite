using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record NeutralHostility(string First, string Second);

public static class WorldDiplomacy
{
    public static bool IsNeutral(WorldState state, string faction, IReadOnlyCollection<string> camps) =>
        camps.Contains(state.Factions.Single(f => f.Id == faction).Camp);

    public static bool Provoked(WorldState state, string first, string second) =>
        state.NeutralHostilities.Any(h => h.First == first && h.Second == second || h.Second == first && h.First == second);

    public static bool NeutralTo(WorldState state, string faction, string observer, IReadOnlyCollection<string> camps) =>
        IsNeutral(state, faction, camps) && !Provoked(state, faction, observer);

    public static bool Dormant(WorldState state, string faction, IReadOnlyCollection<string> camps) =>
        IsNeutral(state, faction, camps) && !state.NeutralHostilities.Any(h => h.First == faction || h.Second == faction);

    public static bool Hostile(WorldState state, string first, string second, IReadOnlyCollection<string> camps) =>
        state.Factions.Single(f => f.Id == first).Camp != state.Factions.Single(f => f.Id == second).Camp
        && (!(IsNeutral(state, first, camps) || IsNeutral(state, second, camps)) || Provoked(state, first, second));

    public static WorldState Attacked(WorldState state, string attacker, string defender, IReadOnlyCollection<string> camps)
    {
        if (!(IsNeutral(state, attacker, camps) || IsNeutral(state, defender, camps)) || attacker == defender || Provoked(state, attacker, defender)) return state;
        var pair = string.CompareOrdinal(attacker, defender) < 0 ? new NeutralHostility(attacker, defender) : new NeutralHostility(defender, attacker);
        return state with { NeutralHostilities = state.NeutralHostilities.Append(pair).ToArray() };
    }
}
