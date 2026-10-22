using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Ew4.Core;

namespace Ew4.Legacy;

public sealed record LegacyInitializedFactions(IReadOnlyList<LegacyFaction> Records, IReadOnlyList<int> TurnOrder,
    int PlayerIndex, VictoryRules Victory, int HighestRatingRound, string SourceRevision);

public static class LegacyScenarioInitialization
{
    public static LegacyInitializedFactions Prepare(LegacyBattle battle, int mode, string? selectedCountry,
        LegacyWarzoneTechnology technology, int warzone = 0, IReadOnlyList<int>? technologySnapshot = null,
        IReadOnlyDictionary<string, string>? countryUnitVariants = null)
    {
        if (mode is not (1 or 2 or 4)) throw new RuleViolation("legacy.start_mode", "This single-player importer supports campaign, conquest and tutorial modes");
        if (battle.Factions.Count == 0
            || battle.Header[12] < 1 || battle.Header[13] < 0)
            throw new RuleViolation("legacy.start_header", "Invalid or ambiguous scenario startup metadata");
        var records = battle.Factions.Select(f => f with { Values68 = Array.AsReadOnly(f.Values68.ToArray()) }).ToArray();
        if (countryUnitVariants != null)
            for (int i = 0; i < records.Length; i++)
                if (countryUnitVariants.TryGetValue(records[i].CountryId, out string? variant)) records[i] = records[i] with { Variant = variant };
        if (mode == 2)
        {
            int chosen = Array.FindIndex(records, f => f.CountryId == selectedCountry);
            if (chosen < 0) throw new RuleViolation("legacy.start_player", "Selected conquest country does not exist");
            records[chosen] = records[chosen] with { Ai = false };
            if (records.Where((f, i) => i != chosen).Any(f => !f.Ai))
                throw new RuleViolation("legacy.start_player", "Conquest contains another human faction; an explicit multiplayer policy is required");
        }
        else if (selectedCountry != null)
            throw new RuleViolation("legacy.start_player", "Campaign and tutorial human factions come from their scenario records");
        int player = Array.FindIndex(records, f => !f.Ai);
        if (player < 0) throw new RuleViolation("legacy.start_player", "Scenario has no human faction");
        if (mode == 2)
            for (int i = 0; i < records.Length; i++)
                if (records[i].Camp == records[player].Camp)
                    records[i] = records[i] with { IncomeMultiplier = 1, AiEvasionBonus = 0 };
        records[player] = technology.Apply(records[player], mode, true, warzone, technologySnapshot);
        var order = new[] { player }.Concat(Enumerable.Range(0, records.Length).Where(i => i != player)).ToArray();
        var victory = new VictoryRules(battle.Header[12], battle.Header[11] == 0);
        string revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { schema = 1, battle = Convert.ToBase64String(battle.ToBytes()), mode, selectedCountry, warzone,
                technology = technology.SourceRevision, records, order, victory, ratingRound = battle.Header[13] })));
        return new(Array.AsReadOnly(records), Array.AsReadOnly(order), player, victory, battle.Header[13], revision);
    }

    public static string FactionId(string scenarioId, int sourceIndex, LegacyFaction source)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || sourceIndex < 0 || string.IsNullOrWhiteSpace(source.CountryId))
            throw new RuleViolation("legacy.start_identity", "Missing scenario faction identity");
        return scenarioId + ".faction." + sourceIndex.ToString(CultureInfo.InvariantCulture) + "." + source.CountryId;
    }
}
