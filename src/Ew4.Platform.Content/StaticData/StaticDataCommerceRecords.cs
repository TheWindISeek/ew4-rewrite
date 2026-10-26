using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.StaticData;

public sealed record StaticDataMarketSlot(int ItemId, int Quantity);
public sealed record StaticDataTavernCandidate(int CommanderId, int Money, int Industry, int Medals, int AvailableRound);

public sealed class StaticDataMarket
{
    private readonly byte[]? source;
    public int Cell { get; }
    public IReadOnlyList<StaticDataMarketSlot> Slots { get; }
    private StaticDataMarket(byte[]? raw, int cell, StaticDataMarketSlot[] slots)
    { source = raw; Cell = cell; Slots = Array.AsReadOnly(slots); }
    public static StaticDataMarket FromValues(int cell, IEnumerable<StaticDataMarketSlot> slots) => new(null, cell, slots.ToArray());
    public byte[] ToBytes() => source == null ? throw new InvalidOperationException("Market was read from JSON") : (byte[])source.Clone();
    public static StaticDataMarket Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 116) throw new RuleViolation("legacy.market_record", "Expected a 116-byte market record");
        var slots = new StaticDataMarketSlot[14];
        for (int i = 0; i < slots.Length; i++) slots[i] = new(StaticDataBytes.I32(raw, i * 8), StaticDataBytes.I32(raw, i * 8 + 4));
        return new(raw.ToArray(), StaticDataBytes.I32(raw, 112), slots);
    }
}

public sealed class StaticDataTavern
{
    private readonly byte[]? source;
    public int Cell { get; }
    public IReadOnlyList<StaticDataTavernCandidate> Candidates { get; }
    private StaticDataTavern(byte[]? raw, int cell, StaticDataTavernCandidate[] candidates)
    { source = raw; Cell = cell; Candidates = Array.AsReadOnly(candidates); }
    public static StaticDataTavern FromValues(int cell, IEnumerable<StaticDataTavernCandidate> candidates) => new(null, cell, candidates.ToArray());
    public byte[] ToBytes() => source == null ? throw new InvalidOperationException("Tavern was read from JSON") : (byte[])source.Clone();
    public static StaticDataTavern Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 108) throw new RuleViolation("legacy.tavern_record", "Expected a 108-byte tavern record");
        int count = StaticDataBytes.I32(raw, 100);
        if (count < 0 || count > 5) throw new RuleViolation("legacy.tavern_count", "Invalid original tavern candidate count");
        var candidates = new StaticDataTavernCandidate[count];
        for (int i = 0; i < count; i++)
        {
            int offset = i * 20;
            candidates[i] = new(StaticDataBytes.I32(raw, offset), StaticDataBytes.I32(raw, offset + 4), StaticDataBytes.I32(raw, offset + 8),
                StaticDataBytes.I32(raw, offset + 12), StaticDataBytes.I32(raw, offset + 16));
        }
        return new(raw.ToArray(), StaticDataBytes.I32(raw, 104), candidates);
    }
}
