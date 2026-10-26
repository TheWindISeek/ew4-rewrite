using System.Buffers.Binary;
using System.Text;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.StaticData;

internal static class StaticDataBytes
{
    internal static int I32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    internal static short I16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
    internal static float F32(ReadOnlySpan<byte> data, int offset) => BitConverter.Int32BitsToSingle(I32(data, offset));
    internal static string Text(ReadOnlySpan<byte> data)
    {
        int end = data.IndexOf((byte)0);
        if (end < 0) throw new RuleViolation("legacy.string", "Unterminated fixed-width string");
        try { return new UTF8Encoding(false, true).GetString(data[..end]); }
        catch (DecoderFallbackException) { throw new RuleViolation("legacy.string", "Invalid UTF-8 resource string"); }
    }
    internal static int Area(int width, int height)
    {
        if (width < 1 || height < 1 || width > 32768 || height > 32768 || (long)width * height > 100_000_000)
            throw new RuleViolation("legacy.dimensions", "Invalid dimensions or resource reader budget exceeded");
        return width * height;
    }
}

public sealed record StaticDataTerrainCell(byte BackgroundKind, byte BackgroundIndex, sbyte BackgroundX, sbyte BackgroundY,
    byte ForegroundKind, byte ForegroundIndex, sbyte ForegroundX, sbyte ForegroundY,
    byte RiverEdges, byte BlockedSeaEdges, byte Unknown10, byte Unknown11)
{
    public static StaticDataTerrainCell Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 12) throw new RuleViolation("legacy.terrain_record", "Expected a 12-byte terrain record");
        return new(raw[0], raw[1], unchecked((sbyte)raw[2]), unchecked((sbyte)raw[3]), raw[4], raw[5],
            unchecked((sbyte)raw[6]), unchecked((sbyte)raw[7]), raw[8], raw[9], raw[10], raw[11]);
    }
}

public sealed class StaticDataTerrainMap
{
    private readonly byte[] source;
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<StaticDataTerrainCell> Cells { get; }
    private StaticDataTerrainMap(byte[] raw, int width, int height, StaticDataTerrainCell[] cells)
    { source = raw; Width = width; Height = height; Cells = Array.AsReadOnly(cells); }
    public byte[] ToBytes() => (byte[])source.Clone();
    public static StaticDataTerrainMap Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 16 || !raw[..4].SequenceEqual("YSAE"u8) || StaticDataBytes.I32(raw, 4) != 1)
            throw new RuleViolation("legacy.terrain_header", "Unsupported or truncated terrain header");
        int width = StaticDataBytes.I32(raw, 8), height = StaticDataBytes.I32(raw, 12), count = StaticDataBytes.Area(width, height);
        if (16L + (long)count * 12 != raw.Length) throw new RuleViolation("legacy.terrain_length", "Terrain size differs from dimensions");
        var cells = new StaticDataTerrainCell[count];
        for (int i = 0; i < count; i++) cells[i] = StaticDataTerrainCell.Read(raw.Slice(16 + i * 12, 12));
        return new(raw.ToArray(), width, height, cells);
    }
}

public sealed record StaticDataFaction(Resources Resources, bool Ai, int Camp, int SurvivalMode, string CountryId,
    string Variant, float IncomeMultiplier, float AiEvasionBonus, int Color, IReadOnlyList<int> Values68,
    int Count172, int State176)
{
    public static StaticDataFaction Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 180) throw new RuleViolation("legacy.faction_record", "Expected a 180-byte scenario faction record");
        var values = new int[26];
        for (int i = 0; i < values.Length; i++) values[i] = StaticDataBytes.I32(raw, 68 + i * 4);
        return new(new(StaticDataBytes.I32(raw, 0), StaticDataBytes.I32(raw, 4), StaticDataBytes.I32(raw, 8)),
            StaticDataBytes.I32(raw, 12) != 0, StaticDataBytes.I32(raw, 16), StaticDataBytes.I32(raw, 20),
            StaticDataBytes.Text(raw.Slice(24, 16)), StaticDataBytes.Text(raw.Slice(40, 16)), StaticDataBytes.F32(raw, 56),
            StaticDataBytes.F32(raw, 60), StaticDataBytes.I32(raw, 64), Array.AsReadOnly(values),
            StaticDataBytes.I32(raw, 172), StaticDataBytes.I32(raw, 176));
    }
}

public sealed record StaticDataSite(short Cell, short CityId, byte BuildingKind, byte BuildingLevel,
    byte State6, byte State7, sbyte State8, sbyte InstallationKind, byte Marker, sbyte State11,
    byte BridgeStyle0, byte BridgeStyle1, byte BridgeStyle2, byte State15)
{
    public byte BridgeEdges => (byte)(BridgeStyle0 | BridgeStyle1 | BridgeStyle2);
    public static StaticDataSite Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 16) throw new RuleViolation("legacy.site_record", "Expected a 16-byte site record");
        return new(StaticDataBytes.I16(raw, 0), StaticDataBytes.I16(raw, 2), raw[4], raw[5], raw[6], raw[7],
            unchecked((sbyte)raw[8]), unchecked((sbyte)raw[9]), raw[10], unchecked((sbyte)raw[11]), raw[12], raw[13], raw[14], raw[15]);
    }
}

public sealed record StaticDataArmy(short Cell, sbyte Unit, sbyte Grade, short Health, short MaximumHealth,
    uint Experience, sbyte Veterancy, sbyte Movement, sbyte Morale, sbyte MoraleDuration, short Commander,
    byte Flag58, sbyte Objective, byte Behavior, byte Facing, byte Transport, byte Acted, byte ForceRecovery,
    byte Flag5c, sbyte State70, sbyte ConstructionTurns, short BehaviorExpiry, byte RestoreBehavior, sbyte Burning)
{
    public static StaticDataArmy Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 32) throw new RuleViolation("legacy.army_record", "Expected a 32-byte army record");
        static sbyte S(ReadOnlySpan<byte> data, int index) => unchecked((sbyte)data[index]);
        return new(StaticDataBytes.I16(raw, 0), S(raw, 2), S(raw, 3), StaticDataBytes.I16(raw, 4), StaticDataBytes.I16(raw, 6),
            BinaryPrimitives.ReadUInt32LittleEndian(raw[8..]), S(raw, 12), S(raw, 13), S(raw, 14), S(raw, 15), StaticDataBytes.I16(raw, 16),
            raw[18], S(raw, 19), raw[20], raw[21], raw[22], raw[23], raw[24], raw[25], S(raw, 26), S(raw, 27),
            StaticDataBytes.I16(raw, 28), raw[30], S(raw, 31));
    }
}

public sealed record StaticDataSection(string Name, int Offset, int Count, int Stride);

public sealed record StaticDataEvent(int Id, int Condition, int Effect, int Round, int Dialogue, int Cell,
    string Country, bool Triggered, byte Padding41, byte Padding42, byte Padding43)
{
    public static StaticDataEvent Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 44 || raw[40] > 1) throw new RuleViolation("legacy.event_record", "Invalid 44-byte event record");
        return new(StaticDataBytes.I32(raw, 0), StaticDataBytes.I32(raw, 4), StaticDataBytes.I32(raw, 8), StaticDataBytes.I32(raw, 12),
            StaticDataBytes.I32(raw, 16), StaticDataBytes.I32(raw, 20), StaticDataBytes.Text(raw.Slice(24, 16)), raw[40] != 0, raw[41], raw[42], raw[43]);
    }
}

public sealed record StaticDataEnvironment(int Cell, int Intensity);

public sealed partial class StaticDataBattle
{
    private readonly byte[]? source;
    private readonly StaticDataBattleExtension[] extensions;
    public int StaticDataLength { get; }
    public StaticDataJurisdictionData? Jurisdictions { get; }
    public IReadOnlyList<int> Header { get; }
    public IReadOnlyList<StaticDataSection> Sections { get; }
    public IReadOnlyList<StaticDataFaction> Factions { get; }
    public IReadOnlyList<StaticDataSite> Sites { get; }
    public IReadOnlyList<StaticDataSite> EffectiveSites { get; }
    public IReadOnlyList<StaticDataArmy> Armies { get; }
    public IReadOnlyList<StaticDataEvent> Events { get; }
    public IReadOnlyList<StaticDataMarket> Markets { get; }
    public IReadOnlyList<StaticDataTavern> Taverns { get; }
    public int MapId => Header[1];
    public Ew4.Platform.Engine.ExtensionVictoryRules? Victory { get; }
    public StaticDataCountryIds? CountryIds { get; }
    public int Left => Header[2];
    public int Top => Header[3];
    public int Width => Header[4];
    public int Height => Header[5];
    /// <summary>每格一个势力序号（0xFF = 无主），按剧本区域行优先。</summary>
    public IReadOnlyList<byte> Ownership { get; }
    public IReadOnlyList<StaticDataEnvironment> Environments { get; }
    /// <summary>剧本头里的非 0 表示关闭"目标点"胜利规则。</summary>
    public int ObjectivesDisabled { get; }
    public int MinimumRound { get; }
    public int RatingRound { get; }
    /// <summary>剧本内容的稳定指纹串，进入各级内容修订号。</summary>
    public string Fingerprint { get; }
    private static IReadOnlyList<StaticDataEnvironment> ReadEnvironments(ReadOnlySpan<byte> data)
    {
        var list = new List<StaticDataEnvironment>();
        for (int offset = 0; offset + 8 <= data.Length; offset += 8)
            list.Add(new(StaticDataBytes.I32(data, offset), StaticDataBytes.I32(data, offset + 4)));
        return list;
    }
    private byte[] Bytes => source ?? throw new InvalidOperationException("This scenario was read from JSON and has no BTL bytes");
    public byte[] SectionBytes(string name)
    {
        var section = Sections.Single(s => s.Name == name);
        return Bytes.AsSpan(section.Offset, section.Count * section.Stride).ToArray();
    }
}
