using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

public sealed record OriginalMarketSlot(int ItemId, int Quantity);
public sealed record OriginalTavernCandidate(int CommanderId, int Money, int Industry, int Medals, int AvailableRound);

public sealed class OriginalMarket
{
    private readonly byte[] source;
    public int Cell { get; }
    public IReadOnlyList<OriginalMarketSlot> Slots { get; }
    private OriginalMarket(byte[] raw, int cell, OriginalMarketSlot[] slots)
    { source = raw; Cell = cell; Slots = Array.AsReadOnly(slots); }
    public byte[] ToBytes() => (byte[])source.Clone();
    public static OriginalMarket Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 116) throw new RuleViolation("legacy.market_record", "Expected a 116-byte market record");
        var slots = new OriginalMarketSlot[14];
        for (int i = 0; i < slots.Length; i++) slots[i] = new(OriginalBytes.I32(raw, i * 8), OriginalBytes.I32(raw, i * 8 + 4));
        return new(raw.ToArray(), OriginalBytes.I32(raw, 112), slots);
    }
}

public sealed class OriginalTavern
{
    private readonly byte[] source;
    public int Cell { get; }
    public IReadOnlyList<OriginalTavernCandidate> Candidates { get; }
    private OriginalTavern(byte[] raw, int cell, OriginalTavernCandidate[] candidates)
    { source = raw; Cell = cell; Candidates = Array.AsReadOnly(candidates); }
    public byte[] ToBytes() => (byte[])source.Clone();
    public static OriginalTavern Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 108) throw new RuleViolation("legacy.tavern_record", "Expected a 108-byte tavern record");
        int count = OriginalBytes.I32(raw, 100);
        if (count < 0 || count > 5) throw new RuleViolation("legacy.tavern_count", "Invalid original tavern candidate count");
        var candidates = new OriginalTavernCandidate[count];
        for (int i = 0; i < count; i++)
        {
            int offset = i * 20;
            candidates[i] = new(OriginalBytes.I32(raw, offset), OriginalBytes.I32(raw, offset + 4), OriginalBytes.I32(raw, offset + 8),
                OriginalBytes.I32(raw, offset + 12), OriginalBytes.I32(raw, offset + 16));
        }
        return new(raw.ToArray(), OriginalBytes.I32(raw, 104), candidates);
    }
}
