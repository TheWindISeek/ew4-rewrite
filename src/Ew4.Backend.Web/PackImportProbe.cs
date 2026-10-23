using System.IO.Compression;
using Ew4.Platform.Content;

namespace Ew4.Backend.Web;

internal static class PackImportProbe
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
                if (string.Equals(normalized, "pack.json", StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith("/pack.json", StringComparison.OrdinalIgnoreCase))
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
                "仅支持根目录含 pack.json 的 .zip / .ew4mod 内容包（不再支持 APK 或旧格式归档）。");
        }
    }
}
