using System;
using System.IO;
using System.IO.Compression;

namespace Ew4.Platform.Content;

public static class PackArchiveProbe
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
                if (string.Equals(normalized, PackManifestPaths.FileName, StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith("/" + PackManifestPaths.FileName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalized, PackManifestPaths.LegacyPackJson, StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith("/" + PackManifestPaths.LegacyPackJson, StringComparison.OrdinalIgnoreCase))
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

    public static void EnsureContentPackArchive(string path)
    {
        if (!IsContentPackArchive(path))
        {
            throw new ContentError(
                "pack.import",
                "仅支持根目录含 mod.json 的 .zip / .ew4mod 内容包（不再支持 APK 或旧格式归档）。");
        }
    }
}
