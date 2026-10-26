using System.Text;
using System.Text.Json.Nodes;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Maps;

namespace Ew4.Platform.Editor.Campaign;

/// <summary>对 schema 3 场景 JSON 的结构化编辑：按能力集校验，字段范围与原记录宽度一致。</summary>
public static class ScenarioJsonPatch
{
    public static void Apply(JsonObject doc, ScenarioEdit edit, Capabilities capabilities, IMapCatalog? maps = null)
    {
        switch (edit)
        {
            case ScenarioEditArmyCell e:
                Require(capabilities.EditScenarioUnits, "scenario units");
                Set(Row(doc, "armies", e.Index), "cell", e.Cell, short.MinValue, short.MaxValue);
                break;
            case ScenarioEditSiteCell e:
                Require(capabilities.EditScenarioUnits, "scenario units");
                Set(Row(doc, "sites", e.Index), "cell", e.Cell, short.MinValue, short.MaxValue);
                break;
            case ScenarioEditFactionResources e:
                Require(capabilities.EditScenarioUnits, "scenario units");
                JsonObject resources = Row(doc, "factions", e.Index);
                Set(resources, "money", e.Money, int.MinValue, int.MaxValue);
                Set(resources, "industry", e.Industry, int.MinValue, int.MaxValue);
                Set(resources, "food", e.Food, int.MinValue, int.MaxValue);
                break;
            case ScenarioEditArmyFields e:
                Require(capabilities.EditScenarioUnits, "scenario units");
                ArmyFields(Row(doc, "armies", e.Index), e.Fields);
                break;
            case ScenarioEditSiteFields e:
                Require(capabilities.EditScenarioUnits, "scenario units");
                SiteFields(Row(doc, "sites", e.Index), e.Fields);
                break;
            case ScenarioEditFactionFields e:
                Require(capabilities.EditScenarioUnits, "scenario units");
                FactionFields(Row(doc, "factions", e.Index), e.Fields);
                break;
            case ScenarioEditEventFields e:
                Require(capabilities.EditEvents, "events");
                EventFields(Row(doc, "events", e.Index), e.Fields);
                break;
            case ScenarioEditMapInfo e:
                Require(capabilities.EditMap, "map");
                MapInfo(doc, e.Fields);
                break;
            case ScenarioEditMapSelect e:
                Require(capabilities.EditMap, "map");
                SelectMap(doc, e, maps);
                break;
            default:
                throw new ContentError("scenario.edit", "Unsupported scenario edit type.");
        }
    }

    private static void Require(bool allowed, string what)
    {
        if (!allowed) throw new ContentError("editor.capability", "This editor session may not edit " + what + ".");
    }

    private static JsonObject Row(JsonObject doc, string array, int index)
    {
        if (doc[array] is not JsonArray rows || index < 0 || index >= rows.Count || rows[index] is not JsonObject row)
            throw new ContentError("scenario.patch." + array, $"{array} index {index} is out of range.");
        return row;
    }

    private static void Set(JsonObject row, string name, long? value, long min, long max)
    {
        if (value is not { } v) return;
        if (v < min || v > max) throw new ContentError("scenario.patch.range", $"{name} must be between {min} and {max}.");
        row[name] = (int)v;
    }

    private static void Text(JsonObject row, string name, string? value)
    {
        if (value == null) return;
        if (Encoding.UTF8.GetByteCount(value) > 15) throw new ContentError("scenario.patch.range", name + " must be at most 15 bytes.");
        row[name] = value;
    }

    private static void ArmyFields(JsonObject row, ArmyFieldPatch p)
    {
        Set(row, "cell", p.Cell, short.MinValue, short.MaxValue);
        Set(row, "unit", p.Unit, sbyte.MinValue, sbyte.MaxValue);
        Set(row, "grade", p.Grade, sbyte.MinValue, sbyte.MaxValue);
        Set(row, "health", p.Health, short.MinValue, short.MaxValue);
        Set(row, "maximumHealth", p.MaximumHealth, short.MinValue, short.MaxValue);
        Set(row, "commander", p.Commander, short.MinValue, short.MaxValue);
        Set(row, "movement", p.Movement, sbyte.MinValue, sbyte.MaxValue);
        Set(row, "morale", p.Morale, sbyte.MinValue, sbyte.MaxValue);
        Set(row, "moraleDuration", p.MoraleDuration, sbyte.MinValue, sbyte.MaxValue);
    }

    private static void SiteFields(JsonObject row, SiteFieldPatch p)
    {
        Set(row, "cell", p.Cell, short.MinValue, short.MaxValue);
        Set(row, "cityId", p.CityId, short.MinValue, short.MaxValue);
        Set(row, "buildingKind", p.BuildingKind, byte.MinValue, byte.MaxValue);
        Set(row, "buildingLevel", p.BuildingLevel, byte.MinValue, byte.MaxValue);
        Set(row, "installationKind", p.InstallationKind, sbyte.MinValue, sbyte.MaxValue);
    }

    private static void FactionFields(JsonObject row, FactionFieldPatch p)
    {
        Set(row, "money", p.Money, int.MinValue, int.MaxValue);
        Set(row, "industry", p.Industry, int.MinValue, int.MaxValue);
        Set(row, "food", p.Food, int.MinValue, int.MaxValue);
        Set(row, "camp", p.Camp, int.MinValue, int.MaxValue);
        Text(row, "country", p.CountryId);
        Text(row, "variant", p.Variant);
        if (p.Ai is { } ai) row["ai"] = ai;
    }

    private static void EventFields(JsonObject row, EventFieldPatch p)
    {
        Set(row, "id", p.Id, int.MinValue, int.MaxValue);
        Set(row, "condition", p.Condition, int.MinValue, int.MaxValue);
        Set(row, "effect", p.Effect, int.MinValue, int.MaxValue);
        Set(row, "round", p.Round, int.MinValue, int.MaxValue);
        Set(row, "dialogue", p.Dialogue, int.MinValue, int.MaxValue);
        Set(row, "cell", p.Cell, int.MinValue, int.MaxValue);
        Text(row, "country", p.Country);
    }

    private static void MapInfo(JsonObject doc, MapInfoPatch p)
    {
        if (doc["map"] is not JsonObject map || map["crop"] is not JsonObject crop)
            throw new ContentError("scenario.patch.map", "Scenario has no map.");
        if (p.Width is { } w && w != crop["w"]?.GetValue<int>() || p.Height is { } h && h != crop["h"]?.GetValue<int>())
            throw new ContentError("scenario.patch.map_size", "Changing the crop size is not supported yet.");
        Set(map, "mapId", p.MapId, 0, int.MaxValue);
    }

    /// <summary>改用 base 里另一张地图或另一块裁剪；尺寸变化时归属重置为全无主，并要求现有部队/据点仍在范围内。</summary>
    private static void SelectMap(JsonObject doc, ScenarioEditMapSelect e, IMapCatalog? maps)
    {
        MapSummary summary = maps?.Describe(e.MapId) ?? throw new ContentError("map.unknown", $"Base has no map {e.MapId}.");
        MapCrop crop = e.Crop ?? new MapCrop(0, 0, summary.Width, summary.Height);
        if (maps!.Preview(e.MapId, crop) == null) throw new ContentError("map.crop", "Crop lies outside the map.");
        if (doc["map"] is not JsonObject map || map["crop"] is not JsonObject old)
            throw new ContentError("scenario.patch.map", "Scenario has no map.");
        bool resize = old["w"]?.GetValue<int>() != crop.W || old["h"]?.GetValue<int>() != crop.H
            || map["mapId"]?.GetValue<int>() != e.MapId;
        foreach (string array in new[] { "armies", "sites" })
            foreach (JsonNode? row in doc[array] as JsonArray ?? [])
                if (row?["cell"]?.GetValue<int>() is int cell && (cell < 0 || cell % summary.Width < crop.X || cell % summary.Width >= crop.X + crop.W
                    || cell / summary.Width < crop.Y || cell / summary.Width >= crop.Y + crop.H))
                    throw new ContentError("map.cells", $"A {array[..^1]} lies outside the new crop; move it first.");
        map["source"] = "base";
        map["mapId"] = e.MapId;
        map["hash"] = summary.Hash;
        map["crop"] = new JsonObject { ["x"] = crop.X, ["y"] = crop.Y, ["w"] = crop.W, ["h"] = crop.H };
        if (resize) doc["ownership"] = EmptyOwnership(crop.W, crop.H);
    }

    public static JsonObject EmptyOwnership(int width, int height)
    {
        var rows = new JsonArray();
        for (int y = 0; y < height; y++) rows.Add(new string('.', width));
        return new JsonObject { ["rows"] = rows };
    }
}
