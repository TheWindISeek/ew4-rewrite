using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class ActionChecks
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
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code + " != " + error.Code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/action_vectors.json")));
        foreach (var vector in data.RootElement.GetProperty("attack").EnumerateArray())
        {
            var point = vector.GetProperty("points");
            var map = new HexMap(8, 8);
            int distance = map.Distance(map.Index(point[0].GetInt32(), point[1].GetInt32()), map.Index(point[2].GetInt32(), point[3].GetInt32()));
            bool Flag(string key) => vector.GetProperty(key).GetBoolean();
            var actual = AttackLegality.Check(Flag("attacker"), Flag("defender"), Flag("construction") ? 1 : 0,
                Flag("acted"), Flag("stunned") ? -3 : 0, distance, vector.GetProperty("minimum").GetInt32(), vector.GetProperty("maximum").GetInt32());
            Check((actual == AttackBlock.None) == Flag("expected"), "native attack range and action flags");
        }
        string[] categories = ["infantry", "cavalry", "artillery", "naval", "fort"];
        foreach (var vector in data.RootElement.GetProperty("recruit").EnumerateArray())
        {
            bool Flag(string key) => vector.GetProperty(key).GetBoolean();
            int Int(string key) => vector.GetProperty(key).GetInt32();
            var example = WorldChecks.Example();
            var actor = example.Units[1];
            var profile = actor.Combatant.Profile with { Category = categories[Int("category")] };
            var grant = new RecruitmentGrant(Int("veteran").ToString(), !Flag("fort") && Int("category") < 3 && Int("transport_level") > Int("category"));
            var before = example with
            {
                Cells = example.Cells.Select((c, i) => i != 0 ? c : c with
                {
                    Owner = Flag("own") ? "winner" : "loser", Terrain = c.Terrain with { Active = Flag("active"), Sea = Flag("sea") },
                    BuildingKind = !Flag("building") ? null : Flag("recruitable") ? "city" : "unsupported"
                }).ToArray(),
                Units = Flag("occupied") ? [actor with { Cell = 0 }] : [],
                Factions = example.Factions.Select(f => f.Id != "winner" ? f : f with
                {
                    Resources = new(Int("money"), Int("industry"), 0),
                    Recruitment = Flag("available") ? new Dictionary<string, RecruitmentGrant> { ["offer"] = grant } : new Dictionary<string, RecruitmentGrant>(),
                    TurnUses = new Dictionary<string, int> { ["forts"] = Flag("fort_used") ? 1 : 0 }
                }).ToArray()
            };
            var offer = new RecruitmentOffer
            {
                Id = "offer", Cost = new(Int("money_cost"), Int("industry_cost"), 0),
                Template = Flag("created") ? actor.Combatant with { Profile = profile } : null,
                Building = Flag("fort") ? BuildingRequirement.Forbidden : BuildingRequirement.Required,
                BuildingKinds = Flag("fort") ? [] : ["city"], AllowSea = !Flag("fort"),
                TurnGroup = Flag("fort") ? "forts" : "ordinary", PerTurnLimit = Flag("fort") ? 1 : null,
                ConstructionTurns = Flag("fort") ? 2 : 0
            };
            if (!Flag("active") && Flag("occupied"))
            {
                Reject(() => WorldActions.CanRecruit(before, 0, offer), "world.unit");
                continue;
            }
            var expected = vector.GetProperty("expected");
            Check(WorldActions.CanRecruit(before, 0, offer) == expected.GetProperty("eligible").GetBoolean(), "native recruitment eligibility");
            byte[] original = WorldSave.Encode(before);
            if (!expected.GetProperty("success").GetBoolean())
            {
                string code = !expected.GetProperty("eligible").GetBoolean() ? "recruit.site"
                    : Int("money") < Int("money_cost") || Int("industry") < Int("industry_cost") ? "recruit.resources" : "recruit.profile";
                Reject(() => WorldActions.Recruit(before, before.Revision, 0, "new.unit", offer), code);
                Check(original.SequenceEqual(WorldSave.Encode(before)), "failed recruit retains all state and RNG");
                continue;
            }
            var after = WorldActions.Recruit(before, before.Revision, 0, "new.unit", offer).State;
            var recruited = after.Units.Single(u => u.Id == "new.unit");
            var faction = after.Factions.Single(f => f.Id == "winner");
            Check(faction.Resources == new Resources(expected.GetProperty("money").GetInt64(), expected.GetProperty("industry").GetInt64(), 0)
                && recruited.RemainingMovement == 0 && recruited.Acted && recruited.Veterancy == Int("veteran").ToString()
                && recruited.Transport == expected.GetProperty("transport").GetBoolean()
                && (faction.TurnUses["forts"] > 0) == expected.GetProperty("fort_used").GetBoolean(), "native recruit spending and unit initialization");
            Check(original.SequenceEqual(WorldSave.Encode(before)), "successful recruit does not mutate previous snapshot");
            var restored = WorldSave.Decode(WorldSave.Encode(after), "fixture.1");
            Check(restored.Units.Single(u => u.Id == "new.unit").Transport == recruited.Transport
                && restored.Factions.Single(f => f.Id == "winner").TurnUses[offer.TurnGroup] == 1, "recruit grants and turn counts persist");
        }
        var baseState = WorldChecks.Example();
        baseState = baseState with
        {
            Units = [], Cells = baseState.Cells.Select((c, i) => i == 0 ? c with { Owner = "winner", BuildingKind = "city" } : c).ToArray(),
            Factions = baseState.Factions.Select(f => f.Id == "winner" ? f with
            {
                Resources = new(100, 10, 0), Recruitment = new Dictionary<string, RecruitmentGrant> { ["draft"] = new() }
            } : f).ToArray()
        };
        var draft = new RecruitmentOffer { Id = "draft", Template = WorldChecks.Example().Units[0].Combatant, Cost = new(2, 0, 0) };
        var first = WorldActions.Recruit(baseState, 0, 0, "one", draft).State;
        Reject(() => WorldActions.Recruit(first, 0, 0, "two", draft), "action.stale");
        Check(!WorldActions.CanRecruit(first, 0, draft), "configured cell capacity stops second recruit");
        var stackable = baseState with { Limits = new() { UnitsPerCell = 2, UnitsPerFaction = 2 } };
        var second = WorldActions.Recruit(WorldActions.Recruit(stackable, 0, 0, "one", draft).State, 1, 0, "two", draft).State;
        Check(second.Units.Count == 2 && second.Factions.Single(f => f.Id == "winner").Resources.Money == 96, "scenario-configured recruitment stacking");
        Check(!WorldActions.CanRecruit(second, 0, draft), "configured faction and tile cap enforced");
        var limited = draft with { PerTurnLimit = 1, TurnGroup = "shared.production" };
        var once = WorldActions.Recruit(stackable, 0, 0, "one", limited).State;
        Check(!WorldActions.CanRecruit(once, 0, limited), "production group turn cap enforced");
        var copy = first.Copy();
        ((Dictionary<string, RecruitmentGrant>)copy.Factions.Single(f => f.Id == "winner").Recruitment).Clear();
        Check(first.Factions.Single(f => f.Id == "winner").Recruitment.Count == 1, "recruit grant snapshots detached");
        return checks;
    }
}
