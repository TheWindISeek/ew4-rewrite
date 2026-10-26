using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public sealed record SessionMarket(int? Cell, ItemMarket Market);
public sealed record TavernCandidate(string Id, string CommanderId, int Cell, int AvailableRound, Resources Cost, long Medals);
// The session is the transaction boundary for battlefield resources and persistent account medals.
public sealed record GameSession
{
    public string BattleId { get; init; } = "";
    public BattleResult? BattleResult { get; init; }
    public long CollectedMedals { get; init; }
    public string ContentRevision { get; init; } = "";
    public long Revision { get; init; }
    public string PlayerFaction { get; init; } = "";
    public WorldState World { get; init; } = new();
    public PlayerAccount Account { get; init; } = new();
    public IReadOnlyList<SessionMarket> Markets { get; init; } = Array.Empty<SessionMarket>();
    public IReadOnlyList<TavernCandidate> TavernCandidates { get; init; } = Array.Empty<TavernCandidate>();
    public IReadOnlyList<RosterDeploymentState> RosterDeployments { get; init; } = Array.Empty<RosterDeploymentState>();
    public void Validate()
    {
        World.Validate(); Account.Validate();
        if (CollectedMedals < 0 || BattleResult != null && (BattleResult.Id != BattleId
            || BattleResult.Scenario != World.ScenarioId || World.Phase != TurnPhase.Finished
            || BattleResult.Outcome != World.Outcome.ToString() || BattleResult.Round != World.Round))
            throw new RuleViolation("session.result", "Invalid battle result binding");
        if (string.IsNullOrWhiteSpace(ContentRevision) || Revision < 0 || !World.Factions.Any(f => f.Id == PlayerFaction)
            || World.ScenarioPlayerFaction != null && World.ScenarioPlayerFaction != PlayerFaction
            || Markets.Select(m => m.Market.Id).Distinct(StringComparer.Ordinal).Count() != Markets.Count
            || TavernCandidates.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != TavernCandidates.Count
            || TavernCandidates.Select(c => c.CommanderId).Distinct(StringComparer.Ordinal).Count() != TavernCandidates.Count)
            throw new RuleViolation("session.state", "Invalid session identity, player or duplicate service entry");
        foreach (var market in Markets)
            if (market.Cell < 0 || market.Cell >= World.Cells.Count || string.IsNullOrWhiteSpace(market.Market.Id)
                || market.Market.Offers.Any(o => string.IsNullOrWhiteSpace(o.Id) || string.IsNullOrWhiteSpace(o.ItemId) || o.Remaining < 0)
                || market.Market.Offers.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != market.Market.Offers.Count)
                throw new RuleViolation("session.market", "Invalid market location, identity or stock");
        foreach (var candidate in TavernCandidates)
        {
            candidate.Cost.Validate();
            if (string.IsNullOrWhiteSpace(candidate.Id) || string.IsNullOrWhiteSpace(candidate.CommanderId)
                || candidate.Cell < 0 || candidate.Cell >= World.Cells.Count || candidate.AvailableRound < 1 || candidate.Medals < 0
                || World.Commanders.Any(c => c.Id == candidate.CommanderId) || Account.Roster.Any(c => c.Id == candidate.CommanderId))
                throw new RuleViolation("session.tavern", "Invalid candidate location, cost or instance identity");
        }
        if (RosterDeployments.Select(q => q.Faction).Distinct(StringComparer.Ordinal).Count() != RosterDeployments.Count
            || RosterDeployments.SelectMany(q => q.CommanderIds).Distinct(StringComparer.Ordinal).Count() != RosterDeployments.Sum(q => q.CommanderIds.Count))
            throw new RuleViolation("session.roster_history", "Duplicate deployment faction or owned commander history");
        // History belongs to the deploying country even if an event later transfers the commander.
        foreach (var quota in RosterDeployments)
            if (!World.Factions.Any(f => f.Id == quota.Faction) || quota.Limit < 0 || quota.CommanderIds.Count > quota.Limit
                || quota.CommanderIds.Any(id => !Account.Roster.Any(c => c.Id == id) || !World.Commanders.Any(c => c.Id == id)))
                throw new RuleViolation("session.roster_history", "Invalid deployment quota or historical commander reference");
    }
    public GameSession Copy() => this with { World = World.Copy(), Account = Account.Copy(),
        Markets = Markets.Select(m => new SessionMarket(m.Cell, new(m.Market.Id, m.Market.Offers.Select(o => o with { }).ToArray()))).ToArray(),
        TavernCandidates = TavernCandidates.Select(c => c with { }).ToArray(),
        RosterDeployments = RosterDeployments.Select(q => q with { CommanderIds = q.CommanderIds.ToArray() }).ToArray() };
}

public sealed record SessionChange(GameSession State, IReadOnlyList<WorldNotice> Notices);

public static class SessionActions
{
    public static GameSession SyncPlayer(GameSession state)
    {
        if (state.World.LocalMultiplayer != null) return LocalMultiplayerGame.SyncPlayer(state);
        string player = state.World.ScenarioPlayerFaction ?? state.PlayerFaction;
        if (player == state.PlayerFaction) return state;
        var quotas = state.RosterDeployments;
        if (quotas.All(q => q.Faction != player))
        {
            int? limit = quotas.SingleOrDefault(q => q.Faction == state.PlayerFaction)?.Limit;
            quotas = quotas.Append(new RosterDeploymentState(player, limit, Array.Empty<string>())).ToArray();
        }
        return state with { PlayerFaction = player, RosterDeployments = quotas };
    }

    private static void Revision(GameSession state, long expected)
    {
        state.Validate();
        if (state.Revision != expected) throw new RuleViolation("session.revision", "Stale session command");
    }
    public static SessionChange CommitWorld(GameSession state, long expectedRevision, WorldChange change)
    {
        Revision(state, expectedRevision);
        if (change.State.Revision != checked(state.World.Revision + 1) || change.State.ScenarioId != state.World.ScenarioId
            || change.State.ContentRevision != state.World.ContentRevision)
            throw new RuleViolation("session.world_revision", "World transaction must advance this scenario once");
        var next = SyncPlayer(state with { Revision = checked(state.Revision + 1), World = change.State }).Copy(); next.Validate();
        return new(next, change.Notices.ToArray());
    }
}
