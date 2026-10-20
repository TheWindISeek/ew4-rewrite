using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class ImprovementChecks
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
        var fixture = WorldChecks.Example();
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/improvement_vectors.json")));
        var data = vectors.RootElement;
        long[] trainingCosts = data.GetProperty("training_costs").EnumerateArray().Select(v => v.GetInt64()).ToArray();
        foreach (var v in data.GetProperty("training").EnumerateArray())
        {
            int Int(string name) => v.GetProperty(name).GetInt32();
            bool Flag(string name) => v.GetProperty(name).GetBoolean();
            var commander = new CommanderRuntime { Id = "trainer", Owner = "winner", MaximumTrainingLevel = Int("cap") };
            var unit = fixture.Units[1] with
            {
                CommanderId = Flag("commander") ? commander.Id : null, RemainingMovement = Int("movement"), Acted = Flag("acted"),
                Veterancy = Int("level").ToString(), Experience = Int("experience"), Morale = new(Int("morale"), 0, Int("morale")),
                Combatant = fixture.Units[1].Combatant with { Health = Int("health"), Morale = Int("morale"), ConstructionTurns = Int("construction") },
                Support = new() { FoodConsumption = Int("consumption"), Logistics = true }
            };
            var state = fixture with { Units = [unit], Commanders = [commander], Factions = fixture.Factions.Select(f => f.Id != "winner" ? f
                : f with { Resources = new(Int("money"), 999, Int("food")) }).ToArray() };
            var rules = new TrainingRules { Stages = trainingCosts.Select((cost, i) => new TrainingStage(i.ToString(), i == trainingCosts.Length - 1 ? null : cost, Int("healing"))).ToArray() };
            byte[] before = WorldSave.Encode(state);
            var expected = v.GetProperty("expected");
            WorldChange? result = null;
            try { result = WorldImprovements.Train(state, state.Revision, unit.Id, rules); }
            catch (RuleViolation e)
            {
                Check(!expected.GetProperty("success").GetBoolean() && e.Code.StartsWith("training.", StringComparison.Ordinal), "native training rejection");
            }
            Check((result != null) == expected.GetProperty("success").GetBoolean(), "native training success");
            Check(before.SequenceEqual(WorldSave.Encode(state)), "training leaves source unchanged");
            if (result == null) continue;
            var trained = result.State.Units.Single();
            var funds = result.State.Factions.Single(f => f.Id == "winner").Resources;
            Check(trained.Veterancy == expected.GetProperty("level").GetInt32().ToString(), "native trained level");
            Check(trained.Experience == expected.GetProperty("experience").GetInt64(), "native training retains experience");
            Check(trained.Combatant.Health == expected.GetProperty("health").GetInt32(), "native training healing");
            Check(trained.RemainingMovement == expected.GetProperty("movement").GetInt32() && trained.Acted == expected.GetProperty("acted").GetBoolean(), "native training consumes action");
            Check(funds == new Resources(expected.GetProperty("money").GetInt64(), 999, expected.GetProperty("food").GetInt64()), "native training spending even with logistics");
            Check(result.State.Revision == state.Revision + 1 && result.State.RandomState == state.RandomState && result.State.AiRandomState == state.AiRandomState,
                "training commits once without random consumption");
            Check(WorldSave.Encode(result.State).SequenceEqual(WorldSave.Encode(WorldSave.Decode(WorldSave.Encode(result.State), state.ContentRevision))), "training save round trip");
        }
        long[] moneyCosts = data.GetProperty("building_money").EnumerateArray().Select(v => v.GetInt64()).ToArray();
        long[] industryCosts = data.GetProperty("building_industry").EnumerateArray().Select(v => v.GetInt64()).ToArray();
        foreach (var v in data.GetProperty("building").EnumerateArray())
        {
            int Int(string name) => v.GetProperty(name).GetInt32();
            bool Flag(string name) => v.GetProperty(name).GetBoolean();
            string kind = "building." + Int("kind");
            var commander = new CommanderRuntime { Id = "engineer", Owner = "winner" };
            var unit = fixture.Units[1] with { CommanderId = Flag("commander") ? commander.Id : null,
                Combatant = fixture.Units[1].Combatant with { Skills = Flag("discount") ? [Ew4Combat.Skill(30)] : [] } };
            var state = fixture with { Units = [unit], Commanders = [commander],
                Factions = fixture.Factions.Select(f => f.Id != "winner" ? f : f with { Resources = new(Int("money"), Int("industry"), 123) }).ToArray(),
                Cells = fixture.Cells.Select((c, i) => i != unit.Cell ? c : c with { Owner = Flag("owner") ? "winner" : null,
                    BuildingKind = kind, BuildingLevel = Int("level"), BuildingUpgradesThisTurn = Flag("used") ? 1 : 0 }).ToArray() };
            var rules = new BuildingUpgradeRules { Steps = Enumerable.Range(0, Int("cap")).Select(i => new BuildingUpgradeStep(kind, i, i + 1,
                new(moneyCosts[Int("kind")], industryCosts[Int("kind")], 0))).ToArray() };
            byte[] before = WorldSave.Encode(state);
            var expected = v.GetProperty("expected");
            WorldChange? result = null;
            try { result = WorldImprovements.UpgradeBuilding(state, state.Revision, unit.Cell, rules); }
            catch (RuleViolation e)
            {
                Check(!expected.GetProperty("success").GetBoolean() && e.Code.StartsWith("upgrade.", StringComparison.Ordinal), "native upgrade rejection");
            }
            Check((result != null) == expected.GetProperty("success").GetBoolean(), "native building upgrade success");
            Check(before.SequenceEqual(WorldSave.Encode(state)), "upgrade leaves source unchanged");
            if (result == null) continue;
            var site = result.State.Cells[unit.Cell];
            Check(site.BuildingLevel == expected.GetProperty("level").GetInt32() && site.BuildingUpgradesThisTurn == 1, "native building upgrade state");
            Check(result.State.Factions.Single(f => f.Id == "winner").Resources == new Resources(expected.GetProperty("money").GetInt64(), expected.GetProperty("industry").GetInt64(), 123), "native upgrade spending and discount");
            Check(result.State.Revision == state.Revision + 1 && JsonSerializer.Serialize(result.State.Units.Single()) == JsonSerializer.Serialize(unit), "building upgrade independent of unit action");
            Check(WorldSave.Decode(WorldSave.Encode(result.State), state.ContentRevision).Cells[unit.Cell].BuildingUpgradesThisTurn == 1, "save retains upgrade allowance");
        }

        var learner = fixture.Units[1] with { CommanderId = "modern", Veterancy = "stage.11", Experience = 77, Combatant = fixture.Units[1].Combatant with { Health = 50 } };
        var modern = fixture with { Units = [learner], Commanders = [new() { Id = "modern", Owner = "winner", MaximumTrainingLevel = 19 }],
            Factions = fixture.Factions.Select(f => f with { Resources = new(1000, 1000, 1000) }).ToArray(),
            Cells = fixture.Cells.Select((c, i) => c with { Owner = "winner", BuildingKind = "modern.city", BuildingLevel = 12, BuildingUpgradesThisTurn = i == 8 ? 0 : 2 }).ToArray() };
        var training = new TrainingRules { Stages = Enumerable.Range(0, 20).Select(i => new TrainingStage("stage." + i, i < 19 ? 10 : null, 5)).ToArray() };
        Check(WorldImprovements.Train(modern, modern.Revision, learner.Id, training).State.Units.Single().Veterancy == "stage.12", "training exceeds old five-level cap");
        var upgrades = new BuildingUpgradeRules { Steps = [new("modern.city", 12, 13, new(10, 10, 0)), new("modern.city", 13, 14, new(10, 10, 0))], PerTurnLimit = 2 };
        var first = WorldImprovements.UpgradeBuilding(modern, modern.Revision, 8, upgrades).State;
        var second = WorldImprovements.UpgradeBuilding(first, first.Revision, 8, upgrades).State;
        Check(second.Cells[8].BuildingLevel == 14 && second.Cells[8].BuildingUpgradesThisTurn == 2, "configurable building levels and per-turn allowance");
        Reject(() => WorldImprovements.UpgradeBuilding(second, second.Revision, 8, upgrades), "upgrade.used");
        Reject(() => WorldImprovements.Train(modern, modern.Revision + 1, learner.Id, training), "action.stale");
        Reject(() => WorldImprovements.UpgradeBuilding(modern, modern.Revision + 1, 8, upgrades), "action.stale");
        Reject(() => WorldImprovements.UpgradeBuilding(modern, modern.Revision, -1, upgrades), "upgrade.site");
        Reject(() => WorldImprovements.Train(modern, modern.Revision, learner.Id, training with { Stages = [] }), "training.configuration");
        var turnRules = new WorldTurnRules { Buildings = Enumerable.Range(12, 3).Select(i => new BuildingTurnRule("modern.city", i, new(0, 0, 0))).ToArray() };
        var other = WorldTurns.EndTurn(second, second.Revision, turnRules).State;
        Check(other.ActiveFaction == "loser" && other.Cells[8].BuildingUpgradesThisTurn == 2, "other faction turn preserves upgrade counter");
        var returned = WorldTurns.EndTurn(other, other.Revision, turnRules).State;
        Check(returned.ActiveFaction == "winner" && returned.Cells.All(c => c.BuildingUpgradesThisTurn == 0), "owner start resets upgrade counters");
        var huge = modern with { Factions = modern.Factions.Select(f => f with { Resources = new(long.MaxValue, long.MaxValue, 0) }).ToArray(),
            Units = [learner with { Combatant = learner.Combatant with { Skills = [Ew4Combat.Skill(30)] } }] };
        var expensive = new BuildingUpgradeRules { Steps = [new("modern.city", 12, 13, new(long.MaxValue, long.MaxValue, 0))] };
        var spent = WorldImprovements.UpgradeBuilding(huge, huge.Revision, 8, expensive).State.Factions.Single(f => f.Id == "winner").Resources;
        long discounted = long.MaxValue / 100 * 60 + long.MaxValue % 100 * 60 / 100;
        Check(spent.Money == long.MaxValue - discounted && spent.Industry == spent.Money, "large modern resource discount does not overflow");
        return checks;
    }
}
