using Ew4.Platform.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Workspaces;
using Xunit;

namespace Ew4.Test.Editor;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public void Create_overlay_workspace_lists_and_reads_parent_file()
    {
        string temp = Path.Combine(Path.GetTempPath(), "ew4-ws-" + Guid.NewGuid().ToString("N"));
        string packs = Path.Combine(temp, "packs");
        string workspaces = Path.Combine(temp, "workspaces");
        Directory.CreateDirectory(packs);
        WriteMinimalBasePack(packs);

        var repository = new FileModRepository(packs);
        var registry = ContentKindRegistry.CreateDefault();
        var engine = new global::Ew4.Platform.Engine.Engine();
        var builder = new ScenarioJsonBuilder();
        var resolver = new ContentResolver(registry, engine);
        var validator = new ContentValidator(registry, engine, builder);
        var loader = new ModSnapshotLoader(resolver, validator);
        var service = new WorkspaceService(
            new WorkspaceStore(workspaces),
            repository,
            registry,
            loader,
            validator);

        WorkspaceInfo created = service.Create(ModId.Base, "test-ws", "Test workspace");
        Assert.Equal("test-ws", created.Id);
        Assert.Equal(ModId.Base, created.ParentModId);

        IReadOnlyList<WorkspaceInfo> list = service.List();
        Assert.Contains(list, w => w.Id == "test-ws");

        byte[] packJson = service.ReadFile("test-ws", "mod.json");
        Assert.NotEmpty(packJson);

        Assert.Contains("\"parent\"", System.Text.Encoding.UTF8.GetString(service.ReadFile("test-ws", "mod.json")));
        var standalone = new ExportModSource(service.OpenMergedMod("test-ws"), dropParent: true);
        Assert.Null(standalone.Manifest.Parent);
        Assert.DoesNotContain("\"parent\"", System.Text.Encoding.UTF8.GetString(standalone.Read("mod.json")));

        try
        {
            Directory.Delete(temp, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup on shared runners.
        }
    }

    private static void WriteMinimalBasePack(string packsRoot)
    {
        string baseDir = Path.Combine(packsRoot, ModId.Base);
        Directory.CreateDirectory(baseDir);
        string manifest = """
            {
              "schema": 1,
              "id": "base",
              "name": "Test Base",
              "version": "0.0.0",
              "requires": { "engine": ">=0.3.0", "capabilities": [] },
              "formats": {
                "scenario": 1, "rules": 1, "map": 1, "commander": 1, "unit": 1,
                "country": 1, "text": 1, "script": 1, "asset": 1, "source": 1, "extension": 1
              }
            }
            """;
        File.WriteAllText(Path.Combine(baseDir, "mod.json"), manifest);
    }
}
