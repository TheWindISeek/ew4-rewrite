using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Platform.Content.Original;
using Ew4.Platform.Content.ScenarioEdit;
using Ew4.Platform.Engine;

namespace Ew4.Tool.Import;

/// <summary>
/// 校验 Pack 内封存的 BTL / 地形 bin 经 <see cref="OriginalBattle"/> / <see cref="OriginalTerrainMap"/> 读回后字节一致。
/// </summary>
public static class OriginalSourceRoundtrip
{
    public sealed record Result(
        int BtlFiles,
        int TerrainFiles,
        int DocumentRoundtrips,
        int DocumentJsonRoundtrips,
        int LayerReencodeRoundtrips,
        IReadOnlyList<string> Failures);

    public static Result ValidatePackDirectory(string packDirectory)
    {
        string root = Path.GetFullPath(packDirectory);
        var failures = new List<string>();
        int btl = 0;
        int terrain = 0;
        int documentRoundtrips = 0;
        int documentJsonRoundtrips = 0;
        int layerReencodeRoundtrips = 0;
        string btlDir = Path.Combine(root, "source", "original");
        if (Directory.Exists(btlDir))
        {
            foreach (string file in Directory.EnumerateFiles(btlDir, "*.btl", SearchOption.TopDirectoryOnly))
            {
                byte[] raw = File.ReadAllBytes(file);
                string scenarioId = Path.GetFileNameWithoutExtension(file);
                try
                {
                    ScenarioBtlRoundtrip.Result check = ScenarioBtlRoundtrip.Validate(scenarioId, raw);
                    if (!check.OriginalClone)
                    {
                        failures.Add("btl bytes: " + Path.GetFileName(file));
                    }
                    else
                    {
                        btl++;
                    }

                    if (!check.Document)
                    {
                        failures.Add("scenario document: " + Path.GetFileName(file));
                    }
                    else
                    {
                        documentRoundtrips++;
                    }

                    if (!check.DocumentJson)
                    {
                        failures.Add("scenario document json: " + Path.GetFileName(file));
                    }
                    else
                    {
                        documentJsonRoundtrips++;
                    }

                    if (!check.Layers)
                    {
                        failures.Add("scenario layers: " + Path.GetFileName(file)
                            + (check.LayerMismatchSection == null ? "" : " (" + check.LayerMismatchSection + ")"));
                    }
                    else
                    {
                        layerReencodeRoundtrips++;
                    }
                }
                catch (RuleViolation error)
                {
                    failures.Add("btl " + Path.GetFileName(file) + ": " + error.Code);
                }
            }
        }

        string terrainDir = Path.Combine(root, "assets", "original");
        if (Directory.Exists(terrainDir))
        {
            foreach (string file in Directory.EnumerateFiles(terrainDir, "*.bin", SearchOption.TopDirectoryOnly))
            {
                byte[] raw = File.ReadAllBytes(file);
                try
                {
                    var map = OriginalTerrainMap.Read(raw);
                    if (!map.ToBytes().AsSpan().SequenceEqual(raw))
                    {
                        failures.Add("terrain bytes: " + Path.GetFileName(file));
                    }
                    else
                    {
                        terrain++;
                    }
                }
                catch (RuleViolation error)
                {
                    failures.Add("terrain " + Path.GetFileName(file) + ": " + error.Code);
                }
            }
        }

        return new(btl, terrain, documentRoundtrips, documentJsonRoundtrips, layerReencodeRoundtrips, failures);
    }
}
