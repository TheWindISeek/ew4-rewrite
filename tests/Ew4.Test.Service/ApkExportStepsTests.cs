using System.Text.Json.Nodes;
using Ew4.Interface.Host;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Packages;
using Ew4.Service.Host;
using Ew4.Service.Plans;
using Ew4.Service.Play;
using Xunit;

namespace Ew4.Test.Service;

public sealed class ApkExportStepsTests
{
    [Fact]
    public void Check_route_returns_ok_for_ex_commander_skills()
    {
        string repo = RepoRoot();
        var install = new Ew4InstallOptions
        {
            RepositoryRoot = repo,
            Install = Ew4HostLaunch.ToInstallLayout(new Ew4HostLaunchOptions(repo, Path.Combine(repo, "install", "web"))),
        };
        var manifest = new ModManifest(1, "base", "Base", "1", null, new ModRequirements("1", []), new Dictionary<string, int>());
        var resolved = new ResolvedContent(manifest, "fp", new EmptyModSource(manifest));
        string user = Path.Combine(repo, "install", "web", "user");
        var play = new PlayHost(new Engine());
        play.OnActivated(new HostActivationInfo(resolved, false, null, Path.Combine(user, "saves"), user));

        string json = ExportApkSteps.Run(
            install,
            play,
            """{"op":"check","modId":"ex-commander-skills","allowLossy":true}""");
        JsonObject doc = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException();
        Assert.True(doc["ok"]?.GetValue<bool>());
    }

    private sealed class EmptyModSource : IModSource
    {
        public EmptyModSource(ModManifest manifest) => Manifest = manifest;
        public ModManifest Manifest { get; }
        public bool Exists(string path) => false;
        public byte[] Read(string path) => throw new NotSupportedException();
        public IEnumerable<string> List(string prefix) => Array.Empty<string>();
    }

    private static string RepoRoot()
    {
        string? dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir, "Ew4.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Ew4.sln not found");
    }
}
