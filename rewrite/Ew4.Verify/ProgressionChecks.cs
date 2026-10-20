using System.Text.Json;
using Ew4.Core;

internal static class ProgressionChecks
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
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/progression_vectors.json")));
        ProgressionRules Track(string name)
        {
            var costs = data.RootElement.GetProperty(name).EnumerateArray().Select(x => x.GetInt64()).ToArray();
            return new() { Levels = costs.Select((cost, i) => new ProgressionLevel("level." + i, cost))
                .Append(new ProgressionLevel("cap", null)).ToArray() };
        }
        var rank = Track("rank_costs");
        var nobility = Track("nobility_costs");
        foreach (var vector in data.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            bool isNobility = vector.GetProperty("kind").GetString() == "nobility";
            var rules = isNobility ? nobility : rank;
            var before = new ProgressionState(vector.GetProperty("level").GetInt32(), vector.GetProperty("experience").GetInt64());
            var item = vector.GetProperty("equipment_percent");
            long gain = Ew4Experience.CommanderGain(vector.GetProperty("gain").GetInt64(), isNobility,
                vector.GetProperty("skills").EnumerateArray().Select(v => Ew4Combat.Skill(v.GetInt32())).ToArray(),
                item.ValueKind == JsonValueKind.Null ? null : item.GetInt32(), vector.GetProperty("training").GetBoolean());
            var actual = Progression.Award(before, gain, rules);
            var expected = vector.GetProperty("expected");
            Check(actual.State == new ProgressionState(expected[0].GetInt32(), expected[1].GetInt64())
                && (actual.LevelsGained > 0) == (expected[2].GetInt32() != 0), "native commander XP");
        }
        foreach (var vector in data.RootElement.GetProperty("fire").EnumerateArray())
        {
            bool Flag(string name) => vector.GetProperty(name).GetBoolean();
            int Int(string name) => vector.GetProperty(name).GetInt32();
            int weapon = Int("weapon"), terrain = Int("terrain");
            int[] probabilities = Flag("building") ? [6, 4, 2] : terrain == 0 ? [6, 3, 1] : terrain == 1 ? [6, 5, 4] : [6, 6, 6];
            if (weapon == 4) probabilities = probabilities.Select(p => p * 5 / 2).ToArray();
            var random = new Ew4Random(vector.GetProperty("seed").GetUInt64());
            var actual = Ignition.Resolve(new() { Sea = Flag("sea"), Occupied = Flag("occupied"), Naval = Flag("naval"),
                Transport = Flag("transport"), Immune = Flag("immune") || Flag("equipment"), Environment = Int("effect"), Burning = Int("burning") },
                weapon == 3 || weapon == 4, probabilities, random);
            var expected = vector.GetProperty("expected");
            Check(actual == new IgnitionResult(expected[0].GetInt32() != 0, expected[1].GetInt32(), expected[2].GetInt32(), expected[3].GetUInt64())
                && random.State == actual.RandomState, "native ignition and RNG");
        }
        var many = new ProgressionRules { Levels = Enumerable.Range(0, 1000).Select(i => new ProgressionLevel("stage." + i, i == 999 ? null : 1)).ToArray() };
        Check(Ew4Experience.CommanderGain(16777217, false, []) == 16777217, "unmodified XP retains integer precision");
        Check(Progression.Award(new(0, 0), 999, many).State == new ProgressionState(999, 0), "no fixed rank capacity");
        var single = many with { SingleAdvancePerAward = true };
        Check(Progression.Award(new(0, 0), 5, single).State == new ProgressionState(1, 4), "unit single-stage advancement policy");
        var uncappedXp = many with { DiscardExperienceOnReachingCap = false, AccumulateAtCap = true };
        Check(Progression.Award(new(998, 0), 50, uncappedXp).State == new ProgressionState(999, 49), "explicit cap XP policy");
        Reject(() => Progression.Award(new(0, 0), 1, many with { Levels = [new("bad", 0), new("cap", null)] }), "progression.configuration");
        Reject(() => Progression.Award(new(-1, 0), 1, many), "progression.state");
        var rng = new Ew4Random(123);
        Reject(() => Ignition.Resolve(new(), true, [50, 51], rng), "fire.configuration");
        Check(rng.State == 123, "invalid ignition does not advance RNG");
        var deepFire = Ignition.Resolve(new(), true, [0, 0, 0, 0, 0, 100], rng);
        Check(deepFire.Environment == 6, "configurable fire intensity count");
        return checks;
    }
}
