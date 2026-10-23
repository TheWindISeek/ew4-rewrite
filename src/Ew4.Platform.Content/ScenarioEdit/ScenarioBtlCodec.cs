using Ew4.Platform.Content.Build;

namespace Ew4.Platform.Content.ScenarioEdit;

/// <summary>
/// BTL ↔ <see cref="ScenarioDocument"/> 唯一编解码入口（Importer、Exporter、Authoring 共用）。
/// </summary>
public static class ScenarioBtlCodec
{
    public static ScenarioDocument Import(string scenarioId, byte[] btlBytes)
    {
        OriginalBattle battle = OriginalBattle.Read(btlBytes);
        int[] header = battle.Header.ToArray();
        IReadOnlyDictionary<string, byte[]> sections = OriginalBattleAssembly.ExtractSections(battle);
        return new ScenarioDocument
        {
            Id = scenarioId,
            MapId = battle.MapId,
            Width = battle.Width,
            Height = battle.Height,
            Header = header,
            Sections = sections,
            ExtensionTrailer = btlBytes.AsSpan(battle.OriginalLength).ToArray(),
            Layers = BuildLayers(battle, sections),
        };
    }

    private static ScenarioLayers BuildLayers(OriginalBattle battle, IReadOnlyDictionary<string, byte[]> sections)
    {
        byte[] factionBytes = sections["factions"];
        var factionRows = new ScenarioFactionRow[battle.Factions.Count];
        for (int i = 0; i < factionRows.Length; i++)
        {
            byte[] raw = factionBytes.AsSpan(i * 180, 180).ToArray();
            factionRows[i] = new(battle.Factions[i], raw);
        }

        byte[] eventBytes = sections["opaque44"];
        var eventRows = new ScenarioEventRow[battle.Events.Count];
        for (int i = 0; i < eventRows.Length; i++)
        {
            byte[] raw = eventBytes.AsSpan(i * 44, 44).ToArray();
            eventRows[i] = new(battle.Events[i], raw);
        }

        return new ScenarioLayers
        {
            FactionRows = factionRows,
            EventRows = eventRows,
            Factions = battle.Factions,
            Sites = battle.Sites,
            Armies = battle.Armies,
            Events = battle.Events,
        };
    }

    public static byte[] Export(ScenarioDocument document, bool reencodeLayers = false)
    {
        int[] header = document.Header.ToArray();
        IReadOnlyDictionary<string, byte[]> sections = document.Sections;
        if (reencodeLayers)
        {
            header = OriginalBattleAssembly.SyncHeaderCounts(header, document.Layers);
            sections = OriginalBattleAssembly.SectionsFromLayers(document.Sections, document.Layers);
        }

        byte[] body = OriginalBattleAssembly.BuildBody(header, sections);
        if (document.ExtensionTrailer.Length == 0)
        {
            return body;
        }

        var combined = new byte[body.Length + document.ExtensionTrailer.Length];
        body.CopyTo(combined, 0);
        document.ExtensionTrailer.CopyTo(combined, body.Length);
        return combined;
    }

    /// <summary>将 <see cref="ScenarioDocument.Layers"/> 写回 <see cref="ScenarioDocument.Sections"/> 与 header 计数（编辑器保存前调用）。</summary>
    public static ScenarioDocument ApplyLayers(ScenarioDocument document) => document with
    {
        Header = OriginalBattleAssembly.SyncHeaderCounts(document.Header, document.Layers),
        Sections = OriginalBattleAssembly.SectionsFromLayers(document.Sections, document.Layers),
    };
}
