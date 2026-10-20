using System.Text.Json;
using Ew4.Core;

internal static class AiStrategyChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        var rules = new AiStrategyRules();
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_strategy_vectors.json")));
        foreach (var v in data.RootElement.EnumerateArray())
        {
            int Int(string name) => v.GetProperty(name).GetInt32();
            bool Flag(string name) => v.GetProperty(name).GetBoolean();
            var unit = new AiStrategyUnit(Int("health"), Int("maximum"), Int("movement"), Flag("naval"), Flag("transport"), Flag("commander"), Flag("source_sea"), Int("own_strength"), Int("pressure"));
            var goal = new AiStrategyGoal(Int("goal_value"), Int("goal_strength"), Flag("target_present"), Flag("target_naval"), Flag("target_sea"), Flag("sea_reachable"));
            bool eligible = AiStrategy.Eligible(unit, goal, rules);
            Check(eligible == Flag("eligible"), "native strategic goal eligibility");
            long weighted = AiStrategy.Weight(unit, goal, 2, rules);
            Check(weighted == Int("before_behavior"), "native strategic threat/health/distance weighting");
            if (!eligible) continue;
            var behavior = Int("behavior") switch { 0 => AiStrategyBehavior.VariedHalf, 2 => AiStrategyBehavior.VariedQuarter, 3 => AiStrategyBehavior.Zero, _ => AiStrategyBehavior.Full };
            long bound = AiStrategy.RandomBound(weighted, behavior);
            long draw = bound == 0 ? 0 : Int("random_value") % bound;
            Check(AiStrategy.ApplyBehavior(weighted, behavior, draw) == Int("score"), "native strategic behavior weighting");
            var bounds = v.GetProperty("random_bounds").EnumerateArray().Select(x => x.GetInt64()).ToArray();
            Check((behavior is AiStrategyBehavior.VariedHalf or AiStrategyBehavior.VariedQuarter ? new[] { bound } : []).SequenceEqual(bounds), "native strategic random call bound");
        }
        var random = new AiRandom(0);
        Check(random.Next(0) == 0 && random.State == 0, "zero AI random bound does not advance state");
        Check((ulong)random.Next(long.MaxValue) == 0x6220a8397b1dcdB0UL, "published SplitMix64 seed-zero first output modulo long bound");
        var savedState = random.State;
        var restored = new AiRandom(savedState);
        foreach (long bound in new[] { 1L, 2L, 32768L, 1_000_000_000_000L, long.MaxValue })
        {
            for (int i = 0; i < 100; i++)
            {
                long value = random.Next(bound);
                Check(value >= 0 && value < bound && value == restored.Next(bound) && random.State == restored.State, "AI full-width bounded draws deterministic after state restoration");
            }
        }
        Check(AiStrategy.RandomBound(1_000_000_000_000L, AiStrategyBehavior.VariedQuarter) == 750_000_000_000L, "strategic weighting has no original 32-bit score cap");
        var combat = new Ew4Random(1946);
        new AiRandom(1946).Next(100);
        Check(combat.State == 1946, "AI sampling independent of combat random state");
        var unitBoundary = new AiStrategyUnit(100, 100, 2, false, false, false, false, 50, 100);
        var boundaryGoal = new AiStrategyGoal(600, 0, true, false, false, true);
        Check(AiStrategy.Weight(unitBoundary, boundaryGoal, 0, rules) > AiStrategy.Weight(unitBoundary with { Strength = 49 }, boundaryGoal, 0, rules),
            "native exact-half-threat boundary preserves undivided score");
        return checks;
    }
}
