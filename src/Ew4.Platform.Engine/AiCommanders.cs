using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public sealed record AiCommanderSite(int Cell, bool Building, bool OfferAvailable, bool HasUnit, bool Assigned, bool Sea, string Category, int Health);
public sealed record AiCommanderRules
{
    public IReadOnlyDictionary<string, int> LandMinimumHealth { get; init; } = new Dictionary<string, int>
        { ["infantry"] = 90, ["cavalry"] = 100, ["artillery"] = 60 };
    public IReadOnlyDictionary<string, int> SeaMinimumHealth { get; init; } = new Dictionary<string, int> { ["naval"] = 126 };
    public void Validate()
    {
        if (LandMinimumHealth.Concat(SeaMinimumHealth).Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 1))
            throw new RuleViolation("ai.commander_configuration", "Invalid commander placement health thresholds");
    }
}
public sealed record AiDeployCommand(long ExpectedRevision, string UnitId, string OfferId);

public static class AiCommanders
{
    public static int? SelectSite(IReadOnlyList<AiCommanderSite> sites, AiCommanderRules rules)
    {
        rules.Validate();
        foreach (var site in sites)
        {
            if (site.Cell < 0 || site.Health < 0 || string.IsNullOrWhiteSpace(site.Category))
                throw new RuleViolation("ai.commander_site", "Invalid commander deployment site");
            var thresholds = site.Sea ? rules.SeaMinimumHealth : rules.LandMinimumHealth;
            if (site.Building && site.OfferAvailable && site.HasUnit && !site.Assigned
                && thresholds.TryGetValue(site.Category, out int minimum) && site.Health >= minimum) return site.Cell;
        }
        return null;
    }
}

public static partial class WorldAi
{
    public static AiDeployCommand? PlanCommander(WorldState state, WorldCommanderRules rules, AiCommanderRules scoring)
    {
        var faction = WorldActions.Ready(state, state.Revision); rules.Validate(); scoring.Validate();
        var offers = state.CommanderOffers.Where(o => o.Owner == faction.Id).GroupBy(o => o.Cell).ToDictionary(g => g.Key,
            g => g.OrderBy(o => o.AvailableRound).ThenBy(o => o.Id, StringComparer.Ordinal).ToArray());
        var sites = new List<AiCommanderSite>();
        var commands = new Dictionary<int, AiDeployCommand>();
        foreach (var unit in state.Units.Where(u => u.Owner == faction.Id && u.CommanderId == null)
            .OrderBy(u => u.Cell).ThenBy(u => u.Id, StringComparer.Ordinal))
        {
            if (commands.ContainsKey(unit.Cell) || !offers.TryGetValue(unit.Cell, out var available)) continue;
            var offer = available.FirstOrDefault(o => WorldCommanders.CanDeployValidated(state, faction, unit, o, rules));
            if (offer == null) continue;
            var site = state.Cells[unit.Cell];
            sites.Add(new(unit.Cell, rules.DeploymentBuilding != BuildingRequirement.Required || site.BuildingKind != null,
                true, true, false, site.Terrain.Sea, unit.Combatant.Profile.Category, unit.Combatant.Health));
            // With configured stacking, another unit at the same site may satisfy the AI threshold.
            if (AiCommanders.SelectSite(new[] { sites[sites.Count - 1] }, scoring) != null)
                commands[unit.Cell] = new(state.Revision, unit.Id, offer.Id);
        }
        int? chosen = AiCommanders.SelectSite(sites, scoring);
        return chosen.HasValue ? commands[chosen.Value] : null;
    }
}
