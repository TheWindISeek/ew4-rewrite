using Ew4.Tool.Lib.Import;

namespace Ew4.Tool.Cli;

internal static class ImportBaseCli
{
    internal static int Run(string[] args)
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
}
