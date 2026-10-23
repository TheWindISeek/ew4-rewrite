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
    public static byte[] ResolveBtlBytes(IPackSource source, string scenarioId)
    {
        ScenarioDocument document = OpenScenarioDocument(source, scenarioId);
        return ScenarioBtlCodec.Export(document, reencodeLayers: false);
    }

    public static ScenarioDocument OpenScenarioDocument(IPackSource source, string scenarioId)
    {
        PackModProfile.ModProfile? profile = PackModProfile.TryRead(source);
        PackModProfile.ScenarioStorageMode mode = profile?.ScenarioStorage ?? PackModProfile.ScenarioStorageMode.Auto;

        return mode switch
        {
            PackModProfile.ScenarioStorageMode.EmbeddedJson => OpenWithEmbeddedFirst(source, scenarioId),
            PackModProfile.ScenarioStorageMode.LegacyBtl => OpenWithLegacyFirst(source, scenarioId),
            _ => OpenAuto(source, scenarioId),
        };
    }

    private static ScenarioDocument OpenWithEmbeddedFirst(IPackSource source, string scenarioId)
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

    private static ScenarioDocument OpenWithLegacyFirst(IPackSource source, string scenarioId)
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

    private static ScenarioDocument OpenAuto(IPackSource source, string scenarioId)
    {
        string path = ScenarioPackAccess.ScenarioJsonPath(scenarioId);
        if (!source.Exists(path))
        {
            throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
        }

        byte[] jsonBytes = source.Read(path);
        using JsonDocument json = JsonDocument.Parse(jsonBytes);
        if (ScenarioPackAccess.IsEmbeddedScenarioJson(json.RootElement))
        {
            return ScenarioPackAccess.ReadEmbeddedDocument(jsonBytes, scenarioId);
        }

        string sidecarPath = "scenarios/" + scenarioId + ".ew4scenario.json";
        if (source.Exists(sidecarPath))
        {
            return ScenarioPackAccess.ReadEmbeddedDocument(source.Read(sidecarPath), scenarioId);
        }

        return OpenLegacyBtl(source, scenarioId);
    }

    private static ScenarioDocument OpenEmbeddedOnly(IPackSource source, string scenarioId)
    {
        string path = ScenarioPackAccess.ScenarioJsonPath(scenarioId);
        if (!source.Exists(path))
        {
            throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
        }

        byte[] jsonBytes = source.Read(path);
        using JsonDocument json = JsonDocument.Parse(jsonBytes);
        if (!ScenarioPackAccess.IsEmbeddedScenarioJson(json.RootElement))
        {
            string sidecarPath = "scenarios/" + scenarioId + ".ew4scenario.json";
            if (source.Exists(sidecarPath))
            {
                return ScenarioPackAccess.ReadEmbeddedDocument(source.Read(sidecarPath), scenarioId);
            }

            throw new ContentError(
                "scenario.document",
                "Scenario '" + scenarioId + "' is not embedded JSON (mod.json scenarios.storage requests embedded-json).");
        }

        return ScenarioPackAccess.ReadEmbeddedDocument(jsonBytes, scenarioId);
    }

    private static ScenarioDocument OpenLegacyBtl(IPackSource source, string scenarioId)
    {
        string path = ScenarioPackAccess.ScenarioJsonPath(scenarioId);
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
        string? packPath = PackAssetRead.Resolve(source, logicalBtl);
        if (packPath != null)
        {
            return ScenarioBtlCodec.Import(scenarioId, source.Read(packPath));
        }

        throw new ContentError("scenario.missing", "Scenario not found in pack: " + scenarioId);
    }
}
