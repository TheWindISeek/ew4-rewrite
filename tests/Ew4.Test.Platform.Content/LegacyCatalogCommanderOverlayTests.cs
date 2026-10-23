using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.Original;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class LegacyCatalogCommanderOverlayTests
{
    [Fact]
    public void Apply_rebuilds_commanders_when_def_commander_xml_differs_from_catalog_hash()
    {
        string root = Path.GetFullPath(Path.Combine("mods"));
        string ww1 = Path.Combine(root, "ww1");
        string baseDir = Path.Combine(root, "base");
        string catalogPath = Path.Combine(baseDir, "assets", "legacy", "catalog.json");
        string commanderPath = Path.Combine(ww1, "assets", "tables", "def_commander.xml");
        if (!File.Exists(catalogPath) || !File.Exists(commanderPath))
        {
            return;
        }

        string catalogJson = File.ReadAllText(catalogPath);
        byte[] commanderXml = File.ReadAllBytes(commanderPath);
        string patched = LegacyCatalogCommanderOverlay.ApplyIfCommanderSourceChanged(catalogJson, commanderXml);
        Assert.NotEqual(catalogJson, patched);

        using var document = JsonDocument.Parse(patched);
        int commanders = document.RootElement.GetProperty("commanders").GetArrayLength();
        Assert.True(commanders > 600);
        string hash = document.RootElement.GetProperty("source_sha256").GetProperty("def_commander.xml").GetString()!;
        Assert.Equal(Convert.ToHexString(SHA256.HashData(commanderXml)), hash, ignoreCase: true);

        using var first = JsonDocument.Parse(catalogJson);
        string napoleon = first.RootElement.GetProperty("commanders")[0].GetProperty("legacy_attributes").GetProperty("name").GetString()!;
        string joffre = document.RootElement.GetProperty("commanders")[0].GetProperty("legacy_attributes").GetProperty("name").GetString()!;
        Assert.Equal("Napoleon", napoleon);
        Assert.Equal("Joffre", joffre);
    }

    [Fact]
    public void GreatFront_merged_catalog_parses_after_legacy_overlays()
    {
        string root = Path.GetFullPath(Path.Combine("install", "mods"));
        string greatFront = Path.Combine(root, "great-front");
        string baseDir = Path.Combine(root, "base");
        string catalogPath = Path.Combine(baseDir, "assets", "legacy", "catalog.json");
        if (!File.Exists(catalogPath) || !Directory.Exists(greatFront))
        {
            return;
        }

        IPackSource merged = new MergedPackSource(new DirectoryPackSource(greatFront), new DirectoryPackSource(baseDir));
        Func<string, byte[]> read = PackAssetRead.FromPack(merged);
        var countries = CountryCatalog.Load(read);
        var extensions = ExtensionLoader.Load(read, countries);
        string catalogJson = LegacyCatalogSourceTreeOverlay.SyncChangedTables(
            File.ReadAllText(catalogPath),
            name => read("original/" + name));
        catalogJson = LegacyCatalogCommanderOverlay.ApplyIfCommanderSourceChanged(
            catalogJson,
            read("original/def_commander.xml"));
        var runtime = OriginalRuntimeCatalog.FromJson(
            extensions.PatchCatalog(catalogJson),
            correctOriginalEquipment: false,
            extensions.LegacySkills);
        var bootstrap = new PackRuntimeBootstrap(merged);
        foreach (int id in runtime.Commanders.Keys)
        {
            bootstrap.Catalogs.Accounts.PreviewAbilities(id);
            bootstrap.Catalogs.Accounts.FromDefinition(id, "preview." + id);
        }
    }
}
