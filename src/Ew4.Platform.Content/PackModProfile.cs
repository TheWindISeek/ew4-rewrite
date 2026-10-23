using System;
using System.IO;
using System.Text.Json;

namespace Ew4.Platform.Content;

/// <summary>
/// Mod 内容主说明文件 <c>mod.json</c>（包根）：声明剧本等数据的存储形态（新格式 vs 兼容 legacy BTL）。
/// </summary>
public static class PackModProfile
{
    public const string FilePath = "mod.json";

    public enum ScenarioStorageMode
    {
        /// <summary>未声明主文件时：按每个剧本 JSON 形态自动判断。</summary>
        Auto,
        /// <summary>优先 <c>scenarios/*.json</c> 嵌入 <see cref="ScenarioEdit.ScenarioDocument"/>，失败再读 legacy BTL。</summary>
        EmbeddedJson,
        /// <summary>优先 legacy 索引 + 包内 BTL，失败再尝试嵌入 JSON。</summary>
        LegacyBtl,
    }

    public sealed record ModProfile(int Schema, ScenarioStorageMode ScenarioStorage, int ScenarioDocumentSchema);

    public static ModProfile? TryRead(IPackSource source)
    {
        if (!source.Exists(FilePath))
        {
            return null;
        }

        return Read(source.Read(FilePath));
    }

    public static ModProfile Read(byte[] jsonBytes)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonBytes);
            JsonElement root = document.RootElement;
            int schema = root.TryGetProperty("schema", out JsonElement schemaEl) ? schemaEl.GetInt32() : 1;
            ScenarioStorageMode storage = ScenarioStorageMode.Auto;
            int documentSchema = 2;
            if (root.TryGetProperty("scenarios", out JsonElement scenariosEl) && scenariosEl.ValueKind == JsonValueKind.Object)
            {
                if (scenariosEl.TryGetProperty("storage", out JsonElement storageEl)
                    && storageEl.ValueKind == JsonValueKind.String)
                {
                    storage = ParseStorage(storageEl.GetString());
                }

                if (scenariosEl.TryGetProperty("documentSchema", out JsonElement docSchemaEl)
                    && docSchemaEl.ValueKind == JsonValueKind.Number)
                {
                    documentSchema = docSchemaEl.GetInt32();
                }
            }

            return new ModProfile(schema, storage, documentSchema);
        }
        catch (JsonException ex)
        {
            throw new ContentError("mod.profile", "Invalid mod.json: " + ex.Message);
        }
    }

    public static byte[] WriteEmbeddedJsonProfileBytes() =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                schema = 1,
                scenarios = new { storage = "embedded-json", documentSchema = 2 },
            },
            new JsonSerializerOptions { WriteIndented = true });

    public static void WriteEmbeddedJsonProfile(string packRootDirectory)
    {
        string root = Path.GetFullPath(packRootDirectory);
        string profilePath = Path.Combine(root, FilePath.Replace('/', Path.DirectorySeparatorChar));
        string? parent = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllBytes(profilePath, WriteEmbeddedJsonProfileBytes());
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
