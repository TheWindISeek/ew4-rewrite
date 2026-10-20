using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Ew4.Core;
using Ew4.Legacy;

internal static class LegacyImportChecks
{
    private static int checks;
    private static void Check(bool condition, string label)
    { checks++; if (!condition) throw new InvalidOperationException("Legacy import: " + label); }
    private static void Reject(Action action, string code)
    {
        try { action(); }
        catch (RuleViolation error) { Check(error.Code == code, code + " actual=" + error.Code); return; }
        throw new InvalidOperationException("Expected legacy rejection: " + code);
    }
    private static byte[] Bytes(JsonElement element) => element.EnumerateArray().Select(v => v.GetByte()).ToArray();
    private static readonly IReadOnlyDictionary<int, string> Buildings = LegacyBuildingCatalog.Kinds;
    private static readonly Dictionary<int, string> Installations = new() { [0] = "trench", [1] = "fence", [2] = "bunker" };
    private static void Set(byte[] raw, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(offset), value);
    private static byte[] Faction(string id, string variant)
    {
        var raw = new byte[180];
        Set(raw, 16, 1); Set(raw, 20, 2); Set(raw, 56, BitConverter.SingleToInt32Bits(1));
        Encoding.UTF8.GetBytes(id).CopyTo(raw, 24); Encoding.UTF8.GetBytes(variant).CopyTo(raw, 40);
        return raw;
    }
    private static byte[] ExampleBattle()
    {
        var raw = new byte[72 + 360 + 9 + 16 + 8];
        int[] header = [2, 1, 2, 1, 3, 3, 2, 1, 0, 0, 0, 1, 999, 0, 0, 1, 1, 0];
        for (int i = 0; i < header.Length; i++) Set(raw, i * 4, header[i]);
        Faction("one", "fra").CopyTo(raw, 72); Faction("two", "rus").CopyTo(raw, 252);
        new byte[] { 0, 1, 255, 0, 255, 1, 128, 0, 1 }.CopyTo(raw, 432);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(441), 9);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(443), 10);
        new byte[] { 0, 2, 0, 1, 255, 2, 1, 255, 1, 4, 16, 0 }.CopyTo(raw, 445);
        Set(raw, 457, 17); Set(raw, 461, 2);
        return raw;
    }
    private static byte[] ExampleTerrain()
    {
        var raw = new byte[16 + 42 * 12];
        "YSAE"u8.CopyTo(raw); Set(raw, 4, 1); Set(raw, 8, 7); Set(raw, 12, 6);
        for (int i = 0; i < 42; i++) new byte[] { 0, 255, 0, 0, 11, 255, 0, 0, 0, 0, 0, 0 }.CopyTo(raw, 16 + i * 12);
        raw[16 + 17 * 12] = 1; raw[17 + 17 * 12] = 0;
        raw[24 + 17 * 12] = 1; raw[25 + 17 * 12] = 2;
        return raw;
    }

    public static int Run(string root)
    {
        checks = 0;
        string assets = Path.Combine(root, "apk_work/original/assets");
        string terrainTypes = File.ReadAllText(Path.Combine(assets, "def_terraintype.xml"));
        var buildingRules = LegacyBuildingCatalog.Read(File.ReadAllText(Path.Combine(assets, "def_construction.xml")), File.ReadAllText(Path.Combine(assets, "def_capital.xml")));
        var rules = LegacyTerrainRules.FromXml(File.ReadAllText(Path.Combine(assets, "def_mapterrain.xml")), terrainTypes);
        string[] types = ["land", "sea", "forest", "forest", "forest", "forest", "forest", "desert", "hill", "mountain"];
        var syntheticDefinitions = new XElement("terrains", types.Select((type, i) => new XElement("terrain", new XAttribute("type", type),
            Enumerable.Range(0, 6).Select(j => new XElement("tile", new XAttribute("idx", j), new XAttribute("n", type == "forest" ? j % 4 + 1 : 0))))));
        var vectorRules = LegacyTerrainRules.FromXml(syntheticDefinitions.ToString(), terrainTypes);
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/world_import_vectors.json")));
        foreach (var vector in vectors.RootElement.GetProperty("terrain").EnumerateArray())
        {
            var result = vectorRules.Resolve(LegacyTerrainCell.Read(Bytes(vector.GetProperty("raw"))));
            Check(result.Movement.TerrainCost == vector.GetProperty("cost").GetInt32() && result.Overlay == vector.GetProperty("overlay").GetInt32()
                && result.DefenseTerrain == new[] { "land", "sea", "plant", "forest", "desert", "hill", "mountain" }[vector.GetProperty("defense_type").GetInt32()]
                && result.Movement.Sea == vector.GetProperty("sea").GetBoolean(), "native layered terrain cache");
        }
        foreach (var vector in vectors.RootElement.GetProperty("faction").EnumerateArray())
        {
            var result = LegacyFaction.Read(Bytes(vector.GetProperty("raw")));
            var wanted = vector.GetProperty("expected");
            Check(result.Resources == new Resources(wanted.GetProperty("resources")[0].GetInt64(), wanted.GetProperty("resources")[1].GetInt64(), wanted.GetProperty("resources")[2].GetInt64())
                && result.Ai == wanted.GetProperty("ai").GetBoolean() && result.Camp == wanted.GetProperty("camp").GetInt32()
                && result.SurvivalMode == wanted.GetProperty("survival").GetInt32() && result.IncomeMultiplier == wanted.GetProperty("income").GetSingle()
                && result.AiEvasionBonus == wanted.GetProperty("evasion").GetSingle() && result.Color == wanted.GetProperty("color").GetInt32()
                && result.Values68[25] == wanted.GetProperty("tail_value").GetInt32() && Math.Min(12, result.Count172) == wanted.GetProperty("count").GetInt32()
                && result.State176 == wanted.GetProperty("expiry").GetInt32() && result.CountryId == "fra" && result.Variant == "rus", "native faction fields; raw count retained");
        }
        foreach (var vector in vectors.RootElement.GetProperty("site").EnumerateArray())
        {
            var result = LegacySite.Read(Bytes(vector.GetProperty("raw")));
            var wanted = vector.GetProperty("expected");
            Check(result.Marker == wanted.GetProperty("marker").GetByte() && result.State11 == wanted.GetProperty("state11").GetSByte()
                && result.BridgeEdges == wanted.GetProperty("bridge_edges").GetByte() && result.CityId == wanted.GetProperty("name_id").GetInt16()
                && (result.BuildingKind != 255) == wanted.GetProperty("building").GetBoolean()
                && (result.InstallationKind != -1) == wanted.GetProperty("installation").GetBoolean()
                && new[] { result.BridgeStyle0, result.BridgeStyle1, result.BridgeStyle2 }.SequenceEqual(Bytes(wanted.GetProperty("bridge_styles"))), "native site deserialization");
        }
        var maps = new Dictionary<int, LegacyTerrainMap>();
        foreach (var pair in new[] { (1, "europe.bin"), (2, "america.bin") })
        {
            byte[] raw = File.ReadAllBytes(Path.Combine(assets, pair.Item2));
            var map = LegacyTerrainMap.Read(raw); maps[pair.Item1] = map;
            Check(map.ToBytes().SequenceEqual(raw), "terrain round trip");
            foreach (var cell in map.Cells) { rules.Resolve(cell); checks++; }
        }
        var names = XDocument.Load(Path.Combine(assets, "def_areaname.xml")).Root!.Elements("areaname")
            .ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
        int files = 0, factionRecords = 0, siteRecords = 0, armyRecords = 0, imported = 0;
        var mapFailures = new List<object>();
        foreach (string file in Directory.GetFiles(assets, "*.btl").Order(StringComparer.Ordinal))
        {
            byte[] raw = File.ReadAllBytes(file);
            var battle = LegacyBattle.Read(raw);
            Check(battle.ToBytes().SequenceEqual(raw), "battle round trip " + Path.GetFileName(file));
            Check(battle.Factions.Count == battle.Header[6] && battle.Sites.Count == battle.Header[7] && battle.Armies.Count == battle.Header[8], "typed counts");
            // All sections, including unknown records, survive independently of caller-owned buffers.
            var preserved = battle.ToBytes(); raw[0] = 99;
            Check(battle.ToBytes().SequenceEqual(preserved), "input buffer isolation");
            foreach (var section in battle.Sections) Check(battle.SectionBytes(section.Name).Length == section.Count * section.Stride, "section lengths");
            files++; factionRecords += battle.Factions.Count; siteRecords += battle.Sites.Count; armyRecords += battle.Armies.Count;
            try
            {
                var ids = battle.Factions.Select((f, i) => $"legacy.{i}.{f.CountryId}").ToArray();
                var result = LegacyScenarioMap.Import(maps[battle.MapId], battle, rules, ids, Buildings, Installations, names, buildingRules);
                Check(result.Cells.Count(c => c.Terrain.Active) == battle.Width * battle.Height, "active rectangle " + Path.GetFileName(file));
                Check(result.SiteRecords.Count == battle.Sites.Count, "all site records retained");
                imported++;
            }
            catch (RuleViolation error) { mapFailures.Add(new { file = Path.GetFileName(file), error.Code }); }
        }
        Check(files == 95 && factionRecords == 543 && siteRecords == 7860 && armyRecords == 7289, "preserved corpus inventory");
        if (mapFailures.Count > 0) throw new InvalidOperationException(JsonSerializer.Serialize(mapFailures));

        var mapData = ExampleTerrain(); var battleData = ExampleBattle();
        var sampleMap = LegacyTerrainMap.Read(mapData); var sample = LegacyBattle.Read(battleData);
        string[] factionIds = ["human", "opponent"];
        Dictionary<int, string> labels = new() { [10] = "Xi'an 西安" };
        LegacyMapImport Import(byte[] data) => LegacyScenarioMap.Import(sampleMap, LegacyBattle.Read(data), rules, factionIds, Buildings, Installations, labels, buildingRules);
        var importedMap = Import(battleData);
        Check(importedMap.Cells[9].Owner == "human" && importedMap.Cells[10].Owner == "opponent" && importedMap.Cells[11].Owner == null
            && importedMap.Cells[16].Owner == "human" && importedMap.Cells[23].Owner == null, "relative ownership becomes absolute full-map cells");
        Check(importedMap.Cells.Count(c => c.Terrain.Active) == 9 && importedMap.Cells[9].Name == "Xi'an 西安" && importedMap.Cells[9].Terrain.BridgeEdges == 21
            && importedMap.Cells[9].InstallationKind == "bunker" && importedMap.Cells[9].Objective, "Unicode, bridges, facility, marker");
        Check(importedMap.Cells[17].Environment == 2 && importedMap.Cells[17].Terrain.Sea && importedMap.Cells[17].Terrain.RiverEdges == 1
            && importedMap.Cells[17].Terrain.BlockedSeaEdges == 2 && importedMap.Cells[9].Environment == 0, "separate environment section, sea and river flags");
        Check(Import(battleData).SourceRevision == importedMap.SourceRevision, "deterministic map identity");
        labels[10] = "西安"; Check(Import(battleData).SourceRevision != importedMap.SourceRevision, "map identity includes changed labels");
        var modern = new WorldState { ScenarioId = "map-import-check", ContentRevision = importedMap.SourceRevision,
            Width = sampleMap.Width, Height = sampleMap.Height, Cells = importedMap.Cells, ActiveFaction = "human", Factions = [
                new() { Id = "human", Camp = "a", Human = true }, new() { Id = "opponent", Camp = "b" }],
            Units = [new() { Id = "import-map.unit", Owner = "human", Cell = 9, RemainingMovement = 6,
                Combatant = new() { Health = 100, Profile = new() { Id = "fixture", Category = "infantry", Movement = 6, MaxHealth = 100 } } }] };
        modern.Validate();
        var moved = WorldActions.Move(modern, 0, "import-map.unit", 16);
        Check(moved.State.Units.Single().Cell == 16 && moved.State.Cells[16].Terrain.Active, "map component participates in core movement");

        foreach (int length in new[] { 0, 1, 15, mapData.Length - 1 })
            Reject(() => LegacyTerrainMap.Read(mapData.AsSpan(0, length)), length < 16 ? "legacy.terrain_header" : "legacy.terrain_length");
        foreach (int field in new[] { 6, 7, 8, 9, 10, 14, 16 })
        {
            var broken = (byte[])battleData.Clone(); Set(broken, field * 4, int.MaxValue);
            Reject(() => LegacyBattle.Read(broken), "legacy.battle_section");
            Set(broken, field * 4, -1); Reject(() => LegacyBattle.Read(broken), "legacy.battle_section");
        }
        var region = (byte[])battleData.Clone(); Set(region, 8, 6); Reject(() => Import(region), "legacy.region");
        var owner = (byte[])battleData.Clone(); owner[432] = 2;
        Check(Import(owner).Cells[9].Owner == null && Import(owner).Diagnostics.Any(d => d.Code == "legacy.owner_unresolved"),
            "unresolved native faction lookup leaves tile unowned and records source value");
        var name = (byte[])battleData.Clone(); BinaryPrimitives.WriteInt16LittleEndian(name.AsSpan(443), 999);
        var unnamed = Import(name);
        Check(unnamed.Cells[9] == (importedMap.Cells[9] with { Name = null }) && unnamed.SiteRecords[9].CityId == 999,
            "missing label preserves city identity, ownership, facilities and objective");
        var siteOutside = (byte[])battleData.Clone(); BinaryPrimitives.WriteInt16LittleEndian(siteOutside.AsSpan(441), 0);
        Check(!Import(siteOutside).Cells[0].Terrain.Active && Import(siteOutside).Cells[0].BuildingKind == "city",
            "site outside playable rectangle retains metadata without activating terrain");
        BinaryPrimitives.WriteInt16LittleEndian(siteOutside.AsSpan(441), short.MaxValue);
        Check(Import(siteOutside).SiteRecords.Count == 0 && Import(siteOutside).Diagnostics.Any(d => d.Code == "legacy.site_outside_map"),
            "native null tile lookup skips out-of-map site with diagnostic");
        var duplicate = new byte[battleData.Length + 16];
        battleData.AsSpan(0, 457).CopyTo(duplicate);
        battleData.AsSpan(441, 16).CopyTo(duplicate.AsSpan(457));
        battleData.AsSpan(457).CopyTo(duplicate.AsSpan(473));
        Set(duplicate, 7 * 4, 2); duplicate[461] = 255; duplicate[466] = 0; duplicate[467] = 0;
        var finalSite = Import(duplicate);
        Check(finalSite.Cells[9].BuildingKind == null && finalSite.Cells[9].InstallationKind == "trench" && !finalSite.Cells[9].Objective
            && LegacyBattle.Read(duplicate).EffectiveSites.Count == 1 && finalSite.Diagnostics.Any(d => d.Code == "legacy.site_replaced"),
            "later site record replaces building, installation and objective together");
        var environment = (byte[])battleData.Clone(); Set(environment, 457, 42); Reject(() => Import(environment), "legacy.environment");
        var unterminated = Faction("one", "fra"); Array.Fill(unterminated, (byte)'x', 24, 16); Reject(() => LegacyFaction.Read(unterminated), "legacy.string");
        var invalidForest = new LegacyTerrainCell(3, 255, 0, 0, 11, 255, 0, 0, 0, 0, 0, 0);
        Reject(() => rules.Resolve(invalidForest), "legacy.terrain_sprite");
        Reject(() => rules.Resolve(invalidForest with { BackgroundKind = 255 }), "legacy.terrain_kind");
        Reject(() => rules.Resolve(sampleMap.Cells[0] with { Unknown10 = 1 }), "legacy.terrain_flags");
        Reject(() => rules.Resolve(sampleMap.Cells[0] with { RiverEdges = 64 }), "legacy.terrain_flags");
        var report = new { files, factionRecords, siteRecords, armyRecords, importedMaps = imported, mapFailures, checks,
            limitations = new[] { "Map component only; this is not a complete legacy world or rule importer.",
                "Original site state bytes, faction auxiliary tables, units, tavern medals, event/AI records and runtime rule catalogs still need semantic integration." } };
        File.WriteAllText(Path.Combine(root, "rewrite/reports/legacy_map_import.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return checks;
    }
}
