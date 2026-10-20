using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Client;
using Ew4.Core;
using Ew4.Mods;

internal static class ExtensionModChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception("Extension import: " + message); checks++; }
        string data = Path.Combine(root, "rewrite/data"), report = Path.Combine(root, "rewrite/reports/extensions-20261020");
        string run = Path.Combine(report, "import-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(run);
        var baseline = new ModBase(p => File.ReadAllBytes(p.StartsWith("frontend/") ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", p[9..]) : Path.Combine(data, p)),
            Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')));
        string raw = Path.Combine(run, "raw");Directory.CreateDirectory(raw);
        foreach (string file in Directory.EnumerateFiles(Path.Combine(report, "example"), "*", SearchOption.AllDirectories))
        { string target = Path.Combine(raw, Path.GetRelativePath(Path.Combine(report, "example"), file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file, target); }
        File.WriteAllBytes(Path.Combine(raw, "conquest1.btl"), baseline.Read("original/conquest1.btl"));
        string icon = "rewrite/presentation/icons/skill.png", iconPath = Path.Combine(raw, icon); Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
        var iconBytes = baseline.Read("client/ui/image_ui_hd/skill_01.png");File.WriteAllBytes(iconPath, iconBytes);
        string skillFile = Path.Combine(raw, "rewrite/definitions/skills.json");
        var skills = JsonNode.Parse(File.ReadAllBytes(skillFile))!.AsArray();
        foreach (var s in skills) { s!["icon"] = icon; s["descriptionKey"] = "测试说明 / Extension description"; }
        File.WriteAllText(skillFile, skills.ToJsonString());
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        string Start() => store.Dispatch("/api/start", "{\"scenario\":\"conquest1\",\"country\":\"fra\"}");
        using var builtin = JsonDocument.Parse(Start());Check(builtin.RootElement.GetProperty("ok").GetBoolean(), "builtin start");
        var savePath = Path.Combine(run, "saves/saves/mode-2/slot-0.json");var originalSave = File.ReadAllBytes(savePath);
        var imported = store.Import(raw, "extension-test", "技能扩展验证");
        Check(imported.Files > 5 && store.List().Length == 1, "raw legacy plus extension import");
        Check(store.Read("original/conquest1.btl").SequenceEqual(baseline.Read("original/conquest1.btl")), "import leaves builtin active");
        store.Activate(imported.Key);
        Check(store.ReadContent("extensions/presentation/icons/skill.png").SequenceEqual(iconBytes), "extension icon route");
        Check(store.ReadContent("r-" + imported.Key + "/extensions/presentation/icons/skill.png").SequenceEqual(iconBytes), "versioned icon route");
        using var started = JsonDocument.Parse(Start());Check(started.RootElement.GetProperty("ok").GetBoolean(), "actual API starts extended scenario");
        var units = started.RootElement.GetProperty("data").GetProperty("units");
        var unit = units.EnumerateArray().First(u => u.GetProperty("skills").GetArrayLength() > 0 && u.GetProperty("owner").GetString() == started.RootElement.GetProperty("data").GetProperty("playerFaction").GetString());
        string unitId = unit.GetProperty("id").GetString()!;int cell = unit.GetProperty("cell").GetInt32();
        long revision = started.RootElement.GetProperty("data").GetProperty("revision").GetInt64();
        using var selected = JsonDocument.Parse(store.Dispatch("/api/action", JsonSerializer.Serialize(new { op = "select", cell, revision })));
        Check(selected.RootElement.GetProperty("data").GetProperty("skills").GetArrayLength() == 1, "active skills advertised");
        using var used = JsonDocument.Parse(store.Dispatch("/api/action", JsonSerializer.Serialize(new { op = "skill", unit = unitId, offer = "heal", revision })));
        Check(used.RootElement.GetProperty("ok").GetBoolean(), "active skill through API");
        revision = used.RootElement.GetProperty("data").GetProperty("revision").GetInt64();
        using var repeated = JsonDocument.Parse(store.Dispatch("/api/action", JsonSerializer.Serialize(new { op = "skill", unit = unitId, offer = "heal", revision })));
        Check(!repeated.RootElement.GetProperty("ok").GetBoolean(), "API refuses cooldown");
        store.Dispatch("/api/save", "{\"mode\":2,\"slot\":1}");
        using var loaded = JsonDocument.Parse(store.Dispatch("/api/load", "{\"mode\":2,\"slot\":1}"));
        Check(loaded.RootElement.GetProperty("ok").GetBoolean(), "extended session save reload");
        using var reselected = JsonDocument.Parse(store.Dispatch("/api/action", JsonSerializer.Serialize(new { op = "select", cell, revision })));
        Check(reselected.RootElement.GetProperty("data").GetProperty("skills")[0].GetProperty("remainingCooldown").GetInt64() > 0, "cooldown survives actual load");
        using var hq = JsonDocument.Parse(store.Dispatch("/api/headquarters", "{}"));
        Check(hq.RootElement.GetProperty("data").GetProperty("definitions").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "ew4.commander.1").GetProperty("skills").GetArrayLength() == 7, "seven skills in headquarters API");
        string sealedFile = Path.Combine(run, "extension.ew4mod");store.Export(imported.Key, sealedFile);
        var other = new ModStore(baseline, Path.Combine(run, "other-mods"), Path.Combine(run, "other-saves"));
        Check(other.Import(sealedFile, "ignored", "ignored").Key == imported.Key, "sealed offline package roundtrip");
        string archive = Path.Combine(run, "assets.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) foreach (string f in Directory.EnumerateFiles(raw, "*", SearchOption.AllDirectories))
            zip.CreateEntryFromFile(f, "assets/" + Path.GetRelativePath(raw, f).Replace('\\', '/'));
        Check(other.Import(archive, "extension-test", "技能扩展验证").LogicRevision == imported.LogicRevision, "assets ZIP retains scripts");
        skills[0]!["effects"]![0]!["value"] = 3;File.WriteAllText(skillFile, skills.ToJsonString());
        var changed = other.Import(raw, "extension-test", "技能扩展验证");
        Check(changed.LogicRevision != imported.LogicRevision, "code/config change isolates content identity");
        store.Activate(null);Check(File.ReadAllBytes(savePath).SequenceEqual(originalSave), "builtin save unchanged");
        using var baseHeadquarters = JsonDocument.Parse(store.Dispatch("/api/headquarters", "{}"));
        Check(baseHeadquarters.RootElement.GetProperty("data").GetProperty("definitions").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "ew4.commander.1").GetProperty("skills").GetArrayLength() <= 4, "builtin commander restored");
        Check(store.ReadContent("r-" + imported.Key + "/extensions/presentation/icons/skill.png").SequenceEqual(iconBytes), "old in-flight icon request stays scoped");
        File.WriteAllText(Path.Combine(report, "latest-import.json"), JsonSerializer.Serialize(new { run, archive, sealedFile, imported.Key, imported.LogicRevision, unitId, cell, checks, errors = 0 }));
        return checks;
    }
}
