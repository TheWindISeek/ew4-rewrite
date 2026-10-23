using System;
using System.Collections.Generic;
using System.IO;

namespace Ew4.Platform.Content.Build;

/// <summary>
/// 将封存运行时路径映射到 Pack 内布局（<c>data/*</c> → <c>assets/tables/*</c>，兼容旧 <c>assets/legacy/*</c>；<c>rewrite/*</c> 支持包根或 <c>assets/rewrite/*</c>）。
/// </summary>
internal static class PackAssetRead
{
    internal static Func<string, byte[]> FromPack(IPackSource pack)
    {
        return path =>
        {
            string? logical = Resolve(pack, path);
            if (logical == null)
            {
                throw new FileNotFoundException("Pack asset not found: " + path, path);
            }

            return pack.Read(logical);
        };
    }

    internal static string? Resolve(IPackSource pack, string path)
    {
        foreach (string candidate in Candidates(path))
        {
            if (pack.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    internal static IEnumerable<string> Candidates(string path)
    {
        if (path.StartsWith("data/", StringComparison.Ordinal))
        {
            string tail = path[5..];
            yield return "assets/tables/" + tail;
            yield return "assets/legacy/" + tail;
            yield break;
        }

        if (path.StartsWith("rewrite/", StringComparison.Ordinal))
        {
            yield return path;
            yield return "assets/" + path;
            yield break;
        }

        if (path.StartsWith("original/", StringComparison.Ordinal)
            && path.EndsWith(".btl", StringComparison.OrdinalIgnoreCase))
        {
            yield return "source/" + path;
            yield return "assets/" + path;
            yield break;
        }

        if (path.StartsWith("original/", StringComparison.Ordinal))
        {
            string file = path["original/".Length..];
            if (!file.EndsWith(".btl", StringComparison.OrdinalIgnoreCase))
            {
                // APK overlay Mod 把 def_*.xml / stringtable 放在 assets/tables/，须优先于父包 assets/original/
                yield return "assets/tables/" + file;
            }

            yield return "assets/" + path;
            yield return "source/" + path;
            yield break;
        }

        if (path.StartsWith("client/", StringComparison.Ordinal)
            || path.StartsWith("legacy/", StringComparison.Ordinal))
        {
            yield return "assets/" + path;
            yield return "source/" + path;
            yield break;
        }

        yield return "assets/" + path;
        yield return path;
    }
}
