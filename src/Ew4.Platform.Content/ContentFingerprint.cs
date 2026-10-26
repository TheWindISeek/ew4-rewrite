using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ew4.Platform.Content;

internal static class ContentFingerprint
{
    public static string Compute(IModSource source, IContentKindRegistry registry)
    {
        var lines = new List<string>();
        foreach (string path in source.List("").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (string.Equals(path, ModManifestPaths.FileName, StringComparison.Ordinal)
                || string.Equals(path, ModManifestPaths.LegacyModJson, StringComparison.Ordinal))
            {
                continue;
            }

            IContentKind? kind = registry.Match(path);
            if (kind == null || !kind.AffectsFingerprint)
            {
                continue;
            }

            byte[] bytes = source.Read(path);
            string hash = Convert.ToHexString(Ew4.Interface.Common.Sha256Hash.HashData(bytes)).ToLowerInvariant();
            lines.Add(path + ":" + hash);
        }

        string payload = string.Join('\n', lines);
        return Convert.ToHexString(Ew4.Interface.Common.Sha256Hash.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
