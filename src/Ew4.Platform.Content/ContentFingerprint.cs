using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ew4.Platform.Content;

internal static class ContentFingerprint
{
    public static string Compute(IPackSource source, IContentKindRegistry registry)
    {
        var lines = new List<string>();
        foreach (string path in source.List("").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (string.Equals(path, PackManifestPaths.FileName, StringComparison.Ordinal)
                || string.Equals(path, PackManifestPaths.LegacyPackJson, StringComparison.Ordinal))
            {
                continue;
            }

            IContentKind? kind = registry.Match(path);
            if (kind == null || !kind.AffectsFingerprint)
            {
                continue;
            }

            byte[] bytes = source.Read(path);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            lines.Add(path + ":" + hash);
        }

        string payload = string.Join('\n', lines);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
