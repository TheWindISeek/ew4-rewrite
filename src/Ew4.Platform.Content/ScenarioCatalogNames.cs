using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Ew4.Platform.Content;

/// <summary>
/// 从 Pack 内 <c>scenarios/*.json</c> 读取选关列表显示名（差异 Mod 覆盖 <c>name</c> 时优先于 battlelist）。
/// </summary>
public static class ScenarioCatalogNames
{
    public static IReadOnlyDictionary<string, string> ReadDisplayNames(IPackSource source)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in source.List("scenarios/"))
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
