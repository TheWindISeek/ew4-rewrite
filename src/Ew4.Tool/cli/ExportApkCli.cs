using System.Text.Json;
using System.Text.Json.Serialization;
using Ew4.Tool.Lib.Export;

namespace Ew4.Tool.Cli;

/// <summary><c>export-apk</c> 命令入口。参数契约见 docs/task/plan/apk-export.md §8。</summary>
public static class ExportApkCli
{
    public static int Run(string[] args)
    {
        bool jsonOutput = false;
        try
        {
            (ApkExportRequest request, jsonOutput) = Parse(args);
            ApkExportResult result = ApkExporter.Export(request);
            if (jsonOutput)
            {
                Console.WriteLine(SerializeResult(result));
            }
            else
            {
                Console.WriteLine(
                    $"export-apk: out={result.OutPath ?? "(dry-run)"} exportId={result.ExportId} filesPatched={result.FilesPatched} compat={result.Compat.Count}");
            }

            return 0;
        }
        catch (ApkExportException ex)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, code = ex.Code, message = ex.Message }));
            }
            else
            {
                Console.Error.WriteLine(ex.Message);
            }

            return ex.ExitCode;
        }
    }

    private static (ApkExportRequest Request, bool Json) Parse(string[] args)
    {
        string? outPath = null;
        string? shell = null;
        string? mods = null;
        string? baseMod = null;
        string? repo = null;
        string? userDir = null;
        string? modId = null;
        string? workspace = null;
        string? ew4mod = null;
        long? exportId = null;
        bool dryRun = false;
        bool allowLossy = false;
        bool stamp = true;
        bool sign = true;
        string? keystore = null;
        string? ksPass = null;
        string? keyPass = null;
        bool json = false;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--out" && i + 1 < args.Length) outPath = args[++i];
            else if (arg == "--shell" && i + 1 < args.Length) shell = args[++i];
            else if (arg == "--mods" && i + 1 < args.Length) mods = args[++i];
            else if (arg == "--base" && i + 1 < args.Length) baseMod = args[++i];
            else if (arg == "--root" && i + 1 < args.Length) repo = args[++i];
            else if (arg == "--active-user" && i + 1 < args.Length) userDir = args[++i];
            else if (arg == "--mod" && i + 1 < args.Length) modId = args[++i];
            else if (arg == "--workspace" && i + 1 < args.Length) workspace = args[++i];
            else if (arg == "--ew4mod" && i + 1 < args.Length) ew4mod = args[++i];
            else if (arg == "--export-id" && i + 1 < args.Length && long.TryParse(args[++i], out long id)) exportId = id;
            else if (arg == "--dry-run") dryRun = true;
            else if (arg == "--allow-lossy") allowLossy = true;
            else if (arg == "--no-stamp") stamp = false;
            else if (arg == "--no-sign") sign = false;
            else if (arg == "--json") json = true;
            else if (arg == "--keystore" && i + 1 < args.Length) keystore = args[++i];
            else if (arg == "--ks-pass" && i + 1 < args.Length) ksPass = args[++i];
            else if (arg == "--key-pass" && i + 1 < args.Length) keyPass = args[++i];
            else if (arg == "--help" || arg == "-h")
            {
                PrintHelp();
                Environment.Exit(0);
            }
            else
            {
                throw new ApkExportException("export.args", "Unknown argument: " + arg, 2);
            }
        }

        if (string.IsNullOrWhiteSpace(outPath))
        {
            if (dryRun)
            {
                outPath = Path.Combine(Path.GetTempPath(), "ew4-export-dry-run.apk");
            }
            else
            {
                throw new ApkExportException("export.args", "export-apk requires --out <path.apk>", 2);
            }
        }

        string repositoryRoot = ApkExporter.FindRepositoryRoot(Directory.GetCurrentDirectory());
        if (!string.IsNullOrWhiteSpace(repo))
        {
            repositoryRoot = Path.GetFullPath(repo);
        }

        string modsDir = mods ?? Path.Combine(repositoryRoot, "mods");
        string baseDir = baseMod ?? Path.Combine(repositoryRoot, "mods", "base");
        if (string.IsNullOrWhiteSpace(userDir))
        {
            string webUser = Path.Combine(repositoryRoot, "install", "web", "user");
            if (Directory.Exists(webUser))
            {
                userDir = webUser;
            }
        }

        return (new ApkExportRequest(
            outPath,
            baseDir,
            modsDir,
            repositoryRoot,
            userDir,
            shell,
            modId,
            workspace,
            ew4mod,
            exportId,
            dryRun,
            allowLossy,
            stamp,
            sign,
            keystore,
            ksPass,
            keyPass), json);
    }

    private static string SerializeResult(ApkExportResult result)
    {
        var payload = new
        {
            ok = true,
            exportId = result.ExportId,
            outPath = result.OutPath,
            filesPatched = result.FilesPatched,
            compat = result.Compat.Select(i => new { path = i.Path, field = i.Field, reason = i.Reason, impact = i.Impact }).ToList(),
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static void PrintHelp()
    {
        Console.WriteLine("""
            export-apk --out <path.apk> [options]
              --shell <ew4-base.apk>     default: data/shell/ew4-base.apk under repo root
              --mods <dir>               default: <repo>/mods
              --base <mods/base>         diff baseline for patching
              --root <repo>              repository root (Ew4.sln)
              --mod <id>                 primary mod (merges parent)
              --active-user <user/>      read active.json (default: install/web/user if present)
              --workspace <dir>          workspace overlay directory
              --ew4mod <file>            overlay from .ew4mod / zip
              --export-id <unix-seconds> default: UTC now
              --dry-run                  plan only, no output file
              --allow-lossy              skip unmapped / incompatible paths
              --no-stamp                 omit assets/ew4-export.json
              --no-sign                  skip zipalign + apksigner (unsigned, not installable)
              --json                     machine-readable result on stdout (for Web → Tool)
            Default: debug sign (build/android/ew4-debug.keystore, same as scripts/ew4.sh apk). Override: EW4_APK_KEYSTORE / --keystore
            Requires apktool 2.9+ for Manifest patch (ew4-dev:local, EW4_APKTOOL, or apktool in PATH).
            """);
    }
}
