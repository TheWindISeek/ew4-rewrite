using System.Text.Json;
using Ew4.Core;

internal static class AiOfferPoolChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        void Reject(Action action, string code)
        {
            try { action(); }
            catch (RuleViolation e) { Check(e.Code == code, code + ": " + e.Code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        int[] bases = [0, 3, 6, 9, 12, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 34, 35, 36];
        var rules = LegacyAiOfferPool.Rules();
        using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_offer_pool_vectors.json")));
        foreach (var vector in native.RootElement.EnumerateArray())
        {
            var v = vector.GetProperty("inputs");
            var units = v.GetProperty("units").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var grades = v.GetProperty("grades").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var raw = v.GetProperty("raw_random").EnumerateArray().Select(x => x.GetInt64()).ToArray();
            var bounds = new List<long>();
            int drawIndex = 0;
            long Draw(long bound) { bounds.Add(bound); return raw[drawIndex++] % bound; }
            var groups = units.Select((unit, i) => new AiRecruitOfferGroup(unit.ToString(), v.GetProperty("unlocked").GetProperty(unit.ToString()).GetBoolean(),
                Enumerable.Range(0, grades[i] + 1).Reverse().Select(grade => new AiRecruitOfferEntry((bases[unit] + grade).ToString(),
                    v.GetProperty("affordable").GetProperty($"{unit}.{grade}").GetBoolean())).ToArray())).ToArray();
            var pool = AiProduction.RecruitPool(groups, rules, Draw);
            Check(pool.SequenceEqual(vector.GetProperty("pool").EnumerateArray().Select(x => x.GetInt32().ToString())), "native affordability-biased candidate pool and order");
            string? choice = AiProduction.SelectRecruitOffer(pool, rules, Draw);
            int expected = vector.GetProperty("expected").GetInt32();
            Check(choice == (expected == 47 ? null : expected.ToString()), "native pooled recruit choice and per-offer admission");
            Check(bounds.SequenceEqual(vector.GetProperty("random_bounds").EnumerateArray().Select(x => x.GetInt64())), "native offer-pool random call order and bounds");
        }
        var modern = new AiOfferPoolRules { SecondAffordableSkipChance = 0, LaterAffordableSkipChance = 0 };
        var random = new AiRandom(1930);
        var categories = Enumerable.Range(0, 75).Select(i => new AiRecruitOfferGroup("category." + i, true,
            Enumerable.Range(0, 12).Reverse().Select(grade => new AiRecruitOfferEntry($"offer.{i}.{grade}", true)).ToArray())).ToArray();
        var largePool = AiProduction.RecruitPool(categories, modern, random.Next);
        Check(largePool.Count == 900 && largePool[0] == "offer.0.11" && largePool[^1] == "offer.74.0", "dynamic pool supports categories and grades far beyond native 18 slots");
        Check(AiProduction.SelectRecruitOffer(largePool, modern, _ => 899) == "offer.74.0", "new offer IDs require no native card mapping or enum extension");
        var locked = categories.Select(g => g with { Unlocked = false }).ToArray();
        ulong before = random.State;
        Check(AiProduction.RecruitPool(locked, modern, random.Next).Count == 0 && random.State == before, "locked categories create no candidates and consume no sampling");
        Check(AiProduction.SelectRecruitOffer([], modern, random.Next) == null && random.State == before, "empty pool does not consume AI randomness");
        var tiered = new[] { new AiRecruitOfferGroup("new.category", true, [new("expensive", false), new("highest.affordable", true), new("lower", false)]) };
        Check(AiProduction.RecruitPool(tiered, modern, random.Next).SequenceEqual(["highest.affordable"]), "highest affordable tier is guaranteed even when higher tier cannot be paid");
        var duplicates = new[] { categories[0], categories[1] with { HighestFirst = categories[0].HighestFirst } };
        Reject(() => AiProduction.RecruitPool(duplicates, modern, random.Next), "ai.offer_identity");
        Reject(() => AiProduction.SelectRecruitOffer(largePool, modern with { SelectionStride = 0 }, random.Next), "ai.offer_configuration");
        Reject(() => AiProduction.SelectRecruitOffer(largePool, modern, _ => 900), "ai.production_random");
        return checks;
    }
}
