using System;
using System.IO;
using System.Text.Json;
using Ew4.Platform.Content.Importers.Original;

namespace Ew4.Platform.Content.Importers;

/// <summary>
/// 将 BTL 字节导入为 Pack 内 <c>scenarios/{id}.json</c>（含摘要 + 源文件路径）。
/// </summary>
public static class BtlScenarioImporter
{
    public static void ImportToDirectory(string scenarioId, byte[] btlBytes, string scenariosDirectory, string legacyRelativePath)
    {
        BtlDocument summary = BtlReader.Read(btlBytes);
        Directory.CreateDirectory(scenariosDirectory);
        var document = new
        {
            schema = 1,
            id = scenarioId,
            mapId = summary.MapId,
            width = summary.Width,
            height = summary.Height,
            legacy = new
            {
                btl = legacyRelativePath,
                factionCount = summary.FactionCount,
                armyCount = summary.ArmyCount,
            },
        };
        string json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(scenariosDirectory, scenarioId + ".json"), json);
    }
}
