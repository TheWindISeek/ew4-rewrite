using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Ew4.Client;
using Ew4.Core;
using Ew4.Mods;
using Ew4.Persistence;

internal static class RealApkChecks
{
    public static void Run(string root, string[] filters)
    {
        string report = Path.Combine(root, "rewrite/reports/real-mod-compat-20261019", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(report);
        string data = Path.Combine(root, "rewrite/data");
        var baseline = new ModBase(p => File.ReadAllBytes(p.StartsWith("frontend/", StringComparison.Ordinal)
                ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", p[9..]) : Path.Combine(data, p)),
            Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/'))
                .Concat(["frontend/conquest-pages.json", "frontend/city-names.json"]));
        var results = new List<object>();
        int index = 0, failures = 0;
        foreach (string apk in Directory.EnumerateFiles(root).Where(p => Path.GetExtension(p).Equals(".apk", StringComparison.OrdinalIgnoreCase))
            .Where(p => filters.Length == 0 || filters.Any(f => Path.GetFileName(p).Contains(f, StringComparison.OrdinalIgnoreCase))).Order())
        {
            string id = "real-" + (++index);
            string folder = Path.Combine(report, id);
            Directory.CreateDirectory(folder);
            string stage = "import", detail = "";
            string? key = null;
            var watch = Stopwatch.StartNew();
            var checks = new List<string>();
            using var input = File.OpenRead(apk);
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(input));
            Console.WriteLine("Testing " + Path.GetFileName(apk));
            try
            {
                var store = new ModStore(baseline, Path.Combine(folder, "mods"), Path.Combine(folder, "saves"));
                var info = store.Import(apk, id, Path.GetFileNameWithoutExtension(apk), p => detail = p.Message);
                key = info.Key; checks.Add("import + all scenario validation");
                var graphics = ModFiles.Object(File.ReadAllBytes(Path.Combine(folder, "mods", info.Key, "client/manifest.json")));
                foreach (var (layout, output) in new[] { ("form_selcampaign", "campaign_background"), ("form_selconquest", "conquest_background") })
                {
                    var attributes = graphics["layouts"]![layout]!["attributes"]!;
                    var texture = graphics["menuTextures"]![(string)attributes["back"]!]!;
                    int ratio = (int)texture["ratio"]!;
                    string logical = "client/" + (string)texture["file"]!;
                    string installedTexture = Path.Combine(folder, "mods", info.Key, logical);
                    var raster = Raster.Decode(File.Exists(installedTexture) ? File.ReadAllBytes(installedTexture) : baseline.Read(logical));
                    int V(string field) => (int)(double.Parse((string?)attributes[field] ?? "0", System.Globalization.CultureInfo.InvariantCulture) * ratio);
                    var expected = raster.Crop(V("tx"), V("ty"), V("tw"), V("th"));
                    var actual = Raster.Decode(File.ReadAllBytes(Path.Combine(folder, "mods", info.Key, "client/ui/" + output + ".png")));
                    if (expected.Width != actual.Width || expected.Height != actual.Height || !expected.Pixels.SequenceEqual(actual.Pixels))
                        throw new InvalidDataException(layout + " does not match package layout background");
                }
                checks.Add("campaign/conquest backgrounds match layout texture and exact crop pixels");
                stage = "activate"; store.Activate(info.Key); checks.Add(stage);
                JsonNode Call(string route, object args)
                {
                    var json = JsonNode.Parse(store.Dispatch("/api/" + route, JsonSerializer.Serialize(args)))!;
                    if ((bool?)json["ok"] != true) throw new InvalidDataException(json.ToJsonString());
                    return json["data"]!;
                }
                stage = "catalog";
                var catalog = Call("catalog", new { });
                File.WriteAllText(Path.Combine(folder, "catalog.json"), catalog.ToJsonString());
                checks.Add(stage);
                var manifest = JsonSerializer.Deserialize<ModManifest>(File.ReadAllBytes(Path.Combine(folder, "mods", info.Key, "mod.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                var content = baseline.Content(store.Read, manifest.Scenarios);
                stage = "single-use weapons";
                int consumables = CheckConsumables(content);
                if (consumables > 0) checks.Add($"all {consumables} imported single-use profiles: full damage, consumption and save/load passed");
                stage = "scenario views";
                string viewSaves = Path.Combine(folder, "view-saves"); Directory.CreateDirectory(viewSaves);
                File.WriteAllText(Path.Combine(viewSaves, "account.json"), JsonSerializer.Serialize(new PlayerAccount
                {
                    Id = "view-check", ContentRevision = content.Catalogs.Accounts.SourceRevision, Medals = 20_000_000,
                    UnlockedCampaigns = content.Presentations.Values.Where(p => p.Hidden).Select(p => p.Id).ToArray()
                }, ClientGame.Json));
                var viewGame = new ClientGame(content, viewSaves);
                int views = 0;
                foreach (var scenario in content.Scenarios.Where(s => s.Mode != 3))
                {
                    var battle = content.Battle(scenario.Id);
                    var reply = JsonNode.Parse(viewGame.Dispatch("/api/start", JsonSerializer.Serialize(new { scenario = scenario.Id,
                        country = scenario.Mode == 2 ? battle.Factions.First(f => f.Camp != 4).CountryId : null })))!;
                    if ((bool?)reply["ok"] != true) throw new InvalidDataException(scenario.Id + ": " + reply.ToJsonString());
                    var viewed = reply["data"]!;
                    if ((string?)viewed["scenario"] != scenario.Id || viewed["cells"] is not JsonArray visibleCells || visibleCells.Count == 0)
                        throw new InvalidDataException("Empty scene view: " + scenario.Id);
                    views++;
                }
                checks.Add("all " + views + " playable scenario views passed (isolated test account unlocks hidden scenes)");
                foreach (int mode in new[] { 1, 2, 4 })
                {
                    var choice = content.Scenarios.FirstOrDefault(s => s.Mode == mode);
                    if (choice == null) continue;
                    stage = "play " + choice.Id;
                    string? country = mode == 2 ? content.Battle(choice.Id).Factions.First(f => f.Camp != 4).CountryId : null;
                    var state = Call("start", new { scenario = choice.Id, country });
                    if (state["units"] is not JsonArray units || units.Count == 0 || state["cells"] is not JsonArray cells || cells.Count == 0)
                        throw new InvalidDataException("Started scene is empty");
                    File.WriteAllText(Path.Combine(folder, choice.Id + "-state.json"), state.ToJsonString());
                    JsonNode Act(string op, string? unit = null, int cell = -1, string? target = null, string? offer = null)
                        => Call("action", new { op, revision = (long)state["revision"]!, unit, cell, target, offer });
                    void Dialogues()
                    {
                        for (int d = 0; state["dialogue"] != null && d < 64; d++) state = Act("dialogue");
                        if (state["dialogue"] != null) throw new InvalidDataException("Dialogue loop exceeds 64 acknowledgements");
                    }
                    Dialogues();
                    bool PlayerTurn() => (string?)state["activeFaction"] == (string?)state["playerFaction"] && (string?)state["phase"] == "Active" && (string?)state["outcome"] == "Continue";
                    foreach (string op in new[] { "attack", "move" })
                    {
                        bool exercised = false;
                        if (PlayerTurn()) foreach (var unit in state["units"]!.AsArray().Where(u => (string?)u!["owner"] == (string?)state["playerFaction"]).ToArray())
                        {
                            var selection = Act("select", cell: (int)unit!["cell"]!);
                            string field = op == "attack" ? "targets" : "range";
                            if (selection[field] is not JsonArray options || options.Count == 0) continue;
                            int cell = (int)options[0]!;
                            string? target = op == "attack" ? (string?)state["units"]!.AsArray().First(u => (int)u!["cell"]! == cell)!["id"] : null;
                            state = Act(op, (string)unit["id"]!, cell, target);
                            exercised = true; Dialogues(); break;
                        }
                        checks.Add(choice.Id + " " + op + (exercised ? " passed" : " skipped: no legal action at opening"));
                    }
                    bool recruited = false;
                    if (PlayerTurn()) foreach (var pair in state["cells"]!.AsArray().Select((c, i) => (c, i)).Where(p => (string?)p.c!["owner"] == (string?)state["playerFaction"] && p.c["buildingKind"] != null))
                    {
                        var selection = Act("select", cell: pair.i);
                        if (selection["offers"] is not JsonArray offers || offers.Count == 0) continue;
                        var resources = state["factions"]!.AsArray().First(f => (string?)f!["id"] == (string?)state["playerFaction"])!["resources"]!;
                        var affordable = offers.FirstOrDefault(o => new[] { "money", "industry", "food" }.All(k => (long)o!["cost"]![k]! <= (long)resources[k]!));
                        if (affordable == null) continue;
                        state = Act("recruit", cell: pair.i, offer: (string)affordable["id"]!); recruited = true; break;
                    }
                    checks.Add(choice.Id + " recruit " + (recruited ? "passed" : "skipped: no affordable opening offer"));
                    if (PlayerTurn())
                    {
                        state = Act("end"); Dialogues(); checks.Add(choice.Id + " end turn passed");
                        if ((string?)state["activeFaction"] != (string?)state["playerFaction"] && (string?)state["outcome"] == "Continue")
                        { state = Act("ai"); Dialogues(); checks.Add(choice.Id + " first AI step passed"); }
                    }
                    string fingerprint = string.Join('|', new[] { "revision", "round", "units", "cells", "factions" }.Select(k => state[k]!.ToJsonString()));
                    Call("save", new { mode, slot = 1 }); state = Call("load", new { mode, slot = 1 });
                    if (fingerprint != string.Join('|', new[] { "revision", "round", "units", "cells", "factions" }.Select(k => state[k]!.ToJsonString())))
                        throw new InvalidDataException("Save/load changed scene state");
                    checks.Add(stage + " + save/load");
                }
                results.Add(new { apk = Path.GetFileName(apk), sha256, bytes = input.Length, success = true, key, scenarios = content.Scenarios.Count, checks,
                    limitations = new[] { "Representative opening actions and first AI step only; no complete playthrough or browser/Android validation" }, seconds = watch.Elapsed.TotalSeconds, folder });
                Console.WriteLine("PASS " + Path.GetFileName(apk));
            }
            catch (Exception e)
            {
                failures++;
                File.WriteAllText(Path.Combine(folder, "error.txt"), e.ToString());
                results.Add(new { apk = Path.GetFileName(apk), sha256, bytes = input.Length, success = false, key, stage, detail, error = e.Message, checks, seconds = watch.Elapsed.TotalSeconds, folder });
                Console.WriteLine("FAIL " + Path.GetFileName(apk) + ": " + stage + " / " + detail + " / " + e.Message);
            }
            File.WriteAllText(Path.Combine(report, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine("Report: " + report);
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }

    private static int CheckConsumables(ClientContent content)
    {
        int count = 0;
        foreach (var entry in content.Catalogs.Runtime.Profiles.Values.Where(p => p.Profile.HasTrait("consumed_on_attack")))
        {
            var profile = entry.Profile;
            int distance = Math.Max(1, profile.MinimumRange);
            if (distance > profile.MaximumRange) throw new InvalidDataException("Single-use weapon has no legal target range: " + profile.Id);
            var target = profile with { Id = "check.target", Category = "infantry", MaxHealth = 100_000,
                MinimumRange = 0, MaximumRange = 0, Traits = [] };
            var world = new WorldState
            {
                ScenarioId = "single-use-check", ContentRevision = "single-use-check", Width = distance + 1, Height = 1,
                RandomState = 1946, ActiveFaction = "player",
                Factions = [new() { Id = "player", Camp = "a", Human = true }, new() { Id = "enemy", Camp = "b" }],
                Cells = Enumerable.Range(0, distance + 1).Select(i => new CellState { Owner = i == 0 ? "player" : "enemy" }).ToArray(),
                Units = [new() { Id = "missile", Owner = "player", Cell = 0, Combatant = new() { Profile = profile, Health = profile.MaxHealth } },
                    new() { Id = "target", Owner = "enemy", Cell = distance, Combatant = new() { Profile = target, Health = target.MaxHealth } }]
            };
            var restored = WorldSave.Decode(WorldSave.Encode(world), world.ContentRevision);
            var rules = new WorldBattleRules { CheckFactionSurvival = false, ResolveIgnition = false, RecalculateLocalMorale = false, KillDialogueProbability = 0 };
            var preview = WorldBattle.Preview(restored, 0, "missile", "target", rules);
            var change = WorldBattle.Attack(restored, 0, "missile", "target", rules);
            var reloaded = WorldSave.Decode(WorldSave.Encode(change.State), world.ContentRevision);
            if (preview.Main.DefenderDamage <= 0 || preview.Main.Counterattack || reloaded.Units.Any(u => u.Id == "missile")
                || reloaded.Units.Single().Combatant.Health != target.MaxHealth - preview.Main.DefenderDamage
                || change.Notices.Count(n => n.Kind == "unit.consumed" && n.Subject == "missile") != 1
                || reloaded.Factions.Any(f => f.Kills.Count > 0))
                throw new InvalidDataException("Imported single-use weapon failed damage/consumption/save checks: " + profile.Id);
            count++;
        }
        return count;
    }
}
