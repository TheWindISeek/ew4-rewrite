using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;
using Ew4.Legacy;

internal static class AiTacticalChecks
{
    public static int Run(string root, AiTacticalRules? mappedRules = null)
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
        string[] categories = ["infantry", "cavalry", "artillery", "naval", "fort"];
        var rules = mappedRules ?? LegacyAiTactics.Rules();
        string? Building(int id) => id == 255 ? null : mappedRules == null ? id.ToString() : LegacyBuildingCatalog.Kinds[id];
        string? Installation(int id) => id == 255 ? null : mappedRules == null ? id.ToString() : LegacyRecruitmentCatalog.InstallationKinds[id];
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_attack_vectors.json")));
        foreach (var v in data.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            int Int(string name) => v.GetProperty(name).GetInt32();
            bool Flag(string name) => v.GetProperty(name).GetBoolean();
            var attacker = new AiUnitFacts
            {
                Category = categories[Int("category")], Grade = Int("grade").ToString(), Health = Int("health"), MaximumHealth = Int("maximum"),
                AtSea = Flag("source_sea"), HasCommander = Flag("commander"), ExperienceLevel = Int("veteran"), Traits = LegacyAiTactics.Traits(Int("unit_id"))
            };
            var defender = new AiUnitFacts
            {
                Category = categories[Int("target_category")], Grade = Int("target_grade").ToString(), Health = Int("target_health"), MaximumHealth = Int("target_maximum"),
                AtSea = Flag("target_sea"), HasCommander = Flag("target_commander"), ExperienceLevel = Int("target_veteran"), Traits = LegacyAiTactics.Traits(Int("target_id")),
                Transport = Flag("transport"), UnderConstruction = Int("construction") > 0, PriorityCommander = Int("behavior") == 1
            };
            var source = new AiSiteFacts(InstallationKind: Installation(Int("source_installation")));
            var target = new AiSiteFacts(Building(Int("building")), Int("building") == 255 ? 0 : Int("level"),
                Installation(Int("target_installation")), Int("priority"), Flag("target_has_owner") && Flag("human"));
            bool allied = Int("source_camp") == (Flag("target_has_owner") ? Int("target_camp") : 5) && Int("source_camp") != 5;
            Check(AiTactics.Location(target, defender, rules) == Int("location"), "native AI location value");
            Check(AiTactics.Strength(defender, rules) == Int("strength"), "native AI grade/sea strength");
            long attack = AiTactics.AttackScore(attacker, defender, source, target, allied, rules);
            Check(attack == Int("attack_score"), "native AI attack score");
            Check(AiTactics.StrikeScore(attack, attacker, rules) == Int("strike_score"), "native AI weighted strike candidate");
        }
        var fixture = WorldChecks.Example();
        var attackerUnit = fixture.Units[1] with { Cell = 4, Combatant = fixture.Units[1].Combatant with { Profile = fixture.Units[1].Combatant.Profile with { Grade = "0" } } };
        var ordinary = fixture.Units[0] with { Cell = 5, Combatant = fixture.Units[0].Combatant with { Profile = fixture.Units[0].Combatant.Profile with { Grade = "0" } } };
        var constructing = ordinary with { Id = "construction", Cell = 3, Combatant = ordinary.Combatant with { ConstructionTurns = 1 } };
        var friendly = attackerUnit with { Id = "ally", Cell = 1 };
        var scene = fixture with { Units = [attackerUnit, ordinary, constructing, friendly] };
        var ai = new WorldAiRules { Tactics = rules, ExperienceLevels = new Dictionary<string, int> { ["0"] = 0 } };
        byte[] before = WorldSave.Encode(scene);
        var candidates = WorldAi.StrikeCandidates(scene, attackerUnit.Id, ai);
        Check(candidates.Count == 2 && candidates.All(c => c.Action.TargetId != friendly.Id), "world AI excludes allies");
        Check(candidates[0].Action.TargetId == ordinary.Id && candidates[1].Action.TargetId == constructing.Id, "world AI generation order is stable");
        int selected = AiPlanning.Best(candidates);
        Check(candidates[selected].Action.TargetId == constructing.Id && candidates[selected].Score > candidates[0].Score, "world AI chooses higher native tactical value");
        Check(before.SequenceEqual(WorldSave.Encode(scene)), "AI planning changes neither world nor random states");
        var dispatched = WorldAi.ExecuteStrike(scene, candidates[selected].Action, new());
        Check(dispatched.State.Revision == scene.Revision + 1 && dispatched.State.Units.Single(u => u.Id == attackerUnit.Id).Acted, "AI strike dispatches real world attack");
        Reject(() => WorldAi.ExecuteStrike(dispatched.State, candidates[selected].Action, new()), "action.stale");
        var restored = WorldSave.Decode(before, scene.ContentRevision);
        Check(WorldAi.StrikeCandidates(restored, attackerUnit.Id, ai).SequenceEqual(candidates), "AI candidates survive save/load deterministically");
        var exhausted = scene with { Units = scene.Units.Select(u => u.Id != attackerUnit.Id ? u : u with { Acted = true }).ToArray() };
        Check(WorldAi.StrikeCandidates(exhausted, attackerUnit.Id, ai).Count == 0, "acted unit generates no strikes");
        var noMovement = scene with { Units = scene.Units.Select(u => u.Id != attackerUnit.Id ? u : u with { RemainingMovement = 0 }).ToArray() };
        Check(WorldAi.StrikeCandidates(noMovement, attackerUnit.Id, ai).Count == 2, "stationary unused attack is still legal");
        Check(WorldAi.StrikeCandidates(scene, attackerUnit.Id, ai with { NeutralCamps = ["a"] }).Count == 0, "configured neutral camps excluded");
        Reject(() => WorldAi.StrikeCandidates(scene, attackerUnit.Id, ai with { ExperienceLevels = new Dictionary<string, int>() }), "ai.stage");
        Reject(() => AiTactics.Location(new("unknown"), null, rules), "ai.building");
        Reject(() => AiTactics.Strength(new() { Category = "infantry", Health = 100, MaximumHealth = 100, Grade = "unknown" }, rules), "ai.grade");
        var customRules = new AiTacticalRules
        {
            Grades = new Dictionary<string, float> { ["custom.grade"] = 2 }, NavalCategory = "custom.fleet",
            Buildings = new Dictionary<string, AiBuildingValue> { ["custom.city"] = new(10, 5) },
            Installations = new Dictionary<string, long> { ["custom.defense"] = 15 },
            Modifiers = [new() { AttackerCategories = ["custom.unit"], DefenderTrait = "custom.target", Bonus = 321 }]
        };
        var customAttacker = new AiUnitFacts { Category = "custom.unit", Grade = "custom.grade", Health = 200, MaximumHealth = 200, ExperienceLevel = 12 };
        var customDefender = customAttacker with { Category = "custom.fleet", AtSea = true, Traits = ["custom.target"] };
        Check(AiTactics.Strength(customDefender, customRules) == 200, "dynamic category/grade strength");
        var customSite = new AiSiteFacts("custom.city", 20, "custom.defense");
        Check(AiTactics.Location(customSite, customDefender, customRules) == 125, "dynamic building/installation value beyond old level limits");
        Check(AiTactics.AttackScore(customAttacker, customDefender, new(), customSite, false, customRules) == 546, "dynamic AI target modifier");
        var equalHalf = customAttacker with { Health = 100 };
        var noModifiers = customRules with { Modifiers = [] };
        Check(AiTactics.AttackScore(equalHalf, customDefender, new(), customSite, false, noModifiers) == 125, "native exactly-half-health boundary gives no health bonus");
        return checks;
    }
}
