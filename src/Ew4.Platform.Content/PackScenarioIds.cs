using System;
using System.Collections.Generic;
using System.IO;

namespace Ew4.Platform.Content;

public static class PackScenarioIds
{
    /// <summary>
    /// 列出 Pack 内剧本 id（<c>scenarios/*.json</c> + 有 BTL 的 battlelist 条目），供 Play 与 <c>ClientContent</c> 使用。
    /// </summary>
    public static IReadOnlyList<string> List(ResolvedContent content)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        IPackSource catalog = content.Source is MergedPackSource merged && merged.Parent != null
            ? merged.Primary
            : content.Source;
        foreach (string path in catalog.List("scenarios/"))
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
}
