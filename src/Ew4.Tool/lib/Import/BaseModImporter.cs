using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ew4.Platform.Content;

namespace Ew4.Tool.Lib.Import;

/// <summary>
/// 从封存运行时目录生成内容包布局（资源复制 + 剧本嵌入 JSON，不复制 BTL 进包）。
/// </summary>
public sealed class BaseModImporter
{
    public BaseModImportResult Import(BaseModImportOptions options)
    {
        if (!Directory.Exists(options.StaticDataDataDirectory))
        {
            throw new ContentError("import.data", "Import seed data directory does not exist: " + options.StaticDataDataDirectory);
        }

        string destination = Path.GetFullPath(options.DestinationModDirectory);
        Directory.CreateDirectory(destination);
        CopyTree(Path.Combine(options.StaticDataDataDirectory, "client"), Path.Combine(destination, "assets", "client"));
        CopyTree(Path.Combine(options.StaticDataDataDirectory, "original"), Path.Combine(destination, "assets", "original"));
        CopyTree(Path.Combine(options.StaticDataDataDirectory, "legacy"), Path.Combine(destination, "assets", "tables"));
        File.Copy(
            Path.Combine(options.StaticDataDataDirectory, "runtime-content.json"),
            Path.Combine(destination, "assets", "runtime-content.json"),
            overwrite: true);

        string sourceOriginal = Path.Combine(
            options.StaticDataBtlDirectory ?? Path.Combine(options.StaticDataDataDirectory, "original"),
            "");
        IReadOnlyList<ScenarioIndexEntry> scenarios = ReadBattleList(Path.Combine(options.StaticDataDataDirectory, "original", "def_battlelist.xml"));
        Directory.CreateDirectory(Path.Combine(destination, "scenarios"));
        int embedded = 0;
        foreach (ScenarioIndexEntry entry in scenarios)
        {
            string id = Path.GetFileNameWithoutExtension(entry.File);
            string btlFile = Path.Combine(sourceOriginal, entry.File);
            if (!File.Exists(btlFile))
            {
                throw new ContentError("import.btl", "Missing BTL for scenario '" + id + "': " + btlFile);
            }

            byte[] btlBytes = File.ReadAllBytes(btlFile);
            string json = new Ew4.Platform.Mod.Scenario.ScenarioBtlConverter().ToJson(id, btlBytes, new(entry.Name)).Json!;
            File.WriteAllText(Path.Combine(destination, "scenarios", id + ".json"), json);
            embedded++;
        }

        string packJsonPath = Path.Combine(destination, ModManifestPaths.FileName);
        if (!File.Exists(packJsonPath))
        {
            const string template = """
                {
                  "schema": 1,
                  "id": "base",
                  "name": "EW4 Rewrite Base",
                  "version": "0.0.0",
                  "requires": { "engine": ">=0.3.0" },
                  "formats": {
                    "scenario": 2, "rules": 1, "map": 1, "commander": 1, "unit": 1,
                    "country": 1, "text": 1, "script": 1, "asset": 1, "source": 1, "extension": 1
                  }
                }
                """;
            File.WriteAllText(packJsonPath, template);
        }

        return new BaseModImportResult(scenarios.Count, embedded, destination);
    }

    private static IReadOnlyList<ScenarioIndexEntry> ReadBattleList(string battleListPath)
    {
        if (!File.Exists(battleListPath))
        {
            return Array.Empty<ScenarioIndexEntry>();
        }

        XDocument document = XDocument.Load(battleListPath);
        if (document.Root == null)
        {
            return Array.Empty<ScenarioIndexEntry>();
        }

        return document.Root.Elements("battle")
            .Select(e => new ScenarioIndexEntry(
                e.Attribute("name")?.Value ?? "",
                e.Attribute("file")?.Value ?? "",
                int.TryParse(e.Attribute("map")?.Value, out int map) ? map : 0))
            .Where(e => !string.IsNullOrWhiteSpace(e.File))
            .ToList();
    }

    private static void CopyTree(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = file.Replace(source, destination, StringComparison.Ordinal);
            string? parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(file, target, overwrite: true);
        }
    }

    private sealed record ScenarioIndexEntry(string Name, string File, int MapId);
}

public sealed record BaseModImportOptions(
    string StaticDataDataDirectory,
    string DestinationModDirectory,
    string? StaticDataBtlDirectory = null);

public sealed record BaseModImportResult(int ScenarioCount, int ScenariosEmbedded, string ModDirectory);
