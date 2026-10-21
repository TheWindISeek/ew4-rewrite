using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

internal static class ScenarioRecurrenceDiplomacyChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new Exception("Recurring events/diplomacy: " + label); }
        void Reject(Action action, string code)
        {
            try { action(); } catch (RuleViolation e) { Check(e.Code == code, code + " got " + e.Code); return; }
            throw new Exception("Recurring events/diplomacy accepted " + code);
        }
        WorldState Reload(WorldState w) => WorldSave.Decode(WorldSave.Encode(w), w.ContentRevision);
        var basis = WorldChecks.Example() with { ScenarioPlayerFaction = "winner",
            Factions = WorldChecks.Example().Factions.Select(f => f with { Resources = new(100, 20, 100) }).ToArray() };
        Resources Funds(WorldState w, string id = "winner") => w.Factions.Single(f => f.Id == id).Resources;
        var upkeep = new ScriptedScenarioEvent { Id = "upkeep", Repeat = new() { EveryRounds = 3, StartRound = 2, EndRound = 12, MaxOccurrences = 3 },
            Effects = [new() { Type = "money", Value = -10 }, new() { Type = "food", Value = -15 }] };
        var world = basis with { ScenarioEvents = [upkeep] };
        for (int round = 1; round <= 14; round++)
        {
            world = ScenarioRules.Evaluate(world with { Round = round }).State;
            int expected = new[] { 2, 5, 8 }.Count(r => r <= round);
            Check(Funds(world) == new Resources(100 - expected * 10, 20, 100 - expected * 15), "schedule/max at round " + round);
            Check(ScenarioRules.Evaluate(Reload(world)).Notices.Count == 0, "no repeated charge after save in round " + round);
        }
        Check(world.ScenarioEventProgress["upkeep"] == new ScenarioEventProgress(3, 8) && world.TriggeredScenarioEvents.Count == 1, "bounded progress and unique ever-triggered history");
        Check(ScenarioRules.Match(world, new() { Type = "event_count", Key = "upkeep", Value = 3, Compare = "eq" }), "occurrence count query");
        var skipped = basis with { ScenarioEvents = [upkeep with { Repeat = new() { EveryRounds = 3, StartRound = 2, EndRound = 8 },
            Condition = new() { Type = "state", Key = "scenario/expedition", Value = 1 } }] };
        skipped = ScenarioRules.Evaluate(skipped with { Round = 2 }).State;
        Check(skipped.TriggeredScenarioEvents.Count == 0, "inactive expedition skips scheduled charge");
        skipped = skipped with { ExtensionState = new Dictionary<string, long> { ["scenario/expedition"] = 1 }, Round = 4 };
        Check(ScenarioRules.Evaluate(skipped).Notices.Count == 0, "no catch-up off schedule");
        skipped = ScenarioRules.Evaluate(skipped with { Round = 5 }).State;
        Check(Funds(skipped).Money == 90 && skipped.ScenarioEventProgress["upkeep"].Occurrences == 1, "resume on next schedule without arrears");
        skipped = ScenarioRules.Evaluate(skipped with { Round = 9 }).State;
        Check(Funds(skipped).Money == 90, "inclusive end round stops further charges");

        var everyTurn = basis with { ScenarioEvents = [upkeep with { Repeat = new() { EveryRounds = 1, StartRound = 2 } }] };
        var turnRules = new WorldTurnRules();
        for (int i = 0; i < 6; i++)
        {
            everyTurn = WorldTurns.EndTurn(everyTurn, everyTurn.Revision, turnRules).State;
            Check(Funds(everyTurn).Money == 100 - (everyTurn.Round - 1) * 10, "actual multi-faction turns charge once per whole round");
            everyTurn = Reload(everyTurn);
        }

        var shortage = upkeep with { Repeat = new() { EveryRounds = 1 }, Effects = [
            new() { Type = "destroy_unit", Unit = "unit.1" }, new() { Type = "money", Value = -10 }, new() { Type = "food", Value = -101 }],
            InsufficientResourcesEffects = [new() { Type = "add_state", Key = "scenario/shortages", Value = 1 }] };
        var shortChange = ScenarioRules.Evaluate(basis with { ScenarioEvents = [shortage] });
        Check(Funds(shortChange.State) == Funds(basis) && shortChange.State.Units.Count == basis.Units.Count
            && shortChange.State.DestroyedScenarioUnits.Count == 0, "shortage atomically rolls back charges and deaths");
        Check(shortChange.State.ExtensionState["scenario/shortages"] == 1 && shortChange.Notices.Any(n => n.Kind == "scenario.insufficient_resources")
            && !shortChange.Notices.Any(n => n.Kind == "unit.destroyed"), "fallback commits without discarded death notices");
        Check(ScenarioRules.Evaluate(Reload(shortChange.State)).Notices.Count == 0, "shortage fallback is not retried in same round");
        Check(ScenarioRules.Evaluate(shortChange.State with { Round = 2 }).State.ExtensionState["scenario/shortages"] == 2, "fallback may recur next period");
        Reject(() => ScenarioRules.Evaluate(basis with { ScenarioEvents = [shortage with { InsufficientResourcesEffects = [] }] }), "scenario.resources");

        var council = new ScriptedScenarioEvent { Id = "council", Repeat = new() { EveryRounds = 2 },
            Effects = [new() { Type = "money", Value = -5 }], Popup = new() { Text = "Supply", Choices = [
                new() { Id = "pay", Effects = [new() { Type = "food", Value = -10 }] },
                new() { Id = "expensive", Effects = [new() { Type = "food", Value = -1000 }] }] },
            InsufficientResourcesEffects = [new() { Type = "state", Key = "scenario/shortage", Value = 1 }] };
        var pending = Reload(ScenarioRules.Evaluate(basis with { ScenarioEvents = [council] }).State);
        Check(Funds(pending) == Funds(basis) && pending.ScenarioEventProgress["council"].Occurrences == 1, "repeat popup counts when opened and defers costs");
        var chosen = ScenarioRules.Choose(pending, pending.Revision, "council", "pay").State;
        Check(Funds(chosen) == new Resources(95, 20, 90), "first choice pays once");
        Reject(() => ScenarioRules.Choose(chosen, chosen.Revision, "council", "pay"), "scenario.choice");
        pending = Reload(ScenarioRules.Evaluate(chosen with { Round = 3 }).State);
        Check(pending.ScenarioChoices.Count == 0 && !ScenarioRules.Match(pending, new() { Type = "event_completed", Key = "council" }), "next popup clears latest completion");
        Reject(() => ScenarioRules.Choose(pending, chosen.Revision, "council", "pay"), "action.stale");
        chosen = ScenarioRules.Choose(pending, pending.Revision, "council", "expensive").State;
        Check(Funds(chosen) == new Resources(95, 20, 90) && chosen.ExtensionState["scenario/shortage"] == 1, "popup common and selected effects roll back together");
        Check(chosen.ScenarioChoices["council"] == "expensive" && chosen.ScenarioEventProgress["council"].Occurrences == 2, "latest repeat choice retained after fallback");
        var afterCount = new ScriptedScenarioEvent { Id = "after.count", Condition = new() { Type = "event_count", Key = "upkeep", Value = 1 },
            Effects = [new() { Type = "state", Key = "scenario/count.done", Value = 1 }] };
        Check(ScenarioRules.Evaluate(basis with { Round = 2, ScenarioEvents = [afterCount, upkeep] }).State.TriggeredScenarioEvents.Count == 2, "count query supports reverse-order event chain");

        foreach (var repeat in new ScenarioEventRepeat[] { new() { EveryRounds = 0 }, new() { StartRound = 0 }, new() { StartRound = 2, EndRound = 1 }, new() { MaxOccurrences = 0 } })
            Reject(() => ScenarioRules.ValidateDefinitions([upkeep with { Repeat = repeat }]), "scenario.definition");
        foreach (var progress in new ScenarioEventProgress[] { new(0, 2), new(1, 3), new(2, 2), new(1, 17), new(4, 11) })
            Reject(() => ScenarioRules.Validate(world with { ScenarioEventProgress = new Dictionary<string, ScenarioEventProgress> { ["upkeep"] = progress } }), "scenario.state");
        Reject(() => ScenarioRules.Validate(world with { ScenarioEventProgress = new Dictionary<string, ScenarioEventProgress>() }), "scenario.state");

        var three = basis with { Factions = basis.Factions.Append(new FactionState { Id = "third", Name = "Third", Camp = "b" }).ToArray() };
        var diplomacy = WorldDiplomacy.Set(three, "winner", "loser", "allied");
        Check(WorldDiplomacy.Allied(diplomacy, "loser", "winner") && !WorldDiplomacy.Allied(diplomacy, "loser", "third"), "alliance symmetric and not transitive");
        Check(WorldDiplomacy.Relation(diplomacy, "winner", "third") == "allied" && diplomacy.Factions.Select(f => f.Camp).SequenceEqual(three.Factions.Select(f => f.Camp)), "third-party relations and camps unchanged");
        diplomacy = WorldDiplomacy.Set(diplomacy, "third", "winner", "hostile");
        Check(WorldDiplomacy.Hostile(diplomacy, "winner", "third", []) && WorldDiplomacy.Allied(diplomacy, "winner", "loser"), "same-camp hostility independent from other alliance");
        diplomacy = Reload(diplomacy);
        Check(diplomacy.DiplomaticRelations.Count == 2 && WorldDiplomacy.Relation(diplomacy, "third", "winner") == "hostile", "bilateral overrides survive save");
        diplomacy = WorldDiplomacy.Set(diplomacy, "winner", "third", "default");
        Check(WorldDiplomacy.Allied(diplomacy, "winner", "third") && diplomacy.DiplomaticRelations.Count == 1, "default removes only selected override");
        var neutral = WorldDiplomacy.Set(three, "winner", "loser", "neutral");
        Check(!WorldDiplomacy.Hostile(neutral, "loser", "winner", []) && WorldDiplomacy.NeutralTo(neutral, "loser", "winner", []), "explicit neutral applies outside neutral camps");
        var provoked = WorldDiplomacy.Attacked(neutral, "winner", "loser", []);
        Check(WorldDiplomacy.Hostile(provoked, "loser", "winner", []) && !WorldDiplomacy.Hostile(provoked, "winner", "third", []), "attacking explicit neutral declares bilateral hostility");
        var neutralCamp = three with { DiplomacyNeutralCamps = ["a"] };
        Check(WorldDiplomacy.Relation(neutralCamp, "winner", "loser") == "neutral" && WorldDiplomacy.Dormant(neutralCamp, "loser", ["a"]), "persisted neutral policy available to conditions and Lua");
        Check(!WorldDiplomacy.Dormant(WorldDiplomacy.Set(neutralCamp, "winner", "loser", "hostile"), "loser", ["a"]), "explicit hostility activates neutral-camp AI");
        var legacyProvoked = WorldDiplomacy.Attacked(neutralCamp, "winner", "loser", ["a"]);
        Check(WorldDiplomacy.Relation(WorldDiplomacy.Set(legacyProvoked, "winner", "loser", "default"), "winner", "loser") == "neutral", "default also resets legacy bilateral provocation");
        Reject(() => WorldDiplomacy.Set(three, "winner", "winner", "allied"), "world.diplomacy");
        Reject(() => WorldDiplomacy.Set(three, "winner", "missing", "hostile"), "world.diplomacy");
        Reject(() => WorldDiplomacy.Set(three, "winner", "loser", "unknown"), "world.diplomacy");
        Reject(() => ScenarioRules.ValidateDefinitions([new() { Id = "bad", Effects = [new() { Type = "relation", OtherFaction = "loser", Relation = "unknown" }] }]), "scenario.effect");
        Reject(() => ScenarioRules.Validate(three with { ScenarioEvents = [new() { Id = "bad", Effects = [new() { Type = "relation", OtherFaction = "@player", Relation = "hostile" }] }] }, true), "scenario.reference");
        Reject(() => ScenarioRules.Validate(three with { ScenarioEvents = [new() { Id = "bad", Condition = new() { Type = "relation", OtherFaction = "missing", Relation = "hostile" } }] }, true), "scenario.reference");

        var adjacent = basis with { Units = basis.Units.Select(u => u with { Cell = u.Owner == "winner" ? 4 : 5, RemainingMovement = 6,
            Combatant = u.Combatant with { Profile = u.Combatant.Profile with { Grade = "0" } } }).ToArray() };
        var ai = new WorldAiRules { ExperienceLevels = new Dictionary<string, int> { [adjacent.Units[0].Veterancy] = 0 } };
        var battle = new WorldBattleRules();
        var allies = WorldDiplomacy.Set(adjacent, "winner", "loser", "allied");
        Reject(() => WorldBattle.Preview(allies, allies.Revision, "unit.2", "unit.1", battle), "attack.target");
        Check(WorldAi.StrikeCandidates(allies, "unit.2", ai).Count == 0, "AI cannot attack cross-camp ally");
        Check(WorldAi.StrikeCandidates(WorldDiplomacy.Set(adjacent, "winner", "loser", "neutral"), "unit.2", ai).Count == 0, "AI cannot initiate neutral war");
        var sameCamp = adjacent with { Factions = adjacent.Factions.Select(f => f with { Camp = "b" }).ToArray() };
        var enemies = WorldDiplomacy.Set(sameCamp, "winner", "loser", "hostile");
        Check(WorldBattle.Preview(enemies, enemies.Revision, "unit.2", "unit.1", battle).Defender == "unit.1", "same-camp hostile is legal battle target");
        Check(WorldAi.StrikeCandidates(enemies, "unit.2", ai).Count == 1, "AI targets same-camp hostile");
        Check(WorldActions.Reachable(allies, "unit.2").ContainsKey(5) && !WorldActions.Reachable(enemies, "unit.2").ContainsKey(5), "movement passes allied occupants and blocks hostile occupants");

        Check(WorldVictory.Evaluate(WorldDiplomacy.Set(basis, "winner", "loser", "allied"), "b", new(), []) == BattleOutcome.Victory, "default victory excludes cross-camp allies");
        Check(WorldVictory.Evaluate(enemies, "b", new(), []) == BattleOutcome.Continue, "default victory waits for same-camp enemies");
        var victoryRules = new VictoryRules { Custom = new() { Win = new() { Type = "enemy_factions_defeated", IncludeProvokedNeutrals = false } } };
        Check(WorldVictory.Evaluate(WorldDiplomacy.Set(basis, "winner", "loser", "neutral"), "b", victoryRules, []) == BattleOutcome.Victory, "custom victory excludes explicit neutral outside neutral camps");
        Check(WorldVictory.Evaluate(enemies, "b", victoryRules, ["b"]) == BattleOutcome.Continue, "explicit enemies count even when camp is neutral");
        var objectiveRules = new VictoryRules { Custom = new() { Win = new() { Type = "cells_owned", Cells = [0] } } };
        Check(WorldVictory.Evaluate(allies, "b", objectiveRules, []) == BattleOutcome.Victory
            && WorldVictory.Evaluate(enemies, "b", objectiveRules, []) == BattleOutcome.Continue, "custom city objective uses bilateral allies");
        var scripted = world with { DiplomaticRelations = WorldDiplomacy.Set(world, "winner", "loser", "allied").DiplomaticRelations };
        var runtime = new ExtensionScriptRuntime(new Dictionary<string, string> { ["rewrite/query.lua"] =
            "function query(c) return c:relation('winner','loser') == 'allied' and c:event_count('upkeep') == 3 and c:relation('missing','loser') == nil end" });
        Check(ScenarioRules.Match(scripted, new() { Type = "script", Script = "rewrite/query.lua", Function = "query" }, runtime), "Lua relation and count queries");

        // Remove new optional fields and recompute the envelope checksum to emulate a genuine old save.
        var envelope = JsonNode.Parse(WorldSave.Encode(basis))!.AsObject();
        var payload = envelope["payload"]!.AsObject();
        foreach (string field in new[] { "diplomaticRelations", "diplomacyNeutralCamps", "scenarioEventProgress" }) payload.Remove(field);
        byte[] oldPayload = Encoding.UTF8.GetBytes(payload.ToJsonString());
        envelope["sha256"] = Convert.ToHexStringLower(SHA256.HashData(oldPayload));
        var old = WorldSave.Decode(Encoding.UTF8.GetBytes(envelope.ToJsonString()), basis.ContentRevision);
        Check(old.DiplomaticRelations.Count == 0 && old.ScenarioEventProgress.Count == 0 && old.DiplomacyNeutralCamps.Count == 0, "old saves default new optional state");

        ClientChecks(root, Check);
        string folder = Path.Combine(root, "rewrite/reports/scenario-recurrence-diplomacy-20261021");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "core.json"), JsonSerializer.Serialize(new { checks, errors = 0 }));
        return checks;
    }

    private static void ClientChecks(string root, Action<bool, string> check)
    {
        byte[] ReadBase(string path) => File.ReadAllBytes(path.StartsWith("data/", StringComparison.Ordinal)
            ? Path.Combine(root, "rewrite/data/legacy", path[5..]) : Path.Combine(root, "rewrite/data", path));
        var plain = new ClientContent(ReadBase, ["conquest1.btl"]);
        var initial = plain.Start("conquest1", "fra").Session;
        string rival = initial.World.Factions.First(f => f.Id != initial.PlayerFaction && f.Camp == initial.World.Factions.Single(p => p.Id == initial.PlayerFaction).Camp).Id;
        var events = new ScriptedScenarioEvent[] { new() { Id = "relations", Repeat = new() { EveryRounds = 2 },
            Effects = [new() { Type = "money", Faction = rival, Value = 9 }, new() { Type = "relation", OtherFaction = rival, Relation = "hostile" }],
            Popup = new() { Text = "War council", Choices = [new() { Id = "war" }] } }, new() { Id = "relations.followup",
                Condition = new() { Type = "relation", OtherFaction = rival, Relation = "hostile" }, Effects = [new() { Type = "food", Faction = rival, Value = 7 }] } };
        byte[] Read(string path) => path switch
        {
            "rewrite/extension.json" => JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, apiVersion = 1, scenarioEvents = "rewrite/events.json" }),
            "rewrite/events.json" => JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, ScriptedScenarioEvent[]> { ["conquest1"] = events }, ExtensionLoader.Json),
            _ => ReadBase(path)
        };
        var content = new ClientContent(Read, ["conquest1.btl"]);
        string directory = Path.Combine(root, "rewrite/reports/scenario-recurrence-diplomacy-20261021", "client-" + Guid.NewGuid().ToString("N"));
        var client = new ClientGame(content, directory);
        JsonElement Call(string route, object body)
        {
            using var doc = JsonDocument.Parse(client.Dispatch("/api/" + route, JsonSerializer.Serialize(body)));
            check(doc.RootElement.GetProperty("ok").GetBoolean(), "client " + route + " " + doc.RootElement.GetRawText()[..Math.Min(200, doc.RootElement.GetRawText().Length)]);
            return doc.RootElement.GetProperty("data").Clone();
        }
        var view = Call("start", new { scenario = "conquest1", country = "fra" });
        check(view.GetProperty("scenarioPopup").GetProperty("id").GetString() == "relations", "loader parses repeat popup from actual extension JSON");
        Call("save", new { mode = 2, slot = 1 });
        client = new ClientGame(content, directory);
        view = Call("load", new { mode = 2, slot = 1 });
        view = Call("action", new { op = "event_choice", offer = "relations", target = "war", revision = view.GetProperty("revision").GetInt64() });
        var foreign = view.GetProperty("factions").EnumerateArray().Single(f => f.GetProperty("id").GetString() == rival);
        check(foreign.GetProperty("relation").GetString() == "hostile", "client projects independent same-camp hostility");
        var original = initial.World.Factions.Single(f => f.Id == rival).Resources;
        check(foreign.GetProperty("resources").GetProperty("money").GetInt64() == original.Money + 9
            && foreign.GetProperty("resources").GetProperty("food").GetInt64() == original.Food + 7, "event edits AI funds and chains on bilateral condition");
        Call("save", new { mode = 2, slot = 1 });
        client = new ClientGame(content, directory);
        view = Call("load", new { mode = 2, slot = 1 });
        check(view.GetProperty("scenarioPopup").ValueKind == JsonValueKind.Null
            && view.GetProperty("factions").EnumerateArray().Single(f => f.GetProperty("id").GetString() == rival).GetProperty("relation").GetString() == "hostile", "client reload retains diplomacy and resolved recurring popup");
    }
}
