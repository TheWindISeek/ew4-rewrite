using System;
using System.Text.Json;
using Ew4.Platform.Content.StaticData;
using Ew4.Platform.Mod.Scenario;

namespace Ew4.Platform.Content;

/// <summary>
/// 剧本读取入口：Content 只认场景 JSON（schema 3），不接触 btl。
/// </summary>
public static class ScenarioModAccess
{
    public static string ScenarioJsonPath(string scenarioId) => "scenarios/" + scenarioId + ".json";

    public static bool IsScenarioJson(JsonElement root) =>
        root.TryGetProperty("schema", out JsonElement schema) && schema.ValueKind == JsonValueKind.Number
        && schema.GetInt32() == ScenarioBtlConverter.SchemaVersion;

    /// <summary>读取并构造开局用的剧本数据。</summary>
    public static StaticDataBattle OpenBattle(IModSource source, string scenarioId)
    {
        string path = ScenarioJsonPath(scenarioId);
        if (!source.Exists(path))
            throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
        using JsonDocument json = JsonDocument.Parse(source.Read(path));
        JsonElement root = json.RootElement;
        if (!IsScenarioJson(root))
            throw new ContentError("scenario.schema", "Scenario '" + scenarioId + "' is not schema 3 (use `scenario to-v3` to convert).");
        string? id = root.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
        if (!string.Equals(id, scenarioId, StringComparison.Ordinal))
            throw new ContentError("scenario.id", "Scenario id mismatch: expected '" + scenarioId + "', got '" + id + "'.");
        return StaticDataBattle.FromJson(root);
    }
}
