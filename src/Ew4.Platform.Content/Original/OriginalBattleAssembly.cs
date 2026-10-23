using System.Buffers.Binary;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.ScenarioEdit;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

internal static class OriginalBattleAssembly
{
    private sealed record LayoutEntry(string Name, int CountHeaderIndex, int Stride, bool AreaSized);

    private static readonly LayoutEntry[] BodyLayout =
    [
        new("factions", 6, 180, false),
        new("ownership", -1, 1, true),
        new("sites", 7, 16, false),
        new("armies", 8, 32, false),
        new("opaque8", 16, 8, false),
        new("opaque116", 10, 116, false),
        new("opaque108", 14, 108, false),
        new("opaque44", 9, 44, false),
    ];

    internal static IReadOnlyDictionary<string, byte[]> ExtractSections(OriginalBattle battle)
    {
        var sections = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (OriginalSection section in battle.Sections)
        {
            sections[section.Name] = battle.SectionBytes(section.Name);
        }

        return sections;
    }

    internal static byte[] BuildBody(int[] header, IReadOnlyDictionary<string, byte[]> sections)
    {
        if (header.Length != 18 || header[0] != 2)
        {
            throw new RuleViolation("legacy.battle_header", "Expected BTL v2 header with 18 integers");
        }

        int area = OriginalBytes.Area(header[4], header[5]);
        int bodySize = 72;
        foreach (LayoutEntry entry in BodyLayout)
        {
            int count = entry.AreaSized ? area : header[entry.CountHeaderIndex];
            bodySize += count * entry.Stride;
        }

        var body = new byte[bodySize];
        int cursor = 0;
        for (int i = 0; i < header.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(cursor, 4), header[i]);
            cursor += 4;
        }

        foreach (LayoutEntry entry in BodyLayout)
        {
            if (!sections.TryGetValue(entry.Name, out byte[]? data))
            {
                throw new RuleViolation("legacy.battle_section", "Missing section: " + entry.Name);
            }

            int count = entry.AreaSized ? area : header[entry.CountHeaderIndex];
            int expected = count * entry.Stride;
            if (data.Length != expected)
            {
                throw new RuleViolation(
                    "legacy.battle_section",
                    $"Section {entry.Name} length {data.Length} != {expected}");
            }

            data.CopyTo(body.AsSpan(cursor));
            cursor += data.Length;
        }

        if (cursor != body.Length)
        {
            throw new RuleViolation("legacy.battle_section", "BTL body size mismatch");
        }

        return body;
    }

    internal static int[] SyncHeaderCounts(int[] header, ScenarioLayers layers)
    {
        var next = header.ToArray();
        next[6] = layers.FactionCount;
        next[7] = layers.SiteCount;
        next[8] = layers.ArmyCount;
        next[9] = layers.EventCount;
        return next;
    }

    internal static IReadOnlyDictionary<string, byte[]> SectionsFromLayers(
        IReadOnlyDictionary<string, byte[]> preserved,
        ScenarioLayers layers)
    {
        var sections = new Dictionary<string, byte[]>(preserved, StringComparer.Ordinal)
        {
            ["factions"] = layers.FactionRows.Count > 0
                ? OriginalRecordBytes.WriteFactionRows(layers.FactionRows)
                : OriginalRecordBytes.WriteAll(layers.Factions),
            ["sites"] = OriginalRecordBytes.WriteAll(layers.Sites),
            ["armies"] = OriginalRecordBytes.WriteAll(layers.Armies),
            ["opaque44"] = layers.EventRows.Count > 0
                ? OriginalRecordBytes.WriteEventRows(layers.EventRows)
                : OriginalRecordBytes.WriteAll(layers.Events),
        };
        return sections;
    }
}
