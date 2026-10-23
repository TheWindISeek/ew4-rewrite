using System;
using System.Collections.Generic;

namespace Ew4.Platform.Content;

/// <summary>
/// Mod 切换后 UI 正常所需的客户端资源最低集（M1）；合并后的 effective 包上检查。
/// </summary>
public static class PackContentContract
{
    public static readonly string[] RecommendedClientPaths =
    [
        "client/manifest.json",
        "client/countries.json",
        "client/unit-visuals.json",
        "client/effects.json",
    ];

    public static IEnumerable<Diagnostic> Validate(IPackSource source)
    {
        foreach (string path in RecommendedClientPaths)
        {
            if (!source.Exists(path))
            {
                yield return new Diagnostic(
                    Severity.Warning,
                    "pack.client.missing",
                    path,
                    "Recommended client asset is missing after pack merge; UI may show wrong flags, units, or colors.",
                    "Add the file under assets/client/ or ensure parent pack provides it.");
            }
        }
    }
}
