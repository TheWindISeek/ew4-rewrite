using Ew4.Platform.Content.Build;

namespace Ew4.Platform.Content.ScenarioEdit;

internal static class LayerRoundtripDiagnostics
{
    internal static string DescribeMismatch(ScenarioDocument document, byte[] raw)
    {
        ScenarioDocument applied = ScenarioBtlCodec.ApplyLayers(document);
        OriginalBattle before = OriginalBattle.Read(raw);
        foreach (OriginalSection section in before.Sections)
        {
            ReadOnlySpan<byte> expected = before.SectionBytes(section.Name);
            if (!applied.Sections.TryGetValue(section.Name, out byte[]? actual) || !actual.AsSpan().SequenceEqual(expected))
            {
                return section.Name;
            }
        }

        byte[] exported = ScenarioBtlCodec.Export(applied, reencodeLayers: false);
        if (!exported.AsSpan().SequenceEqual(raw))
        {
            return exported.Length == raw.Length ? "extensionTrailer" : "bodyLength";
        }

        return "unknown";
    }
}
