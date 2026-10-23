using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Ew4.Platform.Content;

/// <summary>
/// Play 侧剧本 id 列表与 Mod overlay 选关显示名（<c>scenarios/*.json</c> 的 <c>name</c>）。
/// </summary>
public static class PackScenarioCatalog
{
    public static IReadOnlyList<string> ListIds(ResolvedContent content)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in PackMerge.OverlayCatalog(content.Source).List("scenarios/"))
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string id = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }
        }

        LegacyBattleListScenarios.AddIds(content.Source, ids);
        var list = new List<string>(ids);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    public static IReadOnlyDictionary<string, string> ReadOverlayDisplayNames(IPackSource source)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        IPackSource overlay = PackMerge.OverlayCatalog(source);
        foreach (string path in overlay.List("scenarios/"))
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string id = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            try
            {
                byte[] jsonBytes = source.Read(path);
                using JsonDocument document = JsonDocument.Parse(jsonBytes);
                if (!document.RootElement.TryGetProperty("name", out JsonElement nameEl)
                    || nameEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? name = nameEl.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names[id] = name.Trim();
                }
            }
            catch (ContentError)
            {
                // ignore missing/unreadable overlay index
            }
            catch (JsonException)
            {
                // ignore invalid json in catalog pass
            }
        }

        return names;
    }
}
