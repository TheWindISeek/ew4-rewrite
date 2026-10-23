using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Tool;
using Ew4.Tool.Import;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;
using Ew4.Platform.Session;

if (args.Length == 0)
{
    PrintHelp();
    return 1;
}

return args[0] switch
{
    "import-base" => RunImportBase(args),
    "smoke-start" => RunSmokeStart(args),
    "validate-pack" => RunValidatePack(args),
    "validate-btl-roundtrip" => RunValidateBtlRoundtrip(args),
    "validate-scenario-json-btl" => RunValidateScenarioJsonBtl(args),
    "pack-embed-scenarios" => RunPackEmbedScenarios(args),
    "pack-convert-legacy-scenarios" => RunPackConvertLegacyScenarios(args),
    "import-apk-overlay" => ApkImportCli.Run(args),
    "scenario" => ScenarioCli.Run(args),
    "workspace" => WorkspaceCli.Run(args),
    _ => Unknown(args[0]),
};

static int RunImportBase(string[] args)
{
    string? data = null;
    string? dest = null;
    string? btl = null;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--data" && i + 1 < args.Length)
        {
            data = args[++i];
        }
        else if (args[i] == "--dest" && i + 1 < args.Length)
        {
            dest = args[++i];
        }
        else if (args[i] == "--btl" && i + 1 < args.Length)
        {
            btl = args[++i];
        }
    }

    if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(dest))
    {
        Console.Error.WriteLine("import-base requires --data and --dest");
        return 2;
    }

    data = Path.GetFullPath(data);
    dest = Path.GetFullPath(dest);
    if (!string.IsNullOrWhiteSpace(btl))
    {
        btl = Path.GetFullPath(btl);
    }

    var importer = new BaseModImporter();
    BasePackImportResult result = importer.Import(new BasePackImportOptions(data, dest, btl));
    Console.WriteLine($"Imported {result.ScenarioCount} embedded scenario JSON → {result.PackDirectory}");
    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine("Unknown command: " + command);
    PrintHelp();
    return 2;
}

static int RunValidatePack(string[] args)
{
    string packs = "mods";
    string modId = "base";
    ValidationLevel level = ValidationLevel.SmokeStart;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--packs" && i + 1 < args.Length)
        {
            packs = args[++i];
        }
        else if (args[i] == "--pack" && i + 1 < args.Length)
        {
            modId = args[++i];
        }
        else if (args[i] == "--level" && i + 1 < args.Length)
        {
            level = args[++i] switch
            {
                "schema" => ValidationLevel.Schema,
                "reference" => ValidationLevel.Reference,
                "constraint" => ValidationLevel.Constraint,
                "smoke" or "smokestart" => ValidationLevel.SmokeStart,
                _ => ValidationLevel.SmokeStart,
            };
        }
    }

    var repository = new FileModRepository(Path.GetFullPath(packs));
    var engine = new global::Ew4.Platform.Engine.Engine();
    var builder = new ScenarioJsonBuilder();
    var resolver = new ContentResolver(ContentKindRegistry.CreateDefault(), engine);
    var validator = new ContentValidator(ContentKindRegistry.CreateDefault(), engine, builder);
    IModSource pack = repository.Open(modId);
    IModSource? parent = null;
    if (!string.IsNullOrWhiteSpace(pack.Manifest.Parent))
    {
        parent = repository.Open(pack.Manifest.Parent);
    }

    ResolvedContent resolved = resolver.Resolve(pack, parent);
    IReadOnlyList<Diagnostic> diagnostics = validator.Run(resolved, level);
    int errors = 0;
    int warnings = 0;
    foreach (Diagnostic diagnostic in diagnostics)
    {
        if (diagnostic.Severity == Severity.Error)
        {
            errors++;
            Console.Error.WriteLine($"ERROR {diagnostic.Code} [{diagnostic.Path}] {diagnostic.Message}");
        }
        else if (diagnostic.Severity == Severity.Warning)
        {
            warnings++;
            Console.WriteLine($"WARN {diagnostic.Code} [{diagnostic.Path}] {diagnostic.Message}");
        }
    }

    Console.WriteLine($"validate-pack: pack={modId} level={level} errors={errors} warnings={warnings} checks={diagnostics.Count}");
    return errors == 0 ? 0 : 1;
}

static int RunValidateScenarioJsonBtl(string[] args)
{
    string packDir = "mods/base";
    string? btlDir = null;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--pack" && i + 1 < args.Length)
        {
            packDir = args[++i];
        }
        else if (args[i] == "--btl" && i + 1 < args.Length)
        {
            btlDir = args[++i];
        }
    }

    ScenarioJsonBtlValidator.Result result = ScenarioJsonBtlValidator.ValidatePackDirectory(
        Path.GetFullPath(packDir),
        string.IsNullOrWhiteSpace(btlDir) ? null : Path.GetFullPath(btlDir));
    foreach (string failure in result.Failures)
    {
        Console.Error.WriteLine("ERROR " + failure);
    }

    Console.WriteLine(
        $"validate-scenario-json-btl: scenarios={result.ScenarioCount} compared={result.Compared} stubs={result.SkippedStub} failures={result.Failures.Count}");
    return result.Failures.Count == 0 && result.Compared > 0 ? 0 : 1;
}

static int RunPackConvertLegacyScenarios(string[] args)
{
    string packDir = "mods/base";
    bool removeBtl = false;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--pack" && i + 1 < args.Length)
        {
            packDir = args[++i];
        }
        else if (args[i] == "--remove-btl")
        {
            removeBtl = true;
        }
    }

    ModScenarioConvert.Result result = ModScenarioConvert.ToEmbeddedDocumentFormat(Path.GetFullPath(packDir), removeBtl);
    ModScenarioEmbedder.Result embed = result.Embed;
    Console.WriteLine(
        $"pack-convert-legacy-scenarios: embedded={embed.Embedded} already={embed.AlreadyEmbedded} skipped_missing_btl={embed.SkippedMissingBtl} manifest={ModManifestPaths.FileName}");
    return 0;
}

static int RunPackEmbedScenarios(string[] args)
{
    string packDir = "mods/base";
    bool removeBtl = false;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--pack" && i + 1 < args.Length)
        {
            packDir = args[++i];
        }
        else if (args[i] == "--remove-btl")
        {
            removeBtl = true;
        }
    }

    ModScenarioEmbedder.Result result = ModScenarioEmbedder.Embed(Path.GetFullPath(packDir), removeBtl);
    Console.WriteLine(
        $"pack-embed-scenarios: embedded={result.Embedded} already={result.AlreadyEmbedded} skipped_missing_btl={result.SkippedMissingBtl} removed_btl={result.RemovedBtlFiles}");
    return 0;
}

static int RunValidateBtlRoundtrip(string[] args)
{
    string packDir = "mods/base";
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--pack" && i + 1 < args.Length)
        {
            packDir = args[++i];
        }
    }

    OriginalSourceRoundtrip.Result result = OriginalSourceRoundtrip.ValidatePackDirectory(Path.GetFullPath(packDir));
    foreach (string failure in result.Failures)
    {
        Console.Error.WriteLine("ERROR " + failure);
    }

    if (result.BtlFiles == 0 && result.TerrainFiles == 0)
    {
        Console.WriteLine("validate-btl-roundtrip: no source/original/*.btl or assets/original/*.bin (run import-base-pack with BTL corpus?)");
        return result.Failures.Count == 0 ? 0 : 1;
    }

    Console.WriteLine(
        $"validate-btl-roundtrip: btl={result.BtlFiles} terrain={result.TerrainFiles} document={result.DocumentRoundtrips} documentJson={result.DocumentJsonRoundtrips} layers={result.LayerReencodeRoundtrips} failures={result.Failures.Count}");
    return result.Failures.Count == 0 ? 0 : 1;
}

static int RunSmokeStart(string[] args)
{
    string packs = "mods";
    string scenario = "campaign1_01";
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--packs" && i + 1 < args.Length)
        {
            packs = args[++i];
        }
        else if (args[i] == "--scenario" && i + 1 < args.Length)
        {
            scenario = args[++i];
        }
    }

    string stateDir = Path.Combine(Path.GetTempPath(), "ew4-cli-smoke-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(stateDir);
    var app = new HostApp(new HostPaths(Path.GetFullPath(packs), stateDir), new global::Ew4.Platform.Engine.Engine());
    app.Activate("base");
    SessionView view = app.Session!.Start(new StartRequest(scenario));
    Console.WriteLine($"smoke-start ok: scenario={scenario} revision={view.Revision}");
    return 0;
}

static void PrintHelp()
{
    Console.WriteLine("""
        Ew4.Tool
          import-base --data <mods/import-seed> --dest <mods/base> [--btl <dir-with-btl>]
          smoke-start [--packs mods] [--scenario campaign1_01]
          validate-pack [--packs mods] [--pack base] [--level schema|reference|smoke]
          validate-btl-roundtrip [--pack mods/base]
          validate-scenario-json-btl [--pack mods/base] [--btl <reference-btl-dir>]
          pack-embed-scenarios [--pack mods/base] [--remove-btl]
          pack-convert-legacy-scenarios [--pack mods/base]  (BTL→嵌入 JSON，更新 mod.json scenarios)
          import-apk-overlay --apk <file.apk> --id <modId> --name <display> [--base mods/base] [--dest mods/<id>]
          scenario <subcommand>  (see: scenario btl-to-json --help via invalid subcommand)
          workspace <subcommand>  (list|create|delete|validate|export)
        """);
}
