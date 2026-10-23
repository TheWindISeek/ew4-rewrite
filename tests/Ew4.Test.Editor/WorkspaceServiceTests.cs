using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
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

        var repository = new FilePackRepository(packs);
        var registry = ContentKindRegistry.CreateDefault();
        var engine = new global::Ew4.Platform.Engine.Engine();
        var builder = new ScenarioJsonBuilder();
        var resolver = new ContentResolver(registry, engine);
        var validator = new ContentValidator(registry, engine, builder);
        var service = new WorkspaceService(workspaces, repository, registry, resolver, validator);

        WorkspaceInfo created = service.Create(PackId.Base, "test-ws", "Test workspace");
        Assert.Equal("test-ws", created.Id);
        Assert.Equal(PackId.Base, created.ParentPackId);

        IReadOnlyList<WorkspaceInfo> list = service.List();
        Assert.Contains(list, w => w.Id == "test-ws");

        byte[] packJson = service.ReadFile("test-ws", "pack.json");
        Assert.NotEmpty(packJson);

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
        string baseDir = Path.Combine(packsRoot, PackId.Base);
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
        File.WriteAllText(Path.Combine(baseDir, "pack.json"), manifest);
    }
}
