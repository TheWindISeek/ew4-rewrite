using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class CombatContextChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        var fixture = WorldChecks.Example();
        string[] categories = ["infantry", "cavalry", "artillery", "naval", "fort"];
        IReadOnlyDictionary<string, int> Row(JsonElement data) => categories.Select((name, i) => (name, value: data[Math.Min(i, 2)].GetInt32())).ToDictionary(p => p.name, p => p.value);
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/defense_context_vectors.json")));
        foreach (var vector in data.RootElement.EnumerateArray())
        {
            bool Flag(string name) => vector.GetProperty(name).GetBoolean();
            int terrain = vector.GetProperty("overlay").GetInt32() switch { 0 => vector.GetProperty("terrain").GetInt32(), 1 => 2, _ => 3 };
            var rules = new CombatContextRules
            {
                TerrainDefense = Enumerable.Range(0, 7).ToDictionary(i => i.ToString(), i => Row(vector.GetProperty("penalties")[i])),
                InstallationDefense = new Dictionary<string, IReadOnlyDictionary<string, int>> { ["installation"] = Row(vector.GetProperty("installation_penalties")) },
                Buildings = [new("city", 1, Flag("building_row") ? vector.GetProperty("building_penalty").GetInt32() : 0)], UseResolvedFallback = false
            };
            var unit = fixture.Units[0] with { Cell = 4 };
            var cells = fixture.Cells.Select((c, i) => i != 4 ? c : c with { DefenseTerrain = terrain.ToString(),
                BuildingKind = Flag("built") ? "city" : null, BuildingLevel = Flag("built") ? 1 : 0, InstallationKind = Flag("installed") ? "installation" : null }).ToArray();
            var state = fixture with { Units = [unit], Cells = cells, Factions = fixture.Factions.Select(f => f.Id != unit.Owner ? f : f with
                { Human = !Flag("ai"), AiEvasionBonus = vector.GetProperty("bonus").GetSingle() }).ToArray() };
            var context = CombatContext.Resolve(state, unit, categories[vector.GetProperty("category").GetInt32()], rules, !Flag("skip_ai"));
            float result = Flag("ignore_building") ? context.EvasionIgnoringBuilding : context.Evasion;
            Check(result == vector.GetProperty("expected").GetSingle(), "native terrain/installation/building/AI evasion");
        }
        using var adjacency = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/turn_settlement_vectors.json")));
        var map = new HexMap(3, 3);
        foreach (var vector in adjacency.RootElement.GetProperty("adjacency").EnumerateArray())
        {
            var center = fixture.Units[0] with { Cell = 4, Owner = "camp." + vector.GetProperty("camp").GetInt32() };
            var units = new List<UnitState> { center };
            var cells = Enumerable.Range(0, 9).Select(_ => new CellState()).ToArray();
            var facts = vector.GetProperty("neighbors").EnumerateArray().ToArray();
            for (int direction = 0; direction < 6; direction++)
            {
                var fact = facts[direction];
                int cell = map.Neighbor(4, direction);
                bool present = fact.GetProperty("present").GetBoolean();
                cells[cell] = new() { Terrain = new() { Active = present && fact.GetProperty("active").GetBoolean() } };
                if (present && fact.GetProperty("unit").GetBoolean()) units.Add(center with
                {
                    Id = "neighbor." + direction, Cell = cell, Owner = "camp." + fact.GetProperty("owner").GetInt32(),
                    CommanderId = fact.GetProperty("commander").GetBoolean() ? "general." + direction : null,
                    Support = new() { AdjacentAttack = fact.GetProperty("attack").GetInt32(), AdjacentDefense = fact.GetProperty("defense").GetInt32() }
                });
            }
            var state = fixture with { Units = units, Cells = cells, Factions = Enumerable.Range(1, 4).Select(i => new FactionState { Id = "camp." + i, Camp = i.ToString() }).ToArray() };
            var actual = CombatContext.Auras(state, map, center);
            Check(actual == new CombatAura(vector.GetProperty("attack").GetInt32(), vector.GetProperty("defense").GetInt32()), "native allied adjacent attack/defense aura");
        }
        var basic = fixture.Units[1] with { Cell = 4, RemainingMovement = 6 };
        var target = fixture.Units[0] with { Cell = 5, Combatant = fixture.Units[0].Combatant with
            { Profile = fixture.Units[0].Combatant.Profile with { MinimumAttack = 0, MaximumAttack = 0 } } };
        var general = new CommanderRuntime { Id = "aura", Owner = "winner" };
        var provider = basic with { Id = "provider", Cell = 3, CommanderId = general.Id, Support = new() { AdjacentAttack = 8, AdjacentDefense = 4 } };
        var scene = fixture with { Units = [basic, target, provider], Commanders = [general], Cells = fixture.Cells.Select((c, i) => c with
            { Owner = i == 5 ? "loser" : "winner", BuildingKind = i == 5 ? "city" : null, BuildingLevel = i == 5 ? 1 : 0 }).ToArray(),
            Factions = fixture.Factions.Select(f => f.Id != "loser" ? f : f with { AiEvasionBonus = .2f }).ToArray() };
        var contextRules = new CombatContextRules { Buildings = [new("city", 1, 40)], VeterancyArmor = new Dictionary<string, int> { ["0"] = 3 } };
        var ready = CombatContext.Resolve(scene, basic, "infantry", contextRules);
        var defended = CombatContext.Resolve(scene, target, "infantry", contextRules);
        Check(ready.AttackBonus == basic.Combatant.AttackBonus + 8 && ready.DefenseBonus == basic.Combatant.DefenseBonus + 4 && ready.Armor == 3,
            "resolved combat context adds aura once and selects configured armor");
        Check(defended.Evasion == .6f && defended.EvasionIgnoringBuilding == .2f, "building and AI evasion stay distinct");
        var deterministic = basic.Combatant with { Profile = basic.Combatant.Profile with { MinimumAttack = 4, MaximumAttack = 4 }, Skills = [Ew4Combat.Skill(29)] };
        var baseTarget = defended with { Armor = 0, DefenseBonus = 0 };
        var siege = Ew4Combat.Calculate(deterministic, baseTarget, 1, new(1946));
        Check(siege.DefenderDamage == 16, "siege ignores forty-percent building evasion but keeps twenty-percent AI evasion");
        var ignore = Ew4Combat.Calculate(deterministic with { Skills = [Ew4Combat.Skill(14)] }, baseTarget, 1, new(1946));
        Check(ignore.DefenderDamage == 20, "unit terrain-ignore skill bypasses complete evasion context");
        var turn = WorldBattle.Attack(scene, scene.Revision, basic.Id, target.Id, new() { Context = contextRules });
        Check(turn.Notices.Any(n => n.Kind == "battle.damage") && scene.Units.Single(u => u.Id == basic.Id).Combatant.AttackBonus == 0,
            "public attack uses transient map context without accumulating bonuses");
        Check(WorldSave.Encode(scene).SequenceEqual(WorldSave.Encode(WorldSave.Decode(WorldSave.Encode(scene), "fixture.1"))), "map context source fields survive save/load");
        return checks;
    }
}
