using System.Text.Json;
using System.Xml.Linq;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

internal static class LegacyBuildingChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException("Legacy buildings: " + label); }
        void Reject(Action action, string code)
        {
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code + ": " + error.Code); return; }
            throw new InvalidOperationException("Expected legacy building rejection: " + code);
        }
        string assets = Path.Combine(root, "apk_work/original/assets");
        string source = File.ReadAllText(Path.Combine(assets, "def_construction.xml"));
        string capitals = File.ReadAllText(Path.Combine(assets, "def_capital.xml"));
        var catalog = LegacyBuildingCatalog.Read(source, capitals);
        Check(catalog.Definitions.Count == 5 && catalog.Capitals.Count == 12 && LegacyBuildingCatalog.Kinds[3] == "port"
            && LegacyBuildingCatalog.Kinds[4] == "farmland", "native XML loader type mapping, not guessed labels");
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/building_catalog_vectors.json")));
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            int kind = vector.GetProperty("kind").GetInt32(), level = vector.GetProperty("level").GetInt32();
            var definition = catalog.Definitions[kind];
            Check(definition.Kind == vector.GetProperty("name").GetString(), "native kind name");
            if (vector.TryGetProperty("capital", out var special))
            {
                int id = special.ValueKind == JsonValueKind.Null ? -1 : 123;
                var synthetic = LegacyBuildingCatalog.Read(source, special.ValueKind == JsonValueKind.Null ? "<capitals/>" : "<capitals><capital id='123' name='test' tax='123'/></capitals>");
                Check(synthetic.Income(kind, level, id).Money == vector.GetProperty("expected_tax").GetInt64(), "native capital priority only overrides city tax");
                continue;
            }
            var row = catalog.Level(kind, level);
            var income = catalog.Income(kind, level);
            int[] actual = [(int)income.Money, (int)income.Industry, (int)income.Food, row?.Turn.Healing ?? 0, row?.DefensePercent ?? 0];
            Check(actual.SequenceEqual(vector.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32())), "native one-based row accessors");
            var allowed = vector.GetProperty("recruitment").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetInt32());
            for (int unit = 0; unit < 22; unit++)
            {
                int grade = row != null && row.Recruitment.TryGetValue(unit, out int maximum) ? maximum : -1;
                Check(grade == allowed.GetValueOrDefault(unit, -1), "native building maximum recruit grade");
                for (int g = 0; g < 3; g++) Check(catalog.CanRecruit(kind, level, unit, g) == (allowed.TryGetValue(unit, out int max) && g <= max), "native recruit eligibility");
            }
        }
        foreach (var definition in catalog.Definitions.Values)
            foreach (int requested in new[] { 0, 1, definition.Levels.Count, definition.Levels.Count + 1, 255 })
                Check(catalog.NormalizeLevel(definition.LegacyId, requested) == Math.Min(requested, definition.Levels.Count), "native constructor level clamp");
        Check(catalog.Definitions[0].MaximumUpgradeLevel == 6 && catalog.Definitions[0].Levels.Count == 7
            && catalog.UpgradeRules().Steps.All(s => s.Kind != "city" || s.ToLevel <= 6), "placed seventh city row is distinct from upgrade ceiling");
        Reject(() => catalog.NormalizeLevel(99, 1), "legacy.building_kind");
        Reject(() => catalog.NormalizeLevel(0, -1), "legacy.building_kind");
        Reject(() => LegacyRuntimeCatalog.UnitIndex("invented soldier"), "legacy.unit_definition");
        Reject(() => LegacyBuildingCatalog.Read("<constructions><construction type='unknown'/></constructions>", capitals), "legacy.building_definitions");
        Reject(() => LegacyBuildingCatalog.Read(source.Replace("maxlevel=\"6\"", "maxlevel=\"100\""), capitals), "legacy.building_definitions");
        Check(catalog.SourceRevision == LegacyBuildingCatalog.Read(source, capitals).SourceRevision
            && catalog.SourceRevision != LegacyBuildingCatalog.Read(source, capitals.Replace("tax=\"70\"", "tax=\"71\"")).SourceRevision, "building/capital sources participate in deterministic rule digest");

        var sampleBattle = LegacyBattle.Read(File.ReadAllBytes(Path.Combine(assets, "campaign1_01.btl")));
        var rules = catalog.TurnRules(sampleBattle);
        rules.Validate(); catalog.UpgradeRules().Validate();
        new CombatContextRules { Buildings = catalog.BuildingDefenseRules() }.Validate();
        Check(rules.Buildings.Count == 25 && catalog.BuildingDefenseRules().Count == 25, "zero level plus twenty native rows imported");
        var fixture = WorldChecks.Example();
        var actor = fixture.Factions.Single(f => f.Id == "winner") with { Resources = new(1000, 1000, 1000), IncomeMultiplier = 1.25f };
        var world = fixture with { Factions = fixture.Factions.Select(f => f.Id == actor.Id ? actor : f).ToArray(),
            Cells = fixture.Cells.Select((c, i) => c with { Owner = actor.Id, BuildingKind = i == 8 ? "city" : null, BuildingLevel = i == 8 ? 3 : 0 }).ToArray() };
        var cityRules = rules with { SiteIncomeOverrides = new Dictionary<int, BuildingIncomeOverride> { [8] = new("city", Money: 70) } };
        Check(WorldTurns.FactionIncome(world, actor, cityRules) == new Resources(87, 0, 0), "capital tax overrides money with original float truncation");
        var upgraded = WorldImprovements.UpgradeBuilding(world, world.Revision, 8, catalog.UpgradeRules()).State;
        Check(WorldTurns.FactionIncome(upgraded, upgraded.Factions.Single(f => f.Id == actor.Id), cityRules) == new Resources(87, 2, 0),
            "capital retains tax while upgraded city changes industry income");
        var restored = WorldSave.Decode(WorldSave.Encode(upgraded), upgraded.ContentRevision);
        Check(WorldTurns.FactionIncome(restored, restored.Factions.Single(f => f.Id == actor.Id), cityRules) == new Resources(87, 2, 0), "external rules resolve capital income after state save");
        var replaced = restored with { Cells = restored.Cells.Select((c, i) => i == 8 ? c with { BuildingKind = "farmland", BuildingLevel = 1 } : c).ToArray() };
        Check(WorldTurns.FactionIncome(replaced, actor, cityRules) == new Resources(0, 0, 5), "city-specific override cannot leak into another building kind");
        var explicitIncome = restored with { Cells = restored.Cells.Select((c, i) => i == 8 ? c with { IncomeOverride = new(11, 13, 17) } : c).ToArray() };
        Check(WorldTurns.FactionIncome(explicitIncome, actor, cityRules) == new Resources(13, 16, 17), "explicit full site income still takes priority");
        Reject(() => (cityRules with { SiteIncomeOverrides = new Dictionary<int, BuildingIncomeOverride> { [8] = new("city", Money: -1) } }).Validate(), "turn.configuration");
        var defense = CombatContext.Resolve(restored, restored.Units.Single(u => u.Id == "unit.2"), "infantry", new() { Buildings = catalog.BuildingDefenseRules() });
        Check(defense.Evasion == .12f, "actual imported building defense feeds battle context");
        Check(FactionSurvival.Defeated([new(false, "farmland")], FactionSurvival.Legacy(0))
            && !FactionSurvival.Defeated([new(false, "port")], FactionSurvival.Legacy(0)), "actual farmland is excluded from building-based survival; port counts");
        var expansionSites = fixture.Cells.Select((c, i) => c with { BuildingKind = i == 1 ? "port" : i == 3 ? "farmland" : null,
            BuildingLevel = i is 1 or 3 ? 1 : 0 }).ToArray();
        var expansion = TerritoryClaims.Expand(new(3, 3), expansionSites, [0], [], "winner");
        Check(expansion[1].Owner == "loser" && expansion[3].Owner == "winner", "expansion passes farmland, reserves real port");

        var terrainRules = LegacyTerrainRules.FromXml(File.ReadAllText(Path.Combine(assets, "def_mapterrain.xml")), File.ReadAllText(Path.Combine(assets, "def_terraintype.xml")));
        var maps = new Dictionary<int, LegacyTerrainMap> { [1] = LegacyTerrainMap.Read(File.ReadAllBytes(Path.Combine(assets, "europe.bin"))),
            [2] = LegacyTerrainMap.Read(File.ReadAllBytes(Path.Combine(assets, "america.bin"))) };
        var names = XDocument.Load(Path.Combine(assets, "def_areaname.xml")).Root!.Elements("areaname").ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
        var installations = new Dictionary<int, string> { [0] = "trench", [1] = "fence", [2] = "bunker" };
        int firstBuilding = sampleBattle.Sites.ToList().FindIndex(s => s.BuildingKind != 255);
        int siteOffset = sampleBattle.Sections.Single(s => s.Name == "sites").Offset + firstBuilding * 16;
        var overLevelBytes = sampleBattle.ToBytes(); overLevelBytes[siteOffset + 5] = 255; overLevelBytes[siteOffset + 15] = 1;
        var overLevel = LegacyBattle.Read(overLevelBytes);
        var sampleIds = sampleBattle.Factions.Select((_, i) => "faction." + i).ToArray();
        var normalized = LegacyScenarioMap.Import(maps[overLevel.MapId], overLevel, terrainRules, sampleIds,
            LegacyBuildingCatalog.Kinds, installations, names, catalog);
        var sourceSite = overLevel.Sites[firstBuilding];
        Check(normalized.Cells[sourceSite.Cell].BuildingLevel == catalog.Definitions[sourceSite.BuildingKind].Levels.Count
            && normalized.Cells[sourceSite.Cell].BuildingUpgradesThisTurn == 1 && sourceSite.BuildingLevel == 255,
            "map applies native constructor clamp and upgrade-use state while retaining raw source");
        var wrongKinds = LegacyBuildingCatalog.Kinds.ToDictionary(p => p.Key, p => p.Value); wrongKinds[sourceSite.BuildingKind] = "wrong-kind";
        Reject(() => LegacyScenarioMap.Import(maps[overLevel.MapId], overLevel, terrainRules, sampleIds, wrongKinds,
            installations, names, catalog), "legacy.building_mapping");
        int files = 0, sites = 0, capitalSites = 0, clamped = 0;
        var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
        var absentCapitals = new List<object>();
        foreach (string file in Directory.GetFiles(assets, "*.btl").Order(StringComparer.Ordinal))
        {
            var battle = LegacyBattle.Read(File.ReadAllBytes(file));
            var ids = battle.Factions.Select((_, i) => "faction." + i).ToArray();
            var map = LegacyScenarioMap.Import(maps[battle.MapId], battle, terrainRules, ids, LegacyBuildingCatalog.Kinds, installations, names, catalog);
            var turns = catalog.TurnRules(battle); turns.Validate();
            foreach (var site in battle.Sites.Where(s => s.BuildingKind != 255))
            {
                var cell = map.Cells[site.Cell];
                kinds[cell.BuildingKind!] = kinds.GetValueOrDefault(cell.BuildingKind!) + 1;
                var wanted = catalog.Income(site.BuildingKind, cell.BuildingLevel, site.State8);
                Check(turns.Income(cell, site.Cell) == wanted && cell.BuildingUpgradesThisTurn == (site.State15 != 0 ? 1 : 0), "real site income/capital and upgrade-use state");
                Check(turns.Building(cell) != null, "every real imported building has a turn row");
                if (cell.BuildingLevel != site.BuildingLevel) clamped++;
                if (site.BuildingKind == 0 && site.State8 >= 0)
                {
                    capitalSites++;
                    if (!catalog.Capitals.ContainsKey(site.State8)) absentCapitals.Add(new { file = Path.GetFileName(file), site.Cell, capital = site.State8 });
                }
                sites++;
            }
            files++;
        }
        Check(files == 95 && absentCapitals.Count == 0, "all original building components resolve their rule rows and capitals");
        var report = new { files, buildingDefinitions = catalog.Definitions.Count, nativeRows = catalog.Definitions.Values.Sum(d => d.Levels.Count),
            capitals = catalog.Capitals.Count, buildingSites = sites, capitalSites, normalizedClampedLevels = clamped, buildingKinds = kinds, absentCapitals, checks,
            limitations = new[] { "Building income/healing/defense/recruit-query/upgrade components; no full legacy world Start or Android client.",
                "Faction grants and special recruitment rules, metagame roster/medals, remaining site/AI state and complete rule host are still required." } };
        File.WriteAllText(Path.Combine(root, "rewrite/reports/legacy_buildings.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return checks;
    }
}
