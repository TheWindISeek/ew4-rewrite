using System;
using System.IO;
using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Tool;

internal static class ScenarioCli
{
    private static readonly IScenarioEditToolkit Toolkit = ScenarioEditToolkit.Default;

    internal static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            PrintHelp();
            return 2;
        }

        return args[1] switch
        {
            "btl-to-json" => BtlToJson(args),
            "json-to-btl" => JsonToBtl(args),
            "roundtrip" => Roundtrip(args),
            "pack-to-json" => PackToJson(args),
            "patch-army" => PatchArmy(args),
            _ => Unknown(args[1]),
        };
    }

    private static int BtlToJson(string[] args)
    {
        string? input = null;
        string? output = null;
        string? id = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length) input = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) id = args[++i];
        }

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("btl-to-json requires --id --input --output");
            return 2;
        }

        ScenarioDocument document = Toolkit.ReadBtlFile(id, input);
        Toolkit.WriteJsonFile(output, document);
        Console.WriteLine($"scenario btl-to-json: id={id} → {Path.GetFullPath(output)}");
        return 0;
    }

    private static int JsonToBtl(string[] args)
    {
        string? input = null;
        string? output = null;
        bool applyLayers = true;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length) input = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
            else if (args[i] == "--preserve-sections") applyLayers = false;
        }

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("json-to-btl requires --input --output");
            return 2;
        }

        ScenarioDocument document = Toolkit.ReadJsonFile(input);
        Toolkit.WriteBtlFile(
            output,
            document,
            applyLayers ? ScenarioExportMode.ApplyLayers : ScenarioExportMode.PreserveSections);
        Console.WriteLine($"scenario json-to-btl: {Path.GetFullPath(input)} → {Path.GetFullPath(output)}");
        return 0;
    }

    private static int Roundtrip(string[] args)
    {
        string? input = null;
        string? id = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length) input = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) id = args[++i];
        }

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("roundtrip requires --id --input");
            return 2;
        }

        byte[] raw = File.ReadAllBytes(Path.GetFullPath(input));
        ScenarioBtlRoundtrip.Result result = Toolkit.ValidateBtlRoundtrip(id, raw);
        Console.WriteLine(
            $"scenario roundtrip: id={id} legacy={result.OriginalClone} document={result.Document} json={result.DocumentJson} layers={result.Layers}");
        if (!result.Layers && result.LayerMismatchSection != null)
        {
            Console.Error.WriteLine("layer mismatch section: " + result.LayerMismatchSection);
        }

        return result.OriginalClone && result.Document && result.DocumentJson && result.Layers ? 0 : 1;
    }

    private static int PackToJson(string[] args)
    {
        string packs = "mods";
        string packId = "base";
        string? scenarioId = null;
        string? output = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length) packs = args[++i];
            else if (args[i] == "--pack" && i + 1 < args.Length) packId = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) scenarioId = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
        }

        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("pack-to-json requires --id --output [--packs packs] [--pack base]");
            return 2;
        }

        var repository = new FilePackRepository(Path.GetFullPath(packs));
        ScenarioDocument document = Toolkit.OpenFromPack(repository.Open(packId), scenarioId);
        Toolkit.WriteJsonFile(output, document);
        Console.WriteLine($"scenario pack-to-json: pack={packId} id={scenarioId} → {Path.GetFullPath(output)}");
        return 0;
    }

    private static int PatchArmy(string[] args)
    {
        string? jsonPath = null;
        string? output = null;
        int? index = null;
        short? cell = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--json" && i + 1 < args.Length) jsonPath = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
            else if (args[i] == "--index" && i + 1 < args.Length && int.TryParse(args[++i], out int parsedIndex)) index = parsedIndex;
            else if (args[i] == "--cell" && i + 1 < args.Length && short.TryParse(args[++i], out short parsedCell)) cell = parsedCell;
        }

        if (string.IsNullOrWhiteSpace(jsonPath) || index is null || cell is null)
        {
            Console.Error.WriteLine("patch-army requires --json --index --cell");
            return 2;
        }

        ScenarioDocument document = Toolkit.ReadJsonFile(jsonPath);
        ScenarioDocument patched = Toolkit.PatchArmyCell(document, index.Value, cell.Value);
        string outPath = string.IsNullOrWhiteSpace(output) ? jsonPath : output;
        Toolkit.WriteJsonFile(outPath, patched);
        Console.WriteLine(
            $"scenario patch-army: index={index.Value} cell={cell.Value} → {Path.GetFullPath(outPath)}");
        return 0;
    }

    private static int Unknown(string sub) { Console.Error.WriteLine("Unknown scenario subcommand: " + sub); PrintHelp(); return 2; }

    internal static void PrintHelp()
    {
        Console.WriteLine("""
          scenario btl-to-json --id <scenarioId> --input <file.btl> --output <file.ew4scenario.json>
          scenario json-to-btl --input <file.ew4scenario.json> --output <file.btl> [--preserve-sections]
          scenario roundtrip --id <scenarioId> --input <file.btl>
          scenario pack-to-json --id <scenarioId> --output <file.ew4scenario.json> [--packs packs] [--pack base]
          scenario patch-army --json <file.ew4scenario.json> --index <n> --cell <cell> [--output <file>]
          """);
    }
}
