using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class SplashChecks
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
        string[] categories = ["infantry", "cavalry", "artillery", "naval", "fort"];
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/splash_vectors.json")));
        float[] multipliers = data.RootElement.GetProperty("multipliers").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        Combatant Decode(JsonElement v)
        {
            int Int(string name) => v.GetProperty(name).GetInt32();
            var traits = new List<string>();
            if (Int("unit_id") == 12) traits.Add("siege_gun");
            if (Int("unit_id") == 13) traits.AddRange(["no_retaliation", "line_splash"]);
            if (Int("unit_id") == 21) traits.Add("coastal_gun");
            return new()
            {
                Health = Int("health"), Morale = Int("morale"), Armor = Int("armor"), AttackBonus = Int("attack_bonus"), DefenseBonus = Int("defense_bonus"),
                AtSea = v.GetProperty("sea").GetBoolean(), BuildingKind = Int("building") < 0 ? null : Int("building") == 4 ? "farmland" : "city",
                Evasion = v.GetProperty("evasion").GetSingle(), Skills = v.GetProperty("skills").EnumerateArray().Select(s => Ew4Combat.Skill(s.GetInt32())).ToArray(),
                ConstructionTurns = Int("construction_turns"),
                Profile = new()
                {
                    Id = "legacy." + Int("unit_id"), Category = categories[Int("category")], Grade = Int("grade").ToString(), MaxHealth = Int("maximum"),
                    MinimumAttack = Int("minimum_attack"), MaximumAttack = Int("maximum_attack"), MinimumRange = Int("minimum_range"), MaximumRange = Int("maximum_range"),
                    AttackBands = new[] { 5, 15, 30, 50, 65, 80 }.Take(Int("grade") + 4).Select((threshold, i) => new AttackBand(threshold, i + 2)).ToArray(),
                    DamageMultipliers = categories.Select((category, i) => (category, value: multipliers[Int("weapon")*5+i])).ToDictionary(p => p.category, p => p.value), Traits = traits
                }
            };
        }
        foreach (var v in data.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var a = Decode(v.GetProperty("attacker"));
            var d = Decode(v.GetProperty("defender"));
            var s = Decode(v.GetProperty("splash"));
            int distance = v.GetProperty("distance").GetInt32();
            bool occupied = v.GetProperty("present").GetBoolean() && v.GetProperty("occupied").GetBoolean();
            var random = new Ew4Random(v.GetProperty("seed").GetUInt64());
            var actual = SplashCombat.Calculate(new(a, d, distance), occupied ? new(a, s, distance) : null, random,
                new() { LegacySiegeRetaliationBuildingType = true }, new());
            var expected = v.GetProperty("expected");
            Check(actual.Main.AttackerDamage == expected.GetProperty("attacker_damage").GetInt32(), "native splash wrapper primary retaliation damage");
            Check(actual.Main.DefenderDamage == expected.GetProperty("defender_damage").GetInt32(), "native splash wrapper primary damage after secondary rolls");
            Check(actual.Main.Counterattack == expected.GetProperty("counterattack").GetBoolean(), "native splash wrapper primary counterattack");
            Check(actual.SplashDamage == expected.GetProperty("splash_damage").GetInt32(), "native splash independent damage and sixty-percent truncation");
            Check(actual.Main.RandomState == expected.GetProperty("rng_state").GetUInt64() && random.State == actual.Main.RandomState, "native secondary/main random sequence");
            Check((a.Profile.HasTrait("line_splash") && occupied) == (expected.GetProperty("splash_target").GetInt32() == 3), "native splash availability");
        }
        var fixture = WorldChecks.Example();
        var launcher = fixture.Units[1] with { Cell = 3, Combatant = fixture.Units[1].Combatant with { Profile = fixture.Units[1].Combatant.Profile with
            { MinimumAttack = 2, MaximumAttack = 2, MaximumRange = 3, Traits = ["no_retaliation", "line_splash"] } } };
        var target = fixture.Units[0] with { Cell = 4 };
        var splash = target with { Id = "splash", Cell = 5 };
        var state = fixture with { Units = [launcher, target, splash], Cells = fixture.Cells.Select((c, i) => c with
            { DefenseTerrain = i == 5 ? "protected" : "open" }).ToArray() };
        var context = new CombatContextRules { UseResolvedFallback = false, TerrainDefense = new Dictionary<string, IReadOnlyDictionary<string, int>>
            { ["open"] = new Dictionary<string, int> { ["infantry"] = 0 }, ["protected"] = new Dictionary<string, int> { ["infantry"] = 50 } } };
        var rules = new WorldBattleRules { Context = context };
        byte[] before = WorldSave.Encode(state);
        var preview = WorldBattle.Preview(state, state.Revision, launcher.Id, target.Id, rules);
        Check(preview.SplashTarget == splash.Id && preview.SplashDamage == 3 && preview.Main.DefenderDamage == 10, "public preview selects behind target and resolves independent terrain defense");
        Check(before.SequenceEqual(WorldSave.Encode(state)) && preview == WorldBattle.Preview(state, state.Revision, launcher.Id, target.Id, rules), "repeated battle preview is pure");
        var committed = WorldBattle.Attack(state, state.Revision, launcher.Id, target.Id, rules).State;
        Check(committed.Units.Single(u => u.Id == target.Id).Combatant.Health == 90 && committed.Units.Single(u => u.Id == splash.Id).Combatant.Health == 97,
            "public attack commits both damages");
        Check(committed.Revision == state.Revision + 1 && committed.RandomState == preview.Main.RandomState, "splash attack commits once using preview random sequence");
        Check(WorldSave.Encode(committed).SequenceEqual(WorldSave.Encode(WorldSave.Decode(WorldSave.Encode(committed), state.ContentRevision))), "splash battle save round trip");
        var fragile = state with { Units = state.Units.Select(u => u.Id != splash.Id ? u : u with { Combatant = u.Combatant with { Health = 1 } }).ToArray() };
        var killed = WorldBattle.Attack(fragile, fragile.Revision, launcher.Id, target.Id, rules).State;
        Check(killed.Units.All(u => u.Id != splash.Id) && killed.Factions.Single(f => f.Id == launcher.Owner).Kills[splash.Combatant.Profile.Id] == 1, "automatic splash death uses existing kill settlement");
        var friendly = state with { Units = state.Units.Select(u => u.Id == splash.Id ? u with { Owner = launcher.Owner } : u).ToArray() };
        Check(WorldBattle.Preview(friendly, friendly.Revision, launcher.Id, target.Id, rules).SplashTarget == splash.Id, "native-compatible splash can hit friendly unit");
        Check(WorldBattle.Preview(friendly, friendly.Revision, launcher.Id, target.Id, rules with { Splash = new() { AllowFriendlyFire = false } }).SplashTarget == null,
            "friendly splash policy can be disabled");
        var left = state with { Units = [launcher with { Cell = 5 }, target, splash with { Cell = 3 }] };
        Check(WorldBattle.Preview(left, left.Revision, launcher.Id, target.Id, rules with { Context = new() }).SplashTarget == splash.Id, "left-facing automatic splash");
        var edge = state with { Units = [launcher with { Cell = 4 }, target with { Cell = 5 }] };
        Check(WorldBattle.Preview(edge, edge.Revision, launcher.Id, target.Id, rules with { Context = new() }).SplashTarget == null, "map-edge splash does not wrap row");
        var hex = new HexMap(3, 3);
        Check(!SplashCombat.FacesRight(hex, 1, 7) && SplashCombat.FacesRight(hex, 7, 1), "equal-X orientation uses row tie-break");
        Check(SplashCombat.FacesRight(hex, 1, 4) && !SplashCombat.FacesRight(hex, 4, 1), "odd-row horizontal offset participates in facing");
        var randomFailure = new Ew4Random(1946);
        var randomAttacker = launcher.Combatant with { Profile = launcher.Combatant.Profile with { MaximumAttack = 9 } };
        var invalid = target.Combatant with { Profile = target.Combatant.Profile with { DamageMultipliers = new Dictionary<string, float>() } };
        Reject(() => SplashCombat.Calculate(new(invalid, target.Combatant, 1), new(randomAttacker, target.Combatant, 1), randomFailure, new(), new() { RequiredTrait = "anything" }), "combat.multiplier_missing");
        Check(randomFailure.State == 1946, "failed primary calculation cannot commit secondary random draws");
        var invalidPrimary = randomAttacker with { Profile = randomAttacker.Profile with { DamageMultipliers = new Dictionary<string, float>() } };
        Reject(() => SplashCombat.Calculate(new(invalidPrimary, target.Combatant, 1), new(randomAttacker, target.Combatant, 1), randomFailure, new(), new()), "combat.multiplier_missing");
        Check(randomFailure.State == 1946, "secondary succeeds then primary fails with complete RNG rollback");
        return checks;
    }
}
