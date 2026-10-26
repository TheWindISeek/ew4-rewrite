using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Tool;
using Ew4.Tool.Import;
using Ew4.Interface.Common;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Service.Host;
using Ew4.Service;
if (args.Length == 0)
{
    PrintHelp();
    return 1;
}

return args[0] switch
{
    "import-base" => RunImportBase(args),
    "smoke-start" => RunSmokeStart(args),
    "validate-mod" => RunValidateMod(args),
    "import-apk-overlay" => ApkImportCli.Run(args),
    "scenario" => ScenarioCli.Run(args),
    "workspace" => WorkspaceCli.Run(args),
    "export-apk" => Ew4.Tool.Export.ExportApkCli.Run(args),
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
    BaseModImportResult result = importer.Import(new BaseModImportOptions(data, dest, btl));
    Console.WriteLine($"Imported {result.ScenarioCount} embedded scenario JSON → {result.ModDirectory}");
    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine("Unknown command: " + command);
    PrintHelp();
    return 2;
}

static int RunValidateMod(string[] args)
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
    var engine = new global::Ew4.Platform.Engine.Engine();
    var app = HostAppFactory.FromPaths(new HostPaths(Path.GetFullPath(packs), stateDir), engine);
    app.Activate("base");
    var game = new CliEngineGame(
        app.ActiveContent ?? throw new InvalidOperationException("No active content."),
        engine,
        new ScenarioJsonBuilder());
    CliGameView view = game.Start(new CliStartRequest(scenario));
    Console.WriteLine($"smoke-start ok: scenario={scenario} revision={view.Revision}");
    return 0;
}

static void PrintHelp()
{
    Console.WriteLine("""
        Ew4.Tool
          import-base --data <mods/import-seed> --dest <mods/base> [--btl <dir-with-btl>]
          smoke-start [--packs mods] [--scenario campaign1_01]
          validate-mod [--packs mods] [--pack base] [--level schema|reference|smoke]
          import-apk-overlay --apk <file.apk> --id <modId> --name <display> [--base mods/base] [--dest mods/<id>]
          scenario <subcommand>  (see: scenario btl-to-json --help via invalid subcommand)
          workspace <subcommand>  (list|create|delete|validate|export)
          export-apk              (placeholder; not implemented, see docs/task/plan/apk-export.md)
        """);
}
