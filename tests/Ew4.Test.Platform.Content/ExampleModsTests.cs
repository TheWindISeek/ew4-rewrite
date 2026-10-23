using Ew4.Platform.Content;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.ScenarioEdit;
using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class ExampleModsTests
{
    private static readonly IEngine Engine = new global::Ew4.Platform.Engine.Engine();

    private static string ModsRoot => Path.Combine(RepositoryRoot(), "mods");

    private static string RepositoryRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Ew4.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? BasePackDirectory
    {
        get
        {
            foreach (string relative in new[] { Path.Combine("install", "web", "mods", "base"), Path.Combine("install", "mods", "base") })
            {
                string install = Path.GetFullPath(relative);
                if (PackManifestPaths.DirectoryHasManifest(install))
                {
                    return install;
                }
            }

            string mods = Path.GetFullPath(Path.Combine("mods", "base"));
            return PackManifestPaths.DirectoryHasManifest(mods) ? mods : null;
        }
    }

    [Theory]
    [InlineData("ex-campaign-tweak")]
    [InlineData("ex-conquest-tweak")]
    [InlineData("ex-commander-skills")]
    public void Example_mod_manifest_matches_directory(string id)
    {
        string dir = Path.Combine(ModsRoot, id);
        Assert.True(Directory.Exists(dir), "Missing example mod: " + dir);
        IPackSource pack = new DirectoryPackSource(dir);
        Assert.Equal(id, pack.Manifest.Id);
        Assert.Equal("base", pack.Manifest.Parent);
    }

    [Theory]
    [InlineData("ex-campaign-tweak", "campaign1_01")]
    [InlineData("ex-conquest-tweak", "conquest1")]
    public void Example_scenario_overlay_resolves_with_base(string modId, string scenarioId)
    {
        string? baseDir = BasePackDirectory;
        if (baseDir == null)
        {
            return;
        }

        string modDir = Path.Combine(ModsRoot, modId);
        var child = new DirectoryPackSource(modDir);
        var parent = new DirectoryPackSource(baseDir);
        var merged = new MergedPackSource(child, parent);
        var repository = new FilePackRepository(Path.GetDirectoryName(baseDir)!);
        PackInstallGuard.ValidateActivation(child, repository, Engine);

        ScenarioDocument document = ScenarioBtlResolver.OpenScenarioDocument(merged, scenarioId);
        Assert.Equal(scenarioId, document.Id);
    }

    [Fact]
    public void Example_commander_overlay_changes_napoleon_skill_with_base()
    {
        string? baseDir = BasePackDirectory;
        string modDir = Path.Combine(ModsRoot, "ex-commander-skills");
        if (baseDir == null || !File.Exists(Path.Combine(modDir, "assets", "tables", "def_commander.xml")))
        {
            return;
        }

        var child = new DirectoryPackSource(modDir);
        var parent = new DirectoryPackSource(baseDir);
        var merged = new MergedPackSource(child, parent);
        var repository = new FilePackRepository(Path.GetDirectoryName(baseDir)!);
        PackInstallGuard.ValidateActivation(child, repository, Engine);

        byte[] xml = PackAssetRead.FromPack(merged)("original/def_commander.xml");
        string text = System.Text.Encoding.UTF8.GetString(xml);
        Assert.Contains("Napoleon (example)", text, StringComparison.Ordinal);
        Assert.Contains("skill2=\"99\"", text, StringComparison.Ordinal);
    }
}
