using Ew4.Platform.Content.Build;

namespace Ew4.Platform.Content.ScenarioEdit;

/// <summary>
/// 单份 BTL 的编解码往返检查（供 Authoring 工具库与 Pack 批量验收共用）。
/// </summary>
public static class ScenarioBtlRoundtrip
{
    public sealed record Result(
        bool OriginalClone,
        bool Document,
        bool DocumentJson,
        bool Layers,
        string? LayerMismatchSection);

    public static Result Validate(string scenarioId, byte[] raw)
    {
        bool legacyClone = false;
        try
        {
            OriginalBattle battle = OriginalBattle.Read(raw);
            legacyClone = battle.ToBytes().AsSpan().SequenceEqual(raw);
        }
        catch
        {
            return new(false, false, false, false, null);
        }

        ScenarioDocument imported = ScenarioBtlCodec.Import(scenarioId, raw);
        bool documentOk = ScenarioBtlCodec.Export(imported, reencodeLayers: false).AsSpan().SequenceEqual(raw);
        ScenarioDocument restored = ScenarioDocumentJson.Deserialize(ScenarioDocumentJson.Serialize(imported));
        bool documentJson = ScenarioBtlCodec.Export(restored, reencodeLayers: false).AsSpan().SequenceEqual(raw);
        ScenarioDocument applied = ScenarioBtlCodec.ApplyLayers(imported);
        bool layers = ScenarioBtlCodec.Export(applied, reencodeLayers: false).AsSpan().SequenceEqual(raw);
        string? mismatch = layers ? null : LayerRoundtripDiagnostics.DescribeMismatch(imported, raw);
        return new(legacyClone, documentOk, documentJson, layers, mismatch);
    }
}
