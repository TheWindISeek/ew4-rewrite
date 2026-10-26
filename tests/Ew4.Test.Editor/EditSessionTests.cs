using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Interface.Common;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Maps;
using Ew4.Platform.Editor.Campaign;
using Ew4.Platform.Mod.Scenario;
using Xunit;

namespace Ew4.Test.Editor;

public sealed class EditSessionTests
{
    private static string? BaseDirectory()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "mods", "base");
            if (File.Exists(Path.Combine(candidate, "scenarios", "campaign1_01.json"))) return candidate;
        }

        return null;
    }

    private static EditSession? Open(EditorMode mode = EditorMode.BaseTweak) =>
        BaseDirectory() is { } dir ? EditSession.Open(new DirectoryModSource(dir), "campaign1_01", mode) : null;

    [Fact]
    public void Army_cell_edit_changes_only_that_army()
    {
        if (Open() is not { } session) return;
        var before = (JsonObject)session.Document.DeepClone();
        int cell = before["armies"]![0]!["cell"]!.GetValue<int>() == 0 ? 1 : 0;
        session.Apply(new ScenarioEditArmyCell(0, (short)cell));
        Assert.Equal(cell, session.Document["armies"]![0]!["cell"]!.GetValue<int>());
        Assert.Equal(before["armies"]!.AsArray().Count, session.Document["armies"]!.AsArray().Count);
        Assert.Equal(before["armies"]![1]!.ToJsonString(), session.Document["armies"]![1]!.ToJsonString());
        Assert.Equal(before["factions"]!.ToJsonString(), session.Document["factions"]!.ToJsonString());
    }

    [Fact]
    public void Faction_fields_are_written_with_json_names()
    {
        if (Open() is not { } session) return;
        session.Apply(new ScenarioEditFactionFields(0, new FactionFieldPatch(Money: 777, CountryId: "fra", Ai: true)));
        JsonNode faction = session.Document["factions"]![0]!;
        Assert.Equal(777, faction["money"]!.GetValue<int>());
        Assert.Equal("fra", faction["country"]!.GetValue<string>());
        Assert.True(faction["ai"]!.GetValue<bool>());
    }

    [Fact]
    public void Out_of_range_and_bad_index_are_rejected_without_changing_the_document()
    {
        if (Open() is not { } session) return;
        string before = session.Document.ToJsonString();
        Assert.Equal("scenario.patch.range", Assert.Throws<ContentError>(() =>
            session.Apply(new ScenarioEditFactionResources(0, long.MaxValue, 0, 0))).Code);
        Assert.Throws<ContentError>(() => session.Apply(new ScenarioEditArmyCell(9999, 1)));
        Assert.Throws<ContentError>(() => session.Apply(new ScenarioEditFactionFields(0, new FactionFieldPatch(Variant: "this-name-is-too-long"))));
        Assert.Equal(before, session.Document.ToJsonString());
    }

    [Fact]
    public void Base_tweak_cannot_edit_the_map_but_free_can()
    {
        if (Open() is not { } tweak || Open(EditorMode.Free) is not { } free) return;
        Assert.False(tweak.Capabilities.EditMap);
        Assert.Equal("editor.capability", Assert.Throws<ContentError>(() => tweak.Apply(new ScenarioEditMapInfo(new MapInfoPatch(MapId: 3)))).Code);
        Assert.Throws<ContentError>(() => free.Apply(new ScenarioEditMapInfo(new MapInfoPatch(Width: 1))));
    }

    [Fact]
    public void Document_for_another_scenario_or_old_schema_is_rejected()
    {
        if (Open() is not { } session) return;
        byte[] json = session.ToUtf8();
        Assert.Equal("scenario.id", Assert.Throws<ContentError>(() => EditSession.FromJson(json, "other")).Code);
        var old = (JsonObject)JsonNode.Parse(json)!;
        old["schema"] = 2;
        Assert.Equal("scenario.schema", Assert.Throws<ContentError>(() => EditSession.FromJson(old.ToJsonString().Select(c => (byte)c).ToArray(), "campaign1_01")).Code);
    }

    [Fact]
    public void Edited_document_survives_save_reload_and_binary_roundtrip()
    {
        if (Open() is not { } session) return;
        session.Apply(new ScenarioEditFactionResources(0, 1234, 56, 789));
        byte[] json = session.ToUtf8();
        EditSession reloaded = EditSession.FromJson(json, "campaign1_01");
        Assert.Equal(1234, reloaded.Document["factions"]![0]!["money"]!.GetValue<int>());
        Assert.Empty(reloaded.Validate());
        Assert.True(ScenarioRoundtrip.CheckJson(new ScenarioBtlConverter(), "campaign1_01", json).Stable);
    }

    private static IMapCatalog? Catalog() =>
        BaseDirectory() is { } dir ? MapCatalog.FromMod(new DirectoryModSource(dir)) : null;

    [Fact]
    public void Map_catalog_lists_describes_and_previews_base_maps()
    {
        if (Catalog() is not { } maps) return;
        MapSummary one = Assert.IsType<MapSummary>(maps.Describe(1));
        Assert.Contains(maps.List(), m => m.MapId == 1);
        Assert.Equal(64, one.Hash.Length);
        MapPreview preview = Assert.IsType<MapPreview>(maps.Preview(1, new MapCrop(0, 0, 3, 2)));
        Assert.Equal(6, preview.Cells.Count);
        Assert.Null(maps.Preview(1, new MapCrop(one.Width, 0, 1, 1)));
        Assert.Null(maps.Describe(-5));
    }

    [Fact]
    public void Map_hash_mismatch_is_a_warning_not_an_error()
    {
        if (Catalog() is not { } maps || BaseDirectory() is not { } dir) return;
        EditSession session = EditSession.Open(new DirectoryModSource(dir), "campaign1_01", EditorMode.BaseTweak, maps);
        Assert.DoesNotContain(session.Validate(), d => d.Severity != Severity.Info);
        var doc = (JsonObject)JsonNode.Parse(session.ToUtf8())!;
        doc["map"]!["hash"] = "00";
        EditSession stale = EditSession.FromJson(System.Text.Encoding.UTF8.GetBytes(doc.ToJsonString()), "campaign1_01", maps: maps);
        Diagnostic warning = Assert.Single(stale.Validate());
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal("map.hash_mismatch", warning.Code);
    }

    [Fact]
    public void Free_mode_creates_an_empty_scenario_on_a_base_map_crop()
    {
        if (Catalog() is not { } maps) return;
        EditSession session = EditSession.CreateFree("my_free", "Mine", 1, new MapCrop(13, 30, 21, 21), maps);
        Assert.Empty(session.Validate());
        Assert.Equal(21, session.Document["ownership"]!["rows"]!.AsArray().Count);
        Assert.Equal(maps.Describe(1)!.Hash, session.Document["map"]!["hash"]!.GetValue<string>());
        EditSession reloaded = EditSession.FromJson(session.ToUtf8(), "my_free", EditorMode.Free, maps);
        Assert.Empty(reloaded.Document["armies"]!.AsArray());
        Assert.Equal("scenario.id", Assert.Throws<ContentError>(() => EditSession.CreateFree("Bad Id", "x", 1, null, maps)).Code);
        Assert.Equal("map.unknown", Assert.Throws<ContentError>(() => EditSession.CreateFree("my_free", "x", -1, null, maps)).Code);
        Assert.Equal("map.crop", Assert.Throws<ContentError>(() => EditSession.CreateFree("my_free", "x", 1, new MapCrop(0, 0, 9999, 1), maps)).Code);
    }

    [Fact]
    public void Map_select_requires_capability_and_keeps_units_inside_the_crop()
    {
        if (Catalog() is not { } maps || BaseDirectory() is not { } dir) return;
        var pack = new DirectoryModSource(dir);
        EditSession tweak = EditSession.Open(pack, "campaign1_01", EditorMode.BaseTweak, maps);
        Assert.Equal("editor.capability", Assert.Throws<ContentError>(() => tweak.Apply(new ScenarioEditMapSelect(1))).Code);

        EditSession free = EditSession.Open(pack, "campaign1_01", EditorMode.Free, maps);
        string before = free.Document.ToJsonString();
        Assert.Equal("map.cells", Assert.Throws<ContentError>(() => free.Apply(new ScenarioEditMapSelect(1, new MapCrop(0, 0, 2, 2)))).Code);
        Assert.Equal(before, free.Document.ToJsonString());
        free.Apply(new ScenarioEditMapSelect(1, new MapCrop(13, 30, 21, 21)));
        Assert.Equal(21, free.Document["map"]!["crop"]!["w"]!.GetValue<int>());
    }
}
