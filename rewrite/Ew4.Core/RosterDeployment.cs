using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record RosterDeploymentState(string Faction, int? Limit, IReadOnlyList<string> CommanderIds);
public sealed record OwnedDeploymentTemplate(Commander Definition, CommanderRuntime Runtime);
public sealed record RosterDeploymentRules
{
    public AccountCommanderRules Account { get; init; } = new();
    public WorldCommanderRules Assignment { get; init; } = new();
    public IReadOnlyDictionary<int, int> RankHealth { get; init; } = new Dictionary<int, int>();

    public OwnedDeploymentTemplate Resolve(OwnedCommander source, string owner)
    {
        Account.Validate(); Assignment.Validate();
        var definition = AccountCommanders.Resolve(source, Account);
        if (string.IsNullOrWhiteSpace(owner) || RankHealth.Any(p => p.Key < 0 || p.Value < 0)
            || !RankHealth.TryGetValue(source.Learning.Rank.Level, out int health)
            || !Assignment.TitleHealing.ContainsKey(source.Learning.Title.Level))
            throw new RuleViolation("session.roster_definition", "Missing rank or title stage for owned commander");
        int? Percent(int effect) => definition.Items.Where(i => i.Effect == Ew4Commander.Effect(effect)).Select(i => (int?)i.Value).FirstOrDefault();
        return new(definition, new() { Id = source.Id, Owner = owner, Rank = source.Learning.Rank, Title = source.Learning.Title,
            RankHealthBonus = health, MaximumTrainingLevel = source.Training,
            RankExperiencePercent = Percent(1), TitleExperiencePercent = Percent(2) });
    }
}

public static class RosterDeployment
{
    public static SessionChange Deploy(GameSession state, long expectedRevision, string unitId, string commanderId, RosterDeploymentRules rules)
    {
        state.Validate();
        if (state.Revision != expectedRevision) throw new RuleViolation("session.revision", "Stale session command");
        var faction = WorldActions.Ready(state.World, state.World.Revision);
        var quota = state.RosterDeployments.SingleOrDefault(q => q.Faction == faction.Id);
        var owned = state.Account.Roster.SingleOrDefault(c => c.Id == commanderId);
        var unit = state.World.Units.SingleOrDefault(u => u.Id == unitId);
        if (faction.Id != state.PlayerFaction || !faction.Human || quota == null || owned == null || unit == null
            || unit.Owner != faction.Id || unit.CommanderId != null || !state.World.Cells[unit.Cell].Terrain.Active
            || state.World.Cells[unit.Cell].Owner != faction.Id || state.World.Commanders.Any(c => c.Id == commanderId)
            || state.TavernCandidates.Any(c => c.CommanderId == commanderId)
            || state.RosterDeployments.Any(q => q.CommanderIds.Contains(commanderId))
            || quota.CommanderIds.Count >= quota.Limit
            || (rules.Assignment.DeploymentBuilding == BuildingRequirement.Required && state.World.Cells[unit.Cell].BuildingKind == null)
            || (rules.Assignment.DeploymentBuilding == BuildingRequirement.Forbidden && state.World.Cells[unit.Cell].BuildingKind != null))
            throw new RuleViolation("session.roster_unavailable", "Owned commander or deployment quota/location is unavailable");
        var template = rules.Resolve(owned, faction.Id);
        var definitions = rules.Assignment.Definitions.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (!definitions.TryAdd(commanderId, template.Definition))
            throw new RuleViolation("session.roster_definition", "Owned instance collides with an existing assignment definition");
        var world = state.World with { Commanders = state.World.Commanders.Concat(new[] { template.Runtime }).ToArray() };
        var assigned = WorldCommanders.Assign(world, world.Revision, unitId, commanderId, rules.Assignment with { Definitions = definitions });
        var next = (state with { Revision = checked(state.Revision + 1), World = assigned.State,
            RosterDeployments = state.RosterDeployments.Select(q => q.Faction != faction.Id ? q
                : q with { CommanderIds = q.CommanderIds.Concat(new[] { commanderId }).ToArray() }).ToArray() }).Copy();
        next.Validate();
        return new(next, assigned.Notices.Concat(new[] { new WorldNotice("roster.deployed", commanderId, unit.Cell) }).ToArray());
    }
}
