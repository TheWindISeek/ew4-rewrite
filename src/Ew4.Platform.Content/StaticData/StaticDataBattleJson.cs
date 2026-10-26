using System.Text;
using System.Text.Json;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.StaticData;

/// <summary>语义化场景 JSON（schema 3）→ <see cref="StaticDataBattle"/>，不经过 btl 字节。</summary>
public sealed partial class StaticDataBattle
{
    private static RuleViolation Bad(string message) => new("scenario.document", message);

    private StaticDataBattle(int[] header, StaticDataFaction[] factions, StaticDataSite[] sites, StaticDataArmy[] armies,
        StaticDataEvent[] events, StaticDataMarket[] markets, StaticDataTavern[] taverns, StaticDataEnvironment[] environments,
        byte[] ownership, string fingerprint, StaticDataJurisdictionData? jurisdictions,
        Ew4.Platform.Engine.ExtensionVictoryRules? victory, StaticDataCountryIds? countryIds)
    {
        source = null; extensions = [];
        Header = Array.AsReadOnly(header); Sections = [];
        CountryIds = countryIds; countryIds?.Apply(factions, events);
        Factions = Array.AsReadOnly(factions); Sites = Array.AsReadOnly(sites); Armies = Array.AsReadOnly(armies); Events = Array.AsReadOnly(events);
        EffectiveSites = Array.AsReadOnly(sites.GroupBy(s => s.Cell).Select(g => g.Last()).ToArray());
        Markets = Array.AsReadOnly(markets); Taverns = Array.AsReadOnly(taverns);
        Environments = Array.AsReadOnly(environments); Ownership = Array.AsReadOnly(ownership);
        ObjectivesDisabled = header[11]; MinimumRound = header[12]; RatingRound = header[13];
        StaticDataLength = 0; Fingerprint = fingerprint; Jurisdictions = jurisdictions; Victory = victory;
    }

    public static StaticDataBattle FromJson(JsonElement root)
    {
        try { return Build(root); }
        catch (Exception e) when (e is InvalidOperationException or FormatException or KeyNotFoundException or JsonException or OverflowException)
        { throw Bad("scenario JSON field is missing or has the wrong type: " + e.Message); }
    }

    private static StaticDataBattle Build(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw Bad("scenario must be an object");
        var map = root.GetProperty("map");
        var crop = map.GetProperty("crop");
        int mapId = map.GetProperty("mapId").GetInt32(), left = Num(crop, "x"), top = Num(crop, "y"), width = Num(crop, "w"), height = Num(crop, "h");
        if (width < 1 || height < 1) throw Bad("map.crop must have positive size");

        var factions = Rows(root, "factions").Select(Faction).ToArray();
        var sites = Rows(root, "sites").Select(Site).ToArray();
        var armies = Rows(root, "armies").Select(Army).ToArray();
        var events = Rows(root, "events").Select(Event).ToArray();
        var markets = Rows(root, "markets").Select(m => StaticDataMarket.FromValues(Num(m, "cell"),
            m.GetProperty("slots").EnumerateArray().Select(s => new StaticDataMarketSlot(Num(s, "itemId"), Num(s, "quantity"))))).ToArray();
        var taverns = Rows(root, "taverns").Select(t => StaticDataTavern.FromValues(Num(t, "cell"),
            t.GetProperty("candidates").EnumerateArray().Select(c => new StaticDataTavernCandidate(
                Num(c, "commanderId"), Num(c, "money"), Num(c, "industry"), Num(c, "medals"), Num(c, "availableRound"))))).ToArray();
        var environments = Rows(root, "environments").Select(e => new StaticDataEnvironment(Num(e, "cell"), Num(e, "intensity"))).ToArray();
        var ownership = ParseOwnership(root.GetProperty("ownership"), width, height);

        var settings = root.GetProperty("settings");
        var header = new int[18];
        header[0] = 2; header[1] = mapId; header[2] = left; header[3] = top; header[4] = width; header[5] = height;
        header[6] = factions.Length; header[7] = sites.Length; header[8] = armies.Length; header[9] = events.Length;
        header[10] = markets.Length; header[14] = taverns.Length; header[16] = environments.Length;
        header[11] = Num(settings, "objectivesDisabled"); header[12] = Num(settings, "roundLimit"); header[13] = Num(settings, "ratingRound");

        StaticDataJurisdictionData? jurisdictions = null;
        Ew4.Platform.Engine.ExtensionVictoryRules? victory = null;
        StaticDataCountryIds? countryIds = null;
        if (root.TryGetProperty("extensions", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            foreach (var block in blocks.EnumerateArray())
            {
                switch (block.GetProperty("tag").GetString())
                {
                    case "JURI" when !block.TryGetProperty("payload", out _):
                        jurisdictions = new(1, Num(block, "mapId"), Num(block, "mapWidth"), Num(block, "mapHeight"), Num(block, "left"),
                            Num(block, "top"), Num(block, "width"), Num(block, "height"), "",
                            Array.AsReadOnly(block.GetProperty("cellCenters").EnumerateArray().Select(c => c.GetInt32()).ToArray()));
                        if (jurisdictions.CellCenters.Count != (long)width * height) throw Bad("JURI must cover every cell of the crop");
                        StaticDataBattleExtensions.Validate(jurisdictions, [], header, verifySource: false);
                        break;
                    case "VICT" when !block.TryGetProperty("payload", out _):
                        victory = ModContentJson.ParseJson<Ew4.Platform.Engine.ExtensionVictoryRules>(
                            Encoding.UTF8.GetBytes(block.GetProperty("rules").GetRawText()));
                        ExtensionVictory.Validate(victory);
                        break;
                    case "CNTR" when !block.TryGetProperty("payload", out _):
                        var events2 = block.TryGetProperty("eventCountries", out var ec) && ec.ValueKind == JsonValueKind.Object
                            ? ec.EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetString()!) : null;
                        countryIds = new(1, "", block.GetProperty("factions").EnumerateArray().Select(f => f.GetString()!).ToArray(), events2);
                        countryIds.Validate([], factions.Length, events.Length, verifySource: false);
                        break;
                }
            }

        return new(header, factions, sites, armies, events, markets, taverns, environments, ownership,
            Convert.ToHexStringLower(Ew4.Interface.Common.Sha256Hash.HashData(Encoding.UTF8.GetBytes(root.GetRawText()))),
            jurisdictions, victory, countryIds);
    }

    private static int Num(JsonElement e, string name) => e.GetProperty(name).GetInt32();
    private static IEnumerable<JsonElement> Rows(JsonElement root, string name) =>
        root.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray() : [];

    private static StaticDataFaction Faction(JsonElement f)
    {
        var values = f.GetProperty("values").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        if (values.Length != 26) throw Bad("faction.values must have 26 entries");
        return new(new(Num(f, "money"), Num(f, "industry"), Num(f, "food")), f.GetProperty("ai").GetBoolean(), Num(f, "camp"),
            Num(f, "survivalMode"), f.GetProperty("country").GetString()!, f.GetProperty("variant").GetString()!,
            f.GetProperty("incomeMultiplier").GetSingle(), f.GetProperty("aiEvasionBonus").GetSingle(), Num(f, "color"),
            Array.AsReadOnly(values), Num(f, "count172"), Num(f, "state176"));
    }

    private static StaticDataSite Site(JsonElement s) => new((short)Num(s, "cell"), (short)Num(s, "cityId"), (byte)Num(s, "buildingKind"),
        (byte)Num(s, "buildingLevel"), (byte)Num(s, "state6"), (byte)Num(s, "state7"), (sbyte)Num(s, "state8"), (sbyte)Num(s, "installationKind"),
        (byte)Num(s, "marker"), (sbyte)Num(s, "state11"), (byte)Num(s, "bridgeStyle0"), (byte)Num(s, "bridgeStyle1"),
        (byte)Num(s, "bridgeStyle2"), (byte)Num(s, "state15"));

    private static StaticDataArmy Army(JsonElement a) => new((short)Num(a, "cell"), (sbyte)Num(a, "unit"), (sbyte)Num(a, "grade"),
        (short)Num(a, "health"), (short)Num(a, "maximumHealth"), a.GetProperty("experience").GetUInt32(), (sbyte)Num(a, "veterancy"),
        (sbyte)Num(a, "movement"), (sbyte)Num(a, "morale"), (sbyte)Num(a, "moraleDuration"), (short)Num(a, "commander"),
        (byte)Num(a, "flag58"), (sbyte)Num(a, "objective"), (byte)Num(a, "behavior"), (byte)Num(a, "facing"), (byte)Num(a, "transport"),
        (byte)Num(a, "acted"), (byte)Num(a, "forceRecovery"), (byte)Num(a, "flag5c"), (sbyte)Num(a, "state70"),
        (sbyte)Num(a, "constructionTurns"), (short)Num(a, "behaviorExpiry"), (byte)Num(a, "restoreBehavior"), (sbyte)Num(a, "burning"));

    private static StaticDataEvent Event(JsonElement e)
    {
        int triggered = Num(e, "triggered");
        if (triggered is < 0 or > 1) throw Bad("event.triggered must be 0 or 1");
        return new(Num(e, "id"), Num(e, "condition"), Num(e, "effect"), Num(e, "round"), Num(e, "dialogue"), Num(e, "cell"),
            e.GetProperty("country").GetString()!, triggered != 0, (byte)Num(e, "padding41"), (byte)Num(e, "padding42"), (byte)Num(e, "padding43"));
    }

    private static byte[] ParseOwnership(JsonElement ownership, int width, int height)
    {
        var rows = ownership.GetProperty("rows");
        byte[]? raw = ownership.TryGetProperty("raw", out var r) && r.ValueKind == JsonValueKind.String ? Convert.FromBase64String(r.GetString()!) : null;
        if (rows.GetArrayLength() != height) throw Bad("ownership.rows must have one entry per row");
        var data = new byte[checked(width * height)];
        int y = 0;
        foreach (var row in rows.EnumerateArray())
        {
            string text = row.GetString()!;
            if (text.Length != width) throw Bad("ownership row width mismatch");
            for (int x = 0; x < width; x++)
            {
                char c = text[x];
                data[y * width + x] = c switch
                {
                    '.' => 0xFF,
                    >= '0' and <= '9' => (byte)(c - '0'),
                    >= 'a' and <= 'z' => (byte)(c - 'a' + 10),
                    >= 'A' and <= 'Z' => (byte)(c - 'A' + 36),
                    '?' when raw != null && raw.Length == data.Length => raw[y * width + x],
                    _ => throw Bad("invalid ownership character"),
                };
            }
            y++;
        }
        return data;
    }
}
