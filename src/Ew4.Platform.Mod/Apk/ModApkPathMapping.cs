using Ew4.Platform.Persistence.Packages;

namespace Ew4.Platform.Mod.Apk;

/// <summary>APK <c>assets/</c> 相对路径 → Mod 逻辑路径（与 Tool import 对称）。</summary>
public static class ModApkPathMapping
{
    public static string? ResolveModPath(IModSource baseline, string apkRelativePath)
    {
        string fileName = Path.GetFileName(apkRelativePath);
        if (fileName.EndsWith(".btl", StringComparison.OrdinalIgnoreCase))
        {
            return "source/original/" + fileName;
        }

        if (fileName.StartsWith("def_", StringComparison.Ordinal))
        {
            string original = "assets/original/" + fileName;
            if (baseline.Exists(original))
            {
                return original;
            }

            return "assets/tables/" + fileName;
        }

        if (fileName.StartsWith("stringtable", StringComparison.OrdinalIgnoreCase)
            || (fileName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) && fileName.Contains("string", StringComparison.OrdinalIgnoreCase)))
        {
            string tables = "assets/tables/" + fileName;
            if (baseline.Exists(tables))
            {
                return tables;
            }

            return "assets/original/" + fileName;
        }

        string? byName = baseline.List("")
            .FirstOrDefault(p => p.EndsWith("/" + fileName, StringComparison.Ordinal) || string.Equals(p, fileName, StringComparison.Ordinal));
        if (byName != null)
        {
            return byName;
        }

        if (apkRelativePath.StartsWith("map/", StringComparison.Ordinal))
        {
            return null;
        }

        if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".pkm", StringComparison.OrdinalIgnoreCase))
        {
            return "assets/client/images/" + fileName;
        }

        if (fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            return "assets/original/" + fileName;
        }

        return null;
    }
}
