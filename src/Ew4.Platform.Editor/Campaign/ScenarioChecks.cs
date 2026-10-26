using System.Text.Json.Nodes;
using Ew4.Interface.Common;
using Ew4.Platform.Content.Maps;

namespace Ew4.Platform.Editor.Campaign;

/// <summary>场景 JSON 的地图引用检查。Warning（如 hash 不一致）不阻止加载与游玩。</summary>
public static class ScenarioChecks
{
    public static IReadOnlyList<Diagnostic> Map(JsonObject doc, IMapCatalog? maps, string path)
    {
        var result = new List<Diagnostic>();
        if (doc["map"] is not JsonObject map || map["crop"] is not JsonObject crop)
        {
            result.Add(new(Severity.Error, "map.missing", path, "Scenario has no map reference."));
            return result;
        }

        string? source = map["source"]?.GetValue<string>();
        if (source != "base")
        {
            result.Add(new(Severity.Error, "map.source", path, "map.source must be 'base'."));
            return result;
        }

        if (maps == null) return result;
        int mapId = map["mapId"]!.GetValue<int>();
        MapSummary? summary = maps.Describe(mapId);
        if (summary == null)
        {
            result.Add(new(Severity.Error, "map.unknown", path, $"Base has no map {mapId}."));
            return result;
        }

        int x = crop["x"]!.GetValue<int>(), y = crop["y"]!.GetValue<int>(), w = crop["w"]!.GetValue<int>(), h = crop["h"]!.GetValue<int>();
        if (w < 1 || h < 1 || x < 0 || y < 0 || x + w > summary.Width || y + h > summary.Height)
            result.Add(new(Severity.Error, "map.crop", path, $"Crop {x},{y} {w}x{h} lies outside map {mapId} ({summary.Width}x{summary.Height})."));
        string? hash = map["hash"]?.GetValue<string>();
        if (hash != null && !string.Equals(hash, summary.Hash, StringComparison.OrdinalIgnoreCase))
            result.Add(new(Severity.Warning, "map.hash_mismatch", path, "The referenced map has changed since this scenario was made."));
        return result;
    }
}
