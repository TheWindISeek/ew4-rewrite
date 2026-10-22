using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Ew4.Mods;

internal static class UnitVisualChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { if (!pass) throw new Exception(label); checks++; }
        void Reject(Action action, string label)
        {
            try { action(); } catch (InvalidDataException) { checks++; return; }
            throw new Exception(label);
        }
        string data = Path.Combine(root, "rewrite/data");
        byte[] Read(string path) => File.ReadAllBytes(path.StartsWith("frontend/", StringComparison.Ordinal)
            ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", path[9..]) : Path.Combine(data, path));
        var manifest = JsonNode.Parse(Read("client/manifest.json"))!.AsObject();
        var effects = JsonNode.Parse(Read("client/effects.json"))!.AsObject();
        var config = JsonNode.Parse(Read("client/unit-visuals.json"))!.AsObject();
        var original = XDocument.Load(Path.Combine(root, "apk_work/original/assets/def_motion.xml")).Root!;
        Check(JsonNode.DeepEquals(UnitVisualConfiguration.FromLegacy(original, manifest["profiles"]!.AsObject()), config), "Importer matches original visual configuration");
        UnitVisualConfiguration.Validate(config, manifest, effects, Read); checks++;
        var custom = (JsonObject)config.DeepClone();
        custom["factions"]!["my.republic"] = "my.skin";
        custom["variants"]!["my.skin"] = new JsonObject { ["militia.0"] = "my.militia" };
        custom["definitions"]!["my.militia"] = effects["motions"]!["Militia hre 1"]!.DeepClone();
        custom["definitions"]!["my.militia"]!["x"] = 12;
        UnitVisualConfiguration.Validate(custom, manifest, effects, Read); checks++;
        var invalid = (JsonObject)custom.DeepClone(); invalid["variants"]!["my.skin"]!["militia.0"] = "typo";
        Reject(() => UnitVisualConfiguration.Validate(invalid, manifest, effects, Read), "Unknown motion accepted");
        invalid = (JsonObject)custom.DeepClone(); invalid["definitions"]!["my.militia"]!["motions"]![0]!["name"] = "missing-action";
        Reject(() => UnitVisualConfiguration.Validate(invalid, manifest, effects, Read), "Missing action accepted");
        invalid = (JsonObject)custom.DeepClone(); invalid["definitions"]!["my.militia"]!["motions"]![0]!["speed"] = 0;
        Reject(() => UnitVisualConfiguration.Validate(invalid, manifest, effects, Read), "Zero animation speed accepted");
        invalid = (JsonObject)custom.DeepClone(); invalid["definitions"]!["my.militia"]!["res"] = "../other";
        Reject(() => UnitVisualConfiguration.Validate(invalid, manifest, effects, Read), "Escaping resource path accepted");
        string run = Path.Combine(root, "build/unit-visuals-20261021/mod-" + Guid.NewGuid().ToString("N"));
        var baseline = new ModBase(Read, Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')));
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        string author = Path.Combine(run, "author"); Directory.CreateDirectory(Path.Combine(author, "client"));
        File.WriteAllText(Path.Combine(author, "client/unit-visuals.json"), custom.ToJsonString());
        store.Seal(author, "unit-visual-check", "Unit visuals", "1.0", ["conquest1"]);
        Check(store.Validate(author).Files.Count == 1, "Visual-only Mod packs without copied gameplay resources");
        var installed = store.Import(author, "ignored", "ignored"); store.Activate(installed.Key);
        Check(JsonNode.DeepEquals(JsonNode.Parse(store.Read("client/unit-visuals.json")), custom), "Installed Mod retains editable visual JSON");
        Check(Read("legacy/catalog.json").SequenceEqual(store.Read("legacy/catalog.json")), "Visual Mod retains actual combat catalog bytes");
        var started = JsonNode.Parse(store.Dispatch("/api/start", "{\"scenario\":\"conquest1\",\"country\":\"fra\"}"))!;
        Check((bool)started["ok"]!, "Visual Mod starts actual client");
        Check(started["data"]!["factions"]!.AsArray().All(f => f!["unitVariant"] is JsonValue), "Every original faction exposes independent visual variant");
        string archive = Path.Combine(run, "visuals.ew4mod"); store.Export(installed.Key, archive);
        Check(store.Validate(archive).Files.Count == 1, "Visual interface roundtrips through offline archive");
        string raw = Path.Combine(run, "raw"); Directory.CreateDirectory(raw);
        File.Copy(Path.Combine(root, "apk_work/original/assets/def_motion.xml"), Path.Combine(raw, "def_motion.xml"));
        var converted = store.Import(raw, "raw-visual-check", "Raw visuals"); store.Activate(converted.Key);
        Check(JsonNode.DeepEquals(JsonNode.Parse(store.Read("client/unit-visuals.json")), config), "Original XML import creates editable complete visual JSON");
        File.WriteAllText(Path.Combine(root, "rewrite/reports/unit-visuals-20261021/mod-tests.json"), JsonSerializer.Serialize(new { checks, errors = 0, run }));
        return checks;
    }
}
