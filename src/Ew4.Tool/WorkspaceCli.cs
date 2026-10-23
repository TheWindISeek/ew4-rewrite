using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;

namespace Ew4.Tool;

internal static class WorkspaceCli
{
    internal static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            PrintHelp();
            return 2;
        }

        return args[1] switch
        {
            "list" => List(args),
            "create" => Create(args),
            "delete" => Delete(args),
            "validate" => Validate(args),
            "export" => Export(args),
            _ => Unknown(args[1]),
        };
    }

    private static IWorkspaceService CreateService(string packs, string workspaces)
    {
        var repository = new FilePackRepository(Path.GetFullPath(packs));
        var registry = ContentKindRegistry.CreateDefault();
        var engine = new global::Ew4.Platform.Engine.Engine();
        var builder = new ScenarioJsonBuilder();
        var resolver = new ContentResolver(registry, engine);
        var validator = new ContentValidator(registry, engine, builder);
        return new WorkspaceService(Path.GetFullPath(workspaces), repository, registry, resolver, validator);
    }

    private static int List(string[] args)
    {
        string packs = "mods";
        string workspaces = "build/workspaces";
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length) packs = args[++i];
            else if (args[i] == "--workspaces" && i + 1 < args.Length) workspaces = args[++i];
        }

        IWorkspaceService service = CreateService(packs, workspaces);
        foreach (WorkspaceInfo info in service.List())
        {
            Console.WriteLine($"{info.Id}\t{info.Name}\tparent={info.ParentPackId}");
        }

        return 0;
    }

    private static int Create(string[] args)
    {
        string packs = "mods";
        string workspaces = "build/workspaces";
        string? id = null;
        string? name = null;
        string fromPack = PackId.Base;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length) packs = args[++i];
            else if (args[i] == "--workspaces" && i + 1 < args.Length) workspaces = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) id = args[++i];
            else if (args[i] == "--name" && i + 1 < args.Length) name = args[++i];
            else if (args[i] == "--from" && i + 1 < args.Length) fromPack = args[++i];
        }

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("create requires --id and --name");
            return 2;
        }

        WorkspaceInfo info = CreateService(packs, workspaces).Create(fromPack, id, name);
        Console.WriteLine($"workspace create: id={info.Id} parent={info.ParentPackId}");
        return 0;
    }

    private static int Delete(string[] args)
    {
        string packs = "mods";
        string workspaces = "build/workspaces";
        string? id = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length) packs = args[++i];
            else if (args[i] == "--workspaces" && i + 1 < args.Length) workspaces = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) id = args[++i];
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("delete requires --id");
            return 2;
        }

        CreateService(packs, workspaces).Delete(id);
        Console.WriteLine("workspace delete: id=" + id);
        return 0;
    }

    private static int Validate(string[] args)
    {
        string packs = "mods";
        string workspaces = "build/workspaces";
        string? id = null;
        var level = ValidationLevel.Schema;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length) packs = args[++i];
            else if (args[i] == "--workspaces" && i + 1 < args.Length) workspaces = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) id = args[++i];
            else if (args[i] == "--level" && i + 1 < args.Length)
            {
                level = Enum.Parse<ValidationLevel>(args[++i], ignoreCase: true);
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("validate requires --id");
            return 2;
        }

        IReadOnlyList<Diagnostic> diagnostics = CreateService(packs, workspaces).Validate(id, level);
        foreach (Diagnostic diagnostic in diagnostics)
        {
            Console.WriteLine($"{diagnostic.Severity}\t{diagnostic.Code}\t{diagnostic.Path}\t{diagnostic.Message}");
        }

        return diagnostics.Any(d => d.Severity == Severity.Error) ? 1 : 0;
    }

    private static int Export(string[] args)
    {
        string packs = "mods";
        string workspaces = "build/workspaces";
        string? id = null;
        var level = ValidationLevel.SmokeStart;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--packs" && i + 1 < args.Length) packs = args[++i];
            else if (args[i] == "--workspaces" && i + 1 < args.Length) workspaces = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) id = args[++i];
            else if (args[i] == "--level" && i + 1 < args.Length)
            {
                level = Enum.Parse<ValidationLevel>(args[++i], ignoreCase: true);
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("export requires --id");
            return 2;
        }

        ExportResult result = CreateService(packs, workspaces).Export(id, new ExportOptions(level));
        Console.WriteLine($"workspace export: success={result.Success} archive={result.ArchivePath}");
        return result.Success ? 0 : 1;
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine("Unknown workspace subcommand: " + sub);
        PrintHelp();
        return 2;
    }

    internal static void PrintHelp()
    {
        Console.WriteLine("""
          workspace list [--packs packs] [--workspaces workspaces]
          workspace create --id <id> --name <name> [--from base] [--packs packs] [--workspaces workspaces]
          workspace delete --id <id> [--packs packs] [--workspaces workspaces]
          workspace validate --id <id> [--level Schema|SmokeStart] [--packs packs] [--workspaces workspaces]
          workspace export --id <id> [--level SmokeStart] [--packs packs] [--workspaces workspaces]
          """);
    }
}
