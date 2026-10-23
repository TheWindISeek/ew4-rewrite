using System;
using System.IO;
using Ew4.Platform.Content;
using Ew4.Tool.Import;

namespace Ew4.Tool;

/// <summary>
/// CLI：<c>import-apk-overlay</c>。其它宿主需要相同能力时，可直接调用 <see cref="ApkOverlayPackImporter"/> / <see cref="PackScenarioConvert"/>。
/// </summary>
internal static class ApkImportCli
{
    internal static int Run(string[] args)
    {
        string? apk = null;
        string packId = "mod";
        string packName = "Mod";
        string basePack = "mods/base";
        string dest = "";
        bool skipEmbed = false;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--apk" && i + 1 < args.Length) apk = args[++i];
            else if (args[i] == "--id" && i + 1 < args.Length) packId = args[++i];
            else if (args[i] == "--name" && i + 1 < args.Length) packName = args[++i];
            else if (args[i] == "--base" && i + 1 < args.Length) basePack = args[++i];
            else if (args[i] == "--dest" && i + 1 < args.Length) dest = args[++i];
            else if (args[i] == "--skip-embed") skipEmbed = true;
        }

        if (string.IsNullOrWhiteSpace(apk))
        {
            Console.Error.WriteLine("import-apk-overlay requires --apk --id --name [--base mods/base] [--dest mods/<id>] [--skip-embed]");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(dest))
        {
            dest = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(basePack)) ?? "mods", packId);
        }

        ApkOverlayPackImporter.Result result = ApkOverlayPackImporter.Import(
            Path.GetFullPath(apk),
            packId,
            packName,
            Path.GetFullPath(basePack),
            Path.GetFullPath(dest));

        int embedded = 0;
        if (!skipEmbed)
        {
            PackScenarioConvert.Result converted = PackScenarioConvert.ToEmbeddedDocumentFormat(result.PackDirectory);
            embedded = converted.Embed.Embedded;
        }

        Console.WriteLine(
            $"import-apk-overlay: id={packId} files={result.FilesCopied} scenarios={result.ScenariosIndexed} embedded={embedded} profile={PackModProfile.FilePath} → {result.PackDirectory}");
        return 0;
    }
}
