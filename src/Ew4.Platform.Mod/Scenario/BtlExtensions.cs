using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ew4.Platform.Mod.Scenario;

/// <summary>
/// btl 尾部扩展容器（<c>EW4XBTL</c>）↔ JSON 的 <c>extensions</c> 数组。
/// JURI（辖区）、VICT（胜利条件）、CNTR（国家 id）语义化；其绑定 btl 主体的 SHA-256 不存进 JSON，导出时按主体重算。
/// 无法按语义无损重建的块（或未知标签）以 <c>payload</c>（base64）原样保留。
/// </summary>
internal static class BtlExtensions
{
    private static readonly byte[] Magic = "EW4XBTL\0"u8.ToArray();
    private const int MaximumBytes = 64 * 1024 * 1024;

    private sealed record Block(string Tag, uint Version, uint Flags, byte[] Payload);

    /// <summary>尾部格式非法时返回 false，调用方改为原样保留整个尾部。</summary>
    public static bool TryWrite(Utf8JsonWriter json, ReadOnlySpan<byte> body, ReadOnlySpan<byte> trailer)
    {
        if (!TryParse(trailer, out List<Block> blocks)) return false;
        json.WriteStartArray("extensions");
        foreach (Block block in blocks)
        {
            json.WriteStartObject();
            json.WriteString("tag", block.Tag);
            if (!TryWriteSemantic(json, body, block))
            {
                json.WriteNumber("version", block.Version);
                json.WriteNumber("flags", block.Flags);
                json.WriteString("payload", Convert.ToBase64String(block.Payload));
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();
        return true;
    }

    public static byte[] Build(JsonElement extensions, ReadOnlySpan<byte> body)
    {
        if (extensions.ValueKind != JsonValueKind.Array) throw new ModError("mod.scenario_field", "extensions must be an array");
        var blocks = new List<Block>();
        int index = 0;
        foreach (JsonElement element in extensions.EnumerateArray())
        {
            string path = "extensions[" + index++ + "]";
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("tag", out JsonElement tagElement)
                || tagElement.ValueKind != JsonValueKind.String)
                throw new ModError("mod.scenario_field", path + ".tag is required");
            string tag = tagElement.GetString()!;
            if (tag.Length != 4 || tag.Any(c => c is < 'A' or > 'Z'))
                throw new ModError("mod.scenario_field", path + ".tag must be four uppercase ASCII letters");
            if (element.TryGetProperty("payload", out JsonElement payload) && payload.ValueKind == JsonValueKind.String)
            {
                byte[] bytes;
                try { bytes = Convert.FromBase64String(payload.GetString()!); }
                catch (FormatException) { throw new ModError("mod.scenario_field", path + ".payload is not valid base64"); }
                blocks.Add(new(tag, UInt(element, "version", 1, path), UInt(element, "flags", 0, path), bytes));
                continue;
            }

            byte[] built = tag switch
            {
                "JURI" => BuildJurisdictions(element, body, path),
                "VICT" => BuildVictory(element, body, path),
                "CNTR" => BuildCountries(element, body, path),
                _ => throw new ModError("mod.scenario_field", path + ": unknown tag '" + tag + "' needs a payload"),
            };
            blocks.Add(new(tag, 1, 1, built));
        }

        return Container(blocks);
    }

    private static bool TryWriteSemantic(Utf8JsonWriter json, ReadOnlySpan<byte> body, Block block)
    {
        if (block.Version != 1 || block.Flags != 1) return false;
        try
        {
            switch (block.Tag)
            {
                case "JURI":
                {
                    byte[] p = block.Payload;
                    if (p.Length < 64) return false;
                    int count = ReadInt(p, 28);
                    if (count < 0 || p.Length != 64L + 4L * count) return false;
                    var centers = new int[count];
                    for (int i = 0; i < count; i++) centers[i] = ReadInt(p, 64 + i * 4);
                    string[] names = ["mapId", "left", "top", "width", "height", "mapWidth", "mapHeight"];
                    var values = names.Select((_, i) => ReadInt(p, i * 4)).ToArray();
                    byte[] rebuilt = JurisdictionPayload(values, centers, body);
                    if (!rebuilt.AsSpan().SequenceEqual(p)) return false;
                    for (int i = 0; i < names.Length; i++) json.WriteNumber(names[i], values[i]);
                    json.WriteStartArray("cellCenters");
                    foreach (int center in centers) json.WriteNumberValue(center);
                    json.WriteEndArray();
                    return true;
                }
                case "VICT":
                {
                    using JsonDocument doc = JsonDocument.Parse(block.Payload);
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("rules", out JsonElement rules)
                        || !SameBytes(VictoryPayload(rules, body), block.Payload)) return false;
                    json.WritePropertyName("rules");
                    rules.WriteTo(json);
                    return true;
                }
                case "CNTR":
                {
                    using JsonDocument doc = JsonDocument.Parse(block.Payload);
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("factions", out JsonElement factions)) return false;
                    JsonElement? events = root.TryGetProperty("eventCountries", out JsonElement e) ? e : null;
                    if (!SameBytes(CountryPayload(factions, events, body), block.Payload)) return false;
                    json.WritePropertyName("factions");
                    factions.WriteTo(json);
                    if (events is { } eventCountries && eventCountries.ValueKind != JsonValueKind.Null)
                    {
                        json.WritePropertyName("eventCountries");
                        eventCountries.WriteTo(json);
                    }

                    return true;
                }
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ModError) { }

        return false;
    }

    private static byte[] BuildJurisdictions(JsonElement element, ReadOnlySpan<byte> body, string path)
    {
        string[] names = ["mapId", "left", "top", "width", "height", "mapWidth", "mapHeight"];
        var values = names.Select(n => Int(element, n, path)).ToArray();
        if (!element.TryGetProperty("cellCenters", out JsonElement centers) || centers.ValueKind != JsonValueKind.Array)
            throw new ModError("mod.scenario_field", path + ".cellCenters must be an array");
        int[] cells;
        try { cells = centers.EnumerateArray().Select(c => c.GetInt32()).ToArray(); }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        { throw new ModError("mod.scenario_field", path + ".cellCenters must contain integers"); }
        return JurisdictionPayload(values, cells, body);
    }

    private static byte[] JurisdictionPayload(int[] values, int[] centers, ReadOnlySpan<byte> body)
    {
        var p = new byte[64 + 4 * centers.Length];
        for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(i * 4), values[i]);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(28), centers.Length);
        Ew4.Interface.Common.Sha256Hash.HashData(body).CopyTo(p, 32);
        for (int i = 0; i < centers.Length; i++) BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(64 + i * 4), centers[i]);
        return p;
    }

    private static byte[] BuildVictory(JsonElement element, ReadOnlySpan<byte> body, string path) =>
        element.TryGetProperty("rules", out JsonElement rules) ? VictoryPayload(rules, body)
            : throw new ModError("mod.scenario_field", path + ".rules is required");

    private static byte[] VictoryPayload(JsonElement rules, ReadOnlySpan<byte> body)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", 1);
            w.WriteString("baseSha256", Convert.ToHexStringLower(Ew4.Interface.Common.Sha256Hash.HashData(body)));
            w.WritePropertyName("rules");
            rules.WriteTo(w);
            w.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] BuildCountries(JsonElement element, ReadOnlySpan<byte> body, string path)
    {
        if (!element.TryGetProperty("factions", out JsonElement factions))
            throw new ModError("mod.scenario_field", path + ".factions is required");
        JsonElement? events = element.TryGetProperty("eventCountries", out JsonElement e) ? e : null;
        return CountryPayload(factions, events, body);
    }

    private static byte[] CountryPayload(JsonElement factions, JsonElement? eventCountries, ReadOnlySpan<byte> body)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", 1);
            w.WriteString("baseSha256", Convert.ToHexStringLower(Ew4.Interface.Common.Sha256Hash.HashData(body)));
            w.WritePropertyName("factions");
            factions.WriteTo(w);
            w.WritePropertyName("eventCountries");
            if (eventCountries is { } events) events.WriteTo(w); else w.WriteNullValue();
            w.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static bool SameBytes(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);

    private static bool TryParse(ReadOnlySpan<byte> trailer, out List<Block> blocks)
    {
        blocks = [];
        if (trailer.Length < 16 || trailer.Length > MaximumBytes || !trailer[..8].SequenceEqual(Magic)) return false;
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(trailer[8..]), count = BinaryPrimitives.ReadUInt32LittleEndian(trailer[12..]);
        if (version != 1 || count is < 1 or > 64) return false;
        int cursor = 16;
        for (int i = 0; i < count; i++)
        {
            if (trailer.Length - cursor < 16) return false;
            ReadOnlySpan<byte> header = trailer.Slice(cursor, 16);
            if (header[..4].ToArray().Any(b => b is < (byte)'A' or > (byte)'Z')) return false;
            string tag = Encoding.ASCII.GetString(header[..4]);
            uint blockVersion = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
            cursor += 16;
            if (length > trailer.Length - cursor || blocks.Any(b => b.Tag == tag)) return false;
            blocks.Add(new(tag, blockVersion, flags, trailer.Slice(cursor, (int)length).ToArray()));
            cursor += (int)length;
        }

        return cursor == trailer.Length;
    }

    private static byte[] Container(List<Block> blocks)
    {
        if (blocks.Count == 0) return [];
        using var stream = new MemoryStream();
        stream.Write(Magic);
        using var writer = new BinaryWriter(stream);
        writer.Write(1u);
        writer.Write((uint)blocks.Count);
        foreach (Block b in blocks)
        {
            writer.Write(Encoding.ASCII.GetBytes(b.Tag));
            writer.Write(b.Version);
            writer.Write(b.Flags);
            writer.Write((uint)b.Payload.Length);
            writer.Write(b.Payload);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static int ReadInt(byte[] p, int offset) => BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(offset));

    private static int Int(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)
            ? n : throw new ModError("mod.scenario_field", path + "." + name + " must be an integer");

    private static uint UInt(JsonElement parent, string name, uint fallback, string path) =>
        !parent.TryGetProperty(name, out JsonElement v) ? fallback
            : v.ValueKind == JsonValueKind.Number && v.TryGetUInt32(out uint n) ? n
            : throw new ModError("mod.scenario_field", path + "." + name + " must be an unsigned integer");
}
