using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Mods;

internal static class ItemExtensionModChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new InvalidOperationException("Item mod import: " + label); }
        string report = Path.Combine(root, "rewrite/reports/item-extensions-20261021");
        string run = Path.Combine(report, "import-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        string data = Path.Combine(root, "rewrite/data"), example = Path.Combine(root, "rewrite/mod-kit/examples/item-effects");
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
            Check(response.RootElement.GetProperty("ok").GetBoolean(), "client " + route);
            return response.RootElement.GetProperty("data").Clone();
        }
        Call("start", new { scenario = "conquest1", country = "fra" });
        string originalSave = Path.Combine(run, "saves/saves/mode-2/slot-0.json"), originalAccount = Path.Combine(run, "saves/account.json");
        byte[] save = File.ReadAllBytes(originalSave), account = File.ReadAllBytes(originalAccount);
        store.Seal(author, "item-effects", "物品装备效果示例", "1.0", ["conquest1"]);
        Check(store.Validate(author).Files.Count == 5, "extension-only package validates without copied legacy assets");
        var imported = store.Import(author, "ignored", "ignored"); store.Activate(imported.Key);
        var content = baseline.Content(store.Read, ["conquest1"]);
        Check(content.Catalogs.Runtime.Items[8].SkillSlots!.Count == 2 && content.Catalogs.Items.Items[13].Equipment.UseSkill != null, "installed package reaches both item paths");
        string fixture = Path.Combine(run, "saves/mods", imported.Id, imported.LogicRevision); Directory.CreateDirectory(fixture);
        File.Copy(Path.Combine(report, "fixture-slot.json"), Path.Combine(fixture, "session.json"));
        File.Copy(Path.Combine(report, "fixture-account.json"), Path.Combine(fixture, "account.json"));
        var view = Call("load", new { });
        var unit = view.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("skills").EnumerateArray()
            .Any(s => s.GetProperty("instanceId").GetString() == "item.8.repair"));
        string unitId = unit.GetProperty("id").GetString()!;
        var after = Call("action", new { op = "use_item", revision = view.GetProperty("revision").GetInt64(), unit = unitId, item = "ew4.item.13" });
        Check(after.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == unitId).GetProperty("health").GetInt32() == 22,
            "imported Lua executes through real client item command");
        Call("save", new { mode = 2, slot = 1 });
        var reloaded = Call("load", new { mode = 2, slot = 1 });
        Check(reloaded.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == unitId).GetProperty("statuses").GetArrayLength() == 1, "installed mod save reloads effect");
        string output = Path.Combine(root, "build/item-extensions-20261021"); Directory.CreateDirectory(output);
        string archive = Path.Combine(run, "item-effects.ew4mod"); store.Export(imported.Key, archive);
        var other = new ModStore(baseline, Path.Combine(run, "other-mods"), Path.Combine(run, "other-saves"));
        var copy = other.Import(archive, "ignored", "ignored"); other.Activate(copy.Key);
        Check(copy.LogicRevision == imported.LogicRevision && baseline.Content(other.Read, ["conquest1"]).Catalogs.Runtime.Items[9].Value == 17, "offline archive retains rules and identity");
        string script = Path.Combine(author, "rewrite/scripts/items.lua");
        File.AppendAllText(script, "\n-- author revision\n");
        store.Seal(author, imported.Id, "物品装备效果示例", "1.1", ["conquest1"]);
        var edited = store.Import(author, "ignored", "ignored");
        Check(edited.LogicRevision != imported.LogicRevision, "external script edits isolate save version");
        store.Activate(null);
        Check(File.ReadAllBytes(originalSave).SequenceEqual(save) && File.ReadAllBytes(originalAccount).SequenceEqual(account), "builtin session and account preserved");
        Check(baseline.Content(store.Read, ["conquest1"]).Catalogs.Runtime.Items[8].SkillSlots == null, "switching to builtin removes item extensions");
        string bindingsPath = Path.Combine(author, "rewrite/definitions/item-bindings.json");
        var bindings = JsonNode.Parse(File.ReadAllBytes(bindingsPath))!.AsArray(); bindings[0]!["target"] = "ew4.item.999999";
        File.WriteAllText(bindingsPath, bindings.ToJsonString()); store.Seal(author, imported.Id, "无效物品", "bad", ["conquest1"]);
        bool rejected = false; try { store.Validate(author); } catch (Ew4.Core.RuleViolation) { rejected = true; }
        Check(rejected, "actual package validation rejects unknown item");
        File.Copy(archive, Path.Combine(output, "item-effects.ew4mod"), true);
        File.WriteAllText(Path.Combine(report, "import.json"), JsonSerializer.Serialize(new { checks, errors = 0, run, archive,
            imported.Key, imported.LogicRevision, editedRevision = edited.LogicRevision, fixture, unitId, cell = unit.GetProperty("cell").GetInt32() }));
        return checks;
    }
}
