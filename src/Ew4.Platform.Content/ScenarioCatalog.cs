using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Ew4.Platform.Content;

/// <summary>
/// 从 Pack 列出 <c>scenarios/*.json</c> 索引项。
/// </summary>
public static class ScenarioCatalog
{
    public static IReadOnlyList<ScenarioIndex> List(IPackSource source)
    {
        var list = new List<ScenarioIndex>();
        foreach (string path in source.List("scenarios/").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            byte[] json = source.Read(path);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? id = root.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            bool embedded = ScenarioPackAccess.IsEmbeddedScenarioJson(root);
            string? btl = null;
            if (!embedded
                && root.TryGetProperty("legacy", out JsonElement legacy)
                && legacy.TryGetProperty("btl", out JsonElement btlEl))
            {
                btl = btlEl.GetString();
            }

            list.Add(new ScenarioIndex(id, path, btl, embedded));
        }

        return list;
    }
}

public sealed record ScenarioIndex(string Id, string JsonPath, string? OriginalBtlPath, bool IsEmbeddedDocument);
