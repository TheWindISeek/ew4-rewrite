using System.Text.Json;
using Ew4.Mods;

internal static class DeploymentLimitModChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new InvalidOperationException("Deployment limit import: " + label); }
        string report = Path.Combine(root, "rewrite/reports/deployment-roster-20261022");
        string run = Path.Combine(report, "import-" + Guid.NewGuid().ToString("N"));
        string data = Path.Combine(root, "rewrite/data"), example = Path.Combine(root, "rewrite/mod-kit/examples/deployment-limits");
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
            Check(response.RootElement.GetProperty("ok").GetBoolean(), route);
            return response.RootElement.GetProperty("data").Clone();
        }
        var original = Call("start", new { scenario = "conquest1", country = "fra" });
        string Target(JsonElement view) => view.GetProperty("units").EnumerateArray().First(u => u.GetProperty("owner").GetString()
            == view.GetProperty("playerFaction").GetString()).GetProperty("id").GetString()!;
        var originalQuota = Call("deployment", new { unit = Target(original), revision = original.GetProperty("revision").GetInt64() });
        string originalSave = Path.Combine(run, "saves/saves/mode-2/slot-0.json"), originalAccount = Path.Combine(run, "saves/account.json");
        byte[] save = File.ReadAllBytes(originalSave), account = File.ReadAllBytes(originalAccount);
        string configuration = Path.Combine(author, "rewrite/definitions/deployment-limits.json");
        File.WriteAllText(configuration, "{\"schema\":1,\"limits\":{\"conquest1\":13}}");
        store.Seal(author, "deployment-limits", "上将名额", "1.0", ["conquest1"]);
        Check(store.Validate(author).Files.Count == 2, "two-file overlay validates without base resource copies");
        var imported = store.Import(author, "ignored", "ignored"); store.Activate(imported.Key);
        var state = Call("start", new { scenario = "conquest1", country = "fra" });
        var quota = Call("deployment", new { unit = Target(state), revision = state.GetProperty("revision").GetInt64() });
        Check(quota.GetProperty("limit").GetInt32() == 13 && quota.GetProperty("used").GetInt32() == 0, "installed configuration reaches real deployment API");
        Call("save", new { mode = 2, slot = 1 });
        var restored = Call("load", new { mode = 2, slot = 1 });
        Check(Call("deployment", new { unit = Target(restored), revision = restored.GetProperty("revision").GetInt64() })
            .GetProperty("limit").GetInt32() == 13, "quota displayed by API survives save/load");
        string output = Path.Combine(root, "build/deployment-roster-20261022"); Directory.CreateDirectory(output);
        string archive = Path.Combine(output, "deployment-limits.ew4mod"); store.Export(imported.Key, archive);
        var offline = new ModStore(baseline, Path.Combine(run, "offline-mods"), Path.Combine(run, "offline-saves"));
        var copy = offline.Import(archive, "ignored", "ignored"); offline.Activate(copy.Key);
        Check(copy.LogicRevision == imported.LogicRevision && baseline.Content(offline.Read, ["conquest1"]).Start("conquest1", "fra")
            .Session.RosterDeployments.Single().Limit == 13, "offline exported package preserves policy and identity");
        File.WriteAllText(configuration, "{\"schema\":1,\"limits\":{\"conquest1\":0}}");
        store.Seal(author, imported.Id, "上将名额", "1.1", ["conquest1"]);
        var disabled = store.Import(author, "ignored", "ignored");
        Check(disabled.LogicRevision != imported.LogicRevision, "changed quota isolates save version");
        store.Activate(disabled.Key);
        state = Call("start", new { scenario = "conquest1", country = "fra" });
        quota = Call("deployment", new { unit = Target(state), revision = state.GetProperty("revision").GetInt64() });
        Check(quota.GetProperty("limit").GetInt32() == 0 && quota.GetProperty("commanders").GetArrayLength() == 0, "zero limit reaches displayed quota and candidate API");
        store.Activate(null);
        Check(File.ReadAllBytes(originalSave).SequenceEqual(save) && File.ReadAllBytes(originalAccount).SequenceEqual(account), "original account and autosave remain byte identical");
        Check(originalQuota.GetProperty("limit").GetInt32() == baseline.Content(baseline.Read, ["conquest1"]).Start("conquest1", "fra").Session.RosterDeployments.Single().Limit,
            "default quota is not changed by imported configuration");
        File.WriteAllText(Path.Combine(report, "import.json"), JsonSerializer.Serialize(new { checks, errors = 0, archive }));
        return checks;
    }
}
