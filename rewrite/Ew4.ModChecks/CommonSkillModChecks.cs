using System.Text.Json;
using Ew4.Mods;

internal static class CommonSkillModChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { if (!pass) throw new Exception("Common skill import: " + label); checks++; }
        string report = Path.Combine(root, "rewrite/reports/common-skills-20261021");
        string run = Path.Combine(report, "import-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        string data = Path.Combine(root, "rewrite/data"), example = Path.Combine(root, "rewrite/mod-kit/examples/common-skills");
        var baseline = new ModBase(p => File.ReadAllBytes(p.StartsWith("frontend/")
            ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", p[9..]) : Path.Combine(data, p)),
            Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')));
        string author = Path.Combine(run, "author");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(example, "rewrite"), "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(author, Path.GetRelativePath(example, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        JsonElement Call(string route, object body)
        {
            using var response = JsonDocument.Parse(store.Dispatch("/api/" + route, JsonSerializer.Serialize(body)));
            Check(response.RootElement.GetProperty("ok").GetBoolean(), route + ": " + response.RootElement.GetRawText()[..Math.Min(300, response.RootElement.GetRawText().Length)]);
            return response.RootElement.GetProperty("data").Clone();
        }
        Call("start", new { scenario = "conquest1", country = "fra" });
        string originalSave = Path.Combine(run, "saves/saves/mode-2/slot-0.json");
        byte[] originalBytes = File.ReadAllBytes(originalSave);
        store.Seal(author, "common-skills-test", "通用技能接口验证", "1.0", ["conquest1"]);
        Check(store.Validate(author).Files.Keys.Count(p => p.StartsWith("rewrite/")) == 5, "seals extension-only author package");
        var imported = store.Import(author, "ignored", "ignored");
        store.Activate(imported.Key);
        var started = Call("start", new { scenario = "conquest1", country = "fra" });
        var unit = started.GetProperty("units").EnumerateArray().First(u => u.GetProperty("owner").GetString() == started.GetProperty("playerFaction").GetString()
            && u.GetProperty("skills").GetArrayLength() == 6);
        string unitId = unit.GetProperty("id").GetString()!; int cell = unit.GetProperty("cell").GetInt32();
        long revision = started.GetProperty("revision").GetInt64();
        var selection = Call("action", new { op = "select", cell, revision });
        var summon = selection.GetProperty("skills").EnumerateArray().Single(s => s.GetProperty("skill").GetProperty("id").GetString() == "example.summon");
        Check(summon.GetProperty("cells").GetArrayLength() > 0 && summon.GetProperty("targets").GetArrayLength() == 0, "cell candidates advertised through API");
        var after = Call("action", new { op = "skill", unit = unitId, offer = "unit.summon", cell = summon.GetProperty("cells")[0].GetInt32(), revision });
        Check(after.GetProperty("units").GetArrayLength() == started.GetProperty("units").GetArrayLength() + 1, "summon through actual client command");
        Call("save", new { mode = 2, slot = 1 });
        var restored = Call("load", new { mode = 2, slot = 1 });
        Check(restored.GetProperty("units").GetArrayLength() == after.GetProperty("units").GetArrayLength(), "summoned session reload");
        var restarted = Call("start", new { scenario = "conquest1", country = "fra" });
        var buffed = Call("action", new { op = "skill", unit = unitId, offer = "unit.rally", revision = restarted.GetProperty("revision").GetInt64() });
        Check(buffed.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == unitId).GetProperty("statuses").GetArrayLength() == 1, "status visible through client view");
        Call("save", new { mode = 2, slot = 2 });
        var buffReload = Call("load", new { mode = 2, slot = 2 });
        Check(buffReload.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == unitId).GetProperty("statuses")[0]
            .GetProperty("id").GetString() == "example.vigor", "status survives actual session load");
        string archive = Path.Combine(run, "common-skills.ew4mod"); store.Export(imported.Key, archive);
        var other = new ModStore(baseline, Path.Combine(run, "other-mods"), Path.Combine(run, "other-saves"));
        Check(other.Import(archive, "ignored", "ignored").LogicRevision == imported.LogicRevision, "offline archive roundtrip");
        store.Activate(null);
        Check(File.ReadAllBytes(originalSave).SequenceEqual(originalBytes), "original account save unchanged");
        File.WriteAllText(Path.Combine(report, "latest-import.json"), JsonSerializer.Serialize(new { run, archive, unitId, cell, checks, errors = 0 }));
        return checks;
    }
}
