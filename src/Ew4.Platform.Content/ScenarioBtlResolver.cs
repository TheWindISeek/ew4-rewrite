using System;
using System.Text.Json;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Platform.Content;

/// <summary>
/// 抽象「如何得到开局用 BTL 字节」：引擎始终消费 BTL，来源可以是嵌入 JSON 或包内 legacy 文件。
/// </summary>
public static class ScenarioBtlResolver
{
    public static byte[] ResolveBtlBytes(IModSource source, string scenarioId)
    {
        ScenarioDocument document = OpenScenarioDocument(source, scenarioId);
        return ScenarioBtlCodec.Export(document, reencodeLayers: false);
    }

    public static ScenarioDocument OpenScenarioDocument(IModSource source, string scenarioId)
    {
        ModManifestProfile.ModProfile? profile = ModManifestProfile.TryRead(source);
        ModManifestProfile.ScenarioStorageMode mode = profile?.ScenarioStorage ?? ModManifestProfile.ScenarioStorageMode.Auto;

        return mode switch
        {
            ModManifestProfile.ScenarioStorageMode.EmbeddedJson => OpenWithEmbeddedFirst(source, scenarioId),
            ModManifestProfile.ScenarioStorageMode.LegacyBtl => OpenWithLegacyFirst(source, scenarioId),
            _ => OpenAuto(source, scenarioId),
        };
    }

    private static ScenarioDocument OpenWithEmbeddedFirst(IModSource source, string scenarioId)
    {
        try
        {
            return OpenEmbeddedOnly(source, scenarioId);
        }
        catch (ContentError)
        {
            return OpenLegacyBtl(source, scenarioId);
        }
    }

    private static ScenarioDocument OpenWithLegacyFirst(IModSource source, string scenarioId)
    {
        try
        {
            return OpenLegacyBtl(source, scenarioId);
        }
        catch (ContentError)
        {
            return OpenEmbeddedOnly(source, scenarioId);
        }
    }

    private static ScenarioDocument OpenAuto(IModSource source, string scenarioId)
    {
        string path = ScenarioModAccess.ScenarioJsonPath(scenarioId);
        if (!source.Exists(path))
        {
            throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
        }

        byte[] jsonBytes = source.Read(path);
        using JsonDocument json = JsonDocument.Parse(jsonBytes);
        if (ScenarioModAccess.IsEmbeddedScenarioJson(json.RootElement))
        {
            return ScenarioModAccess.ReadEmbeddedDocument(jsonBytes, scenarioId);
        }

        string sidecarPath = "scenarios/" + scenarioId + ".ew4scenario.json";
        if (source.Exists(sidecarPath))
        {
            return ScenarioModAccess.ReadEmbeddedDocument(source.Read(sidecarPath), scenarioId);
        }

        return OpenLegacyBtl(source, scenarioId);
    }

    private static ScenarioDocument OpenEmbeddedOnly(IModSource source, string scenarioId)
    {
        string path = ScenarioModAccess.ScenarioJsonPath(scenarioId);
        if (!source.Exists(path))
        {
            throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
        }

        byte[] jsonBytes = source.Read(path);
        using JsonDocument json = JsonDocument.Parse(jsonBytes);
        if (!ScenarioModAccess.IsEmbeddedScenarioJson(json.RootElement))
        {
            string sidecarPath = "scenarios/" + scenarioId + ".ew4scenario.json";
            if (source.Exists(sidecarPath))
            {
                return ScenarioModAccess.ReadEmbeddedDocument(source.Read(sidecarPath), scenarioId);
            }

            throw new ContentError(
                "scenario.document",
                "Scenario '" + scenarioId + "' is not embedded JSON (mod.json scenarios.storage requests embedded-json).");
        }

        return ScenarioModAccess.ReadEmbeddedDocument(jsonBytes, scenarioId);
    }

    private static ScenarioDocument OpenLegacyBtl(IModSource source, string scenarioId)
    {
        string path = ScenarioModAccess.ScenarioJsonPath(scenarioId);
        if (source.Exists(path))
        {
            byte[] jsonBytes = source.Read(path);
            using JsonDocument json = JsonDocument.Parse(jsonBytes);
            if (json.RootElement.TryGetProperty("legacy", out JsonElement legacyEl)
                && legacyEl.TryGetProperty("btl", out JsonElement btlPathEl))
            {
                string btlPath = btlPathEl.GetString() ?? "";
                if (!source.Exists(btlPath))
                {
                    throw new ContentError("scenario.btl", "BTL not found in pack: " + btlPath);
                }

                return ScenarioBtlCodec.Import(scenarioId, source.Read(btlPath));
            }
        }

        string logicalBtl = "original/" + scenarioId + ".btl";
        string? packPath = ModAssetRead.Resolve(source, logicalBtl);
        if (packPath != null)
        {
            return ScenarioBtlCodec.Import(scenarioId, source.Read(packPath));
        }

        throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
    }
}
