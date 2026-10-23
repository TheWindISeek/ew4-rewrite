using System;
using System.IO;

namespace Ew4.Platform.Content;

/// <summary>
/// 内容包根清单路径：作者只维护 <see cref="FileName"/>（<c>mod.json</c>）。
/// </summary>
public static class ModManifestPaths
{
    public const string FileName = "mod.json";

    /// <summary>旧版清单文件名（磁盘上可能与 <see cref="FileName"/> 并存，载入时忽略）。</summary>
    public const string LegacyPackJson = "pack.json";

    public static void MigrateLegacyPackJsonIfNeeded(string packRootDirectory)
    {
        string root = Path.GetFullPath(packRootDirectory);
        string modPath = Path.Combine(root, FileName);
        if (File.Exists(modPath))
        {
            return;
        }

        string packPath = Path.Combine(root, LegacyPackJson);
        if (!File.Exists(packPath))
        {
            throw new ContentError("mod.missing", "Pack is missing mod.json.");
        }

        File.Copy(packPath, modPath);
        File.Delete(packPath);
    }

    public static bool DirectoryHasManifest(string packDirectory)
    {
        string root = Path.GetFullPath(packDirectory);
        return File.Exists(Path.Combine(root, FileName)) || File.Exists(Path.Combine(root, LegacyPackJson));
    }
}
