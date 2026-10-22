using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Mods;
using Ew4.Persistence;

internal static class CountryChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { if (!pass) throw new Exception("Countries: " + label); checks++; }
        void Reject(Action action, string label)
        {
            try { action(); } catch (Exception e) when (e is RuleViolation or JsonException or InvalidDataException) { checks++; return; }
            throw new Exception("Countries accepted: " + label);
        }
        string data = Path.Combine(root, "rewrite/data"), run = Path.Combine(root, "build/unit-visuals-20261021/countries-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        byte[] Base(string path) => File.ReadAllBytes(path.StartsWith("frontend/", StringComparison.Ordinal)
            ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", path[9..]) : Path.Combine(data, path));
        var overrides = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        byte[] Read(string path) => overrides.TryGetValue(path, out var bytes) ? bytes : Base(path.StartsWith("data/", StringComparison.Ordinal) ? "legacy/" + path[5..] : path);
        void Put(string path, object value) => overrides[path] = JsonSerializer.SerializeToUtf8Bytes(value, ExtensionLoader.Json);
        var registry = CountryCatalog.Load(Read);
        Check(registry.Countries.Count == 43, "43 base country identities");
        var content = new ClientContent(Read, ["conquest1.btl"]);
        var original = content.Start("conquest1", "fra");
        byte[] WithoutRegistry(string path) => path == "client/countries.json" ? throw new FileNotFoundException(path) : Read(path);
        Check(new ClientContent(WithoutRegistry, ["conquest1.btl"]).Start("conquest1", "fra").Session.World.ContentRevision == original.Session.World.ContentRevision,
            "base registry preserves original save fingerprint");
        foreach (string path in Directory.EnumerateFiles(Path.Combine(data, "original"), "*.btl"))
        {
            var b = LegacyBattle.Read(File.ReadAllBytes(path));
            // Only differences that actually change native combat resolution are sent to startup.
            var variants = b.Factions.Where(f => registry.Get(f.CountryId)?.UnitVariant is string v && v != f.Variant
                && !(v == "others" && !content.Catalogs.Runtime.Variants.ContainsKey(f.Variant)))
                .GroupBy(f => f.CountryId).ToDictionary(g => g.Key, g => registry.Get(g.Key)!.UnitVariant!);
            Check(variants.Count == 0, "base registry preserves scenario combat selection: " + Path.GetFileName(path));
        }
        var raw = LegacyBattle.Read(Base("original/conquest1.btl"));
        string fullId = "mod/国家势力：法兰西 第①共和国/" + new string('x', 160);
        var countries = JsonNode.Parse(Base("client/countries.json"))!["countries"]!.AsObject();
        countries[fullId] = countries["fra"]!.DeepClone(); countries.Remove("fra");
        countries[fullId]!["name"] = "独立配置共和国";
        countries[fullId]!["flag"] = "rus";
        countries[fullId]!["visualVariant"] = "hre";
        countries["__proto__"] = countries[fullId]!.DeepClone();
        Put("client/countries.json", new { schema = 1, countries, aliases = new Dictionary<string, string> { ["fra"] = fullId } });
        content = new ClientContent(Read, ["conquest1.btl"]);
        var aliased = content.Start("conquest1", fullId);
        Check(aliased.Session.World.Factions.Select(f => f.Id).SequenceEqual(original.Session.World.Factions.Select(f => f.Id)), "alias preserves runtime identities and order");
        Check(aliased.Session.World.ContentRevision != original.Session.World.ContentRevision, "explicit country name edits bind a new content revision");
        var host = new ClientGame(content, Path.Combine(run, "client"));
        JsonNode Call(string route, object body)
        {
            var result = JsonNode.Parse(host.Dispatch("/api/" + route, JsonSerializer.Serialize(body)))!;
            Check((bool?)result["ok"] == true, route + ": " + result.ToJsonString()[..Math.Min(400, result.ToJsonString().Length)]);
            return result["data"]!;
        }
        Check(Call("countries", new { scenario = "conquest1" }).AsArray().Any(f => (string?)f!["id"] == fullId), "arbitrary ID exposed in selection");
        Check(Call("local-players", new { scenario = "conquest1", country = fullId }).AsArray().Any(f => (string?)f!["country"] == fullId), "local selection resolves canonical ID");
        var view = Call("start", new { scenario = "conquest1", country = fullId });
        var player = view["factions"]!.AsArray().Single(f => (bool)f!["human"]!)!;
        Check((string?)player["country"] == fullId && (string?)player["flag"] == "rus" && (string?)player["visualVariant"] == "hre", "country flag and models independent of ID");
        Call("save", new { mode = 2, slot = 1 }); view = Call("load", new { mode = 2, slot = 1 });
        Check((string?)view["startCountry"] == fullId, "long Unicode ID survives save and load");
        var reverse = new JsonObject(countries.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
        Put("client/countries.json", new { schema = 1, countries = reverse, aliases = new Dictionary<string, string> { ["fra"] = fullId } });
        content = new ClientContent(Read, ["conquest1.btl"]);
        Check(content.Start("conquest1", fullId).Session.World.Factions.Select(f => f.Id).SequenceEqual(aliased.Session.World.Factions.Select(f => f.Id)), "dictionary reorder and unused entry preserve order");
        Check(content.Start("conquest1", fullId).Session.World.ContentRevision == aliased.Session.World.ContentRevision, "dictionary order does not change world fingerprint");
        // CNTR embeds full strings in the file, retaining the complete original body.
        var ids = raw.Factions.Select(f => f.CountryId == "fra" ? fullId : f.CountryId).ToArray();
        var cntr = new LegacyCountryIds(1, Convert.ToHexStringLower(SHA256.HashData(raw.ToLegacyBytes())), ids);
        var embeddedBytes = raw.WithCountryIds(cntr);
        var embedded = LegacyBattle.Read(embeddedBytes);
        Check(embedded.ToLegacyBytes().SequenceEqual(raw.ToLegacyBytes()), "CNTR never rewrites legacy body");
        Check(embedded.Factions.Select(f => f.CountryId).SequenceEqual(ids), "CNTR reads arbitrary IDs without truncation");
        Check(embedded.WithCountryIds(null).SequenceEqual(raw.ToBytes()), "CNTR strip restores original bytes");
        Check(LegacyBattle.Read(embedded.WithCountryIds(cntr)).ToBytes().SequenceEqual(embeddedBytes), "CNTR replace roundtrips");
        Put("client/countries.json", new { schema = 1, countries = reverse }); // Embedded IDs need no external aliases.
        overrides["original/conquest1.btl"] = embeddedBytes;
        content = new ClientContent(Read, ["conquest1.btl"]);
        var started = content.Start("conquest1", fullId);
        Check(started.Session.World.Factions.Select(f => f.Id).SequenceEqual(raw.Factions.Select((f, i) => LegacyScenarioInitialization.FactionId("conquest1", i, f with { CountryId = ids[i] })).OrderBy(id => id == started.Session.PlayerFaction ? 0 : 1)), "CNTR participant order preserved");
        host = new ClientGame(content, Path.Combine(run, "embedded-client"));
        view = Call("start", new { scenario = "conquest1", country = fullId });
        Call("save", new { mode = 2, slot = 1 }); view = Call("load", new { mode = 2, slot = 1 });
        Check(view["factions"]!.AsArray().Any(f => (string?)f!["country"] == fullId), "embedded country restores from actual client save");
        var victory = new ExtensionVictoryRules { Win = new() { Type = "round_reached", Round = 999 } };
        var withVictory = LegacyBattle.Read(embedded.WithVictory(victory));
        Check(withVictory.CountryIds != null && withVictory.Victory != null, "CNTR and VICT coexist");
        Check(LegacyBattle.Read(withVictory.WithCountryIds(null)).Victory != null, "CNTR strip preserves VICT");
        var juriCells = new List<int>();
        for (int y = 0; y < raw.Height; y++) for (int x = 0; x < raw.Width; x++)
            juriCells.Add(original.Session.World.Jurisdictions!.CellCenters[(raw.Top + y) * original.Session.World.Width + raw.Left + x]);
        var juri = new LegacyJurisdictionData(1, raw.MapId, original.Session.World.Width, original.Session.World.Height,
            raw.Left, raw.Top, raw.Width, raw.Height, cntr.BaseSha256, juriCells);
        var allBlocks = LegacyBattle.Read(withVictory.WithJurisdictions(juri));
        Check(allBlocks.Jurisdictions != null && allBlocks.Victory != null && allBlocks.CountryIds != null, "CNTR JURI VICT coexist");
        Check(LegacyBattle.Read(allBlocks.WithJurisdictions(null)).CountryIds != null, "JURI strip preserves CNTR");
        Check(LegacyBattle.Read(allBlocks.WithCountryIds(null)).Jurisdictions != null, "CNTR strip preserves JURI");
        string[] turnIds = started.Session.World.Factions.Select(f => f.Id).ToArray();
        var local = content.Start("conquest1", fullId, localPlayers: [turnIds[0], turnIds[2]]).Session;
        local = local with { BattleId = "country-order-check", World = local.World with {
            ScenarioVictory = victory, Factions = local.World.Factions.Select(f => f.Id == turnIds[1] ? f with { Camp = "ew4.camp.4" } : f).ToArray() } };
        JsonNode Skip(bool batch)
        {
            string folder = Path.Combine(run, batch ? "batch" : "ordinary"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "slot-1.json"), JsonSerializer.SerializeToUtf8Bytes(new ClientSlot("conquest1", fullId,
                SessionSave.Encode(local), LocalPlayers: local.World.LocalMultiplayer!.HumanFactions), ClientGame.Json));
            host = new ClientGame(content, folder);
            var state = Call("load", new { slot = 1 });
            state = Call("action", new { op = "end", revision = (long)state["revision"]! });
            Check((string?)state["activeFaction"] == turnIds[1], "next AI retains ordered seat");
            state = Call("action", new { op = batch ? "ai_batch" : "ai", revision = (long)state["revision"]! });
            return batch ? state["state"]! : state;
        }
        var normal = Skip(false); var batched = Skip(true);
        foreach (string field in new[] { "round", "activeFaction", "playerFaction", "factions", "units" })
            Check(JsonNode.DeepEquals(normal[field], batched[field]), "normal and skip match " + field);
        var combatCountry = (JsonObject)reverse.DeepClone(); combatCountry[fullId]!["unitVariant"] = "rus";
        Put("client/countries.json", new { schema = 1, countries = combatCountry });
        var combatContent = new ClientContent(Read, ["conquest1.btl"]);
        var changedCombat = combatContent.Start("conquest1", fullId);
        foreach (var unit in changedCombat.Session.World.Units.Where(u => u.Owner == changedCombat.Session.PlayerFaction))
        {
            int index = int.Parse(unit.Id[(unit.Id.LastIndexOf('.') + 1)..]); var source = raw.Armies[index];
            Check(unit.Combatant.Profile.Id == combatContent.Catalogs.Runtime.SerializedUnit("rus", source.Unit, source.Grade).Profile.Id,
                "country combat override applies to initial army " + index);
        }
        Check(combatContent.Country(fullId)!.VisualVariant == "hre" && combatContent.Country(fullId)!.Flag == "rus", "combat override keeps independent visuals and flag");
        Put("client/countries.json", new { schema = 1, countries = reverse });
        Reject(() => raw.WithCountryIds(cntr with { Factions = ["only-one"] }), "incomplete faction array");
        Reject(() => raw.WithCountryIds(cntr with { Factions = ids.Select(_ => "same").ToArray() }), "duplicate ID");
        Reject(() => raw.WithCountryIds(cntr with { BaseSha256 = new string('0', 64) }), "stale source hash");
        Reject(() => raw.WithCountryIds(cntr with { EventCountries = new Dictionary<int, string> { [-1] = fullId } }), "bad event row");
        var corrupted = (byte[])embeddedBytes.Clone(); corrupted[72] ^= 1;
        Reject(() => LegacyBattle.Read(corrupted), "changed body after extension");
        var version = (byte[])embeddedBytes.Clone(); BitConverter.GetBytes(2u).CopyTo(version, raw.LegacyLength + 20);
        Reject(() => LegacyBattle.Read(version), "unsupported CNTR version");
        Reject(() => LegacyBattle.Read(embeddedBytes.AsSpan(0, embeddedBytes.Length - 1)), "truncated CNTR");
        var duplicate = embeddedBytes.Concat(embeddedBytes.AsSpan(raw.LegacyLength + 16).ToArray()).ToArray();
        BitConverter.GetBytes(2u).CopyTo(duplicate, raw.LegacyLength + 12);
        Reject(() => LegacyBattle.Read(duplicate), "duplicate CNTR blocks");
        // Native event records referencing the short country follow the embedded ID.
        string? eventFile = Directory.EnumerateFiles(Path.Combine(data, "original"), "*.btl").FirstOrDefault(p => {
            var b = LegacyBattle.Read(File.ReadAllBytes(p)); return b.Events.Any(e => b.Factions.Any(f => f.CountryId == e.Country)); });
        Check(eventFile != null, "real native event fixture exists");
        var eventBattle = LegacyBattle.Read(File.ReadAllBytes(eventFile!));
        var eventIds = eventBattle.Factions.Select((f, i) => "full/country/" + i).ToArray();
        var eventData = new LegacyCountryIds(1, Convert.ToHexStringLower(SHA256.HashData(eventBattle.ToLegacyBytes())), eventIds);
        var mappedEvents = LegacyBattle.Read(eventBattle.WithCountryIds(eventData));
        for (int i = 0; i < eventBattle.Events.Count; i++)
        {
            int index = eventBattle.Factions.ToList().FindIndex(f => f.CountryId == eventBattle.Events[i].Country);
            if (index >= 0) Check(mappedEvents.Events[i].Country == eventIds[index], "native event maps full country ID at row " + i);
        }
        // Event-created factions reuse the global registry and append to saved order.
        Put("rewrite/extension.json", new { schema = 1, apiVersion = 1, scenarioEvents = "rewrite/events.json" });
        Put("rewrite/events.json", new Dictionary<string, ScriptedScenarioEvent[]> { ["conquest1"] = [new() {
            Id = "country-create", Effects = [new() { Type = "faction_create", Faction = "new.instance", Country = fullId, TemplateFaction = "@player", Camp = "new.camp" }] }] });
        content = new ClientContent(Read, ["conquest1.btl"]);
        var created = content.Start("conquest1", fullId);
        Check(created.Session.World.Factions[^1].Id == "new.instance" && created.Session.World.Factions[^1].Name == "独立配置共和国", "new country event appended with registry defaults");
        host = new ClientGame(content, Path.Combine(run, "created-client"));
        view = Call("start", new { scenario = "conquest1", country = fullId });
        Call("save", new { mode = 2, slot = 1 }); view = Call("load", new { mode = 2, slot = 1 });
        Check((string?)view["factions"]!.AsArray().Single(f => (string?)f!["id"] == "new.instance")!["country"] == fullId, "created country reference survives save");
        overrides.Remove("rewrite/extension.json"); overrides.Remove("rewrite/events.json");
        // Global IDs are strings; appearance/variant resources remain explicitly separate.
        var good = overrides["client/countries.json"];
        overrides["client/countries.json"] = Encoding.UTF8.GetBytes("{\"schema\":1,\"countries\":{\"x\":{\"name\":\"X\"},\"x\":{\"name\":\"Y\"}}}");
        Reject(() => CountryCatalog.Load(Read), "duplicate JSON key");
        Put("client/countries.json", new { schema = 1, countries = reverse, aliases = new Dictionary<string, string> { ["bad"] = "missing" } });
        Reject(() => CountryCatalog.Load(Read), "missing alias target");
        Put("client/countries.json", new { schema = 1, countries = new Dictionary<string, CountryDefinition> { ["invalid\nID"] = new("Invalid") } });
        Reject(() => CountryCatalog.Load(Read), "control character");
        Put("client/countries.json", new { schema = 1, countries = new Dictionary<string, CountryDefinition> { ["country"] = new("Country", UnitVariant: "undefined") } });
        Reject(() => CountryCatalog.Load(Read).ValidateRuntime(content.Catalogs.Runtime), "undefined combat variant");
        Put("client/countries.json", new { schema = 1, countries = reverse, sourceSha256 = new Dictionary<string, string> { ["def_army.xml"] = new string('0', 64) } });
        Check(CountryCatalog.Load(Read).Countries.Count == 0, "changed legacy sources do not inherit original country defaults");
        overrides["client/countries.json"] = good;
        Put("client/countries.json", new { schema = 1, countries = new Dictionary<string, CountryDefinition> { ["Country"] = new("Upper"), ["country"] = new("Lower") } });
        Check(CountryCatalog.Load(Read).Countries.Count == 2, "IDs use exact case-sensitive uniqueness");
        overrides["client/countries.json"] = Encoding.UTF8.GetBytes("{\"schema\":1,\"Schema\":1,\"countries\":{}}");
        Reject(() => CountryCatalog.Load(Read), "unknown/miscased schema field");
        overrides["client/countries.json"] = good;
        var baseline = new ModBase(Base, Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')));
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        string author = Path.Combine(run, "author"); Directory.CreateDirectory(Path.Combine(author, "client")); Directory.CreateDirectory(Path.Combine(author, "original"));
        var packagedCountries = (JsonObject)reverse.DeepClone();
        packagedCountries[fullId]!.AsObject().Remove("flag");
        packagedCountries[fullId]!["flagImage"] = "rewrite/images/country-check.png";
        File.WriteAllBytes(Path.Combine(author, "client/countries.json"), JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, countries = packagedCountries }));
        Directory.CreateDirectory(Path.Combine(author, "rewrite/images"));
        File.WriteAllBytes(Path.Combine(author, "rewrite/images/country-check.png"), Base("client/ui/image_flag_hd/rus1.png"));
        File.WriteAllBytes(Path.Combine(author, "original/conquest1.btl"), embeddedBytes);
        store.Seal(author, "country-check", "Countries", "1.0", ["conquest1"]);
        var manifest = store.Validate(author);
        Check(manifest.Files.Count == 3, "registry CNTR and custom flag package validates");
        var installed = store.Import(author, "ignored", "ignored"); store.Activate(installed.Key);
        var response = JsonNode.Parse(store.Dispatch("/api/start", JsonSerializer.Serialize(new { scenario = "conquest1", country = fullId })))!;
        Check((bool?)response["ok"] == true, "installed package starts arbitrary embedded country");
        string archive = Path.Combine(run, "countries.ew4mod"); store.Export(installed.Key, archive);
        Check(store.Validate(archive).Files.Count == 3, "offline archive preserves CNTR registry and custom flag");
        reverse[fullId]!["name"] = "Version two";
        File.WriteAllBytes(Path.Combine(author, "client/countries.json"), JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, countries = reverse }));
        store.Seal(author, "country-check", "Countries", "2.0", ["conquest1"]);
        Check(store.Validate(author).LogicRevision != manifest.LogicRevision, "country configuration changes save content version");
        string report = Path.Combine(root, "rewrite/reports/unit-visuals-20261021/country-tests.json");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(new { checks, errors = 0, run, fullId, archive }));
        return checks;
    }
}
