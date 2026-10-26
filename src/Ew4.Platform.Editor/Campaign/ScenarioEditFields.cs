using System.Text.Json;

namespace Ew4.Platform.Editor.Campaign;

public sealed record ScenarioEditArmyFields(int Index, ArmyFieldPatch Fields) : ScenarioEdit;

public sealed record ScenarioEditSiteFields(int Index, SiteFieldPatch Fields) : ScenarioEdit;

public sealed record ScenarioEditFactionFields(int Index, FactionFieldPatch Fields) : ScenarioEdit;

public sealed record ScenarioEditEventFields(int Index, EventFieldPatch Fields) : ScenarioEdit;

public sealed record ScenarioEditMapInfo(MapInfoPatch Fields) : ScenarioEdit;

public sealed record ArmyFieldPatch(
    short? Cell = null,
    sbyte? Unit = null,
    sbyte? Grade = null,
    short? Health = null,
    short? MaximumHealth = null,
    short? Commander = null,
    sbyte? Movement = null,
    sbyte? Morale = null,
    sbyte? MoraleDuration = null);

public sealed record SiteFieldPatch(
    short? Cell = null,
    short? CityId = null,
    byte? BuildingKind = null,
    byte? BuildingLevel = null,
    sbyte? InstallationKind = null);

public sealed record FactionFieldPatch(
    long? Money = null,
    long? Industry = null,
    long? Food = null,
    int? Camp = null,
    string? CountryId = null,
    string? Variant = null,
    bool? Ai = null);

public sealed record EventFieldPatch(
    int? Id = null,
    int? Condition = null,
    int? Effect = null,
    int? Round = null,
    int? Dialogue = null,
    int? Cell = null,
    string? Country = null);

public sealed record MapInfoPatch(int? MapId = null, int? Width = null, int? Height = null);

public static class ScenarioEditFieldJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ArmyFieldPatch ParseArmy(JsonElement fields) =>
        fields.ValueKind == JsonValueKind.Undefined || fields.ValueKind == JsonValueKind.Null
            ? new ArmyFieldPatch()
            : fields.Deserialize<ArmyFieldPatch>(Options) ?? new ArmyFieldPatch();

    public static SiteFieldPatch ParseSite(JsonElement fields) =>
        fields.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new SiteFieldPatch()
            : fields.Deserialize<SiteFieldPatch>(Options) ?? new SiteFieldPatch();

    public static FactionFieldPatch ParseFaction(JsonElement fields) =>
        fields.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new FactionFieldPatch()
            : fields.Deserialize<FactionFieldPatch>(Options) ?? new FactionFieldPatch();

    public static EventFieldPatch ParseEvent(JsonElement fields) =>
        fields.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new EventFieldPatch()
            : fields.Deserialize<EventFieldPatch>(Options) ?? new EventFieldPatch();

    public static MapInfoPatch ParseMap(JsonElement fields) =>
        fields.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? new MapInfoPatch()
            : fields.Deserialize<MapInfoPatch>(Options) ?? new MapInfoPatch();
}
