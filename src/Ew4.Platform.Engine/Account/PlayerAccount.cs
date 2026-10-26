using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

// 账户状态数据与不变量（交易、编队、继承等规则在 Content/Account）。
public sealed record ItemTradeOffer(string Id, string ItemId, long? Remaining = null);

public sealed record ItemMarket(string Id, IReadOnlyList<ItemTradeOffer> Offers);

// Inventory counts include equipped copies; assignment must never make unique ownership disappear.
public sealed record PlayerAccount
{
    public string Id { get; init; } = "";
    public string ContentRevision { get; init; } = "";
    public long Revision { get; init; }
    public long Medals { get; init; }
    public long Badges { get; init; }
    public IReadOnlyDictionary<int, IReadOnlyList<string>> CommanderOffers { get; init; } = new Dictionary<int, IReadOnlyList<string>>();
    public IReadOnlyDictionary<string, long> Inventory { get; init; } = new Dictionary<string, long>();
    public IReadOnlyList<OwnedCommander> Roster { get; init; } = Array.Empty<OwnedCommander>();
    public IReadOnlyList<string> ClaimedRewards { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnlockedCampaigns { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, int> CampaignStars { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, BattleResult> BattleResults { get; init; } = new Dictionary<string, BattleResult>();
    public ItemMarket? HeadquartersStock { get; init; }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(ContentRevision) || Revision < 0 || Medals < 0 || Badges < 0
            || Inventory.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 1))
            throw new RuleViolation("account.state", "Invalid account identity, balance or inventory");
        if (Roster.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != Roster.Count)
            throw new RuleViolation("account.roster", "Duplicate roster instance identity");
        foreach (var commander in Roster) commander.Validate();
        if (ClaimedRewards.Any(string.IsNullOrWhiteSpace) || ClaimedRewards.Distinct(StringComparer.Ordinal).Count() != ClaimedRewards.Count)
            throw new RuleViolation("account.rewards", "Invalid reward claims");
        if (UnlockedCampaigns.Any(string.IsNullOrWhiteSpace) || UnlockedCampaigns.Distinct(StringComparer.Ordinal).Count() != UnlockedCampaigns.Count)
            throw new RuleViolation("account.campaigns", "Invalid campaign unlocks");
        if (CampaignStars.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 1 || p.Value > 5)
            || BattleResults.Any(p => p.Key != p.Value.Id || string.IsNullOrWhiteSpace(p.Key)
                || p.Value.Round < 1 || p.Value.Stars < 0 || p.Value.Stars > 5 || p.Value.AwardMedals < 0 || p.Value.CollectedMedals < 0))
            throw new RuleViolation("account.results", "Invalid battle result history");
        if (HeadquartersStock != null && (string.IsNullOrWhiteSpace(HeadquartersStock.Id)
            || HeadquartersStock.Offers.Any(o => string.IsNullOrWhiteSpace(o.Id) || string.IsNullOrWhiteSpace(o.ItemId) || o.Remaining < 0)
            || HeadquartersStock.Offers.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != HeadquartersStock.Offers.Count))
            throw new RuleViolation("account.stock", "Invalid headquarters stock");
        if (CommanderOffers.Keys.Any(k => k < 0) || CommanderOffers.Values.SelectMany(v => v).Any(string.IsNullOrWhiteSpace)
            || CommanderOffers.Values.SelectMany(v => v).Distinct(StringComparer.Ordinal).Count() != CommanderOffers.Values.Sum(v => v.Count))
            throw new RuleViolation("account.offers", "Invalid or duplicate commander offers");
        if (Roster.SelectMany(c => c.Items).GroupBy(id => id, StringComparer.Ordinal)
            .Any(g => !Inventory.TryGetValue(g.Key, out long owned) || owned < g.LongCount()))
            throw new RuleViolation("account.equipment", "Equipped items must be owned by this account");
    }
    public PlayerAccount Copy() => this with { Inventory = Inventory.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), Roster = Roster.Select(c => c.Copy()).ToArray(),
        CommanderOffers = CommanderOffers.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value.ToArray()), ClaimedRewards = ClaimedRewards.ToArray(), UnlockedCampaigns = UnlockedCampaigns.ToArray(),
        CampaignStars = CampaignStars.ToDictionary(p => p.Key, p => p.Value), BattleResults = BattleResults.ToDictionary(p => p.Key, p => p.Value),
        HeadquartersStock = HeadquartersStock == null ? null : HeadquartersStock with { Offers = HeadquartersStock.Offers.ToArray() } };
}


public sealed record OwnedCommander(string Id, string DefinitionId, CommanderLearningState Learning, int Training, IReadOnlyList<string> Items)
{
    public string Tag { get; init; } = "";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DefinitionId) || Training < 0
            || Learning.Rank.Level < 0 || Learning.Title.Level < 0 || Learning.Rank.Experience < 0 || Learning.Title.Experience < 0
            || Learning.Abilities.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0)
            || Items.Any(string.IsNullOrWhiteSpace) || Items.Distinct(StringComparer.Ordinal).Count() != Items.Count
            || Tag == null || Tag.Length > 12 || Tag.Any(char.IsControl))
            throw new RuleViolation("account.commander", "Invalid owned commander");
    }
    public OwnedCommander Copy() => this with { Learning = Learning with { Abilities = Learning.Abilities.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) }, Items = Items.ToArray() };
}


public sealed record CommanderLearningState(ProgressionState Rank, ProgressionState Title, IReadOnlyDictionary<string, int> Abilities);

public sealed record RosterDeploymentState(string Faction, int? Limit, IReadOnlyList<string> CommanderIds);
