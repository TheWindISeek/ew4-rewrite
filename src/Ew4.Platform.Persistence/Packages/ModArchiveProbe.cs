using Ew4.Platform.Persistence;
using System;
using System.IO;
using System.IO.Compression;

namespace Ew4.Platform.Persistence.Packages;

public static class ModArchiveProbe
{
    public static bool IsContentPackArchive(string path)
    {
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                string normalized = entry.FullName.Replace('\\', '/').TrimEnd('/');
                if (string.Equals(normalized, ModManifestPaths.FileName, StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith("/" + ModManifestPaths.FileName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalized, ModManifestPaths.LegacyModJson, StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith("/" + ModManifestPaths.LegacyModJson, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static void EnsureContentModArchive(string path)
    {
        if (!IsContentPackArchive(path))
        {
            throw new PersistenceError(
                "mod.import",
                "仅支持根目录含 mod.json 的 .zip / .ew4mod 内容包（不再支持 APK 或旧格式归档）。");
        }
    }
}
