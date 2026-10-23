using System;
using System.Text.Json;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Platform.Content;

/// <summary>
/// 剧本 Pack 访问便捷入口；解析策略见 <see cref="ScenarioBtlResolver"/>。
/// </summary>
public static class ScenarioPackAccess
{
    public static bool IsEmbeddedScenarioJson(JsonElement root)
    {
        if (root.TryGetProperty("schema", out JsonElement schemaEl)
            && schemaEl.ValueKind == JsonValueKind.Number
            && schemaEl.GetInt32() >= 2)
        {
            return true;
        }

        return root.TryGetProperty("sections", out _);
    }

    public static ScenarioDocument ReadEmbeddedDocument(byte[] jsonBytes, string expectedScenarioId)
    {
        ScenarioDocument document = ScenarioDocumentJson.Deserialize(jsonBytes);
        if (!string.Equals(document.Id, expectedScenarioId, StringComparison.Ordinal))
        {
            throw new ContentError(
                "scenario.id",
                "Scenario id mismatch: expected '" + expectedScenarioId + "', got '" + document.Id + "'.");
        }

        return document;
    }

    public static ScenarioDocument OpenScenario(IPackSource source, string scenarioId) =>
        ScenarioBtlResolver.OpenScenarioDocument(source, scenarioId);

    public static byte[] ExportBtlBytes(ScenarioDocument document) =>
        ScenarioBtlCodec.Export(document, reencodeLayers: false);

    public static byte[] ResolveBtlBytes(IPackSource source, string scenarioId) =>
        ScenarioBtlResolver.ResolveBtlBytes(source, scenarioId);

    public static string ScenarioJsonPath(string scenarioId) => "scenarios/" + scenarioId + ".json";
}
