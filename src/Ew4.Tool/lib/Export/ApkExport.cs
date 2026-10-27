using System.Collections.Frozen;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Mod.Merge;
using Ew4.Platform.Mod.Scenario;
using Ew4.Platform.Persistence.Packages;
using Ew4.Platform.Persistence.User;

namespace Ew4.Tool.Lib.Export;

public sealed record ApkExportRequest(
    string OutPath,
    string BaseModDirectory,
    string ModsDirectory,
    string? RepositoryRoot = null,
    string? UserStateDirectory = null,
    string? ShellApkPath = null,
    string? ModId = null,
    string? WorkspacePath = null,
    string? Ew4ModPath = null,
    long? ExportId = null,
    bool DryRun = false,
    bool AllowLossy = false,
    bool WriteStamp = true,
    bool SignApk = true,
    string? KeystorePath = null,
    string? KeystoreStorePass = null,
    string? KeystoreKeyPass = null);

public sealed record ApkCompatIssue(string Path, string Field, string Reason, string Impact);

public sealed record ApkExportResult(
    string? OutPath,
    long ExportId,
    int FilesPatched,
    IReadOnlyList<ApkCompatIssue> Compat);

public sealed class ApkExportException : Exception
{
    public ApkExportException(string code, string message, int exitCode = 1)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }

    public string Code { get; }

    public int ExitCode { get; }

    public static ApkExportException Compat(IReadOnlyList<ApkCompatIssue> issues) =>
        new(
            "export.compat",
            "APK export blocked by compatibility issues (" + issues.Count + "). Use --allow-lossy to continue.",
            3);
}

/// <summary>Mod 合并 → APK 资产补丁 → apktool 改 Manifest → 签名。实现归属 <c>Ew4.Tool/lib/Export</c>；Web 经子进程调用 CLI。</summary>
public static class ApkExporter
{
    private const string DefaultShellRelative = "data/shell/ew4-base.apk";
    private const string ExportStampApkPath = "assets/ew4-export.json";
    private static readonly ScenarioBtlConverter ScenarioConverter = new();

    public static IReadOnlyList<ApkCompatIssue> Check(ApkExportRequest request) =>
        CollectCompatIssues(CreateContext(request));

    public static ApkExportResult Export(ApkExportRequest request)
    {
        ExportContext ctx = CreateContext(request);
        IReadOnlyList<ApkCompatIssue> compat = CollectCompatIssues(ctx);
        if (compat.Count > 0 && !request.AllowLossy)
        {
            throw ApkExportException.Compat(compat);
        }

        HashSet<string> dropped = request.AllowLossy ? compat.Select(i => i.Path).ToHashSet(StringComparer.Ordinal) : [];
        List<(string ApkPath, byte[] Content)> patches = BuildPatches(ctx, dropped);
        if (request.DryRun)
        {
            return new ApkExportResult(null, ctx.ExportId, patches.Count, compat);
        }

        string outPath = Path.GetFullPath(request.OutPath);
        WriteApk(ctx, outPath, patches);
        return new ApkExportResult(outPath, ctx.ExportId, patches.Count, compat);
    }

    private sealed class ExportContext
    {
        public required ApkExportRequest Request { get; init; }
        public required string ShellApkPath { get; init; }
        public required IModSource Baseline { get; init; }
        public required IModSource Merged { get; init; }
        public required string ModId { get; init; }
        public required string DisplayName { get; init; }
        public required FrozenDictionary<string, string> ShellIndex { get; init; }
        public required long ExportId { get; init; }
    }

    private static ExportContext CreateContext(ApkExportRequest request)
    {
        string repo = FindRepositoryRoot(Directory.GetCurrentDirectory());
        if (!string.IsNullOrWhiteSpace(request.RepositoryRoot))
        {
            repo = Path.GetFullPath(request.RepositoryRoot);
        }

        string baseDir = Path.GetFullPath(request.BaseModDirectory);
        string modsDir = Path.GetFullPath(request.ModsDirectory);
        string shell = ResolveShellApk(repo, request.ShellApkPath);
        if (!File.Exists(shell))
        {
            throw new ApkExportException("export.shell", "Shell APK not found: " + shell);
        }

        var repository = new FileModRepository(modsDir);
        IModSource merged;
        string modId;
        string displayName;

        if (!string.IsNullOrWhiteSpace(request.WorkspacePath))
        {
            string workspace = Path.GetFullPath(request.WorkspacePath);
            var primary = new DirectoryModSource(workspace);
            merged = ModMerge.Apply(primary, ModMerge.OpenParent(primary, repository));
            modId = SanitizePackageSegment(primary.Manifest.Id);
            displayName = primary.Manifest.Name;
        }
        else if (!string.IsNullOrWhiteSpace(request.Ew4ModPath))
        {
            var primary = new ZipModSource(Path.GetFullPath(request.Ew4ModPath));
            merged = ModMerge.Apply(primary, ModMerge.OpenParent(primary, repository));
            modId = SanitizePackageSegment(primary.Manifest.Id);
            displayName = primary.Manifest.Name;
        }
        else
        {
            string? activeId = request.ModId;
            if (string.IsNullOrWhiteSpace(activeId) && !string.IsNullOrWhiteSpace(request.UserStateDirectory))
            {
                activeId = new ActiveModStore(Path.GetFullPath(request.UserStateDirectory)).Read();
            }

            if (string.IsNullOrWhiteSpace(activeId))
            {
                throw new ApkExportException("export.mod", "Specify mod id, user active.json, workspace, or ew4mod.", 2);
            }

            merged = ModMerge.OpenMerged(repository, activeId);
            modId = SanitizePackageSegment(activeId);
            displayName = merged.Manifest.Name;
        }

        IModSource baseline = new DirectoryModSource(baseDir);
        FrozenDictionary<string, string> index = ApkAssetPathMapping.BuildShellIndex(baseline, ListAssetPaths(shell));
        long exportId = request.ExportId ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new ExportContext
        {
            Request = request,
            ShellApkPath = shell,
            Baseline = baseline,
            Merged = merged,
            ModId = modId,
            DisplayName = displayName,
            ShellIndex = index,
            ExportId = exportId,
        };
    }

    private static List<(string ApkPath, byte[] Content)> BuildPatches(ExportContext ctx, IReadOnlySet<string> dropped)
    {
        var patches = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        IModSource merged = ctx.Merged;
        IModSource baseline = ctx.Baseline;

        foreach (string logical in merged.List("").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (ShouldSkipLogical(logical) || dropped.Contains(logical) || logical.StartsWith("scenarios/", StringComparison.Ordinal))
            {
                continue;
            }

            byte[] mergedBytes = merged.Read(logical);
            if (baseline.Exists(logical) && baseline.Read(logical).AsSpan().SequenceEqual(mergedBytes))
            {
                continue;
            }

            string? apkPath = ApkAssetPathMapping.ResolveApkPath(logical, baseline, ctx.ShellIndex);
            if (apkPath == null)
            {
                continue;
            }

            if (logical.EndsWith("def_commander.xml", StringComparison.OrdinalIgnoreCase))
            {
                mergedBytes = LegacyTableXmlRepair.NormalizeCommanderTable(mergedBytes);
            }

            patches[apkPath] = mergedBytes;
        }

        foreach (string logical in merged.List("scenarios/"))
        {
            if (!logical.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || logical.EndsWith(".ew4scenario.json", StringComparison.OrdinalIgnoreCase)
                || dropped.Contains(logical))
            {
                continue;
            }

            byte[] jsonBytes = merged.Read(logical);
            if (baseline.Exists(logical) && baseline.Read(logical).AsSpan().SequenceEqual(jsonBytes))
            {
                continue;
            }

            using var document = JsonDocument.Parse(jsonBytes);
            if (!document.RootElement.TryGetProperty("schema", out var schema)
                || schema.ValueKind != JsonValueKind.Number
                || schema.GetInt32() != ScenarioBtlConverter.SchemaVersion)
            {
                continue;
            }

            byte[] btl = ScenarioConverter.ToBtl(document.RootElement, new()).Bytes
                ?? throw new ApkExportException("export.scenario", "Failed to convert scenario to btl: " + logical);
            string id = document.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (id.Length == 0)
            {
                continue;
            }

            string btlLogical = "source/original/" + id + ".btl";
            string? apkPath = ApkAssetPathMapping.ResolveApkPath(btlLogical, baseline, ctx.ShellIndex) ?? id + ".btl";
            patches[apkPath] = btl;
        }

        if (ctx.Request.WriteStamp)
        {
            patches[ExportStampApkPath] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new { exportId = ctx.ExportId, modId = ctx.ModId, name = ctx.DisplayName, exportedAt = DateTimeOffset.UtcNow },
                new JsonSerializerOptions { WriteIndented = true }));
        }

        return patches.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private static IReadOnlyList<ApkCompatIssue> CollectCompatIssues(ExportContext ctx)
    {
        var issues = new List<ApkCompatIssue>();
        foreach (string logical in ctx.Merged.List(""))
        {
            if (logical.StartsWith("rewrite/", StringComparison.Ordinal))
            {
                issues.Add(new ApkCompatIssue(logical, "path", "Rewrite extension assets are not mapped to legacy APK layout.", "Omitted unless allow-lossy."));
            }

            if (logical.StartsWith("scenarios/", StringComparison.Ordinal) && logical.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    byte[] bytes = ctx.Merged.Read(logical);
                    using var doc = JsonDocument.Parse(bytes);
                    if (!doc.RootElement.TryGetProperty("schema", out var schema)
                        || schema.ValueKind != JsonValueKind.Number
                        || schema.GetInt32() != ScenarioBtlConverter.SchemaVersion)
                    {
                        issues.Add(new ApkCompatIssue(logical, "schema", "Scenario is not schema 3.", "Omitted unless allow-lossy."));
                    }
                }
                catch (JsonException ex)
                {
                    issues.Add(new ApkCompatIssue(logical, "json", ex.Message, "Scenario omitted."));
                }
            }

            if (!logical.StartsWith("assets/", StringComparison.Ordinal)
                && !logical.StartsWith("source/", StringComparison.Ordinal)
                && !logical.StartsWith("scenarios/", StringComparison.Ordinal)
                && !string.Equals(logical, ModManifestPaths.FileName, StringComparison.Ordinal)
                && ApkAssetPathMapping.ResolveApkPath(logical, ctx.Baseline, ctx.ShellIndex) == null
                && !logical.StartsWith("client/", StringComparison.Ordinal))
            {
                issues.Add(new ApkCompatIssue(logical, "path", "No APK asset path mapping.", "Omitted unless allow-lossy."));
            }
        }

        return issues;
    }

    private static void WriteApk(ExportContext ctx, string outputPath, List<(string ApkPath, byte[] Content)> patches)
    {
        string? parent = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        string workRoot = Path.Combine(Path.GetTempPath(), "ew4-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workRoot);
        try
        {
            if (IsApktoolAvailable())
            {
                string decoded = Path.Combine(workRoot, "decoded");
                RunApktool(["d", "-f", "-o", decoded, ctx.ShellApkPath]);
                ApplyPatches(Path.Combine(decoded, "assets"), patches);
                string manifestPath = Path.Combine(decoded, "AndroidManifest.xml");
                PatchManifest(manifestPath, ReadPackageName(manifestPath), ctx.ModId, ctx.ExportId, ctx.DisplayName);
                string unsigned = Path.Combine(workRoot, "unsigned.apk");
                RunApktool(["b", "-o", unsigned, decoded]);
                FinishSign(unsigned, outputPath, ctx.Request);
                return;
            }

            if (!string.Equals(Environment.GetEnvironmentVariable("EW4_EXPORT_ZIP_ONLY"), "1", StringComparison.Ordinal))
            {
                throw new ApkExportException(
                    "export.apktool",
                    "apktool 2.9+ is required for Manifest patch (ew4-dev:local or EW4_APKTOOL).");
            }

            File.Copy(ctx.ShellApkPath, outputPath, overwrite: true);
            ZipPatch(outputPath, patches);
            if (ctx.Request.SignApk)
            {
                string signed = outputPath + ".signed";
                AlignAndSign(outputPath, signed, ctx.Request);
                File.Move(signed, outputPath, overwrite: true);
            }
        }
        finally
        {
            try { Directory.Delete(workRoot, recursive: true); } catch (IOException) { }
        }
    }

    private static void ApplyPatches(string assetsRoot, List<(string ApkPath, byte[] Content)> patches)
    {
        foreach ((string apkRelative, byte[] content) in patches)
        {
            string relative = apkRelative;
            if (relative.StartsWith("assets/", StringComparison.Ordinal))
            {
                relative = relative["assets/".Length..];
            }

            string target = Path.Combine(assetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            string? dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(target, content);
        }
    }

    private static void ZipPatch(string apkPath, List<(string ApkPath, byte[] Content)> patches)
    {
        using var zip = ZipFile.Open(apkPath, ZipArchiveMode.Update);
        foreach ((string apkRelative, byte[] content) in patches)
        {
            string entryName = apkRelative.Replace('\\', '/');
            zip.GetEntry(entryName)?.Delete();
            using Stream stream = zip.CreateEntry(entryName, CompressionLevel.Optimal).Open();
            stream.Write(content, 0, content.Length);
        }
    }

    private static void PatchManifest(string manifestPath, string basePackage, string modId, long exportId, string displayName)
    {
        XDocument document = XDocument.Load(manifestPath);
        XElement manifest = document.Root
            ?? throw new ApkExportException("export.manifest", "AndroidManifest.xml has no root.");
        XNamespace android = "http://schemas.android.com/apk/res/android";
        manifest.SetAttributeValue("package", basePackage + ".mod." + modId + ".e" + exportId);
        manifest.SetAttributeValue(android + "versionCode", exportId.ToString());
        manifest.SetAttributeValue(android + "versionName", displayName + "-" + exportId);
        document.Save(manifestPath);
    }

    private static string ReadPackageName(string manifestPath)
    {
        string? package = XDocument.Load(manifestPath).Root?.Attribute("package")?.Value;
        return string.IsNullOrWhiteSpace(package)
            ? throw new ApkExportException("export.manifest", "AndroidManifest.xml has no package.")
            : package;
    }

    private static void FinishSign(string unsignedApk, string outputPath, ApkExportRequest request)
    {
        if (request.SignApk)
        {
            AlignAndSign(unsignedApk, outputPath, request);
            return;
        }

        File.Copy(unsignedApk, outputPath, overwrite: true);
    }

    private const string DebugKeystoreAlias = "ew4";

    private static string ResolveKeystorePath(ApkExportRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.KeystorePath))
        {
            string path = Path.GetFullPath(request.KeystorePath);
            if (!File.Exists(path))
            {
                throw new ApkExportException("export.sign", "Keystore not found: " + path);
            }

            return path;
        }

        string? env = Environment.GetEnvironmentVariable("EW4_APK_KEYSTORE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return env;
        }

        string repo = !string.IsNullOrWhiteSpace(request.RepositoryRoot)
            ? Path.GetFullPath(request.RepositoryRoot)
            : FindRepositoryRoot(Directory.GetCurrentDirectory());
        string debug = Path.Combine(repo, "build", "android", "ew4-debug.keystore");
        EnsureDebugKeystore(debug);
        return debug;
    }

    private static void EnsureDebugKeystore(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        RunProcess(
            "keytool",
            [
                "-genkeypair",
                "-keystore", path,
                "-alias", DebugKeystoreAlias,
                "-keyalg", "RSA",
                "-keysize", "2048",
                "-validity", "10000",
                "-storepass", "android",
                "-keypass", "android",
                "-dname", "CN=EW4 Debug",
            ]);
    }

    private static void AlignAndSign(string inputApk, string outputApk, ApkExportRequest request)
    {
        string aligned = inputApk + ".aligned";
        try
        {
            RunProcess("zipalign", ["-f", "-p", "4", inputApk, aligned]);
            string ks = ResolveKeystorePath(request);
            string pass = request.KeystoreStorePass ?? Environment.GetEnvironmentVariable("EW4_APK_KS_PASS") ?? "android";
            string kpass = request.KeystoreKeyPass ?? Environment.GetEnvironmentVariable("EW4_APK_KEY_PASS") ?? pass;
            RunProcess(
                "apksigner",
                [
                    "sign",
                    "--ks", ks,
                    "--ks-key-alias", DebugKeystoreAlias,
                    "--ks-pass", "pass:" + pass,
                    "--key-pass", "pass:" + kpass,
                    "--out", outputApk,
                    aligned,
                ]);
            RunProcess("apksigner", ["verify", outputApk]);
        }
        finally
        {
            if (File.Exists(aligned))
            {
                File.Delete(aligned);
            }
        }
    }

    private static bool IsApktoolAvailable() => ResolveApktoolCommand() != null;

    private static void RunApktool(string[] args)
    {
        string? command = ResolveApktoolCommand()
            ?? throw new ApkExportException("export.apktool", "apktool is not available.");
        RunProcess(command, args, jarAsJava: command.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));
    }

    private static void RunProcess(string command, IReadOnlyList<string> args, bool jarAsJava = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = jarAsJava ? "java" : command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (jarAsJava)
        {
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(command);
        }

        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        if (!jarAsJava && !File.Exists(command))
        {
            string? which = Which(command);
            if (which != null)
            {
                psi.FileName = which;
            }
        }

        using Process process = Process.Start(psi) ?? throw new ApkExportException("export.sign", "Failed to start " + command);
        string stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new ApkExportException("export.apktool", command + " failed: " + stderr.Trim());
        }
    }

    private static string? ResolveApktoolCommand()
    {
        string? env = Environment.GetEnvironmentVariable("EW4_APKTOOL");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        if (File.Exists("/usr/local/bin/apktool"))
        {
            return "/usr/local/bin/apktool";
        }

        return Which("apktool") ?? Which("apktool.jar");
    }

    private static string? Which(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string segment in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(segment, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ListAssetPaths(string apkPath)
    {
        var list = new List<string>();
        using var zip = ZipFile.OpenRead(apkPath);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (entry.FullName.StartsWith("assets/", StringComparison.Ordinal) && !string.IsNullOrEmpty(entry.Name))
            {
                list.Add(entry.FullName.Replace('\\', '/'));
            }
        }

        return list;
    }

    private static bool ShouldSkipLogical(string path) =>
        string.Equals(path, ModManifestPaths.FileName, StringComparison.Ordinal)
        || path.StartsWith("exports/", StringComparison.Ordinal)
        || string.Equals(path, ".workspace.json", StringComparison.Ordinal)
        || path.StartsWith("client/lang/", StringComparison.Ordinal)
        || path.StartsWith("assets/client/lang/", StringComparison.Ordinal)
        || path.StartsWith("client/music", StringComparison.Ordinal)
        || path.StartsWith("assets/client/music", StringComparison.Ordinal);

    public static string FindRepositoryRoot(string startDirectory)
    {
        string? current = Path.GetFullPath(startDirectory);
        for (int i = 0; i < 32 && current != null; i++)
        {
            if (File.Exists(Path.Combine(current, "Ew4.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(startDirectory);
    }

    private static string ResolveShellApk(string repositoryRoot, string? shellOverride) =>
        string.IsNullOrWhiteSpace(shellOverride)
            ? Path.Combine(repositoryRoot, DefaultShellRelative)
            : Path.GetFullPath(shellOverride);

    private static string SanitizePackageSegment(string modId) =>
        modId.Replace('-', '_').Replace('.', '_');
}
