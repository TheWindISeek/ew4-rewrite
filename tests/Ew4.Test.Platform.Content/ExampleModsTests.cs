using Ew4.Platform.Content;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Engine;
using Ew4.Platform.Mod;
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

    private static string? BaseModDirectory
    {
        get
        {
            foreach (string relative in new[] { Path.Combine("install", "web", "mods", "base"), Path.Combine("install", "mods", "base") })
            {
                string install = Path.GetFullPath(relative);
                if (ModManifestPaths.DirectoryHasManifest(install))
                {
                    return install;
                }
            }

            string mods = Path.GetFullPath(Path.Combine("mods", "base"));
            return ModManifestPaths.DirectoryHasManifest(mods) ? mods : null;
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
        IModSource pack = new DirectoryModSource(dir);
        Assert.Equal(id, pack.Manifest.Id);
        Assert.Equal("base", pack.Manifest.Parent);
    }

    [Theory]
    [InlineData("ex-campaign-tweak", "campaign1_01")]
    [InlineData("ex-conquest-tweak", "conquest1")]
    public void Example_scenario_overlay_resolves_with_base(string modId, string scenarioId)
    {
        string? baseDir = BaseModDirectory;
        if (baseDir == null)
        {
            return;
        }

        string modDir = Path.Combine(ModsRoot, modId);
        var child = new DirectoryModSource(modDir);
        var parent = new DirectoryModSource(baseDir);
        var merged = new MergedModSource(child, parent);
        var repository = new FileModRepository(Path.GetDirectoryName(baseDir)!);
        ModInstallGuard.ValidateActivation(child, repository, Engine);

        Assert.NotEmpty(ScenarioModAccess.OpenBattle(merged, scenarioId).Factions);
    }

    [Theory]
    [InlineData("ex-campaign-tweak", "campaign1_01", "示例战役：第一关（仅改列表名）")]
    [InlineData("ex-conquest-tweak", "conquest1", "示例征服：第一时代（仅改列表名）")]
    public void Example_scenario_overlay_display_name_for_catalog(string modId, string scenarioId, string expectedName)
    {
        string? baseDir = BaseModDirectory;
        if (baseDir == null)
        {
            return;
        }

        string modDir = Path.Combine(ModsRoot, modId);
        var child = new DirectoryModSource(modDir);
        var parent = new DirectoryModSource(baseDir);
        var merged = new MergedModSource(child, parent);

        IReadOnlyDictionary<string, string> names = ScenarioCatalogNames.ReadDisplayNames(merged);
        Assert.True(names.TryGetValue(scenarioId, out string? name), "Missing display name for " + scenarioId);
        Assert.Equal(expectedName, name);
    }

    [Fact]
    public void Example_commander_overlay_passes_activation_snapshot_load()
    {
        string? baseDir = BaseModDirectory;
        string modDir = Path.Combine(ModsRoot, "ex-commander-skills");
        if (baseDir == null || !File.Exists(Path.Combine(modDir, "assets", "tables", "def_commander.xml")))
        {
            return;
        }

        var child = new DirectoryModSource(modDir);
        var parent = new DirectoryModSource(baseDir);
        var registry = ContentKindRegistry.CreateDefault();
        var resolver = new ContentResolver(registry, Engine);
        var loader = new ModSnapshotLoader(resolver, new ContentValidator(registry, Engine, new ScenarioJsonBuilder()));
        ResolvedContent loaded = loader.LoadActivatedView(child, parent).AsResolvedContent();
        Assert.Equal("ex-commander-skills", loaded.Manifest.Id);
    }

    [Fact]
    public void Example_commander_overlay_changes_napoleon_skill_with_base()
    {
        string? baseDir = BaseModDirectory;
        string modDir = Path.Combine(ModsRoot, "ex-commander-skills");
        if (baseDir == null || !File.Exists(Path.Combine(modDir, "assets", "tables", "def_commander.xml")))
        {
            return;
        }

        var child = new DirectoryModSource(modDir);
        var parent = new DirectoryModSource(baseDir);
        var merged = new MergedModSource(child, parent);
        var repository = new FileModRepository(Path.GetDirectoryName(baseDir)!);
        ModInstallGuard.ValidateActivation(child, repository, Engine);

        byte[] xml = ModAssetRead.FromMod(merged)("original/def_commander.xml");
        string text = System.Text.Encoding.UTF8.GetString(xml);
        Assert.Contains("Napoleon (example)", text, StringComparison.Ordinal);
        Assert.Contains("skill2=\"99\"", text, StringComparison.Ordinal);
    }
}
