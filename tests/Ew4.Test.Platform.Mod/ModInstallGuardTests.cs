using System.IO.Compression;
using Ew4.Platform.Engine;
using Ew4.Platform.Mod;
using Ew4.Platform.Persistence.Packages;
using Xunit;

namespace Ew4.Test.Platform.Mod;

public sealed class ModInstallGuardTests
{
    private static readonly IEngine Engine = new global::Ew4.Platform.Engine.Engine();

    [Fact]
    public void Install_rejects_base_pack_id()
    {
        using TempPacks temp = WritePack("base", parent: null, scenarioFormat: 2);
        var zip = ZipPack(temp.Root, "base");
        Assert.Throws<ModError>(() => ModInstallGuard.ValidateInstall(zip, temp.Repository, Engine));
    }

    [Fact]
    public void Install_rejects_missing_parent()
    {
        using TempPacks temp = WritePack("child", parent: "base", scenarioFormat: 2);
        var zip = ZipPack(temp.Root, "child");
        var ex = Assert.Throws<ModError>(() => ModInstallGuard.ValidateInstall(zip, temp.Repository, Engine));
        Assert.Equal("mod.parent", ex.Code);
    }

    [Fact]
    public void Install_rejects_parent_with_parent()
    {
        using TempPacks temp = WritePack("base", parent: null, scenarioFormat: 2);
        WritePackInto(temp, "mid", parent: "base", scenarioFormat: 2);
        var zip = ZipPack(temp.Root, "mid");
        ModInstallGuard.ValidateInstall(zip, temp.Repository, Engine);

        WritePackInto(temp, "child", parent: "mid", scenarioFormat: 2);
        var childZip = ZipPack(temp.Root, "child");
        var ex = Assert.Throws<ModError>(() => ModInstallGuard.ValidateInstall(childZip, temp.Repository, Engine));
        Assert.Equal("mod.parent.chain", ex.Code);
    }

    [Fact]
    public void Install_rejects_format_mismatch_with_parent()
    {
        using TempPacks temp = WritePack("base", parent: null, scenarioFormat: 2);
        WritePackInto(temp, "child", parent: "base", scenarioFormat: 1);
        var zip = ZipPack(temp.Root, "child");
        var ex = Assert.Throws<ModError>(() => ModInstallGuard.ValidateInstall(zip, temp.Repository, Engine));
        Assert.Equal("mod.formats", ex.Code);
    }

    [Fact]
    public void Remove_rejects_when_dependents_exist()
    {
        using TempPacks temp = WritePack("base", parent: null, scenarioFormat: 2);
        WritePackInto(temp, "parent-pack", parent: "base", scenarioFormat: 2);
        WritePackInto(temp, "child", parent: "parent-pack", scenarioFormat: 2);
        var ex = Assert.Throws<ModError>(() => ModInstallGuard.ValidateRemove("parent-pack", temp.Repository));
        Assert.Equal("mod.remove.dependents", ex.Code);
    }

    [Fact]
    public void Remove_rejects_base()
    {
        using TempPacks temp = WritePack("base", parent: null, scenarioFormat: 2);
        var ex = Assert.Throws<ModError>(() => ModInstallGuard.ValidateRemove("base", temp.Repository));
        Assert.Equal("mod.remove", ex.Code);
    }

    [Fact]
    public void Install_succeeds_for_valid_overlay_zip()
    {
        using TempPacks temp = WritePack("base", parent: null, scenarioFormat: 2);
        WritePackInto(temp, "overlay", parent: "base", scenarioFormat: 2);
        var zip = ZipPack(temp.Root, "overlay");
        ModInstallGuard.ValidateInstall(zip, temp.Repository, Engine);
    }

    private static TempPacks WritePack(string id, string? parent, int scenarioFormat)
    {
        var temp = new TempPacks();
        WritePackInto(temp, id, parent, scenarioFormat);
        return temp;
    }

    private static void WritePackInto(TempPacks temp, string id, string? parent, int scenarioFormat)
    {
        string dir = Path.Combine(temp.Root, id);
        Directory.CreateDirectory(dir);
        string parentJson = parent == null ? "" : $""", "parent": "{parent}" """;
        string manifest = $$"""
            {
              "schema": 1,
              "id": "{{id}}",
              "name": "Test {{id}}",
              "version": "1.0.0"{{parentJson}},
              "requires": { "engine": ">=0.3.0 <1.0.0" },
              "formats": { "scenario": {{scenarioFormat}}, "asset": 1 }
            }
            """;
        File.WriteAllText(Path.Combine(dir, ModManifestPaths.FileName), manifest);
    }

    private static ZipModSource ZipPack(string packsRoot, string id)
    {
        string zipPath = Path.Combine(Path.GetTempPath(), "ew4-pack-" + Guid.NewGuid().ToString("N") + ".zip");
        string sourceDir = Path.Combine(packsRoot, id);
        ZipFile.CreateFromDirectory(sourceDir, zipPath);
        return new ZipModSource(zipPath);
    }

    private sealed class TempPacks : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ew4-packs-" + Guid.NewGuid().ToString("N"));

        public FileModRepository Repository { get; }

        public TempPacks()
        {
            Directory.CreateDirectory(Root);
            Repository = new FileModRepository(Root);
        }

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
