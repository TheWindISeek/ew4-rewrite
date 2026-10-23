using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Content;

namespace Ew4.Platform.Session;

internal static class SessionCatalog
{
    public static IReadOnlyList<string> ListScenarioIds(ResolvedContent content) =>
        content.Source.List("scenarios/")
            .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(path => path["scenarios/".Length..^".json".Length])
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    public static object BuildCatalogPayload(ResolvedContent content)
    {
        var scenarios = ListScenarioIds(content);
        return new Dictionary<string, object?>
        {
            ["packId"] = content.Manifest.Id,
            ["fingerprint"] = content.Fingerprint,
            ["scenarios"] = scenarios,
        };
    }
}
