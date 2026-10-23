using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Build;

namespace Ew4.Tool.Import;

/// <summary>
/// 离线工具：将原版 Mod APK（assets/ 与 base 差异）转为 <c>parent: base</c> 内容包目录。不属于运行时 Content API。
/// </summary>
public static class ApkOverlayModImporter
{
    public sealed record Result(string PackDirectory, int FilesCopied, int ScenariosIndexed);

    public static Result Import(
        string apkPath,
        string modId,
        string packName,
        string basePackDirectory,
        string destinationPackDirectory)
    {
        apkPath = Path.GetFullPath(apkPath);
        string baseRoot = Path.GetFullPath(basePackDirectory);
        string dest = Path.GetFullPath(destinationPackDirectory);
        modId = ModId.Validate(modId);
        if (string.IsNullOrWhiteSpace(packName))
        {
            throw new ContentError("mod.name", "Pack name is required.");
        }

        if (!File.Exists(apkPath))
        {
            throw new ContentError("import.apk", "APK not found: " + apkPath);
        }

        IModSource baseline = new DirectoryModSource(baseRoot);
        if (Directory.Exists(dest))
        {
            Directory.Delete(dest, recursive: true);
        }

        Directory.CreateDirectory(dest);
        string extract = Path.Combine(Path.GetTempPath(), "ew4-apk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extract);
        int copied = 0;
        try
        {
            ZipFile.ExtractToDirectory(apkPath, extract);
            string assetsRoot = Path.Combine(extract, "assets");
            if (!Directory.Exists(assetsRoot))
            {
                throw new ContentError("import.apk", "APK has no assets/ directory.");
            }

            foreach (string file in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(assetsRoot, file).Replace('\\', '/');
                if (relative.StartsWith("dexopt/", StringComparison.Ordinal) || relative.EndsWith(".dex", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? logical = ResolveModPath(baseline, relative);
                if (logical == null)
                {
                    continue;
                }

                byte[] modBytes = File.ReadAllBytes(file);
                if (logical.EndsWith("def_commander.xml", StringComparison.OrdinalIgnoreCase))
                {
                    modBytes = LegacyTableXmlRepair.NormalizeCommanderTable(modBytes);
                }

                if (baseline.Exists(logical) && baseline.Read(logical).AsSpan().SequenceEqual(modBytes))
                {
                    continue;
                }

                string target = Path.Combine(dest, logical.Replace('/', Path.DirectorySeparatorChar));
                string? parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.WriteAllBytes(target, modBytes);
                copied++;
            }

            WritePackJson(dest, modId, packName, baseline.Manifest);
            int scenarios = WriteScenarioStubs(dest, baseline);
            ModFlagPlaceholder.Ensure(dest, baseRoot);
            return new(dest, copied, scenarios);
        }
        finally
        {
            try
            {
                Directory.Delete(extract, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

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

    private static void WritePackJson(string dest, string modId, string packName, ModManifest baseManifest)
    {
        var formats = new Dictionary<string, int>(baseManifest.Formats, StringComparer.Ordinal);
        string json = JsonSerializer.Serialize(
            new
            {
                schema = 1,
                id = modId,
                name = packName,
                version = "1.0.0",
                parent = ModId.Base,
                requires = new { engine = baseManifest.Requires.Engine },
                formats,
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dest, ModManifestPaths.FileName), json);
    }

    private static int WriteScenarioStubs(string dest, IModSource baseline)
    {
        byte[] xmlBytes = ReadFirstExisting(baseline, dest,
            "assets/original/def_battlelist.xml",
            "assets/tables/def_battlelist.xml");
        if (xmlBytes.Length == 0)
        {
            return 0;
        }

        string xmlText = System.Text.Encoding.UTF8.GetString(xmlBytes);
        XDocument document = XDocument.Parse(xmlText);
        string scenarioDir = Path.Combine(dest, "scenarios");
        Directory.CreateDirectory(scenarioDir);
        int count = 0;
        foreach (XElement battle in document.Root?.Elements("battle") ?? [])
        {
            string? file = battle.Attribute("file")?.Value;
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            string id = Path.GetFileNameWithoutExtension(file);
            string btlLogical = "source/original/" + file;
            string btlPhysical = Path.Combine(dest, btlLogical.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(btlPhysical))
            {
                continue;
            }

            var stub = new
            {
                schema = 1,
                id,
                name = battle.Attribute("name")?.Value ?? id,
                mapId = int.TryParse(battle.Attribute("map")?.Value, out int map) ? map : 0,
                legacy = new { btl = btlLogical },
            };
            string json = JsonSerializer.Serialize(stub, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(scenarioDir, id + ".json"), json);
            count++;
        }

        return count;
    }

    private static byte[] ReadFirstExisting(IModSource baseline, string destRoot, params string[] paths)
    {
        foreach (string path in paths)
        {
            string physical = Path.Combine(destRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(physical))
            {
                return File.ReadAllBytes(physical);
            }

            if (baseline.Exists(path))
            {
                return baseline.Read(path);
            }
        }

        return [];
    }
}
