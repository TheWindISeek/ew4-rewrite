using System.Text.Json;
using Ew4.Platform.Content;

namespace Ew4.Platform.Editor.Campaign;

public static class ScenarioEditHttpJson
{
    public static ScenarioEdit Parse(byte[] utf8)
    {
        using var document = JsonDocument.Parse(utf8);
        JsonElement root = document.RootElement;
        string type = root.GetProperty("type").GetString()
            ?? throw new ContentError("scenario.edit", "Missing edit type.");
        int index = root.TryGetProperty("index", out JsonElement indexEl) ? indexEl.GetInt32() : 0;
        short cell = root.TryGetProperty("cell", out JsonElement cellEl) ? (short)cellEl.GetInt32() : (short)0;
        long money = root.TryGetProperty("money", out JsonElement moneyEl) ? moneyEl.GetInt64() : 0;
        long industry = root.TryGetProperty("industry", out JsonElement industryEl) ? industryEl.GetInt64() : 0;
        long food = root.TryGetProperty("food", out JsonElement foodEl) ? foodEl.GetInt64() : 0;
        JsonElement fields = root.TryGetProperty("fields", out JsonElement fieldsEl) ? fieldsEl : default;

        return type switch
        {
            "armyCell" => new ScenarioEditArmyCell(index, cell),
            "siteCell" => new ScenarioEditSiteCell(index, cell),
            "factionResources" => new ScenarioEditFactionResources(index, money, industry, food),
            "army" => new ScenarioEditArmyFields(index, ScenarioEditFieldJson.ParseArmy(fields)),
            "site" => new ScenarioEditSiteFields(index, ScenarioEditFieldJson.ParseSite(fields)),
            "faction" => new ScenarioEditFactionFields(index, ScenarioEditFieldJson.ParseFaction(fields)),
            "event" => new ScenarioEditEventFields(index, ScenarioEditFieldJson.ParseEvent(fields)),
            "mapSelect" => new ScenarioEditMapSelect(
                fields.GetProperty("mapId").GetInt32(),
                fields.TryGetProperty("w", out JsonElement w) && w.ValueKind == JsonValueKind.Number
                    ? new Ew4.Platform.Content.Maps.MapCrop(fields.GetProperty("x").GetInt32(), fields.GetProperty("y").GetInt32(), w.GetInt32(), fields.GetProperty("h").GetInt32())
                    : null),
            "map" => new ScenarioEditMapInfo(ScenarioEditFieldJson.ParseMap(fields)),
            _ => throw new ContentError("scenario.edit", "Unknown edit type: " + type),
        };
    }
}
