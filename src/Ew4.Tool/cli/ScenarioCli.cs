using System;
using System.IO;
using System.Linq;
using Ew4.Platform.Editor.Campaign;
using Ew4.Platform.Content;

using Ew4.Tool.Lib.Import;

namespace Ew4.Tool.Cli;

internal static class ScenarioCli
{
    private static readonly Ew4.Platform.Mod.Scenario.ScenarioBtlConverter Converter = new();

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
            "mod-to-json" => ModToJson(args),
            "to-v3" => ToSchema3(args),
            "patch-army" => PatchArmy(args),
            _ => Unknown(args[1]),
        };
    }

    /// <summary>把一个 mod 的 scenarios/*.json（legacy btl 索引桩）原地转成 schema 3，并逐份验证往返字节一致。</summary>
    private static int ToSchema3(string[] args)
    {
        string? mod = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--mod" && i + 1 < args.Length) mod = args[++i];
        }

        if (string.IsNullOrWhiteSpace(mod) || !Directory.Exists(Path.Combine(mod, "scenarios")))
        {
            Console.Error.WriteLine("scenario to-v3 requires --mod <dir containing scenarios/>");
            return 2;
        }

        var result = ModScenarioConvert.ToSchema3(mod);
        Console.WriteLine($"scenario to-v3: {result.Converted} converted, {result.Skipped} skipped in {Path.GetFullPath(mod)}");
        return 0;
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

        File.WriteAllText(output, Converter.ToJson(id, File.ReadAllBytes(input), new()).Json!);
        Console.WriteLine($"scenario btl-to-json: id={id} → {Path.GetFullPath(output)}");
        return 0;
    }

    private static int JsonToBtl(string[] args)
    {
        string? input = null;
        string? output = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length) input = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
        }

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("json-to-btl requires --input --output");
            return 2;
        }

        using (var json = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(input)))
            File.WriteAllBytes(output, Converter.ToBtl(json.RootElement, new()).Bytes!);
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
        var result = Ew4.Platform.Editor.Campaign.ScenarioRoundtrip.CheckBinary(Converter, id, raw);
        Console.WriteLine($"scenario roundtrip: id={id} stable={result.Stable} source={result.SourceLength} exported={result.ExportedLength}");
        return result.Stable ? 0 : 1;
    }

    private static int ModToJson(string[] args)
    {
        string packs = "mods";
        string modId = "base";
        string? scenarioId = null;
        string? output = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length) packs = args[++i];
            else if (args[i] == "--pack" && i + 1 < args.Length) modId = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) scenarioId = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
        }

        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("pack-to-json requires --id --output [--packs packs] [--pack base]");
            return 2;
        }

        var repository = new FileModRepository(Path.GetFullPath(packs));
        File.WriteAllBytes(output, Ew4.Platform.Editor.Campaign.EditSession.Open(repository.Open(modId), scenarioId).ToUtf8());
        Console.WriteLine($"scenario pack-to-json: pack={modId} id={scenarioId} → {Path.GetFullPath(output)}");
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

        var doc = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(jsonPath))!;
        Ew4.Platform.Editor.Campaign.ScenarioJsonPatch.Apply(doc,
            new Ew4.Platform.Editor.Campaign.ScenarioEditArmyCell(index.Value, cell.Value),
            Ew4.Platform.Editor.Campaign.Capabilities.For(Ew4.Platform.Editor.Campaign.EditorMode.Free));
        string outPath = string.IsNullOrWhiteSpace(output) ? jsonPath : output;
        File.WriteAllText(outPath, doc.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine(
            $"scenario patch-army: index={index.Value} cell={cell.Value} → {Path.GetFullPath(outPath)}");
        return 0;
    }

    private static int Unknown(string sub) { Console.Error.WriteLine("Unknown scenario subcommand: " + sub); PrintHelp(); return 2; }

    internal static void PrintHelp()
    {
        Console.WriteLine("""
          scenario btl-to-json --id <scenarioId> --input <file.btl> --output <file.ew4scenario.json>
          scenario json-to-btl --input <file.ew4scenario.json> --output <file.btl>
          scenario roundtrip --id <scenarioId> --input <file.btl>
          scenario pack-to-json --id <scenarioId> --output <file.ew4scenario.json> [--packs packs] [--pack base]
          scenario patch-army --json <file.ew4scenario.json> --index <n> --cell <cell> [--output <file>]
          """);
    }
}
