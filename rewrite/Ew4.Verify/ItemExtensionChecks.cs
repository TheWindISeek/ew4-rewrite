using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

internal static class ItemExtensionChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new InvalidOperationException("Item extensions: " + label); }
        void Reject(Action action, string label)
        {
            try { action(); } catch (Exception e) when (e is RuleViolation or JsonException) { checks++; return; }
            throw new InvalidOperationException("Item extensions accepted " + label);
        }
        string example = Path.Combine(root, "rewrite/mod-kit/examples/item-effects");
        var files = Directory.EnumerateFiles(Path.Combine(example, "rewrite"), "*", SearchOption.AllDirectories)
            .ToDictionary(p => Path.GetRelativePath(example, p).Replace('\\', '/'), File.ReadAllBytes);
        byte[] Read(string path) => files.TryGetValue(path, out var bytes) ? bytes : File.ReadAllBytes(Path.Combine(root,
            path.StartsWith("data/", StringComparison.Ordinal) ? "rewrite/data/legacy/" + path[5..] : "rewrite/data/" + path));
        var content = new ClientContent(Read, ["conquest1"]); var bus = content.Extensions.Bus; var runtime = content.Catalogs.Runtime;
        Check(content.Extensions.ItemBindings.Count == 3 && bus.Skills.Count == 3, "author example loads");
        Check(runtime.Items.Values.All(i => i == content.Catalogs.Items.Items[int.Parse(i.Id[9..])].Equipment), "trading, roster and battle share definitions");
        Check(runtime.Items[8].Effect == "" && runtime.Items[9].Value == 17, "replacement and numeric override after original corrections");
        var owned = content.Catalogs.Accounts.FromDefinition(1, "audit.item.commander") with { Items = ["ew4.item.8", "ew4.item.9"] };
        var account = new PlayerAccount { Id = "local", ContentRevision = content.Catalogs.Accounts.SourceRevision, Medals = 12345,
            Roster = [owned], Inventory = new Dictionary<string, long> { ["ew4.item.8"] = 1, ["ew4.item.9"] = 1, ["ew4.item.13"] = 3 } };
        var resolved = AccountCommanders.Resolve(owned, content.Catalogs.Accounts.Rules());
        Check(Ew4Commander.Resolve(resolved, "infantry").FieldHealing == 17, "replaced native effect removed, kept effect applies once");
        var start = content.Start("conquest1", "fra", account);
        var target = start.Session.World.Units.First(u => RosterDeployment.CanDeploy(start.Session, u.Id, owned.Id, start.Rules.Roster));
        var deployed = RosterDeployment.Deploy(start.Session, start.Session.Revision, target.Id, owned.Id, start.Rules.Roster).State;
        UnitState Unit(GameSession state) => state.World.Units.Single(u => u.Id == target.Id);
        Check(Unit(deployed).Combatant.SkillSlots.Count(s => s.InstanceId.StartsWith("item.")) == 3, "real deployment grants equipment skills");
        Check(Math.Abs(bus.Modify("combat.modify_incoming_damage", "damage_multiplier", 100, Unit(deployed).Combatant, target.Combatant, new(1)) - 64) < .0001,
            "two equipment passives modify incoming damage");
        var wounded = deployed with { World = deployed.World with { Units = deployed.World.Units.Select(u => u.Id == target.Id
            ? u with { Acted = false, Combatant = u.Combatant with { Health = 1 } } : u).ToArray() } };
        var healed = bus.Dispatch(wounded.World, "turn.start", target.Id, new(wounded.World.RandomState));
        Check(healed.Units.Single(u => u.Id == target.Id).Combatant.Health == 13, "Lua receives equipment skill levels");
        Check(healed.ExtensionState[target.Id + "/item.8.ward/uses"] == 1 && healed.ExtensionState[target.Id + "/item.9.ward/uses"] == 1, "equipment script state is isolated");
        var active = WorldSkillActions.Execute(wounded.World, wounded.World.Revision, target.Id, "item.8.repair", null, bus, start.Rules.Movement);
        Check(active.State.Units.Single(u => u.Id == target.Id).Combatant.Health == 21 && active.State.ExtensionState[target.Id + "/item.8.repair/ready"] == wounded.World.Round + 2,
            "equipment active skill heals with own cooldown");
        Reject(() => WorldSkillActions.Execute(active.State, active.State.Revision, target.Id, "item.8.repair", null, bus, start.Rules.Movement), "cooldown bypass");
        var exhausted = wounded with { World = wounded.World with { Units = wounded.World.Units.Select(u => u.Id == target.Id
            ? u with { Acted = true, RemainingMovement = 0 } : u).ToArray() } };
        SessionChange Use(GameSession state, RuleEventBus? configured = null) => ConsumableItems.Use(state, state.Revision, target.Id, "ew4.item.13",
            content.Catalogs.Items.Rules(), start.Rules.Training, configured ?? bus);
        var first = Use(exhausted).State; var second = Use(first).State;
        Check(Unit(first).Combatant.Health == 22 && Unit(second).Combatant.Health == 44, "Lua consumable persists use counter");
        Check(Unit(second).Acted && Unit(second).RemainingMovement == 0 && Unit(second).Combatant.Statuses.Single().RemainingTurns == 2, "action preserved and status refreshed");
        Check(second.Account.Inventory["ew4.item.13"] == 1 && second.Account.Revision == exhausted.Account.Revision + 2
            && second.Revision == exhausted.Revision + 2 && second.World.Revision == exhausted.World.Revision + 2, "one inventory and revision commit per use");
        Check(!Unit(second).Combatant.SkillSlots.Any(s => s.InstanceId == "item.13.use"), "consumable cannot be activated for free");
        Check(!Use(second).State.Account.Inventory.ContainsKey("ew4.item.13"), "last copy consumed");
        var restored = SessionSave.Decode(SessionSave.Encode(second), new(second.ContentRevision, second.World.ContentRevision, second.Account.ContentRevision));
        Check(Unit(restored).Combatant.SkillSlots.SequenceEqual(Unit(second).Combatant.SkillSlots)
            && restored.World.ExtensionState[target.Id + "/item.13.use/uses"] == 2, "equipment, status and script counters reload");
        var detach = WorldCommanders.Detach(wounded.World, wounded.World.Revision, target.Id, start.Rules.Commanders).State;
        Check(!detach.Units.Single(u => u.Id == target.Id).Combatant.SkillSlots.Any(s => s.InstanceId.StartsWith("item.")), "detach removes equipment skills");
        var noCommander = wounded with { World = wounded.World with { Units = wounded.World.Units.Select(u => u.Id == target.Id
            ? target with { Combatant = target.Combatant with { Health = 1 } } : u).ToArray() } };
        Check(Unit(Use(noCommander).State).Combatant.Health == 22, "custom consumable works without a commander");

        const string bindingPath = "rewrite/definitions/item-bindings.json", skillsPath = "rewrite/definitions/item-skills.json", scriptPath = "rewrite/scripts/items.lua";
        byte[] bindings = files[bindingPath], definitions = files[skillsPath], script = files[scriptPath];
        foreach (string bad in new[] { "[null]", "[{\"target\":\"ew4.item.08\"}]", "[{\"target\":\"ew4.item.-1\"}]",
            "[{\"target\":\"ew4.item.8\",\"value\":-1}]", "[{\"target\":\"ew4.item.8\",\"typo\":true}]",
            "[{\"target\":\"ew4.item.8\"},{\"target\":\"ew4.item.8\"}]", "[{\"target\":\"ew4.item.8\",\"skills\":[null]}]",
            "[{\"target\":\"ew4.item.8\",\"skills\":[{\"id\":\"missing.skill\",\"instanceId\":\"x\"}]}]",
            "[{\"target\":\"ew4.item.8\",\"skills\":[{\"id\":\"ew4.skill.1\",\"instanceId\":\"x\"}]}]",
            "[{\"target\":\"ew4.item.8\",\"useSkill\":{\"id\":\"example.item.medicine\",\"instanceId\":\"x\"}}]",
            "[{\"target\":\"ew4.item.13\",\"skills\":[{\"id\":\"example.item.ward\",\"instanceId\":\"x\"}]}]", "[{\"target\":\"ew4.item.999999\"}]" })
        {
            files[bindingPath] = Encoding.UTF8.GetBytes(bad); Reject(() => new ClientContent(Read, ["conquest1"]), bad);
        }
        files[bindingPath] = bindings;
        var manifest = files["rewrite/extension.json"];
        files["rewrite/extension.json"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(manifest).Replace("\"apiVersion\": 2", "\"apiVersion\": 1"));
        Reject(() => ExtensionLoader.Load(Read), "API 1"); files["rewrite/extension.json"] = manifest;
        foreach (var edit in new Action<JsonObject>[] { s => s["endsAction"] = true, s => s["cooldown"] = 1, s => s["target"] = "enemy",
            s => s["cost"] = new JsonObject { ["money"] = 1 }, s => s["kind"] = "passive" })
        {
            var values = JsonNode.Parse(definitions)!.AsArray(); edit(values[2]!.AsObject());
            files[skillsPath] = Encoding.UTF8.GetBytes(values.ToJsonString()); Reject(() => ExtensionLoader.Load(Read), "use policy");
        }
        files[skillsPath] = definitions;
        var lethal = JsonNode.Parse(definitions)!.AsArray();
        lethal[2]!["effects"] = new JsonArray(new JsonObject { ["event"] = "skill.activate", ["operation"] = "damage", ["value"] = int.MaxValue },
            new JsonObject { ["event"] = "skill.activate", ["operation"] = "money", ["value"] = 77 });
        files[skillsPath] = Encoding.UTF8.GetBytes(lethal.ToJsonString());
        var killed = Use(wounded, ExtensionLoader.Load(Read).Bus);
        Check(!killed.State.World.Units.Any(u => u.Id == target.Id) && killed.State.World.Commanders.Single(c => c.Id == owned.Id).Withdrawn,
            "custom lethal effect uses commander death processing");
        Check(killed.State.World.Factions.Single(f => f.Id == target.Owner).Resources.Money == wounded.World.Factions.Single(f => f.Id == target.Owner).Resources.Money
            && killed.State.Account.Inventory["ew4.item.13"] == 2 && killed.Notices.Any(n => n.Kind == "unit.destroyed"), "death stops later use effects and commits one item");
        files[skillsPath] = definitions;
        files[scriptPath] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(script).Replace("function medicine(ctx)", "function medicine(ctx)\n    ctx:random(100)\n    error('item failure')"));
        var broken = ExtensionLoader.Load(Read); var before = SessionSave.Encode(exhausted);
        Reject(() => Use(exhausted, broken.Bus), "script exception");
        Check(SessionSave.Encode(exhausted).SequenceEqual(before), "script exception leaves world, RNG, account and inventory unchanged");
        var partial = JsonNode.Parse(definitions)!.AsArray();
        partial[2]!["effects"] = new JsonArray(new JsonObject { ["event"] = "skill.activate", ["operation"] = "heal", ["value"] = 10 },
            new JsonObject { ["event"] = "skill.activate", ["operation"] = "money", ["value"] = 1, ["script"] = scriptPath, ["function"] = "medicine" });
        files[skillsPath] = Encoding.UTF8.GetBytes(partial.ToJsonString());
        Reject(() => Use(exhausted, ExtensionLoader.Load(Read).Bus), "failure after earlier effect");
        Check(SessionSave.Encode(exhausted).SequenceEqual(before), "earlier healing is rolled back on later script failure"); files[skillsPath] = definitions;
        files[scriptPath] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(script).Replace("function medicine(ctx)",
            "function medicine(ctx)\n    assert(ctx.itemId == 'ew4.item.13')\n    assert(ctx.instanceId == 'item.13.use')\n    assert(ctx.skillId == 'example.item.medicine')"));
        Use(exhausted, ExtensionLoader.Load(Read).Bus); Check(true, "Lua receives stable identities"); files[scriptPath] = script;

        string report = Path.Combine(root, "rewrite/reports/item-extensions-20261021"); Directory.CreateDirectory(report);
        File.WriteAllBytes(Path.Combine(report, "fixture-slot.json"), JsonSerializer.SerializeToUtf8Bytes(new ClientSlot("conquest1", "fra", SessionSave.Encode(wounded)), ClientGame.Json));
        File.WriteAllBytes(Path.Combine(report, "fixture-account.json"), JsonSerializer.SerializeToUtf8Bytes(wounded.Account, ClientGame.Json));
        string folder = Path.Combine(report, "client-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(report, "fixture-slot.json"), Path.Combine(folder, "session.json")); File.Copy(Path.Combine(report, "fixture-account.json"), Path.Combine(folder, "account.json"));
        var client = new ClientGame(content, folder);
        JsonElement Call(string route, object body, bool success = true)
        {
            using var response = JsonDocument.Parse(client.Dispatch("/api/" + route, JsonSerializer.Serialize(body, ClientGame.Json)));
            Check(response.RootElement.GetProperty("ok").GetBoolean() == success, "client " + route + ": " + response.RootElement.GetRawText()[..Math.Min(160, response.RootElement.GetRawText().Length)]);
            return response.RootElement.GetProperty(success ? "data" : "code").Clone();
        }
        var view = Call("load", new { });
        var selection = Call("action", new { op = "select", cell = target.Cell, revision = view.GetProperty("revision").GetInt64() });
        Check(selection.GetProperty("skills").EnumerateArray().Any(s => s.GetProperty("skill").GetProperty("instanceId").GetString() == "item.8.repair"), "actual client advertises equipment active skill");
        var menu = Call("items", new { unit = target.Id });
        Check(menu.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == "ew4.item.13").GetProperty("available").GetBoolean(), "client consumable availability");
        long revision = view.GetProperty("revision").GetInt64();
        view = Call("action", new { op = "use_item", item = "ew4.item.13", unit = target.Id, revision });
        Check(view.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == target.Id).GetProperty("health").GetInt32() == 22, "client use installs custom effect");
        Call("action", new { op = "use_item", item = "ew4.item.13", unit = target.Id, revision }, false);
        client = new ClientGame(content, folder); view = Call("load", new { mode = 2 });
        Check(view.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == target.Id).GetProperty("statuses").GetArrayLength() == 1, "new client reloads status");
        File.WriteAllText(Path.Combine(report, "backend.json"), JsonSerializer.Serialize(new { checks, errors = 0, target = new { target.Id, target.Cell }, clientFolder = folder }));
        return checks;
    }
}
