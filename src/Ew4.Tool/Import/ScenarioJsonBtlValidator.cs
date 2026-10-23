using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Platform.Content;

namespace Ew4.Tool.Import;

/// <summary>
/// G6：Pack 内嵌入剧本 JSON 导出 BTL 与参考语料逐字节一致。
/// </summary>
public static class ScenarioJsonBtlValidator
{
    public sealed record Result(
        int ScenarioCount,
        int Compared,
        int SkippedStub,
        IReadOnlyList<string> Failures);

    public static Result ValidatePackDirectory(string packDirectory, string? referenceBtlDirectory = null)
    {
        string root = Path.GetFullPath(packDirectory);
        string referenceRoot = Path.GetFullPath(
            referenceBtlDirectory ?? Path.Combine(root, "source", "original"));
        var failures = new List<string>();
        int compared = 0;
        int skippedStub = 0;
        int total = 0;

        IModSource pack = new DirectoryModSource(root);
        foreach (ScenarioIndex scenario in ScenarioCatalog.List(pack))
        {
            total++;
            string jsonPath = Path.Combine(root, scenario.JsonPath.Replace('/', Path.DirectorySeparatorChar));
            byte[] jsonBytes = File.ReadAllBytes(jsonPath);
            using var json = System.Text.Json.JsonDocument.Parse(jsonBytes);
            if (!ScenarioModAccess.IsEmbeddedScenarioJson(json.RootElement))
            {
                skippedStub++;
                if (!scenario.Id.StartsWith("multiplay", StringComparison.Ordinal))
                {
                    failures.Add("stub scenario (re-run import-base or pack-embed-scenarios): " + scenario.Id);
                }

                continue;
            }

            byte[] generated = ScenarioModAccess.ExportBtlBytes(
                ScenarioModAccess.ReadEmbeddedDocument(jsonBytes, scenario.Id));
            string referencePath = Path.Combine(referenceRoot, scenario.Id + ".btl");
            if (!File.Exists(referencePath))
            {
                failures.Add("reference btl missing: " + scenario.Id);
                continue;
            }

            byte[] reference = File.ReadAllBytes(referencePath);
            if (!generated.AsSpan().SequenceEqual(reference))
            {
                failures.Add("json→btl mismatch: " + scenario.Id);
                continue;
            }

            compared++;
        }

        return new(total, compared, skippedStub, failures);
    }
}
