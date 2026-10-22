using System.Text;
using System.Text.Json;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;

internal static class CommanderDialogueChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new InvalidOperationException("Commander dialogues: " + label); }
        void Reject(Action action, string label)
        {
            try { action(); } catch (Exception e) when (e is RuleViolation or JsonException or FileNotFoundException or DirectoryNotFoundException) { checks++; return; }
            throw new InvalidOperationException("Commander dialogues accepted " + label);
        }
        const string path = "rewrite/presentation/commander-dialogues.json";
        string example = Path.Combine(root, "rewrite/mod-kit/examples/commander-dialogues");
        var files = Directory.EnumerateFiles(Path.Combine(example, "rewrite"), "*", SearchOption.AllDirectories)
            .ToDictionary(p => Path.GetRelativePath(example, p).Replace('\\', '/'), File.ReadAllBytes);
        byte[] Read(string p) => files.TryGetValue(p, out var value) ? value : File.ReadAllBytes(Path.Combine(root, "rewrite/data",
            p.StartsWith("data/", StringComparison.Ordinal) ? "legacy/" + p[5..] : p));
        var loaded = ExtensionLoader.Load(Read);
        var original = files[path];
        var entry = loaded.CommanderDialogues["ew4.commander.1"];
        Check(entry.Kill.Length == 2 && entry.Occupy.Length == 0 && entry.Retreat.Length == 0, "example loaded with reserved arrays");
        var absent = ExtensionLoader.Load(_ => throw new FileNotFoundException());
        Check(!absent.Enabled && absent.CommanderDialogues.Count == 0, "absent extension preserves defaults and fingerprint");
        void Put(string json) => files[path] = Encoding.UTF8.GetBytes(json);
        foreach (string invalid in new[] {
            """{"schema":2,"commanders":{}}""", """{"schema":1}""", """{"schema":1,"commanders":null}""",
            """{"schema":1,"commanders":{"ew4.commander.1":null}}""",
            """{"schema":1,"commanders":{"ew4.commander.1":{"kill":null}}}""",
            """{"schema":1,"commanders":{"ew4.commander.1":{"kill":[" "]}}}""",
            """{"schema":1,"commanders":{"ew4.commander.1":{"kill":[null]}}}""",
            """{"schema":1,"commanders":{"ew4.commander.1":{"kills":["line"]}}}""",
            """{"schema":1,"commanders":{"ew4.commander.1":{},"ew4.commander.1":{}}}""",
            """{"schema":1,"commanders":{},"typo":true}""" })
        { Put(invalid); Reject(() => ExtensionLoader.Load(Read), invalid); }
        foreach (var lines in new[] { new[] { new string('x', 2001) }, Enumerable.Repeat("line", 129).ToArray() })
        {
            files[path] = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, commanders = new Dictionary<string, object> { ["ew4.commander.1"] = new { kill = lines } } });
            Reject(() => ExtensionLoader.Load(Read), "line length/count bounds");
        }
        Put("""{"schema":1,"commanders":{"ew4.commander.1":{}}}""");
        Check(ExtensionLoader.Load(Read).CommanderDialogues["ew4.commander.1"].Kill.Length == 0, "omitted event inherits generic lines");
        Check(ExtensionLoader.Load(Read).Revision != loaded.Revision, "dialogue file contributes to extension revision");
        Put("""{"schema":1,"commanders":{"ew4.commander.1":{"kill":["one"],"occupy":["two"],"retreat":["three"]}}}""");
        Check(ExtensionLoader.Load(Read).CommanderDialogues["ew4.commander.1"].Retreat.Single() == "three", "all event arrays accepted");
        byte[] manifest = files["rewrite/extension.json"];
        files["rewrite/extension.json"] = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, apiVersion = 1, commanderDialogues = path });
        Reject(() => ExtensionLoader.Load(Read), "API 1");
        files["rewrite/extension.json"] = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, apiVersion = 2, commanderDialogues = "rewrite/../outside.json" });
        Reject(() => ExtensionLoader.Load(Read), "unsafe path");
        files["rewrite/extension.json"] = manifest;
        files.Remove(path); Reject(() => ExtensionLoader.Load(Read), "missing registered file");
        files[path] = original;
        void RuleFile(object rules) => files[path] = JsonSerializer.SerializeToUtf8Bytes(new {
            schema = 1, commanders = new Dictionary<string, object> { ["ew4.commander.1"] = new { killRules = rules } } });
        foreach (object invalid in new object[] {
            new object?[] { null },
            new[] { new { id = "a", selection = "last", lines = new[] { "line" } } },
            new[] { new { id = "a", chance = 101, lines = new[] { "line" } } },
            new[] { new { id = "a", chance = -1, lines = new[] { "line" } } },
            new[] { new { id = "a", lines = Array.Empty<string>() } },
            new[] { new { id = "a", when = (object?)null, lines = new[] { "line" } } },
            new[] { new { id = "a", when = new { minRound = 5, maxRound = 4 }, lines = new[] { "line" } } },
            new[] { new { id = "a", when = new { killerHealthPercentAtMost = 101 }, lines = new[] { "line" } } },
            new[] { new { id = "a", when = new { victimCategories = new[] { "typo" } }, lines = new[] { "line" } } },
            new[] { new { id = "a", when = new { victimCommanders = new[] { "ew4.commander.1", "ew4.commander.1" } }, lines = new[] { "line" } } },
            new[] { new { id = "a", when = new { victimHasCommander = false, victimCommanders = new[] { "ew4.commander.1" } }, lines = new[] { "line" } } },
            new[] { new { id = "a", lines = new[] { "one" } }, new { id = "a", lines = new[] { "two" } } } })
        { RuleFile(invalid); Reject(() => ExtensionLoader.Load(Read), "invalid conditional rule"); }
        RuleFile(new[] { new { id = "a", lines = new[] { "line" } } });
        var defaultRule = ExtensionLoader.Load(Read).CommanderDialogues["ew4.commander.1"].KillRules.Single();
        Check(defaultRule.Chance == 100 && defaultRule.Selection == "random" && defaultRule.When.MinRound == 1, "rule defaults");
        files[path] = original;
        var content = new ClientContent(Read, ["conquest1.btl"]);
        var host = new ClientGame(content, Path.Combine(root, "rewrite/reports/commander-dialogues-20261021/client-" + Guid.NewGuid().ToString("N")));
        JsonElement Call(string route, object? command = null)
        {
            using var result = JsonDocument.Parse(host.Dispatch("/api/" + route, JsonSerializer.Serialize(command, ClientGame.Json)));
            Check(result.RootElement.GetProperty("ok").GetBoolean(), route + " succeeds");
            return result.RootElement.GetProperty("data").Clone();
        }
        var catalog = Call("catalog");
        Check(catalog.GetProperty("commanderDialogues").GetProperty("ew4.commander.1").GetProperty("kill")[0].GetString() == entry.Kill[0], "API forwards configured lines");
        var view = Call("start", new { scenario = "conquest1", country = "fra" });
        foreach (var unit in view.GetProperty("units").EnumerateArray().Where(u => u.GetProperty("commanderId").ValueKind != JsonValueKind.Null))
        {
            string id = unit.GetProperty("id").GetString()!;
            int index = int.Parse(id[(id.LastIndexOf('.') + 1)..]);
            int legacyId = content.Battle("conquest1").Armies[index].Commander;
            Check(unit.GetProperty("commanderDefinitionId").GetString() == content.Catalogs.Runtime.Commanders[legacyId].Definition.Id, "scenario commander retains definition identity");
        }
        Check(view.GetProperty("units").EnumerateArray().Where(u => u.GetProperty("commanderId").ValueKind == JsonValueKind.Null)
            .All(u => u.GetProperty("commanderDefinitionId").ValueKind == JsonValueKind.Null), "ordinary units have no commander definition");
        Put("""{"schema":1,"commanders":{"ew4.commander.9999999":{"kill":["line"]}}}""");
        Reject(() => ExtensionLoader.Load(Read).ConfigureRuntime(content.Catalogs.Runtime), "unknown commander definition");
        RuleFile(new[] { new { id = "a", when = new { victimCommanders = new[] { "ew4.commander.9999999" } }, lines = new[] { "line" } } });
        Reject(() => ExtensionLoader.Load(Read).ConfigureRuntime(content.Catalogs.Runtime), "unknown victim commander definition");
        Check(loaded.CommanderDialogues["ew4.commander.1"].Kill.SequenceEqual(entry.Kill), "separate configurations stay isolated");
        return checks;
    }
}
