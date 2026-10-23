using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public sealed record AiRecruitOfferEntry(string Id, bool Affordable);
public sealed record AiRecruitOfferGroup(string Id, bool Unlocked, IReadOnlyList<AiRecruitOfferEntry> HighestFirst);
public sealed record AiOfferAdmission(long Bound, long MinimumDraw);
public sealed record AiOfferPoolRules
{
    public int SecondAffordableSkipChance { get; init; } = 21;
    public int LaterAffordableSkipChance { get; init; } = 51;
    public long SelectionStride { get; init; } = 1;
    public IReadOnlyDictionary<string, AiOfferAdmission> Admission { get; init; } = new Dictionary<string, AiOfferAdmission>();
    public void Validate()
    {
        if (SecondAffordableSkipChance < 0 || SecondAffordableSkipChance > 100 || LaterAffordableSkipChance < 0 || LaterAffordableSkipChance > 100
            || SelectionStride < 1 || Admission.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value.Bound < 1
                || p.Value.MinimumDraw < 0 || p.Value.MinimumDraw > p.Value.Bound))
            throw new RuleViolation("ai.offer_configuration", "Invalid recruit offer pooling policy");
    }
}

public static partial class AiProduction
{
    public static IReadOnlyList<string> RecruitPool(IReadOnlyList<AiRecruitOfferGroup> groups, AiOfferPoolRules rules, Func<long, long> draw)
    {
        rules.Validate();
        if (groups.Any(g => string.IsNullOrWhiteSpace(g.Id) || g.HighestFirst.Any(o => string.IsNullOrWhiteSpace(o.Id)))
            || groups.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != groups.Count)
            throw new RuleViolation("ai.offer_group", "Invalid or duplicate recruitment offer group");
        var choices = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            int affordable = 0;
            foreach (var offer in group.HighestFirst)
            {
                if (!seen.Add(offer.Id)) throw new RuleViolation("ai.offer_identity", "Recruitment offer appears in multiple pool positions");
                if (!group.Unlocked || !offer.Affordable) continue;
                affordable++;
                if (affordable == 1 || Draw(draw, 100) >= (affordable == 2 ? rules.SecondAffordableSkipChance : rules.LaterAffordableSkipChance))
                    choices.Add(offer.Id);
            }
        }
        return choices.ToArray();
    }

    public static string? SelectRecruitOffer(IReadOnlyList<string> pool, AiOfferPoolRules rules, Func<long, long> draw)
    {
        rules.Validate();
        if (pool.Any(string.IsNullOrWhiteSpace)) throw new RuleViolation("ai.offer_identity", "Invalid recruitment offer identity");
        if (pool.Count == 0) return null;
        long bound = checked(pool.Count * rules.SelectionStride);
        string selected = pool[(int)(Draw(draw, bound) / rules.SelectionStride)];
        if (rules.Admission.TryGetValue(selected, out var admission) && Draw(draw, admission.Bound) < admission.MinimumDraw) return null;
        return selected;
    }
}

public static class LegacyAiOfferPool
{
    public static AiOfferPoolRules Rules() => new()
    {
        SelectionStride = 20, Admission = new Dictionary<string, AiOfferAdmission> { ["33"] = new(10, 8) }
    };
}
