using Ew4.Interface.Common;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Packages;

namespace Ew4.Tool.Cli;

internal static class ValidateModCli
{
    internal static int Run(string[] args)
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
}
