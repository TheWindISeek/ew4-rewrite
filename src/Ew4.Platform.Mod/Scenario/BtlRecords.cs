using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Ew4.Platform.Mod.Scenario;

internal enum FieldKind { I32, Bool32, F32, I16, U32, U8, S8, Text16, I32Array, IntObjects }

/// <summary>
/// <paramref name="Length"/>：数组最大长度。<c>IntObjects</c> 为一组每项由 <paramref name="Sub"/> 命名的整数对象，
/// 若 <paramref name="CountOffset"/> ≥ 0，则有效项数存放在该偏移处（其后的槽位不暴露）。
/// </summary>
internal sealed record Field(string Name, int Offset, FieldKind Kind, int Length = 1, string[]? Sub = null, int CountOffset = -1);

/// <summary>
/// btl 定长记录 ↔ JSON 对象。字段表即二进制布局的唯一描述。
/// 记录里有 JSON 表达不了的字节（如字符串结束符后的填充）时，额外写出 <c>raw</c>；
/// 读回时以 <c>raw</c> 为模板，只改写与模板不同的字段，所以编辑一个字段不会扰动其它字节。
/// </summary>
internal sealed class RecordSchema
{
    private readonly Field[] _fields;

    public RecordSchema(string name, int stride, params Field[] fields)
    {
        Name = name;
        Stride = stride;
        _fields = fields;
    }

    public string Name { get; }

    public int Stride { get; }

    public void WriteRow(Utf8JsonWriter json, ReadOnlySpan<byte> row)
    {
        object[] values = Decode(row);
        json.WriteStartObject();
        for (int i = 0; i < _fields.Length; i++) WriteValue(json, _fields[i], values[i]);
        if (!Encode(values, null).AsSpan().SequenceEqual(row)) json.WriteString("raw", Convert.ToBase64String(row));
        json.WriteEndObject();
    }

    public byte[] ReadRow(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Invalid(path, "must be an object");
        var values = new object[_fields.Length];
        for (int i = 0; i < _fields.Length; i++)
            values[i] = element.TryGetProperty(_fields[i].Name, out JsonElement value) ? ReadValue(_fields[i], value, path) : Default(_fields[i]);
        if (element.TryGetProperty("raw", out JsonElement rawElement) && rawElement.ValueKind == JsonValueKind.String)
        {
            byte[] raw;
            try { raw = Convert.FromBase64String(rawElement.GetString()!); }
            catch (FormatException) { throw Invalid(path + ".raw", "is not valid base64"); }
            if (raw.Length == Stride) return Encode(values, raw);
        }

        return Encode(values, null);
    }

    private object[] Decode(ReadOnlySpan<byte> row)
    {
        var values = new object[_fields.Length];
        for (int i = 0; i < _fields.Length; i++) values[i] = DecodeField(_fields[i], row);
        return values;
    }

    private static object DecodeField(Field f, ReadOnlySpan<byte> row)
    {
        ReadOnlySpan<byte> at = row[f.Offset..];
        return f.Kind switch
        {
            FieldKind.I32 => BinaryPrimitives.ReadInt32LittleEndian(at),
            FieldKind.Bool32 => BinaryPrimitives.ReadInt32LittleEndian(at) != 0,
            FieldKind.F32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(at)),
            FieldKind.I16 => (int)BinaryPrimitives.ReadInt16LittleEndian(at),
            FieldKind.U32 => (long)BinaryPrimitives.ReadUInt32LittleEndian(at),
            FieldKind.U8 => (int)at[0],
            FieldKind.S8 => (int)unchecked((sbyte)at[0]),
            FieldKind.Text16 => DecodeText(at[..16]),
            FieldKind.I32Array => ReadInts(at, f.Length),
            FieldKind.IntObjects => ReadObjects(f, row),
            _ => throw new InvalidOperationException(),
        };
    }

    private static int[][] ReadObjects(Field f, ReadOnlySpan<byte> row)
    {
        int count = f.CountOffset >= 0 ? BinaryPrimitives.ReadInt32LittleEndian(row[f.CountOffset..]) : f.Length;
        if (count < 0 || count > f.Length) throw new ModError("mod.scenario_btl", $"Invalid item count {count} in {f.Name}");
        var items = new int[count][];
        for (int i = 0; i < count; i++) items[i] = ReadInts(row[(f.Offset + i * f.Sub!.Length * 4)..], f.Sub.Length);
        return items;
    }

    private static bool Same(object a, object b) => a switch
    {
        int[] x when b is int[] y => x.AsSpan().SequenceEqual(y),
        int[][] x when b is int[][] y => x.Length == y.Length && x.Zip(y).All(p => p.First.AsSpan().SequenceEqual(p.Second)),
        _ => a.Equals(b),
    };

    // 以 template 为底（无模板则全零），只写与模板解码值不同的字段。
    private byte[] Encode(object[] values, byte[]? template)
    {
        var row = template != null ? (byte[])template.Clone() : new byte[Stride];
        for (int i = 0; i < _fields.Length; i++)
        {
            Field f = _fields[i];
            if (template != null && Same(DecodeField(f, template), values[i])) continue;
            Span<byte> at = row.AsSpan(f.Offset);
            switch (f.Kind)
            {
                case FieldKind.I32: BinaryPrimitives.WriteInt32LittleEndian(at, (int)values[i]); break;
                case FieldKind.Bool32: BinaryPrimitives.WriteInt32LittleEndian(at, (bool)values[i] ? 1 : 0); break;
                case FieldKind.F32: BinaryPrimitives.WriteInt32LittleEndian(at, BitConverter.SingleToInt32Bits((float)values[i])); break;
                case FieldKind.I16: BinaryPrimitives.WriteInt16LittleEndian(at, checked((short)(int)values[i])); break;
                case FieldKind.U32: BinaryPrimitives.WriteUInt32LittleEndian(at, checked((uint)(long)values[i])); break;
                case FieldKind.U8: at[0] = checked((byte)(int)values[i]); break;
                case FieldKind.S8: at[0] = unchecked((byte)checked((sbyte)(int)values[i])); break;
                case FieldKind.Text16:
                    byte[] text = Encoding.UTF8.GetBytes((string)values[i]);
                    if (text.Length > 16) throw Invalid(Name + "." + f.Name, "is longer than 16 bytes");
                    at[..16].Clear();
                    text.CopyTo(at);
                    break;
                case FieldKind.I32Array:
                    int[] array = (int[])values[i];
                    for (int k = 0; k < f.Length; k++) BinaryPrimitives.WriteInt32LittleEndian(at[(k * 4)..], array[k]);
                    break;
                case FieldKind.IntObjects:
                    int[][] items = (int[][])values[i];
                    if (items.Length > f.Length) throw Invalid(Name + "." + f.Name, $"has more than {f.Length} items");
                    if (f.CountOffset >= 0) BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(f.CountOffset), items.Length);
                    else if (items.Length != f.Length) throw Invalid(Name + "." + f.Name, $"must have exactly {f.Length} items");
                    for (int k = 0; k < items.Length; k++)
                        for (int n = 0; n < f.Sub!.Length; n++)
                            BinaryPrimitives.WriteInt32LittleEndian(at[((k * f.Sub.Length + n) * 4)..], items[k][n]);
                    break;
            }
        }

        return row;
    }

    private static int[] ReadInts(ReadOnlySpan<byte> at, int count)
    {
        var values = new int[count];
        for (int k = 0; k < count; k++) values[k] = BinaryPrimitives.ReadInt32LittleEndian(at[(k * 4)..]);
        return values;
    }

    private static string DecodeText(ReadOnlySpan<byte> data)
    {
        int end = data.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? data : data[..end]);
    }

    private static object Default(Field f) => f.Kind switch
    {
        FieldKind.Bool32 => false,
        FieldKind.F32 => 0f,
        FieldKind.U32 => 0L,
        FieldKind.Text16 => "",
        FieldKind.I32Array => new int[f.Length],
        FieldKind.IntObjects => f.CountOffset >= 0 ? new int[0][] : Enumerable.Range(0, f.Length).Select(_ => new int[f.Sub!.Length]).ToArray(),
        _ => 0,
    };

    private static void WriteValue(Utf8JsonWriter json, Field f, object value)
    {
        switch (value)
        {
            case int i: json.WriteNumber(f.Name, i); break;
            case long l: json.WriteNumber(f.Name, l); break;
            case bool b: json.WriteBoolean(f.Name, b); break;
            case string s: json.WriteString(f.Name, s); break;
            case float x:
                if (!float.IsFinite(x)) throw new ModError("mod.scenario_btl", "Non-finite number in " + f.Name);
                json.WriteNumber(f.Name, x);
                break;
            case int[] array:
                json.WriteStartArray(f.Name);
                foreach (int v in array) json.WriteNumberValue(v);
                json.WriteEndArray();
                break;
            case int[][] items:
                json.WriteStartArray(f.Name);
                foreach (int[] item in items)
                {
                    json.WriteStartObject();
                    for (int n = 0; n < f.Sub!.Length; n++) json.WriteNumber(f.Sub[n], item[n]);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                break;
        }
    }

    private object ReadValue(Field f, JsonElement value, string path)
    {
        string where = path + "." + f.Name;
        try
        {
            return f.Kind switch
            {
                FieldKind.I32 => value.GetInt32(),
                FieldKind.Bool32 => value.GetBoolean(),
                FieldKind.F32 => value.GetSingle(),
                FieldKind.I16 or FieldKind.U8 or FieldKind.S8 => value.GetInt32(),
                FieldKind.U32 => value.GetInt64(),
                FieldKind.Text16 => value.GetString() ?? throw Invalid(where, "must be a string"),
                FieldKind.I32Array => value.EnumerateArray().Select(v => v.GetInt32()).ToArray() is { } a && a.Length == f.Length
                    ? a : throw Invalid(where, $"must have {f.Length} integers"),
                FieldKind.IntObjects => value.EnumerateArray().Select(item => f.Sub!.Select(n => item.GetProperty(n).GetInt32()).ToArray()).ToArray(),
                _ => throw new InvalidOperationException(),
            };
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw Invalid(where, "has the wrong type");
        }
    }

    private static ModError Invalid(string path, string message) => new("mod.scenario_field", path + " " + message);
}

internal static class BtlRecords
{
    public static readonly RecordSchema Faction = new("faction", 180,
        new("money", 0, FieldKind.I32), new("industry", 4, FieldKind.I32), new("food", 8, FieldKind.I32),
        new("ai", 12, FieldKind.Bool32), new("camp", 16, FieldKind.I32), new("survivalMode", 20, FieldKind.I32),
        new("country", 24, FieldKind.Text16), new("variant", 40, FieldKind.Text16),
        new("incomeMultiplier", 56, FieldKind.F32), new("aiEvasionBonus", 60, FieldKind.F32), new("color", 64, FieldKind.I32),
        new("values", 68, FieldKind.I32Array, 26), new("count172", 172, FieldKind.I32), new("state176", 176, FieldKind.I32));

    public static readonly RecordSchema Site = new("site", 16,
        new("cell", 0, FieldKind.I16), new("cityId", 2, FieldKind.I16), new("buildingKind", 4, FieldKind.U8),
        new("buildingLevel", 5, FieldKind.U8), new("state6", 6, FieldKind.U8), new("state7", 7, FieldKind.U8),
        new("state8", 8, FieldKind.S8), new("installationKind", 9, FieldKind.S8), new("marker", 10, FieldKind.U8),
        new("state11", 11, FieldKind.S8), new("bridgeStyle0", 12, FieldKind.U8), new("bridgeStyle1", 13, FieldKind.U8),
        new("bridgeStyle2", 14, FieldKind.U8), new("state15", 15, FieldKind.U8));

    public static readonly RecordSchema Army = new("army", 32,
        new("cell", 0, FieldKind.I16), new("unit", 2, FieldKind.S8), new("grade", 3, FieldKind.S8),
        new("health", 4, FieldKind.I16), new("maximumHealth", 6, FieldKind.I16), new("experience", 8, FieldKind.U32),
        new("veterancy", 12, FieldKind.S8), new("movement", 13, FieldKind.S8), new("morale", 14, FieldKind.S8),
        new("moraleDuration", 15, FieldKind.S8), new("commander", 16, FieldKind.I16), new("flag58", 18, FieldKind.U8),
        new("objective", 19, FieldKind.S8), new("behavior", 20, FieldKind.U8), new("facing", 21, FieldKind.U8),
        new("transport", 22, FieldKind.U8), new("acted", 23, FieldKind.U8), new("forceRecovery", 24, FieldKind.U8),
        new("flag5c", 25, FieldKind.U8), new("state70", 26, FieldKind.S8), new("constructionTurns", 27, FieldKind.S8),
        new("behaviorExpiry", 28, FieldKind.I16), new("restoreBehavior", 30, FieldKind.U8), new("burning", 31, FieldKind.S8));

    public static readonly RecordSchema Event = new("event", 44,
        new("id", 0, FieldKind.I32), new("condition", 4, FieldKind.I32), new("effect", 8, FieldKind.I32),
        new("round", 12, FieldKind.I32), new("dialogue", 16, FieldKind.I32), new("cell", 20, FieldKind.I32),
        new("country", 24, FieldKind.Text16), new("triggered", 40, FieldKind.U8),
        new("padding41", 41, FieldKind.U8), new("padding42", 42, FieldKind.U8), new("padding43", 43, FieldKind.U8));

    public static readonly RecordSchema Market = new("market", 116,
        new("slots", 0, FieldKind.IntObjects, 14, new[] { "itemId", "quantity" }),
        new("cell", 112, FieldKind.I32));

    public static readonly RecordSchema Tavern = new("tavern", 108,
        new("candidates", 0, FieldKind.IntObjects, 5, new[] { "commanderId", "money", "industry", "medals", "availableRound" }, 100),
        new("cell", 104, FieldKind.I32));

    public static readonly RecordSchema Environment = new("environment", 8,
        new("cell", 0, FieldKind.I32), new("intensity", 4, FieldKind.I32));
}
