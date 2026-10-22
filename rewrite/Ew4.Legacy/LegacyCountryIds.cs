using System.Security.Cryptography;
using Ew4.Core;

namespace Ew4.Legacy;

// Ordered by source record index, never by registry order. The legacy body stays
// byte-identical so ownership indices and other extension source hashes survive.
public sealed record LegacyCountryIds(int Schema, string BaseSha256, IReadOnlyList<string> Factions,
    IReadOnlyDictionary<int, string>? EventCountries = null)
{
    internal void Validate(ReadOnlySpan<byte> body, int factions, int events)
    {
        if (Schema != 1 || BaseSha256 != Convert.ToHexStringLower(SHA256.HashData(body))
            || Factions == null || Factions.Count != factions || Factions.Any(id => id == null || !ScenarioFactions.CountryId(id))
            || Factions.Distinct(StringComparer.Ordinal).Count() != Factions.Count
            || EventCountries != null && EventCountries.Any(p => p.Key < 0 || p.Key >= events || !Factions.Contains(p.Value, StringComparer.Ordinal)))
            throw new RuleViolation("legacy.country_ids", "CNTR source, ordered country IDs or event references are invalid");
    }

    internal void Apply(LegacyFaction[] factions, LegacyEvent[] events)
    {
        var legacy = factions.Select((f, i) => (f.CountryId, Id: Factions[i])).GroupBy(f => f.CountryId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToArray(), StringComparer.Ordinal);
        for (int i = 0; i < events.Length; i++)
        {
            if (EventCountries?.TryGetValue(i, out string? country) == true) events[i] = events[i] with { Country = country };
            else if (legacy.TryGetValue(events[i].Country, out var candidates))
            {
                if (candidates.Length != 1)
                    throw new RuleViolation("legacy.country_event", "CNTR requires an explicit eventCountries entry for ambiguous source country at event record " + i);
                events[i] = events[i] with { Country = candidates[0] };
            }
        }
        for (int i = 0; i < factions.Length; i++) factions[i] = factions[i] with { CountryId = Factions[i] };
    }
}
