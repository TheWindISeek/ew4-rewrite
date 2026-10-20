using Ew4.Core;

namespace Ew4.Legacy;

public sealed record LegacyMarketSlot(int ItemId, int Quantity);
public sealed record LegacyTavernCandidate(int CommanderId, int Money, int Industry, int Medals, int AvailableRound);

public sealed class LegacyMarket
{
    private readonly byte[] source;
    public int Cell { get; }
    public IReadOnlyList<LegacyMarketSlot> Slots { get; }
    private LegacyMarket(byte[] raw, int cell, LegacyMarketSlot[] slots)
    { source = raw; Cell = cell; Slots = Array.AsReadOnly(slots); }
    public byte[] ToBytes() => (byte[])source.Clone();
    public static LegacyMarket Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 116) throw new RuleViolation("legacy.market_record", "Expected a 116-byte market record");
        var slots = new LegacyMarketSlot[14];
        for (int i = 0; i < slots.Length; i++) slots[i] = new(LegacyBytes.I32(raw, i * 8), LegacyBytes.I32(raw, i * 8 + 4));
        return new(raw.ToArray(), LegacyBytes.I32(raw, 112), slots);
    }
}

public sealed class LegacyTavern
{
    private readonly byte[] source;
    public int Cell { get; }
    public IReadOnlyList<LegacyTavernCandidate> Candidates { get; }
    private LegacyTavern(byte[] raw, int cell, LegacyTavernCandidate[] candidates)
    { source = raw; Cell = cell; Candidates = Array.AsReadOnly(candidates); }
    public byte[] ToBytes() => (byte[])source.Clone();
    public static LegacyTavern Read(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 108) throw new RuleViolation("legacy.tavern_record", "Expected a 108-byte tavern record");
        int count = LegacyBytes.I32(raw, 100);
        if (count < 0 || count > 5) throw new RuleViolation("legacy.tavern_count", "Invalid original tavern candidate count");
        var candidates = new LegacyTavernCandidate[count];
        for (int i = 0; i < count; i++)
        {
            int offset = i * 20;
            candidates[i] = new(LegacyBytes.I32(raw, offset), LegacyBytes.I32(raw, offset + 4), LegacyBytes.I32(raw, offset + 8),
                LegacyBytes.I32(raw, offset + 12), LegacyBytes.I32(raw, offset + 16));
        }
        return new(raw.ToArray(), LegacyBytes.I32(raw, 104), candidates);
    }
}
