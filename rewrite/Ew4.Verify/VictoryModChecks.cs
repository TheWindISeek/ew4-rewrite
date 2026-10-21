using System.Text.Json;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

internal static class VictoryModChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException("Victory Mod: " + label); }
        string example = Path.Combine(root, "rewrite/mod-kit/examples/victory-stages");
        var overrides = Directory.EnumerateFiles(example, "*.json", SearchOption.AllDirectories)
            .ToDictionary(p => Path.GetRelativePath(example, p).Replace('\\', '/'), File.ReadAllBytes);
        byte[] Read(string path) => overrides.TryGetValue(path, out var value) ? value : File.ReadAllBytes(Path.Combine(root,
            "rewrite/data", path.StartsWith("data/", StringComparison.Ordinal) ? "legacy/" + path[5..] : path));
        var content = new ClientContent(Read, ["conquest1.btl"]);
        var start = content.Start("conquest1", "fra");
        var session = start.Session with { BattleId = "victory-mod-stages" };
        var world = session.World;
        string camp = world.Factions.Single(f => f.Human).Camp;
        BattleOutcome Outcome(WorldState w) => WorldVictory.Evaluate(w, camp, start.Rules.Turns.Victory, ["4"]);
        Check(world.ScenarioVictory!.Win.Type == "round_reached" && Outcome(world) == BattleOutcome.Continue, "first stage replaces original conquest condition");
        world = ScenarioRules.Evaluate(world with { Round = 2, PendingDialogue = null, QueuedDialogues = [] }).State;
        Check(world.ScenarioVictory!.Win.Type == "all" && Outcome(world) == BattleOutcome.Continue, "second stage replaces first without settling");
        session = session with { World = world };
        session = SessionSave.Decode(SessionSave.Encode(session), new(session.ContentRevision, world.ContentRevision, session.Account.ContentRevision));
        Check(session.World.ScenarioVictory!.Win.Type == "all" && session.World.TriggeredScenarioEvents.Contains("objective.stage2"), "middle stage and progress survive save");
        world = ScenarioRules.Evaluate(session.World with { Round = 3 }).State;
        Check(world.PendingDialogue == "scenario.popup/objective.stage3" && world.ScenarioVictory!.Win.Type == "all", "choice holds old rule until confirmed");
        session = session with { World = world };
        session = SessionSave.Decode(SessionSave.Encode(session), new(session.ContentRevision, world.ContentRevision, session.Account.ContentRevision));
        world = ScenarioRules.Choose(session.World, session.World.Revision, "objective.stage3", "accept").State;
        Check(world.ScenarioVictory!.Win.Type == "round_reached" && world.ScenarioChoices["objective.stage3"] == "accept" && Outcome(world) == BattleOutcome.Continue, "third rule installed by saved choice");
        Check(Outcome(world with { Round = 4 }) == BattleOutcome.Victory, "final stage wins");
        Check(Outcome(world with { Round = 8 }) == BattleOutcome.Defeat, "simultaneous final win and loss respects defeat priority");
        var defaultWorld = ScenarioRules.Evaluate(world with { ScenarioEvents = [..world.ScenarioEvents,
            new() { Id = "restore", Effects = [new() { Type = "victory_default" }] }] }).State;
        Check(defaultWorld.ScenarioVictory == null, "default restores original conditions");

        string folder = Path.Combine(root, "rewrite/reports/victory-mod-20261021"); Directory.CreateDirectory(folder);
        string profile = Path.Combine(folder, "profile-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(profile);
        var host = new ClientGame(content, profile);
        JsonElement Call(string route, object body)
        {
            using var doc = JsonDocument.Parse(host.Dispatch("/api/" + route, JsonSerializer.Serialize(body, ClientGame.Json)));
            Check(doc.RootElement.GetProperty("ok").GetBoolean(), route + ": " + doc.RootElement);
            return doc.RootElement.GetProperty("data").Clone();
        }
        for (int slot = 1; slot <= 2; slot++)
        {
            var final = session with { World = world with { Round = 4, Phase = TurnPhase.Finished,
                Outcome = slot == 1 ? BattleOutcome.Victory : BattleOutcome.Defeat, PendingDialogue = null }, BattleId = "victory-mod-" + slot };
            File.WriteAllBytes(Path.Combine(profile, "slot-" + slot + ".json"), JsonSerializer.SerializeToUtf8Bytes(
                new ClientSlot("conquest1", "fra", SessionSave.Encode(final)), ClientGame.Json));
            var view = Call("load", new { slot });
            var result = view.GetProperty("battleResult");
            Check(result.GetProperty("presentation").GetProperty("bindings").GetProperty("exit").GetString() == "leave", "custom binding projected on " + result.GetProperty("outcome"));
            long medals = view.GetProperty("medals").GetInt64();
            Check(Call("action", new { op = "state" }).GetProperty("medals").GetInt64() == medals, "custom screen refresh cannot repay");
        }
        var valid = content.Extensions.VictoryPresentations["conquest1"];
        void Reject(VictoryPresentation value, string label)
        {
            try { VictoryPresentation.Validate(new Dictionary<string, VictoryPresentation> { ["*"] = value }, Read("client/manifest.json")); }
            catch (RuleViolation error) when (error.Code == "extension.victory_presentation") { Check(true, label); return; }
            throw new InvalidOperationException("Accepted invalid victory screen: " + label);
        }
        Reject(valid with { Bindings = new(valid.Bindings) { ["exit"] = null } }, "required exit");
        Reject(valid with { Bindings = new(valid.Bindings) { ["round"] = "missing" } }, "unknown control");
        Reject(valid with { Bindings = new(valid.Bindings) { ["exit"] = "scenario_name" } }, "wrong action type");
        Reject(valid with { Bindings = new(valid.Bindings) { ["unknown"] = "leave" } }, "unknown binding");
        Reject(valid with { StarImage = "missing.png" }, "missing star asset");
        Reject(valid with { DefeatLayout = new() { Attributes = new() { ["w"] = "400", ["h"] = "200" } } }, "defeat controls checked");
        VictoryPresentation.Validate(new Dictionary<string, VictoryPresentation> { ["*"] = valid with { DefeatLayout = valid.Layout } }, Read("client/manifest.json"));
        Check(true, "separate defeat layout accepted");
        VictoryPresentation.Validate(new Dictionary<string, VictoryPresentation> { ["*"] = new() }, Read("client/manifest.json"));
        Check(true, "original layout accepts default bindings");
        overrides["rewrite/victory-screen.json"] = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, VictoryPresentation> { ["*"] = valid }, ExtensionLoader.Json);
        content = new ClientContent(Read, ["conquest1.btl"]);
        host = new ClientGame(content, Path.Combine(profile, "fallback"));
        Check(content.Extensions.VictoryPresentations.ContainsKey("*"), "global fallback config accepted");
        void Projection(Dictionary<string, VictoryPresentation> screens, string? expected, string name)
        {
            overrides["rewrite/victory-screen.json"] = JsonSerializer.SerializeToUtf8Bytes(screens, ExtensionLoader.Json);
            content = new ClientContent(Read, ["conquest1.btl", "campaign1_01.btl"]);
            var basis = content.Start("conquest1", "fra").Session;
            var finished = basis with { BattleId = name, World = basis.World with { Phase = TurnPhase.Finished,
                Outcome = BattleOutcome.Victory, PendingDialogue = null } };
            string path = Path.Combine(profile, name); Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, "slot-1.json"), JsonSerializer.SerializeToUtf8Bytes(
                new ClientSlot("conquest1", "fra", SessionSave.Encode(finished)), ClientGame.Json));
            host = new ClientGame(content, path);
            var screen = Call("load", new { slot = 1 }).GetProperty("battleResult").GetProperty("presentation");
            Check(expected == null ? screen.ValueKind == JsonValueKind.Null : screen.GetProperty("victoryTitle").GetString() == expected, name);
        }
        Projection(new() { ["*"] = valid with { VictoryTitle = "global" } }, "global", "global-fallback");
        Projection(new() { ["*"] = valid with { VictoryTitle = "global" }, ["conquest1"] = valid }, valid.VictoryTitle, "scenario-priority");
        Projection(new() { ["campaign1_01"] = valid }, null, "original-fallback");
        overrides["rewrite/victory-screen.json"] = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, VictoryPresentation> { ["unknown"] = valid }, ExtensionLoader.Json);
        try { _ = new ClientContent(Read, ["conquest1.btl"]); throw new InvalidOperationException("Unknown scenario screen accepted"); }
        catch (RuleViolation e) when (e.Code == "extension.victory_presentation") { Check(true, "unknown scenario rejected"); }
        File.WriteAllText(Path.Combine(folder, "checks.json"), JsonSerializer.Serialize(new { checks, errors = 0, profile }, ClientGame.Json));
        return checks;
    }
}
