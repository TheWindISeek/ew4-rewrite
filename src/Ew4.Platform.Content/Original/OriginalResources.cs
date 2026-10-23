using System.Buffers.Binary;
using System.Text;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

internal static class OriginalBytes
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

public sealed record OriginalTerrainCell(byte BackgroundKind, byte BackgroundIndex, sbyte BackgroundX, sbyte BackgroundY,
    byte ForegroundKind, byte ForegroundIndex, sbyte ForegroundX, sbyte ForegroundY,
    byte RiverEdges, byte BlockedSeaEdges, byte Unknown10, byte Unknown11)
{
    public static OriginalTerrainCell Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 12) throw new RuleViolation("legacy.terrain_record", "Expected a 12-byte terrain record");
        return new(raw[0], raw[1], unchecked((sbyte)raw[2]), unchecked((sbyte)raw[3]), raw[4], raw[5],
            unchecked((sbyte)raw[6]), unchecked((sbyte)raw[7]), raw[8], raw[9], raw[10], raw[11]);
    }
}

public sealed class OriginalTerrainMap
{
    private readonly byte[] source;
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<OriginalTerrainCell> Cells { get; }
    private OriginalTerrainMap(byte[] raw, int width, int height, OriginalTerrainCell[] cells)
    { source = raw; Width = width; Height = height; Cells = Array.AsReadOnly(cells); }
    public byte[] ToBytes() => (byte[])source.Clone();
    public static OriginalTerrainMap Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 16 || !raw[..4].SequenceEqual("YSAE"u8) || OriginalBytes.I32(raw, 4) != 1)
            throw new RuleViolation("legacy.terrain_header", "Unsupported or truncated terrain header");
        int width = OriginalBytes.I32(raw, 8), height = OriginalBytes.I32(raw, 12), count = OriginalBytes.Area(width, height);
        if (16L + (long)count * 12 != raw.Length) throw new RuleViolation("legacy.terrain_length", "Terrain size differs from dimensions");
        var cells = new OriginalTerrainCell[count];
        for (int i = 0; i < count; i++) cells[i] = OriginalTerrainCell.Read(raw.Slice(16 + i * 12, 12));
        return new(raw.ToArray(), width, height, cells);
    }
}

public sealed record OriginalFaction(Resources Resources, bool Ai, int Camp, int SurvivalMode, string CountryId,
    string Variant, float IncomeMultiplier, float AiEvasionBonus, int Color, IReadOnlyList<int> Values68,
    int Count172, int State176)
{
    public static OriginalFaction Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 180) throw new RuleViolation("legacy.faction_record", "Expected a 180-byte scenario faction record");
        var values = new int[26];
        for (int i = 0; i < values.Length; i++) values[i] = OriginalBytes.I32(raw, 68 + i * 4);
        return new(new(OriginalBytes.I32(raw, 0), OriginalBytes.I32(raw, 4), OriginalBytes.I32(raw, 8)),
            OriginalBytes.I32(raw, 12) != 0, OriginalBytes.I32(raw, 16), OriginalBytes.I32(raw, 20),
            OriginalBytes.Text(raw.Slice(24, 16)), OriginalBytes.Text(raw.Slice(40, 16)), OriginalBytes.F32(raw, 56),
            OriginalBytes.F32(raw, 60), OriginalBytes.I32(raw, 64), Array.AsReadOnly(values),
            OriginalBytes.I32(raw, 172), OriginalBytes.I32(raw, 176));
    }
}

public sealed record OriginalSite(short Cell, short CityId, byte BuildingKind, byte BuildingLevel,
    byte State6, byte State7, sbyte State8, sbyte InstallationKind, byte Marker, sbyte State11,
    byte BridgeStyle0, byte BridgeStyle1, byte BridgeStyle2, byte State15)
{
    public byte BridgeEdges => (byte)(BridgeStyle0 | BridgeStyle1 | BridgeStyle2);
    public static OriginalSite Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 16) throw new RuleViolation("legacy.site_record", "Expected a 16-byte site record");
        return new(OriginalBytes.I16(raw, 0), OriginalBytes.I16(raw, 2), raw[4], raw[5], raw[6], raw[7],
            unchecked((sbyte)raw[8]), unchecked((sbyte)raw[9]), raw[10], unchecked((sbyte)raw[11]), raw[12], raw[13], raw[14], raw[15]);
    }
}

public sealed record OriginalArmy(short Cell, sbyte Unit, sbyte Grade, short Health, short MaximumHealth,
    uint Experience, sbyte Veterancy, sbyte Movement, sbyte Morale, sbyte MoraleDuration, short Commander,
    byte Flag58, sbyte Objective, byte Behavior, byte Facing, byte Transport, byte Acted, byte ForceRecovery,
    byte Flag5c, sbyte State70, sbyte ConstructionTurns, short BehaviorExpiry, byte RestoreBehavior, sbyte Burning)
{
    public static OriginalArmy Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 32) throw new RuleViolation("legacy.army_record", "Expected a 32-byte army record");
        static sbyte S(ReadOnlySpan<byte> data, int index) => unchecked((sbyte)data[index]);
        return new(OriginalBytes.I16(raw, 0), S(raw, 2), S(raw, 3), OriginalBytes.I16(raw, 4), OriginalBytes.I16(raw, 6),
            BinaryPrimitives.ReadUInt32LittleEndian(raw[8..]), S(raw, 12), S(raw, 13), S(raw, 14), S(raw, 15), OriginalBytes.I16(raw, 16),
            raw[18], S(raw, 19), raw[20], raw[21], raw[22], raw[23], raw[24], raw[25], S(raw, 26), S(raw, 27),
            OriginalBytes.I16(raw, 28), raw[30], S(raw, 31));
    }
}

public sealed record OriginalSection(string Name, int Offset, int Count, int Stride);

public sealed record OriginalEvent(int Id, int Condition, int Effect, int Round, int Dialogue, int Cell,
    string Country, bool Triggered, byte Padding41, byte Padding42, byte Padding43)
{
    public static OriginalEvent Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 44 || raw[40] > 1) throw new RuleViolation("legacy.event_record", "Invalid 44-byte event record");
        return new(OriginalBytes.I32(raw, 0), OriginalBytes.I32(raw, 4), OriginalBytes.I32(raw, 8), OriginalBytes.I32(raw, 12),
            OriginalBytes.I32(raw, 16), OriginalBytes.I32(raw, 20), OriginalBytes.Text(raw.Slice(24, 16)), raw[40] != 0, raw[41], raw[42], raw[43]);
    }
}

public sealed class OriginalBattle
{
    private readonly byte[] source;
    private readonly OriginalBattleExtension[] extensions;
    public int OriginalLength { get; }
    public OriginalJurisdictionData? Jurisdictions { get; }
    public IReadOnlyList<int> Header { get; }
    public IReadOnlyList<OriginalSection> Sections { get; }
    public IReadOnlyList<OriginalFaction> Factions { get; }
    public IReadOnlyList<OriginalSite> Sites { get; }
    public IReadOnlyList<OriginalSite> EffectiveSites { get; }
    public IReadOnlyList<OriginalArmy> Armies { get; }
    public IReadOnlyList<OriginalEvent> Events { get; }
    public IReadOnlyList<OriginalMarket> Markets { get; }
    public IReadOnlyList<OriginalTavern> Taverns { get; }
    public int MapId => Header[1];
    public Ew4.Platform.Engine.ExtensionVictoryRules? Victory { get; }
    public OriginalCountryIds? CountryIds { get; }
    public int Left => Header[2];
    public int Top => Header[3];
    public int Width => Header[4];
    public int Height => Header[5];
    private OriginalBattle(byte[] raw, int[] header, OriginalSection[] sections, OriginalFaction[] factions, OriginalSite[] sites, OriginalArmy[] armies,
        OriginalEvent[] events, OriginalMarket[] markets, OriginalTavern[] taverns, int legacyLength, OriginalBattleExtension[] extensions)
    {
        source = raw; Header = Array.AsReadOnly(header); Sections = Array.AsReadOnly(sections);
        CountryIds = OriginalBattleExtensions.CountryIds(extensions, raw.AsSpan(0, legacyLength), factions, events);
        CountryIds?.Apply(factions, events);
        Factions = Array.AsReadOnly(factions); Sites = Array.AsReadOnly(sites); Armies = Array.AsReadOnly(armies); Events = Array.AsReadOnly(events);
        EffectiveSites = Array.AsReadOnly(sites.GroupBy(s => s.Cell).Select(g => g.Last()).ToArray());
        Markets = Array.AsReadOnly(markets); Taverns = Array.AsReadOnly(taverns);
        OriginalLength = legacyLength; this.extensions = extensions;
        Jurisdictions = OriginalBattleExtensions.Jurisdictions(extensions, raw.AsSpan(0, legacyLength), Header);
        Victory = OriginalBattleExtensions.Victory(extensions, raw.AsSpan(0, legacyLength));
    }
    public byte[] ToBytes() => (byte[])source.Clone();
    public byte[] ToLegacyBytes() => source.AsSpan(0, OriginalLength).ToArray();
    public byte[] WithJurisdictions(OriginalJurisdictionData? data) => OriginalBattleExtensions.Write(source.AsSpan(0, OriginalLength), Header, extensions, data);
    public byte[] WithVictory(Ew4.Platform.Engine.ExtensionVictoryRules? rules) => OriginalBattleExtensions.WriteVictory(source.AsSpan(0, OriginalLength), extensions, rules);
    public byte[] WithCountryIds(OriginalCountryIds? data) => OriginalBattleExtensions.WriteCountryIds(source.AsSpan(0, OriginalLength), extensions, data, Factions.Count, Events.Count);
    public byte[] SectionBytes(string name)
    {
        var section = Sections.Single(s => s.Name == name);
        return source.AsSpan(section.Offset, section.Count * section.Stride).ToArray();
    }
    public static OriginalBattle Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 72 || OriginalBytes.I32(raw, 0) != 2)
            throw new RuleViolation("legacy.battle_header", "Only verified scenario BTL version 2 is supported");
        var header = new int[18];
        for (int i = 0; i < header.Length; i++) header[i] = OriginalBytes.I32(raw, i * 4);
        int area = OriginalBytes.Area(header[4], header[5]);
        (string Name, int Count, int Stride)[] layout = [("factions", header[6], 180), ("ownership", area, 1),
            ("sites", header[7], 16), ("armies", header[8], 32), ("opaque8", header[16], 8),
            ("opaque116", header[10], 116), ("opaque108", header[14], 108), ("opaque44", header[9], 44)];
        long cursor = 72;
        var sections = new List<OriginalSection>();
        foreach (var entry in layout)
        {
            long length = (long)entry.Count * entry.Stride;
            if (entry.Count < 0 || cursor + length > raw.Length)
                throw new RuleViolation("legacy.battle_section", "Invalid or truncated section: " + entry.Name);
            sections.Add(new(entry.Name, (int)cursor, entry.Count, entry.Stride));
            cursor += length;
        }
        var extensions = OriginalBattleExtensions.Read(raw[(int)cursor..]);
        var factions = new OriginalFaction[header[6]];
        var sites = new OriginalSite[header[7]];
        var armies = new OriginalArmy[header[8]];
        var events = new OriginalEvent[header[9]];
        var markets = new OriginalMarket[header[10]];
        var taverns = new OriginalTavern[header[14]];
        for (int i = 0; i < factions.Length; i++) factions[i] = OriginalFaction.Read(raw.Slice(sections[0].Offset + i * 180, 180));
        for (int i = 0; i < sites.Length; i++) sites[i] = OriginalSite.Read(raw.Slice(sections[2].Offset + i * 16, 16));
        for (int i = 0; i < armies.Length; i++) armies[i] = OriginalArmy.Read(raw.Slice(sections[3].Offset + i * 32, 32));
        for (int i = 0; i < events.Length; i++) events[i] = OriginalEvent.Read(raw.Slice(sections[7].Offset + i * 44, 44));
        for (int i = 0; i < markets.Length; i++) markets[i] = OriginalMarket.Read(raw.Slice(sections[5].Offset + i * 116, 116));
        for (int i = 0; i < taverns.Length; i++) taverns[i] = OriginalTavern.Read(raw.Slice(sections[6].Offset + i * 108, 108));
        return new(raw.ToArray(), header, sections.ToArray(), factions, sites, armies, events, markets, taverns, (int)cursor, extensions);
    }
}
