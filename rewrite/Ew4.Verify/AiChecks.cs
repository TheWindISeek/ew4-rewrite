using System.Text.Json;
using Ew4.Core;

internal static class AiChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_vectors.json")));
        foreach (var vector in data.RootElement.GetProperty("hold").EnumerateArray())
        {
            int Int(string name) => vector.GetProperty(name).GetInt32();
            bool Flag(string name) => vector.GetProperty(name).GetBoolean();
            var facts = new AiHoldFacts(Int("strength"), Int("threat"), Int("pressure"), Int("environment"),
                Flag("bonus_eligible"), Int("bonus") != 255, Flag("naval"), Int("health"), Int("maximum"));
            Check(AiPlanning.HoldScore(facts) == Int("expected"), "native hold score");
        }
        foreach (var vector in data.RootElement.GetProperty("eligibility").EnumerateArray())
        {
            int Int(string name) => vector.GetProperty(name).GetInt32();
            var actual = AiPlanning.Requests(Ew4Ai.Behavior(Int("behavior")), new(Int("movement"), Int("morale"), Int("construction"), vector.GetProperty("acted").GetBoolean()));
            var expected = vector.GetProperty("expected").EnumerateArray().Select(e => e.GetInt32() switch
            {
                0x1000 => AiCandidateKind.Hold, 0x2000 => AiCandidateKind.TacticalMove,
                0x3000 => AiCandidateKind.Strike, 0x4000 => AiCandidateKind.StrategicAdvance,
                _ => throw new InvalidOperationException("Unexpected native candidate kind")
            });
            Check(actual.Eligible == vector.GetProperty("eligible").GetBoolean() && actual.Kinds.SequenceEqual(expected), "native candidate eligibility");
        }
        foreach (var vector in data.RootElement.GetProperty("selection").EnumerateArray())
        {
            var candidates = vector.GetProperty("scores").EnumerateArray().Select((v, i) => new AiCandidate<string>("action." + i, v.GetInt64())).ToArray();
            Check(AiPlanning.Best(candidates, -1) == vector.GetProperty("expected").GetInt32(), "native first best candidate");
        }
        Check(AiPlanning.Best<string>([]) == -1, "empty candidates");
        Check(AiPlanning.Best<string>([new("a", -3), new("b", -2)]) == 1, "modern selection supports negative scores");
        Check(AiPlanning.Best<string>([new("a", 4), new("b", 4)]) == 0, "stable tie ordering");
        Check(AiPlanning.Requests(new(false, false, false), new(6, 0, 0, false)).Kinds.SequenceEqual([AiCandidateKind.Strike]), "policy independent of legacy integer modes");
        return checks;
    }
}
