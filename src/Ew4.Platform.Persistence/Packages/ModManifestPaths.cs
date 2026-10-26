using Ew4.Platform.Persistence;
using System;
using System.IO;

namespace Ew4.Platform.Persistence.Packages;

/// <summary>
/// 内容包根清单路径：作者只维护 <see cref="FileName"/>（<c>mod.json</c>）。
/// </summary>
public static class ModManifestPaths
{
    public const string FileName = "mod.json";

    /// <summary>旧版清单文件名（磁盘上可能与 <see cref="FileName"/> 并存，载入时忽略）。</summary>
    public const string LegacyModJson = "pack.json";

    public static void MigrateLegacyModJsonIfNeeded(string modRootDirectory)
    {
        string root = Path.GetFullPath(modRootDirectory);
        string modPath = Path.Combine(root, FileName);
        if (File.Exists(modPath))
        {
            return;
        }

        string packPath = Path.Combine(root, LegacyModJson);
        if (!File.Exists(packPath))
        {
            throw new PersistenceError("mod.missing", "Pack is missing mod.json.");
        }

        File.Copy(packPath, modPath);
        File.Delete(packPath);
    }

    public static bool DirectoryHasManifest(string modDirectory)
    {
        string root = Path.GetFullPath(modDirectory);
        return File.Exists(Path.Combine(root, FileName)) || File.Exists(Path.Combine(root, LegacyModJson));
    }
}
