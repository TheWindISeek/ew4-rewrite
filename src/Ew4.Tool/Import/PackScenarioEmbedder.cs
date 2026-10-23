using System;
using System.IO;
using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Tool.Import;

/// <summary>
/// 将已有包内的 schema 1 索引桩 + <c>source/original/*.btl</c> 转为嵌入 JSON（就地迁移）。
/// </summary>
public static class ModScenarioEmbedder
{
    public sealed record Result(int Embedded, int AlreadyEmbedded, int SkippedMissingBtl, int RemovedBtlFiles);

    public static Result Embed(string packDirectory, bool removePackBtl = false)
    {
        string root = Path.GetFullPath(packDirectory);
        IModSource pack = new DirectoryModSource(root);
        int embedded = 0;
        int already = 0;
        int skipped = 0;
        int removed = 0;

        foreach (ScenarioIndex scenario in ScenarioCatalog.List(pack))
        {
            string jsonPath = Path.Combine(root, scenario.JsonPath.Replace('/', Path.DirectorySeparatorChar));
            byte[] jsonBytes = File.ReadAllBytes(jsonPath);
            using var json = System.Text.Json.JsonDocument.Parse(jsonBytes);
            if (ScenarioModAccess.IsEmbeddedScenarioJson(json.RootElement))
            {
                already++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(scenario.OriginalBtlPath))
            {
                throw new ContentError("scenario.legacy", "Cannot embed scenario without legacy.btl: " + scenario.Id);
            }

            string btlPath = Path.Combine(root, scenario.OriginalBtlPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(btlPath))
            {
                if (scenario.Id.StartsWith("multiplay", StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }

                throw new ContentError("scenario.btl", "BTL missing for embed: " + scenario.OriginalBtlPath);
            }

            ScenarioDocument document = ScenarioBtlCodec.Import(scenario.Id, File.ReadAllBytes(btlPath));
            File.WriteAllBytes(jsonPath, ScenarioDocumentJson.Serialize(document));
            embedded++;
        }

        if (removePackBtl)
        {
            string btlDir = Path.Combine(root, "source", "original");
            if (Directory.Exists(btlDir))
            {
                foreach (string file in Directory.EnumerateFiles(btlDir, "*.btl", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(file);
                    removed++;
                }
            }
        }

        if (embedded > 0 || already > 0)
        {
            ModManifestProfile.WriteEmbeddedJsonProfile(root);
        }

        return new(embedded, already, skipped, removed);
    }
}
