using System.Text;
using System.Text.Json;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

internal static class DeploymentLimitChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new InvalidOperationException("Deployment limits: " + label); }
        void Reject(Action action, string label)
        {
            try { action(); } catch (Exception e) when (e is RuleViolation or JsonException or FileNotFoundException) { checks++; return; }
            throw new InvalidOperationException("Deployment limits accepted " + label);
        }
        const string config = "rewrite/definitions/deployment-limits.json";
        var files = new Dictionary<string, byte[]>();
        byte[] Read(string path) => files.TryGetValue(path, out var bytes) ? bytes : File.ReadAllBytes(path.StartsWith("data/")
            ? Path.Combine(root, "rewrite/data/legacy", path[5..]) : Path.Combine(root, "rewrite/data", path));
        void Put(string path, string json) => files[path] = Encoding.UTF8.GetBytes(json);
        ClientContent Content() => new(Read, ["conquest1.btl", "tutorials1.btl"]);
        var baseline = Content();
        var original = baseline.Start("conquest1", "fra");
        int? originalLimit = original.Session.RosterDeployments.Single().Limit;
        Check(baseline.Extensions.DeploymentLimits.Count == 0, "absent interface preserves defaults");
        string manifest = "{\"schema\":1,\"apiVersion\":2,\"deploymentLimits\":\"" + config + "\"}";
        Put("rewrite/extension.json", manifest);
        Put(config, "{\"schema\":1,\"limits\":{}}");
        Check(Content().Start("conquest1", "fra").Session.RosterDeployments.Single().Limit == originalLimit, "empty template preserves current limit");
        foreach (string invalid in new[] {
            "{\"schema\":2,\"limits\":{}}", "{\"schema\":1}", "{\"schema\":1,\"limits\":null}",
            "{\"schema\":1,\"limits\":{\"*\":-1}}", "{\"schema\":1,\"limits\":{\"*\":1.5}}",
            "{\"schema\":1,\"limits\":{\"*\":\"24\"}}", "{\"schema\":1,\"limits\":{\"*\":2147483648}}",
            "{\"schema\":1,\"limits\":{\"*\":1,\"*\":2}}", "{\"schema\":1,\"limits\":{\"\":2}}",
            "{\"schema\":1,\"limits\":{},\"typo\":1}" })
        {
            Put(config, invalid); Reject(() => ExtensionLoader.Load(Read), invalid);
        }
        Put(config, "{\"schema\":1,\"limits\":{\"missing\":2}}");
        Reject(() => Content(), "unknown scenario");
        Put(config, "{\"schema\":1,\"limits\":{\"*\":13}}");
        Put("rewrite/extension.json", manifest.Replace("\"apiVersion\":2", "\"apiVersion\":1"));
        Reject(() => ExtensionLoader.Load(Read), "API 1");
        Put("rewrite/extension.json", manifest.Replace(config, "../deployment-limits.json"));
        Reject(() => ExtensionLoader.Load(Read), "path escape");
        Put("rewrite/extension.json", manifest);
        var tuned = Content();
        string revision = tuned.Extensions.Revision;
        var accounts = tuned.Catalogs.Accounts;
        var roster = Enumerable.Range(0, 14).Select(i => accounts.FromDefinition(1, "quota.owned." + i)).ToArray();
        var account = new PlayerAccount { Id = "quota", ContentRevision = accounts.SourceRevision, Roster = roster };
        var started = tuned.Start("conquest1", "fra", account);
        Check(started.Session.RosterDeployments.Single().Limit == 13, "global limit reaches actual scenario session");
        var world = started.Session.World;
        var unit = world.Units.First(u => u.Owner == world.ActiveFaction && u.CommanderId == null && world.Cells[u.Cell].Owner == u.Owner);
        var history = roster.Take(12).Select(c => started.Rules.Roster.Resolve(c, world.ActiveFaction).Runtime).ToArray();
        var session = started.Session with {
            World = world with { Commanders = world.Commanders.Concat(history).ToArray() },
            RosterDeployments = [started.Session.RosterDeployments.Single() with { CommanderIds = roster.Take(12).Select(c => c.Id).ToArray() }] };
        Check(RosterDeployment.CanDeploy(session, unit.Id, roster[12].Id, started.Rules.Roster), "mod permits a thirteenth deployment");
        var deployed = RosterDeployment.Deploy(session, session.Revision, unit.Id, roster[12].Id, started.Rules.Roster).State;
        var other = world.Units.First(u => u.Id != unit.Id && u.Owner == world.ActiveFaction && u.CommanderId == null && world.Cells[u.Cell].Owner == u.Owner);
        Check(deployed.RosterDeployments.Single().CommanderIds.Count == 13
            && !RosterDeployment.CanDeploy(deployed, other.Id, roster[13].Id, started.Rules.Roster), "backend rejects fourteenth deployment at configured limit");
        var encoded = SessionSave.Encode(deployed);
        var binding = new SessionContentBinding(started.Session.ContentRevision, world.ContentRevision, account.ContentRevision);
        var restored = SessionSave.Decode(encoded, binding);
        Check(restored.RosterDeployments.Single().Limit == 13 && SessionSave.Encode(restored).SequenceEqual(encoded), "limit and full history roundtrip in session save");
        Put(config, "{\"schema\":1,\"limits\":{\"*\":0,\"conquest1\":24}}");
        var specific = Content();
        Check(specific.Start("conquest1", "fra").Session.RosterDeployments.Single().Limit == 24, "specific scenario takes priority over global default");
        Check(specific.Start("tutorials1", null).Session.RosterDeployments.Single().Limit == 0, "other scenario uses global default");
        Check(specific.Extensions.Revision != revision, "configuration changes logic fingerprint");
        Reject(() => SessionSave.Decode(encoded, new(started.Session.ContentRevision,
            specific.Start("conquest1", "fra", account).Session.World.ContentRevision, account.ContentRevision)), "save from different quota configuration");
        Put(config, "{\"schema\":1,\"limits\":{\"conquest1\":0}}");
        var disabled = Content().Start("conquest1", "fra", account);
        Check(disabled.Session.RosterDeployments.Single().Limit == 0
            && !RosterDeployment.CanDeploy(disabled.Session, unit.Id, roster[0].Id, disabled.Rules.Roster), "zero disables actual deployment");
        Check(Content().Start("tutorials1", null).Session.RosterDeployments.Single().Limit
            == baseline.Start("tutorials1", null).Session.RosterDeployments.Single().Limit, "unmatched scenario retains source limit");
        Put(config, "{\"schema\":1,\"limits\":{\"*\":null}}");
        var unlimited = Content().Start("conquest1", "fra", account);
        var unrestricted = session with { RosterDeployments = [session.RosterDeployments.Single() with { Limit = unlimited.Session.RosterDeployments.Single().Limit }] };
        Check(unlimited.Session.RosterDeployments.Single().Limit == null
            && RosterDeployment.CanDeploy(unrestricted, unit.Id, roster[12].Id, unlimited.Rules.Roster), "explicit null enables unlimited policy");
        Check(SessionSave.Decode(SessionSave.Encode(unlimited.Session), new(unlimited.Session.ContentRevision,
            unlimited.Session.World.ContentRevision, account.ContentRevision)).RosterDeployments.Single().Limit == null, "unlimited policy survives save");
        Put(config, "{\"schema\":1,\"limits\":{\"*\":21}}");
        var localContent = Content();
        var players = localContent.Battle("conquest1").Factions.Select((f, i) => new { f, i }).Where(p => p.f.Camp != 4).Take(2)
            .Select(p => LegacyScenarioInitialization.FactionId("conquest1", p.i, p.f)).ToArray();
        var local = localContent.Start("conquest1", "fra", account, players);
        Check(local.Session.RosterDeployments.Count == 2 && local.Session.RosterDeployments.All(q => q.Limit == 21), "local human countries receive independent configured quotas");
        files.Clear();
        var untouched = Content().Start("conquest1", "fra");
        Check(untouched.Session.RosterDeployments.Single().Limit == originalLimit
            && untouched.Session.ContentRevision == original.Session.ContentRevision && untouched.Session.World.ContentRevision == original.Session.World.ContentRevision,
            "builtin quota and save fingerprints remain unchanged");
        string folder = Path.Combine(root, "rewrite/reports/deployment-roster-20261022"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "limits.json"), JsonSerializer.Serialize(new { checks, errors = 0, originalLimit }));
        return checks;
    }
}
