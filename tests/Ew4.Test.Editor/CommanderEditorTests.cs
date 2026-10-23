using Ew4.Editor;
using Ew4.Editor.Commander;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Editor;

public sealed class CommanderEditorTests
{
    [Fact]
    public void List_reads_commanders_from_merged_base_pack()
    {
        (WorkspaceService workspaces, string workspaceId) = CreateWorkspaceWithCommanderTable();
        var editor = new CommanderEditor(workspaces);

        CommanderTableView view = editor.List(workspaceId);
        Assert.Equal(2, view.Entries.Count);
        Assert.Contains(view.Entries, e => e.LegacyId == 1 && e.Name == "Alpha" && e.Infantry == 2);
        Assert.Contains(view.Entries, e => e.LegacyId == 2 && e.Skill1 == 5);

        Cleanup(workspaces.WorkspacesRoot);
    }

    [Fact]
    public void Apply_aptitudes_writes_workspace_overlay()
    {
        (WorkspaceService workspaces, string workspaceId) = CreateWorkspaceWithCommanderTable();
        var editor = new CommanderEditor(workspaces);

        editor.Apply(workspaceId, new CommanderEditAptitudes(1, Infantry: 4));

        CommanderEntry merged = editor.Get(workspaceId, 1);
        Assert.Equal(4, merged.Infantry);

        Cleanup(workspaces.WorkspacesRoot);
    }

    [Fact]
    public void Apply_skills_patches_only_specified_slots()
    {
        (WorkspaceService workspaces, string workspaceId) = CreateWorkspaceWithCommanderTable();
        var editor = new CommanderEditor(workspaces);

        editor.Apply(workspaceId, new CommanderEditSkills(2, Skill1: 9, Skill3: 3));

        CommanderEntry entry = editor.Get(workspaceId, 2);
        Assert.Equal(9, entry.Skill1);
        Assert.Equal(3, entry.Skill3);

        Cleanup(workspaces.WorkspacesRoot);
    }

    [Fact]
    public void Apply_loadout_updates_skills_and_aptitudes()
    {
        (WorkspaceService workspaces, string workspaceId) = CreateWorkspaceWithCommanderTable();
        var editor = new CommanderEditor(workspaces);

        editor.Apply(workspaceId, new CommanderEditLoadout(2, 11, 12, 13, 14, 3, 3, 4, 2, 1));

        CommanderEntry entry = editor.Get(workspaceId, 2);
        Assert.Equal(11, entry.Skill1);
        Assert.Equal(4, entry.Artillery);
        Assert.Equal(1, entry.Fort);

        Cleanup(workspaces.WorkspacesRoot);
    }

    [Fact]
    public void Get_unknown_id_throws()
    {
        (WorkspaceService workspaces, string workspaceId) = CreateWorkspaceWithCommanderTable();
        var editor = new CommanderEditor(workspaces);

        ContentError error = Assert.Throws<ContentError>(() => editor.Get(workspaceId, 999));
        Assert.Equal("commander.missing", error.Code);

        Cleanup(workspaces.WorkspacesRoot);
    }

    private static (WorkspaceService Workspaces, string WorkspaceId) CreateWorkspaceWithCommanderTable()
    {
        string temp = Path.Combine(Path.GetTempPath(), "ew4-cmd-" + Guid.NewGuid().ToString("N"));
        string packs = Path.Combine(temp, "packs");
        string workspaces = Path.Combine(temp, "workspaces");
        Directory.CreateDirectory(packs);
        WriteBasePackWithCommanders(packs);

        var repository = new FileModRepository(packs);
        var registry = ContentKindRegistry.CreateDefault();
        var engine = new global::Ew4.Platform.Engine.Engine();
        var builder = new ScenarioJsonBuilder();
        var resolver = new ContentResolver(registry, engine);
        var validator = new ContentValidator(registry, engine, builder);
        var loader = new ModSnapshotLoader(resolver, validator);
        var service = new WorkspaceService(workspaces, repository, registry, loader, validator);
        WorkspaceInfo created = service.Create(ModId.Base, "cmd-ws", "Commander test workspace");
        return (service, created.Id);
    }

    private static void WriteBasePackWithCommanders(string packsRoot)
    {
        string baseDir = Path.Combine(packsRoot, ModId.Base);
        Directory.CreateDirectory(Path.Combine(baseDir, "assets", "tables"));
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
        string commanderXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <commanders>
              <commander id="1" name="Alpha" rank="1" nobilityrank="1" infantry="2" cavalry="1" artillery="1" warship="0" fort="0" skill1="0" skill2="0" skill3="0" skill4="0"/>
              <commander id="2" name="Beta" rank="1" nobilityrank="1" infantry="1" cavalry="1" artillery="1" warship="0" fort="0" skill1="5" skill2="0" skill3="0" skill4="0"/>
            </commanders>
            """;
        File.WriteAllText(Path.Combine(baseDir, "assets", "tables", "def_commander.xml"), commanderXml);
    }

    private static void Cleanup(string workspacesRoot)
    {
        string? temp = Directory.GetParent(workspacesRoot)?.FullName;
        if (temp == null || !Directory.Exists(temp))
        {
            return;
        }

        try
        {
            Directory.Delete(temp, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
