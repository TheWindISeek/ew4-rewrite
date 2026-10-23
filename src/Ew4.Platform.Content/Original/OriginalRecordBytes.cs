using System.Buffers.Binary;
using System.Text;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.ScenarioEdit;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

internal static class OriginalRecordBytes
{
    internal static byte[] WriteFaction(OriginalFaction faction, ReadOnlySpan<byte> template = default)
    {
        var raw = template.Length == 180 ? template.ToArray() : new byte[180];
        WriteI32(raw, 0, checked((int)faction.Resources.Money));
        WriteI32(raw, 4, checked((int)faction.Resources.Industry));
        WriteI32(raw, 8, checked((int)faction.Resources.Food));
        WriteI32(raw, 12, faction.Ai ? 1 : 0);
        WriteI32(raw, 16, faction.Camp);
        WriteI32(raw, 20, faction.SurvivalMode);
        WriteFixedUtf8(raw.AsSpan(24, 16), faction.CountryId);
        WriteFixedUtf8(raw.AsSpan(40, 16), faction.Variant);
        WriteI32(raw, 56, BitConverter.SingleToInt32Bits(faction.IncomeMultiplier));
        WriteI32(raw, 60, BitConverter.SingleToInt32Bits(faction.AiEvasionBonus));
        WriteI32(raw, 64, faction.Color);
        for (int i = 0; i < faction.Values68.Count && i < 26; i++)
        {
            WriteI32(raw, 68 + i * 4, faction.Values68[i]);
        }

        WriteI32(raw, 172, faction.Count172);
        WriteI32(raw, 176, faction.State176);
        return raw;
    }

    internal static byte[] WriteSite(OriginalSite site)
    {
        var raw = new byte[16];
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0), site.Cell);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), site.CityId);
        raw[4] = site.BuildingKind;
        raw[5] = site.BuildingLevel;
        raw[6] = site.State6;
        raw[7] = site.State7;
        raw[8] = unchecked((byte)site.State8);
        raw[9] = unchecked((byte)site.InstallationKind);
        raw[10] = site.Marker;
        raw[11] = unchecked((byte)site.State11);
        raw[12] = site.BridgeStyle0;
        raw[13] = site.BridgeStyle1;
        raw[14] = site.BridgeStyle2;
        raw[15] = site.State15;
        return raw;
    }

    internal static byte[] WriteArmy(OriginalArmy army)
    {
        var raw = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0), army.Cell);
        raw[2] = unchecked((byte)army.Unit);
        raw[3] = unchecked((byte)army.Grade);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), army.Health);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(6), army.MaximumHealth);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), army.Experience);
        raw[12] = unchecked((byte)army.Veterancy);
        raw[13] = unchecked((byte)army.Movement);
        raw[14] = unchecked((byte)army.Morale);
        raw[15] = unchecked((byte)army.MoraleDuration);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(16), army.Commander);
        raw[18] = army.Flag58;
        raw[19] = unchecked((byte)army.Objective);
        raw[20] = army.Behavior;
        raw[21] = army.Facing;
        raw[22] = army.Transport;
        raw[23] = army.Acted;
        raw[24] = army.ForceRecovery;
        raw[25] = army.Flag5c;
        raw[26] = unchecked((byte)army.State70);
        raw[27] = unchecked((byte)army.ConstructionTurns);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(28), army.BehaviorExpiry);
        raw[30] = army.RestoreBehavior;
        raw[31] = unchecked((byte)army.Burning);
        return raw;
    }

    internal static byte[] WriteEvent(OriginalEvent ev)
    {
        var raw = new byte[44];
        WriteI32(raw, 0, ev.Id);
        WriteI32(raw, 4, ev.Condition);
        WriteI32(raw, 8, ev.Effect);
        WriteI32(raw, 12, ev.Round);
        WriteI32(raw, 16, ev.Dialogue);
        WriteI32(raw, 20, ev.Cell);
        WriteFixedUtf8(raw.AsSpan(24, 16), ev.Country);
        raw[40] = ev.Triggered ? (byte)1 : (byte)0;
        raw[41] = ev.Padding41;
        raw[42] = ev.Padding42;
        raw[43] = ev.Padding43;
        return raw;
    }

    internal static byte[] WriteAll(IReadOnlyList<OriginalFaction> rows) => Concat(rows.Select(f => WriteFaction(f)));

    internal static byte[] WriteFactionRow(ScenarioFactionRow row) =>
        row.Value.Equals(OriginalFaction.Read(row.Raw)) ? row.Raw : WriteFaction(row.Value, row.Raw);

    internal static byte[] WriteFactionRows(IReadOnlyList<ScenarioFactionRow> rows) =>
        Concat(rows.Select(r => r.Raw));
    internal static byte[] WriteAll(IReadOnlyList<OriginalSite> rows) => Concat(rows.Select(WriteSite));
    internal static byte[] WriteAll(IReadOnlyList<OriginalArmy> rows) => Concat(rows.Select(WriteArmy));
    internal static byte[] WriteAll(IReadOnlyList<OriginalEvent> rows) => Concat(rows.Select(WriteEvent));

    internal static byte[] WriteEventRows(IReadOnlyList<ScenarioEventRow> rows) =>
        Concat(rows.Select(r => r.Raw));

    private static byte[] Concat(IEnumerable<byte[]> rows)
    {
        var list = rows.ToArray();
        if (list.Length == 0)
        {
            return [];
        }

        int length = list.Sum(r => r.Length);
        var raw = new byte[length];
        int offset = 0;
        foreach (byte[] row in list)
        {
            row.CopyTo(raw.AsSpan(offset));
            offset += row.Length;
        }

        return raw;
    }

    private static void WriteI32(byte[] raw, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(offset), value);

    private static void WriteFixedUtf8(Span<byte> slot, string text)
    {
        slot.Clear();
        if (text.Length == 0)
        {
            return;
        }

        int max = slot.Length - 1;
        int count = Encoding.UTF8.GetBytes(text, slot[..max]);
        if (count >= max && Encoding.UTF8.GetByteCount(text) > max)
        {
            throw new RuleViolation("legacy.string", "Fixed-width string too long");
        }
    }
}
