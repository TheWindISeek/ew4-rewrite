using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Core;
using Ew4.Persistence;

internal static class MapCatalogChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException("Map catalog: " + label); }
        void Reject(Action action, string code)
        {
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code + " actual=" + error.Code); return; }
            throw new InvalidOperationException("Expected map rejection: " + code);
        }
        var sourceCells = Enumerable.Range(0, 6).Select(_ => new CellState()).ToArray();
        sourceCells[0] = sourceCells[0] with { Terrain = new() { Sea = true, TerrainCost = 1 } };
        sourceCells[4] = sourceCells[4] with { Name = "西安 Xi'an", BuildingKind = "city", BuildingLevel = 1 };
        var original = new WorldMapDefinition("east-asia.test", "东亚格式验证", 3, 2, sourceCells,
            [new("map.test.background", -1, -1, 5, 4)]);
        var bytes = MapDocument.Encode(original);
        var loaded = MapDocument.Decode(bytes);
        Check(loaded.Map.Cells[0].Terrain.Sea && loaded.Map.Cells[4].Name == "西安 Xi'an", "water and Unicode labels survive editable JSON");
        Check(loaded.Map.Images[0] == original.Images[0], "bitmap reference and positioning survive JSON");
        Check(MapDocument.Encode(loaded.Map).SequenceEqual(bytes), "canonical map round trip");
        Check(MapDocument.Decode(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("{", "{\n"))).Revision == loaded.Revision,
            "formatting does not change map identity");
        sourceCells[0] = new();
        Check(original.Cells[0].Terrain.Sea, "constructor does not retain mutable caller array");
        var edited = original.Edit([new(0, original.Cells[0] with { Terrain = original.Cells[0].Terrain with { Sea = false, TerrainCost = 7 } })]);
        Check(!edited.Cells[0].Terrain.Sea && edited.Cells[0].Terrain.TerrainCost == 7 && original.Cells[0].Terrain.Sea, "terrain editing is isolated and affects game data");
        Check(MapDocument.Decode(MapDocument.Encode(edited)).Revision != loaded.Revision, "terrain modification changes content revision");
        Reject(() => original.Edit([new(0, new()), new(0, new())]), "map.edit");
        Reject(() => original.Edit([new(6, new())]), "map.edit");
        Reject(() => original.Edit([new(1, new() { Owner = "faction" })]), "map.cell_definition");
        Reject(() => new WorldMapDefinition("bad", "bad", 4, 2, original.Cells), "map.definition");
        Reject(() => new WorldMapDefinition("bad", "bad", 3, 2, original.Cells, [new("asset", 0, 0, double.NaN, 1)]), "map.image_definition");
        var registry = new WorldMapCatalog(Enumerable.Range(0, 120).Select(i => new WorldMapDefinition("map." + i, "Map " + i, 3, 2, original.Cells)));
        Check(registry.Maps.Count == 120 && registry.Resolve("map.119").Width == 3, "registry is not a two-map array");
        var added = registry.Add(original);
        Check(added.Maps.Count == 121 && registry.Maps.Count == 120, "adding map preserves previous catalog");
        Check(!added.Replace(edited).Resolve(original.Id).Cells[0].Terrain.Sea, "replace uses stable identity");
        Reject(() => added.Add(original), "map.duplicate");
        Reject(() => registry.Resolve("unknown"), "map.reference");
        Reject(() => registry.Replace(original), "map.reference");
        var largeCells = Enumerable.Range(0, 40_401).Select(_ => new CellState()).ToArray();
        largeCells[40_400] = new() { Name = "边界城" };
        var large = new WorldMapDefinition("large.test", "大地图格式验证", 201, 201, largeCells);
        var largeRestored = MapDocument.Decode(MapDocument.Encode(large));
        Check(largeRestored.Map.Cells.Count == 40_401 && largeRestored.Map.Cells[40_400].Name == "边界城", "map file does not narrow cells to signed 16-bit indices");
        Check(new HexMap(201, 201).Neighbor(40_400, 3) == 40_399, "large map integrates with core coordinates");
        Reject(() => MapDocument.Decode(bytes, new(MaximumBytes: 32)), "map.size");
        var missing = JsonNode.Parse(bytes)!.AsObject(); missing["cells"]![0]!["terrain"]!.AsObject().Remove("sea");
        Reject(() => MapDocument.Decode(Encoding.UTF8.GetBytes(missing.ToJsonString())), "save.required_field");
        var future = JsonNode.Parse(bytes)!; future["version"] = 99;
        Reject(() => MapDocument.Decode(Encoding.UTF8.GetBytes(future.ToJsonString())), "map.version");
        string path = Path.Combine(root, "rewrite/reports/map_document_test.map.json");
        MapDocument.WriteAtomic(path, original); MapDocument.WriteAtomic(path, edited);
        Check(MapDocument.Read(path).Revision != MapDocument.Read(path + ".bak").Revision, "atomic map replacement keeps previous version");
        File.WriteAllText(Path.Combine(root, "rewrite/reports/map_catalog.json"), JsonSerializer.Serialize(new { checks, errors = 0,
            registeredMaps = added.Maps.Count, largeMapCells = large.Cells.Count,
            scope = "Editable map data/registration/atomic IO and coordinates; no visual editor or renderer yet. Test geography is synthetic." },
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return checks;
    }
}
