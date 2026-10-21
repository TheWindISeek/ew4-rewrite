using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Mods;

internal static class LegacySkillParameterModChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new InvalidOperationException("Original skill import: " + label); }
        string report = Path.Combine(root, "rewrite/reports/legacy-skill-parameters-20261021");
        string run = Path.Combine(report, "import-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        string data = Path.Combine(root, "rewrite/data"), example = Path.Combine(root, "rewrite/mod-kit/examples/legacy-skill-parameters");
        var baseline = new ModBase(p => File.ReadAllBytes(p.StartsWith("frontend/", StringComparison.Ordinal)
            ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", p[9..]) : Path.Combine(data, p)),
            Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')));
        string author = Path.Combine(run, "author");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(example, "rewrite"), "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(author, Path.GetRelativePath(example, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
        string parameterPath = Path.Combine(author, "rewrite/definitions/legacy-skill-parameters.json");
        var parameters = JsonNode.Parse(File.ReadAllBytes(parameterPath))!.AsObject();
        parameters["skills"]!["ew4.skill.26"]!["percent"] = 25;
        parameters["skills"]!["ew4.skill.6"]!["bonus"] = 4;
        File.WriteAllText(parameterPath, parameters.ToJsonString());
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        JsonElement Call(ModStore host, string route, object body)
        {
            using var response = JsonDocument.Parse(host.Dispatch("/api/" + route, JsonSerializer.Serialize(body)));
            Check(response.RootElement.GetProperty("ok").GetBoolean(), route + " succeeds");
            return response.RootElement.GetProperty("data").Clone();
        }
        Call(store, "start", new { scenario = "conquest1", country = "fra" });
        string originalSave = Path.Combine(run, "saves/saves/mode-2/slot-0.json");
        string originalAccount = Path.Combine(run, "saves/account.json");
        byte[] saveBytes = File.ReadAllBytes(originalSave), accountBytes = File.ReadAllBytes(originalAccount);
        store.Seal(author, "legacy-skill-parameters", "原版技能数值验证", "1.0", ["conquest1"]);
        Check(store.Validate(author).Files.Count == 2, "parameter-only package has no copied legacy resources");
        var imported = store.Import(author, "ignored", "ignored");
        store.Activate(imported.Key);
        var started = Call(store, "start", new { scenario = "conquest1", country = "fra" });
        var configured = baseline.Content(store.Read, ["conquest1"]);
        var rules = configured.Start("conquest1", "fra").Rules;
        Check(rules.Transport.Purchase.MoneyPercentWithSkill == 25 && rules.Commanders.LegacySkills!.Bonus(6) == 4, "installed package reaches actual scenario rules");
        Check(configured.Extensions.Bus.Skills.Count == 0, "parameter-only package leaves native skills active");
        Call(store, "save", new { mode = 2, slot = 1 });
        string oldSave = Path.Combine(run, "saves/mods", imported.Id, imported.LogicRevision, "saves/mode-2/slot-1.json");
        byte[] oldSaveBytes = File.ReadAllBytes(oldSave);
        var restored = Call(store, "load", new { mode = 2, slot = 1 });
        Check(restored.GetProperty("revision").GetInt64() == started.GetProperty("revision").GetInt64(), "actual client load with original skill configuration");
        string archive = Path.Combine(run, "legacy-skill-parameters.ew4mod"); store.Export(imported.Key, archive);
        var other = new ModStore(baseline, Path.Combine(run, "other-mods"), Path.Combine(run, "other-saves"));
        var copied = other.Import(archive, "ignored", "ignored");
        Check(copied.Key == imported.Key && copied.LogicRevision == imported.LogicRevision, "offline archive keeps parameter file and logic identity");
        other.Activate(copied.Key);
        Call(other, "start", new { scenario = "conquest1", country = "fra" });
        Check(baseline.Content(other.Read, ["conquest1"]).Extensions.LegacySkills!.Percent(26) == 25, "imported archive loads parameters independently");
        parameters["skills"]!["ew4.skill.26"]!["percent"] = 75;
        File.WriteAllText(parameterPath, parameters.ToJsonString());
        store.Seal(author, imported.Id, "原版技能数值验证", "1.1", ["conquest1"]);
        var changed = store.Import(author, "ignored", "ignored");
        Check(changed.LogicRevision != imported.LogicRevision, "a numeric edit changes logic revision");
        store.Activate(changed.Key);
        Call(store, "start", new { scenario = "conquest1", country = "fra" });
        Check(baseline.Content(store.Read, ["conquest1"]).Extensions.LegacySkills!.Percent(26) == 75, "edited JSON takes effect without recompilation");
        using (var missing = JsonDocument.Parse(store.Dispatch("/api/load", "{\"mode\":2,\"slot\":1}")))
            Check(!missing.RootElement.GetProperty("ok").GetBoolean(), "new rule version cannot reuse the old save directory");
        Check(File.ReadAllBytes(oldSave).SequenceEqual(oldSaveBytes), "old version save remains byte-for-byte intact");
        store.Activate(imported.Key);
        Call(store, "load", new { mode = 2, slot = 1 });
        Check(baseline.Content(store.Read, ["conquest1"]).Extensions.LegacySkills!.Percent(26) == 25, "switching back restores old parameter rules");
        store.Activate(null);
        Check(File.ReadAllBytes(originalSave).SequenceEqual(saveBytes) && File.ReadAllBytes(originalAccount).SequenceEqual(accountBytes), "builtin account and session untouched");
        Check(baseline.Content(store.Read, ["conquest1"]).Extensions.LegacySkills == null, "returning to builtin removes custom numeric context");
        parameters["skills"]!["ew4.skill.26"]!["percent"] = 101;
        File.WriteAllText(parameterPath, parameters.ToJsonString());
        store.Seal(author, imported.Id, "原版技能数值验证", "bad", ["conquest1"]);
        bool rejected = false;
        try { store.Validate(author); } catch (Ew4.Core.RuleViolation) { rejected = true; }
        Check(rejected, "real package validation rejects invalid skill values");
        File.WriteAllText(Path.Combine(report, "import-checks.json"), JsonSerializer.Serialize(new { run, archive, checks, errors = 0,
            originalLogicRevision = imported.LogicRevision, changedLogicRevision = changed.LogicRevision }));
        return checks;
    }
}
