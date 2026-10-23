using System.Text.Json;

namespace Ew4.Platform.Content;

/// <summary>
/// <c>mod.json</c> 内 <c>scenarios</c> 扩展段的解析（剧本如何加载）。
/// </summary>
public static class PackModProfile
{
    public enum ScenarioStorageMode
    {
        /// <summary>未声明 <c>scenarios</c> 时：按每个剧本 JSON 形态自动判断。</summary>
        Auto,
        /// <summary>优先 <c>scenarios/*.json</c> 嵌入 <see cref="ScenarioEdit.ScenarioDocument"/>，失败再读 legacy BTL。</summary>
        EmbeddedJson,
        /// <summary>优先 legacy 索引 + 包内 BTL，失败再尝试嵌入 JSON。</summary>
        LegacyBtl,
    }

    public sealed record ModProfile(int Schema, ScenarioStorageMode ScenarioStorage, int ScenarioDocumentSchema);

    public static ModProfile? TryRead(IPackSource source) => source.Manifest.Mod;

    internal static ModProfile ParseScenariosSection(JsonElement root, JsonElement scenarios)
    {
        int schema = root.TryGetProperty("schema", out JsonElement schemaEl) ? schemaEl.GetInt32() : 1;
        ScenarioStorageMode storage = ScenarioStorageMode.Auto;
        int documentSchema = 2;
        if (scenarios.ValueKind == JsonValueKind.Object)
        {
            if (scenarios.TryGetProperty("storage", out JsonElement storageEl)
                && storageEl.ValueKind == JsonValueKind.String)
            {
                storage = ParseStorage(storageEl.GetString());
            }

            if (scenarios.TryGetProperty("documentSchema", out JsonElement docSchemaEl)
                && docSchemaEl.ValueKind == JsonValueKind.Number)
            {
                documentSchema = docSchemaEl.GetInt32();
            }
        }

        return new ModProfile(schema, storage, documentSchema);
    }

    public static void WriteEmbeddedJsonProfile(string packRootDirectory)
    {
        var profile = new ModProfile(1, ScenarioStorageMode.EmbeddedJson, 2);
        PackManifestScenarios.MergeIntoManifest(packRootDirectory, profile);
    }

    private static ScenarioStorageMode ParseStorage(string? value) => value switch
    {
        null or "" => ScenarioStorageMode.Auto,
        "embedded-json" => ScenarioStorageMode.EmbeddedJson,
        "legacy-btl" => ScenarioStorageMode.LegacyBtl,
        "auto" => ScenarioStorageMode.Auto,
        _ => throw new ContentError("mod.profile", "Unknown scenarios.storage: " + value),
    };
}
