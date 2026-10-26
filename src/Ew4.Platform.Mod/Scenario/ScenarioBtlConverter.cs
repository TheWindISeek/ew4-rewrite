using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Ew4.Platform.Mod.Scenario;

/// <summary>
/// btl v2 ↔ 场景 JSON。已语义化：<c>extensions</c>（辖区/胜利/国家 id）、<c>map</c>、<c>ownership</c>、<c>factions</c>、<c>sites</c>、<c>armies</c>、<c>events</c>、<c>markets</c>、<c>taverns</c>、<c>environments</c>、<c>settings</c>；
/// 其余内容无损放在 <c>extras.btl</c>（其余 header 整数、尚未语义化的分段、扩展尾部），保证往返逐字节一致。
/// 后续逐步把 extras 语义化。
/// </summary>
public sealed class ScenarioBtlConverter : IScenarioBtlConverter
{
    public const int SchemaVersion = 3;
    private const int HeaderInts = 18;
    private const int HeaderBytes = HeaderInts * 4;

    private sealed record Section(string Name, int CountIndex, int Stride);

    private sealed record Semantic(string Section, string Property, RecordSchema Schema, int CountIndex);

    // 已语义化的分段：JSON 数组 ↔ 定长记录；条数写回 header[CountIndex]。
    private static readonly Semantic[] Semantics =
    [
        new("factions", "factions", BtlRecords.Faction, 6),
        new("sites", "sites", BtlRecords.Site, 7),
        new("armies", "armies", BtlRecords.Army, 8),
        new("opaque44", "events", BtlRecords.Event, 9),
        new("opaque8", "environments", BtlRecords.Environment, 16),
        new("opaque116", "markets", BtlRecords.Market, 10),
        new("opaque108", "taverns", BtlRecords.Tavern, 14),
    ];

    // 与 btl v2 磁盘布局一致；CountIndex = -1 表示按地图面积计数。
    private static readonly Section[] Layout =
    [
        new("factions", 6, 180),
        new("ownership", -1, 1),
        new("sites", 7, 16),
        new("armies", 8, 32),
        new("opaque8", 16, 8),
        new("opaque116", 10, 116),
        new("opaque108", 14, 108),
        new("opaque44", 9, 44),
    ];

    public ConvertResult ToJson(string scenarioId, ReadOnlyMemory<byte> btl, ConvertContext context)
    {
        ReadOnlySpan<byte> raw = btl.Span;
        if (raw.Length < HeaderBytes || ReadInt(raw, 0) != 2)
            throw new ModError("mod.scenario_btl", "Only scenario BTL version 2 is supported");
        int[] header = new int[HeaderInts];
        for (int i = 0; i < header.Length; i++) header[i] = ReadInt(raw, i * 4);

        int cursor = HeaderBytes;
        var sections = new List<(string Name, byte[] Data)>();
        foreach (Section section in Layout)
        {
            long length = (long)Count(header, section) * section.Stride;
            if (length < 0 || cursor + length > raw.Length)
                throw new ModError("mod.scenario_btl", "Invalid or truncated section: " + section.Name);
            sections.Add((section.Name, raw.Slice(cursor, (int)length).ToArray()));
            cursor += (int)length;
        }

        byte[] trailer = raw[cursor..].ToArray();
        int bodyLength = cursor;

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", SchemaVersion);
            json.WriteString("id", scenarioId);
            if (!string.IsNullOrWhiteSpace(context.Name)) json.WriteString("name", context.Name);
            json.WriteStartObject("map");
            json.WriteString("source", "base");
            json.WriteNumber("mapId", header[1]);
            json.WriteStartObject("crop");
            json.WriteNumber("x", header[2]);
            json.WriteNumber("y", header[3]);
            json.WriteNumber("w", header[4]);
            json.WriteNumber("h", header[5]);
            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteStartObject("settings");
            json.WriteNumber("objectivesDisabled", header[11]);
            json.WriteNumber("roundLimit", header[12]);
            json.WriteNumber("ratingRound", header[13]);
            json.WriteEndObject();
            json.WritePropertyName("ownership");
            WriteOwnership(json, sections.Single(x => x.Name == "ownership").Data, header[4], header[5]);
            foreach (Semantic semantic in Semantics)
            {
                byte[] data = sections.Single(x => x.Name == semantic.Section).Data;
                json.WriteStartArray(semantic.Property);
                for (int offset = 0; offset < data.Length; offset += semantic.Schema.Stride)
                    semantic.Schema.WriteRow(json, data.AsSpan(offset, semantic.Schema.Stride));
                json.WriteEndArray();
            }

            bool semanticTrailer = trailer.Length == 0 || BtlExtensions.TryWrite(json, raw[..bodyLength], trailer);
            json.WriteStartObject("extras");
            json.WriteStartObject("btl");
            json.WriteString("format", "btl-v2");
            json.WriteStartArray("header");
            foreach (int value in header) json.WriteNumberValue(value);
            json.WriteEndArray();
            json.WriteStartObject("sections");
            foreach ((string name, byte[] data) in sections)
                if (name != "ownership" && Semantics.All(x => x.Section != name)) json.WriteString(name, Convert.ToBase64String(data));
            json.WriteEndObject();
            if (!semanticTrailer) json.WriteString("trailer", Convert.ToBase64String(trailer));
            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return new(Encoding.UTF8.GetString(stream.ToArray()), null, Array.Empty<ConversionDiagnostic>());
    }

    public ConvertResult ToBtl(JsonElement scenario, ConvertContext context)
    {
        var diagnostics = new List<ConversionDiagnostic>();
        if (!scenario.TryGetProperty("schema", out JsonElement schema) || schema.ValueKind != JsonValueKind.Number || schema.GetInt32() != SchemaVersion)
            throw new ModError("mod.scenario_schema", "Scenario schema 3 is required");
        if (!scenario.TryGetProperty("extras", out JsonElement extras) || !extras.TryGetProperty("btl", out JsonElement btl)
            || btl.ValueKind != JsonValueKind.Object)
            throw new ModError("mod.scenario_extras", "Scenario has no extras.btl; btl export needs the preserved legacy data");

        int[] header;
        try
        {
            header = btl.GetProperty("header").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new ModError("mod.scenario_extras", "extras.btl.header is invalid");
        }

        if (header.Length != HeaderInts || header[0] != 2)
            throw new ModError("mod.scenario_extras", "extras.btl.header must be the 18-integer BTL v2 header");

        // 地图引用与各数组是权威来源：写回 header 的对应整数。
        if (scenario.TryGetProperty("map", out JsonElement map) && map.ValueKind == JsonValueKind.Object)
        {
            header[1] = Int(map, "mapId", header[1], "map");
            if (map.TryGetProperty("crop", out JsonElement crop) && crop.ValueKind == JsonValueKind.Object)
            {
                header[2] = Int(crop, "x", header[2], "map.crop");
                header[3] = Int(crop, "y", header[3], "map.crop");
                header[4] = Int(crop, "w", header[4], "map.crop");
                header[5] = Int(crop, "h", header[5], "map.crop");
            }
        }

        if (scenario.TryGetProperty("settings", out JsonElement settings) && settings.ValueKind == JsonValueKind.Object)
        {
            header[11] = Int(settings, "objectivesDisabled", header[11], "settings");
            header[12] = Int(settings, "roundLimit", header[12], "settings");
            header[13] = Int(settings, "ratingRound", header[13], "settings");
        }

        var built = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (scenario.TryGetProperty("ownership", out JsonElement ownership))
            built["ownership"] = ReadOwnership(ownership, header[4], header[5]);
        foreach (Semantic semantic in Semantics)
        {
            if (!scenario.TryGetProperty(semantic.Property, out JsonElement array))
            {
                // 兼容早期只有 extras 的 schema 3 文件：该分段仍以 extras.btl.sections 为准。
                if (btl.TryGetProperty("sections", out JsonElement legacySections) && legacySections.TryGetProperty(semantic.Section, out _)) continue;
                throw new ModError("mod.scenario_field", semantic.Property + " must be an array");
            }

            if (array.ValueKind != JsonValueKind.Array)
                throw new ModError("mod.scenario_field", semantic.Property + " must be an array");
            using var rows = new MemoryStream();
            int count = 0;
            foreach (JsonElement row in array.EnumerateArray())
                rows.Write(semantic.Schema.ReadRow(row, semantic.Property + "[" + count++ + "]"));
            built[semantic.Section] = rows.ToArray();
            header[semantic.CountIndex] = count;
        }

        JsonElement sections = btl.TryGetProperty("sections", out JsonElement s) && s.ValueKind == JsonValueKind.Object
            ? s : throw new ModError("mod.scenario_extras", "extras.btl.sections is missing");
        using var output = new MemoryStream();
        Span<byte> word = stackalloc byte[4];
        foreach (int value in header)
        {
            BinaryPrimitives.WriteInt32LittleEndian(word, value);
            output.Write(word);
        }

        foreach (Section section in Layout)
        {
            byte[] data;
            if (built.TryGetValue(section.Name, out byte[]? semanticData)) data = semanticData;
            else
            {
                if (!sections.TryGetProperty(section.Name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
                    throw new ModError("mod.scenario_extras", "Missing section: " + section.Name);
                data = DecodeBase64(element.GetString()!, "extras.btl.sections." + section.Name);
            }

            long expected = (long)Count(header, section) * section.Stride;
            if (data.Length != expected)
                throw new ModError("mod.scenario_extras", $"Section {section.Name} length {data.Length} != {expected}");
            output.Write(data);
        }

        if (scenario.TryGetProperty("extensions", out JsonElement extensions))
            output.Write(BtlExtensions.Build(extensions, output.GetBuffer().AsSpan(0, (int)output.Length)));
        else if (btl.TryGetProperty("trailer", out JsonElement trailer) && trailer.ValueKind == JsonValueKind.String)
            output.Write(DecodeBase64(trailer.GetString()!, "extras.btl.trailer"));

        return new(null, output.ToArray(), diagnostics);
    }

    // 归属图：每格一个字符，'.' = 无主（0xFF），0-9a-z = 势力序号 0–35，A-Z = 36–61，'?' = 其它字节（见 raw）。
    private static char OwnerChar(byte value) => value switch
    {
        0xFF => '.',
        < 10 => (char)('0' + value),
        < 36 => (char)('a' + value - 10),
        < 62 => (char)('A' + value - 36),
        _ => '?',
    };

    private static int OwnerValue(char c) => c switch
    {
        '.' => 0xFF,
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'z' => c - 'a' + 10,
        >= 'A' and <= 'Z' => c - 'A' + 36,
        _ => -1,
    };

    private static void WriteOwnership(Utf8JsonWriter json, byte[] data, int width, int height)
    {
        json.WriteStartObject();
        json.WriteStartArray("rows");
        for (int y = 0; y < height; y++)
        {
            var row = new char[width];
            for (int x = 0; x < width; x++) row[x] = OwnerChar(data[y * width + x]);
            json.WriteStringValue(new string(row));
        }

        json.WriteEndArray();
        if (data.Any(b => OwnerChar(b) == '?')) json.WriteString("raw", Convert.ToBase64String(data));
        json.WriteEndObject();
    }

    private static byte[] ReadOwnership(JsonElement element, int width, int height)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("rows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            throw new ModError("mod.scenario_field", "ownership.rows must be an array");
        byte[]? template = null;
        if (element.TryGetProperty("raw", out JsonElement raw) && raw.ValueKind == JsonValueKind.String)
            template = DecodeBase64(raw.GetString()!, "ownership.raw");
        if (rows.GetArrayLength() != height)
            throw new ModError("mod.scenario_field", $"ownership.rows must have {height} rows");
        var data = new byte[width * height];
        int y = 0;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            string text = row.ValueKind == JsonValueKind.String ? row.GetString()! : "";
            if (text.Length != width) throw new ModError("mod.scenario_field", $"ownership.rows[{y}] must have {width} characters");
            for (int x = 0; x < width; x++)
            {
                int value = OwnerValue(text[x]);
                if (value >= 0) data[y * width + x] = (byte)value;
                else if (text[x] == '?' && template != null && template.Length == data.Length) data[y * width + x] = template[y * width + x];
                else throw new ModError("mod.scenario_field", $"ownership.rows[{y}][{x}] has an invalid character '{text[x]}'");
            }

            y++;
        }

        return data;
    }

    private static int Int(JsonElement parent, string name, int fallback, string path)
    {
        if (!parent.TryGetProperty(name, out JsonElement value)) return fallback;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number : throw new ModError("mod.scenario_field", path + "." + name + " must be an integer");
    }

    private static byte[] DecodeBase64(string text, string path)
    {
        try { return Convert.FromBase64String(text); }
        catch (FormatException) { throw new ModError("mod.scenario_extras", path + " is not valid base64"); }
    }

    private static int Count(int[] header, Section section)
    {
        if (section.CountIndex >= 0) return header[section.CountIndex];
        int width = header[4], height = header[5];
        if (width < 1 || height < 1 || width > 32768 || height > 32768 || (long)width * height > 100_000_000)
            throw new ModError("mod.scenario_btl", "Invalid map dimensions");
        return width * height;
    }

    private static int ReadInt(ReadOnlySpan<byte> raw, int offset) => BinaryPrimitives.ReadInt32LittleEndian(raw.Slice(offset, 4));
}
