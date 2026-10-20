using System.Text.Json;
using Ew4.Core;

internal static class LifecycleChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/lifecycle_vectors.json")));
        foreach (var vector in data.RootElement.GetProperty("survival").EnumerateArray())
        {
            var assets = vector.GetProperty("tiles").EnumerateArray().Select(t => new SurvivalAsset(t.GetProperty("unit").GetBoolean(),
                t.GetProperty("building").ValueKind == JsonValueKind.Null ? null : t.GetProperty("building").GetInt32() == 4 ? "farmland" : "city"));
            Check(FactionSurvival.Defeated(assets, FactionSurvival.Legacy(vector.GetProperty("mode").GetInt32()))
                == vector.GetProperty("defeated").GetBoolean(), "native faction survival");
        }
        foreach (var vector in data.RootElement.GetProperty("refresh").EnumerateArray())
        {
            int Int(string name) => vector.GetProperty(name).GetInt32();
            bool Flag(string name) => vector.GetProperty(name).GetBoolean();
            var actual = UnitLifecycle.Refresh(new()
            {
                Health = Int("health"), MaximumHealth = Int("maximum"), BaseHealth = Int("base_health"),
                HealthBonus = Flag("general") ? Int("health_bonus") : 0, BaseMovement = Int("base_movement"),
                MovementBonus = Flag("general") ? Int("movement_bonus") : 0, Morale = new(Int("baseline"), Int("duration"), Int("baseline")),
                LocalPenalty = Int("local"), Shortage = Int("shortage"), Logistics = Flag("general") && Flag("logistics"),
                Leadership = Flag("general") && Flag("leadership"), ConstructionTurns = Int("construction"),
                Behavior = Int("behavior").ToString(), RestoreBehavior = Int("previous_behavior").ToString(),
                BehaviorExpiry = Int("expires") == 0 ? null : Int("expires"), Round = Int("round")
            }, new(MinimumLivingHealth: 0, RestoreBehaviorAfterMissedRound: false));
            var expected = vector.GetProperty("expected");
            var morale = expected.GetProperty("morale");
            Check(actual == new UnitRefreshResult(expected.GetProperty("health").GetInt32(), expected.GetProperty("maximum").GetInt32(),
                expected.GetProperty("movement").GetInt32(), new(morale[0].GetInt32(), morale[1].GetInt32(), morale[2].GetInt32()),
                false, expected.GetProperty("construction").GetInt32(), expected.GetProperty("behavior").GetInt32().ToString(),
                expected.GetProperty("expires").GetInt32() == 0 ? null : expected.GetProperty("expires").GetInt32()), "native unit refresh");
        }
        foreach (var vector in data.RootElement.GetProperty("unit_xp").EnumerateArray())
        {
            int Int(string name) => vector.GetProperty(name).GetInt32();
            int threshold = Int("cost");
            if (vector.GetProperty("general").GetBoolean()) threshold = threshold * 3 / 2;
            if (vector.GetProperty("naval").GetBoolean()) threshold *= 2;
            var rules = new ProgressionRules { SingleAdvancePerAward = true, AccumulateAtCap = true, DiscardExperienceOnReachingCap = false,
                Levels = Enumerable.Range(0, 6).Select(i => new ProgressionLevel("unit." + i, i == 5 ? null : threshold, Int("heal"))).ToArray() };
            var actual = Progression.Award(new(Int("level"), Int("experience")), Int("gain"), rules);
            var expected = vector.GetProperty("expected");
            Check(actual.State == new ProgressionState(expected[0].GetInt32(), expected[1].GetInt64())
                && Math.Min(Int("maximum"), Int("health") + actual.Healing) == expected[2].GetInt32()
                && (actual.LevelsGained > 0) == (expected[3].GetInt32() != 0), "native unit experience");
        }
        var burnRules = Hazards.LegacyBurning();
        burnRules = burnRules with { Stages = burnRules.Stages.Append(new(4, burnRules.Stages[3])).ToDictionary(p => p.Key, p => p.Value) };
        foreach (var vector in data.RootElement.GetProperty("burn").EnumerateArray())
        {
            var random = new Ew4Random(vector.GetProperty("seed").GetUInt64());
            var actual = Hazards.EndUnit(vector.GetProperty("health").GetInt32(), vector.GetProperty("maximum").GetInt32(),
                vector.GetProperty("intensity").GetInt32(), random, burnRules);
            var expected = vector.GetProperty("expected");
            Check(actual == new HazardResult(expected[0].GetInt32(), expected[1].GetInt32(), 0, true, expected[4].GetUInt64())
                && random.State == actual.RandomState, "native nonlethal burn and decay");
        }
        Check(Hazards.Damage(1, 100, 8) == 1 && Hazards.Damage(1, 100, 8, false) == 0, "configurable lethal hazards");
        var modern = UnitLifecycle.Refresh(new() { Health = 1, MaximumHealth = 200, BaseHealth = 50,
            Behavior = "temporary", RestoreBehavior = "normal", BehaviorExpiry = 2, Round = 3 });
        Check(modern.Health == 1 && modern.Behavior == "normal", "modern refresh avoids zero-HP survivors and missed expiry");
        var rng = new Ew4Random(1946);
        try { Hazards.EndUnit(1, 100, 5, rng, burnRules); throw new InvalidOperationException("Missing hazard rejection"); }
        catch (RuleViolation error) { Check(error.Code == "hazard.stage" && rng.State == 1946, "invalid hazard preserves RNG"); }
        return checks;
    }
}
