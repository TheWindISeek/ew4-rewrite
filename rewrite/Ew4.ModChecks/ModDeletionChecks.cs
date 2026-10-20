using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Mods;

internal static class ModDeletionChecks
{
    internal static int Run(string root)
    {
        string data = Path.Combine(root, "rewrite/data");
        string run = Path.Combine(root, "rewrite/reports/mod-deletion-20261020", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        var baseline = new ModBase(p => File.ReadAllBytes(p.StartsWith("frontend/")
            ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", p[9..]) : Path.Combine(data, p)),
            new[] { "original", "legacy", "client" }.SelectMany(part => Directory.EnumerateFiles(Path.Combine(data, part), "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(data, p).Replace('\\', '/'))));
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        void Reject(Action action, string label)
        {
            try { action(); } catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException) { checks++; return; }
            throw new Exception(label);
        }
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        JsonNode Call(string route, object body)
        {
            var result = JsonNode.Parse(store.Dispatch("/api/" + route, JsonSerializer.Serialize(body)))!;
            Check((bool?)result["ok"] == true, result.ToJsonString()); return result["data"]!;
        }
        string stage = Path.Combine(run, "source"); Directory.CreateDirectory(stage);
        store.Seal(stage, "delete-test", "删除验证", "1", ["conquest1"]);
        var mod = store.Import(stage, "unused", "unused");
        string sentinel = Path.Combine(run, "mods", "keep.txt"); File.WriteAllText(sentinel, "keep");
        foreach (string? key in new[] { null, "", "builtin", "../saves", new string('g', 64), new string('a', 64) })
            Reject(() => store.Delete(key), "invalid/missing key accepted");
        Check(store.List().Length == 1 && File.Exists(sentinel), "rejected deletion changed installed contents");
        Call("start", new { scenario = "conquest1", country = "fra" });
        string builtin = Call("action", new { op = "state" }).ToJsonString();
        string builtinSave = Path.Combine(run, "saves/saves/mode-2/slot-0.json");
        byte[] save = File.ReadAllBytes(builtinSave);
        var removed = JsonNode.Parse(store.Dispatch("/api/mods", JsonSerializer.Serialize(new { op = "delete", key = mod.Key })))!;
        Check(removed["mods"]!.AsArray().Count == 0, "delete dispatch kept installed mod");
        Check(!Directory.Exists(Path.Combine(store.Root, mod.Key)), "installed directory still exists");
        Check(Call("action", new { op = "state" }).ToJsonString() == builtin, "inactive deletion changed current game");
        Check(File.ReadAllBytes(builtinSave).SequenceEqual(save), "inactive deletion changed save");
        Check(File.Exists(Path.Combine(stage, "mod.json")) && File.Exists(sentinel), "source/unrelated file removed");
        mod = store.Import(stage, "unused", "unused"); store.Activate(mod.Key);
        Call("start", new { scenario = "conquest1", country = "fra" });
        string modState = Call("action", new { op = "state" }).ToJsonString();
        string modSave = Path.Combine(run, "saves/mods", mod.Id, mod.LogicRevision, "saves/mode-2/slot-0.json");
        byte[] modBytes = File.ReadAllBytes(modSave);
        store.Delete(mod.Key.ToUpperInvariant());
        Check(JsonNode.Parse(store.Status())!["active"] == null && store.List().Length == 0, "active deletion did not select builtin");
        Check(JsonSerializer.Deserialize<string>(File.ReadAllText(Path.Combine(store.Root, "active.json"))) == null, "builtin selection not persisted");
        Check(File.ReadAllBytes(modSave).SequenceEqual(modBytes) && File.ReadAllBytes(builtinSave).SequenceEqual(save), "active deletion changed saves");
        Call("catalog", new { });
        var restarted = new ModStore(baseline, store.Root, Path.Combine(run, "saves"));
        Check(JsonNode.Parse(restarted.Status())!["active"] == null, "restart selected deleted content");
        mod = store.Import(stage, "unused", "unused"); store.Activate(mod.Key);
        Check(Call("load", new { mode = 2 }).ToJsonString() == modState, "reimport could not restore retained save");
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var import = Task.Run(() => store.Import(stage, "unused", "unused", _ => { entered.Set(); release.Wait(); }));
        try
        {
            Check(entered.Wait(TimeSpan.FromSeconds(20)), "import did not start");
            Reject(() => store.Delete(mod.Key), "deletion allowed during import");
        }
        finally { release.Set(); }
        import.GetAwaiter().GetResult();
        Check(store.List().Single().Active, "blocked deletion changed active content");
        string damaged = new string('f', 64), damagedFolder = Path.Combine(store.Root, damaged);
        Directory.CreateDirectory(damagedFolder); File.WriteAllText(Path.Combine(damagedFolder, "broken.txt"), "broken");
        store.Delete(damaged);
        Check(!Directory.Exists(damagedFolder) && store.List().Single().Active, "damaged package deletion failed");
        Check(JsonNode.Parse(store.Dispatch("/api/mods", "{\"op\":\"delete\",\"key\":null}"))!["error"] != null, "API accepted builtin deletion");
        File.WriteAllText(Path.Combine(run, "results.json"), JsonSerializer.Serialize(new { checks, errors = 0, run }));
        return checks;
    }
}
