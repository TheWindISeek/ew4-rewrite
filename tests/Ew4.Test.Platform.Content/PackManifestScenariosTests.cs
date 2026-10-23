using Ew4.Platform.Content;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class PackManifestScenariosTests
{
    [Fact]
    public void Mod_json_wins_when_pack_json_also_present()
    {
        using var temp = new TempDir();
        WriteManifest(temp.Root, PackManifestPaths.FileName, id: "from-mod");
        WriteManifest(temp.Root, PackManifestPaths.LegacyPackJson, id: "from-pack");
        IPackSource pack = new DirectoryPackSource(temp.Root);
        Assert.Equal("from-mod", pack.Manifest.Id);
    }

    [Fact]
    public void TryRead_uses_scenarios_from_mod_json()
    {
        using var temp = new TempDir();
        WriteManifest(temp.Root, PackManifestPaths.FileName, withScenarios: true);
        IPackSource pack = new DirectoryPackSource(temp.Root);
        PackModProfile.ModProfile? profile = PackModProfile.TryRead(pack);
        Assert.NotNull(profile);
        Assert.Equal(PackModProfile.ScenarioStorageMode.EmbeddedJson, profile!.ScenarioStorage);
    }

    [Fact]
    public void MergeIntoManifest_writes_scenarios_section()
    {
        using var temp = new TempDir();
        WriteManifest(temp.Root, PackManifestPaths.FileName, withScenarios: false);
        PackManifestScenarios.MergeIntoManifest(
            temp.Root,
            new PackModProfile.ModProfile(1, PackModProfile.ScenarioStorageMode.EmbeddedJson, 2));
        IPackSource pack = new DirectoryPackSource(temp.Root);
        Assert.Equal(PackModProfile.ScenarioStorageMode.EmbeddedJson, pack.Manifest.Mod!.ScenarioStorage);
    }

    private static void WriteManifest(string root, string fileName, bool withScenarios = true, string id = "test")
    {
        string scenarios = withScenarios
            ? """
              ,
              "scenarios": { "storage": "embedded-json", "documentSchema": 2 }
              """
            : "";
        string manifest = $$"""
            {
              "schema": 1,
              "id": "{{id}}",
              "name": "Test",
              "version": "1.0.0",
              "requires": { "engine": ">=0.3.0" },
              "formats": { "scenario": 1, "asset": 1 }{{scenarios}}
            }
            """;
        File.WriteAllText(Path.Combine(root, fileName), manifest);
    }

    private sealed class TempDir : IDisposable
    {
        public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ew4-pack-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Root);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
