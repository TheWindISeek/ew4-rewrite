using System.Collections.Frozen;
using Ew4.Platform.Mod.Apk;
using Ew4.Platform.Persistence.Packages;
using ModPath = Ew4.Platform.Persistence.Packages.ModPath;

namespace Ew4.Tool.Lib.Export;

public static class ApkAssetPathMapping
{
    public static FrozenDictionary<string, string> BuildShellIndex(IModSource baseline, IReadOnlyList<string> apkAssetRelativePaths)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string apkRelative in apkAssetRelativePaths)
        {
            string? logical = ModApkPathMapping.ResolveModPath(baseline, apkRelative);
            if (logical == null)
            {
                continue;
            }

            map.TryAdd(logical, apkRelative);
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public static string? ResolveApkPath(
        string logicalPath,
        IModSource baseline,
        FrozenDictionary<string, string> shellIndex)
    {
        logicalPath = ModPath.Normalize(logicalPath);
        if (shellIndex.TryGetValue(logicalPath, out string? indexed))
        {
            return indexed;
        }

        foreach (string candidate in CandidateApkPaths(logicalPath, baseline))
        {
            if (shellIndex.Values.Contains(candidate))
            {
                return candidate;
            }
        }

        return CandidateApkPaths(logicalPath, baseline).FirstOrDefault();
    }

    public static IEnumerable<string> CandidateApkPaths(string logicalPath, IModSource baseline)
    {
        logicalPath = ModPath.Normalize(logicalPath);
        string fileName = Path.GetFileName(logicalPath);
        if (logicalPath.StartsWith("source/original/", StringComparison.Ordinal)
            && fileName.EndsWith(".btl", StringComparison.OrdinalIgnoreCase))
        {
            yield return fileName;
        }

        if (logicalPath.StartsWith("assets/tables/", StringComparison.Ordinal))
        {
            yield return "assets/tables/" + fileName;
            yield return fileName;
        }

        if (logicalPath.StartsWith("assets/original/", StringComparison.Ordinal))
        {
            yield return "assets/original/" + fileName;
            yield return fileName;
        }

        if (logicalPath.StartsWith("assets/client/images/", StringComparison.Ordinal))
        {
            yield return fileName;
        }

        if (logicalPath.StartsWith("assets/client/", StringComparison.Ordinal))
        {
            string tail = logicalPath["assets/client/".Length..];
            yield return "assets/client/" + tail;
            yield return tail;
        }

        if (logicalPath.StartsWith("assets/", StringComparison.Ordinal))
        {
            yield return logicalPath;
            yield return logicalPath["assets/".Length..];
        }

        if (baseline.Exists(logicalPath))
        {
            string? byName = baseline.List("")
                .FirstOrDefault(p => p.EndsWith("/" + fileName, StringComparison.Ordinal) || string.Equals(p, fileName, StringComparison.Ordinal));
            if (byName != null && !string.Equals(byName, logicalPath, StringComparison.Ordinal))
            {
                foreach (string nested in CandidateApkPaths(byName, baseline))
                {
                    yield return nested;
                }
            }
        }
    }
}
