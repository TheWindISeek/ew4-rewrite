using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.StaticData;

public sealed record StaticDataCommerceImport(IReadOnlyList<SessionMarket> Markets, IReadOnlyList<TavernCandidate> Candidates,
    TavernRules Tavern, SessionTradingRules Trading, string SourceRevision);

public static class StaticDataScenarioCommerce
{
    public static StaticDataCommerceImport Import(StaticDataBattle battle, IReadOnlyList<CellState> cells, StaticDataRuntimeCatalog runtime,
        StaticDataItemTradingCatalog items, StaticDataCommanderAccounts accounts, StaticDataCombatCatalog combat, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) throw new RuleViolation("legacy.commerce_identity", "Missing commerce namespace");
        void Location(int cell)
        {
            // Native services are stored by cell independently of the building currently there.
            if (cell < 0 || cell >= cells.Count)
                throw new RuleViolation("legacy.commerce_location", "Scenario shop or tavern is outside the map");
        }
        var markets = new List<SessionMarket>(); var candidates = new List<TavernCandidate>();
        var templates = new Dictionary<string, CommanderRuntime>(StringComparer.Ordinal);
        var definitions = new Dictionary<string, Commander>(StringComparer.Ordinal);
        var business = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var market in battle.Markets)
        {
            Location(market.Cell);
            markets.Add(new(market.Cell, items.Import(market, prefix + ".shop." + markets.Count.ToString(CultureInfo.InvariantCulture))));
        }
        foreach (var tavern in battle.Taverns)
        {
            Location(tavern.Cell);
            for (int slot = 0; slot < tavern.Candidates.Count; slot++)
            {
                var source = tavern.Candidates[slot];
                if (!runtime.Commanders.TryGetValue(source.CommanderId, out var commander) || source.AvailableRound < 1 || source.Medals < 0)
                    throw new RuleViolation("legacy.tavern_candidate", "Missing commander or invalid candidate timing/cost");
                string id = prefix + ".tavern." + candidates.Count.ToString(CultureInfo.InvariantCulture);
                var cost = new Resources(source.Money, source.Industry, 0); cost.Validate();
                candidates.Add(new(id, id + ".commander", tavern.Cell, source.AvailableRound, cost, source.Medals));
                definitions.Add(id + ".commander", commander.Definition with { Id = id + ".commander" });
                templates.Add(id + ".commander", new() { Id = id + ".commander", Rank = new(commander.Rank, 0), Title = new(commander.Title, 0),
                    RankHealthBonus = commander.HealthBonus, MaximumTrainingLevel = commander.Training,
                    RankExperiencePercent = commander.RankExperiencePercent, TitleExperiencePercent = commander.TitleExperiencePercent });
                business.Add(id + ".commander", accounts.CommanderCommerce[source.CommanderId]);
            }
        }
        var assignment = new WorldCommanderRules { SkillParameters = combat.SkillParameters ?? StaticDataSkillParameters.Default, Definitions = new ReadOnlyDictionary<string, Commander>(definitions),
            TitleHealing = combat.TitleHealing.ToDictionary(p => int.Parse(p.Key, CultureInfo.InvariantCulture), p => p.Value) };
        assignment.Validate();
        var tavernRules = new TavernRules { Assignment = assignment, Templates = new ReadOnlyDictionary<string, CommanderRuntime>(templates) };
        var trading = new SessionTradingRules { Items = items.Rules(), TitleCommerce = accounts.TitleCommerce, CommanderCommerce = new ReadOnlyDictionary<string, int>(business) };
        string revision = Convert.ToHexStringLower(Ew4.Interface.Common.Sha256Hash.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { source = battle.Fingerprint, items.SourceRevision, accounts = accounts.SourceRevision, combat = combat.SourceRevision, prefix })));
        return new(markets.AsReadOnly(), candidates.AsReadOnly(), tavernRules, trading, revision);
    }
}
