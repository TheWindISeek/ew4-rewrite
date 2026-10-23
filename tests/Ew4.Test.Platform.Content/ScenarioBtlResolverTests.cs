using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class ScenarioBtlResolverTests
{
    private static string? BaseCampaignBtl(string fileName)
    {
        foreach (string relative in new[]
                 {
                     Path.Combine("install", "web", "mods", "base", "source", "original", fileName),
                     Path.Combine("install", "mods", "base", "source", "original", fileName),
                 })
        {
            string path = Path.GetFullPath(relative);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    [Fact]
    public void Embedded_profile_falls_back_to_legacy_btl_when_json_is_stub()
    {
        using TempPack temp = new();
        const string id = "campaign1_01";
        string? sourceBtl = BaseCampaignBtl("campaign1_01.btl");
        if (sourceBtl == null)
        {
            return;
        }

        WriteLegacyStub(temp.Root, id, "source/original/" + id + ".btl");
        Directory.CreateDirectory(Path.Combine(temp.Root, "source", "original"));
        File.Copy(
            sourceBtl,
            Path.Combine(temp.Root, "source", "original", id + ".btl"));
        ModManifestProfile.WriteEmbeddedJsonProfile(temp.Root);

        IModSource pack = new DirectoryModSource(temp.Root);
        ScenarioDocument doc = ScenarioBtlResolver.OpenScenarioDocument(pack, id);
        Assert.Equal(id, doc.Id);
    }

    [Fact]
    public void Legacy_btl_profile_opens_pack_btl_without_json_stub()
    {
        using TempPack temp = new();
        const string id = "campaign3_01";
        string? sourceBtl3 = BaseCampaignBtl("campaign3_01.btl");
        if (sourceBtl3 == null)
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(temp.Root, "source", "original"));
        File.Copy(
            sourceBtl3,
            Path.Combine(temp.Root, "source", "original", id + ".btl"));
        File.WriteAllText(
            Path.Combine(temp.Root, "mod.json"),
            """
            {
              "schema": 1,
              "id": "test",
              "name": "Test",
              "version": "1.0.0",
              "requires": { "engine": ">=0.3.0" },
              "formats": { "scenario": 1, "asset": 1 },
              "scenarios": { "storage": "legacy-btl", "documentSchema": 2 }
            }
            """);

        IModSource pack = new DirectoryModSource(temp.Root);
        ScenarioDocument doc = ScenarioBtlResolver.OpenScenarioDocument(pack, id);
        Assert.Equal(id, doc.Id);
    }

    [Fact]
    public void Without_profile_auto_uses_legacy_stub()
    {
        using TempPack temp = new();
        const string id = "campaign1_01";
        string? sourceBtl = BaseCampaignBtl("campaign1_01.btl");
        if (sourceBtl == null)
        {
            return;
        }

        WriteLegacyStub(temp.Root, id, "source/original/" + id + ".btl");
        Directory.CreateDirectory(Path.Combine(temp.Root, "source", "original"));
        File.Copy(
            sourceBtl,
            Path.Combine(temp.Root, "source", "original", id + ".btl"));

        IModSource pack = new DirectoryModSource(temp.Root);
        ScenarioDocument doc = ScenarioBtlResolver.OpenScenarioDocument(pack, id);
        Assert.Equal(id, doc.Id);
    }

    private static void WriteLegacyStub(string root, string id, string btlPath)
    {
        Directory.CreateDirectory(Path.Combine(root, "scenarios"));
        Directory.CreateDirectory(Path.Combine(root, "rewrite"));
        string manifest = """
            {
              "schema": 1,
              "id": "test",
              "name": "Test",
              "version": "1.0.0",
              "requires": { "engine": ">=0.3.0" },
              "formats": { "scenario": 1, "asset": 1 }
            }
            """;
        File.WriteAllText(Path.Combine(root, ModManifestPaths.FileName), manifest);
        string scenario = "{\"schema\":1,\"id\":\"" + id + "\",\"legacy\":{\"btl\":\"" + btlPath + "\"}}";
        File.WriteAllText(Path.Combine(root, "scenarios", id + ".json"), scenario);
    }

    private sealed class TempPack : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ew4-pack-" + Guid.NewGuid().ToString("N"));

        public TempPack() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
