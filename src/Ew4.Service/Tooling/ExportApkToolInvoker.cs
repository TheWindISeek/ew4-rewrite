using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Ew4.Service.Tooling;

/// <summary>Web 编排层通过子进程调用 <c>Ew4.Tool export-apk</c>（Service 不引用 Tool 程序集）。</summary>
public static class ExportApkToolInvoker
{
    public sealed record CompatEntry(string Path, string Field, string Reason, string Impact);

    public sealed record ToolResult(
        bool Ok,
        long ExportId,
        string? OutPath,
        int FilesPatched,
        IReadOnlyList<CompatEntry> Compat,
        string? Code,
        string? Message);

    public static ToolResult Run(
        string repositoryRoot,
        string modsDirectory,
        string baseModDirectory,
        string userStateDirectory,
        string outApkPath,
        string? modId,
        long? exportId,
        bool dryRun,
        bool allowLossy)
    {
        var exportArgs = new List<string>
        {
            "export-apk",
            "--root",
            repositoryRoot,
            "--mods",
            modsDirectory,
            "--base",
            baseModDirectory,
            "--active-user",
            userStateDirectory,
            "--out",
            outApkPath,
            "--json",
        };
        if (!string.IsNullOrWhiteSpace(modId))
        {
            exportArgs.Add("--mod");
            exportArgs.Add(modId);
        }

        if (exportId is { } id)
        {
            exportArgs.Add("--export-id");
            exportArgs.Add(id.ToString());
        }

        if (dryRun)
        {
            exportArgs.Add("--dry-run");
        }

        if (allowLossy)
        {
            exportArgs.Add("--allow-lossy");
        }

        ProcessStartInfo psi = new()
        {
            FileName = ResolveDotnetHost(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot,
        };

        string? dll = ResolveToolDll(repositoryRoot);
        if (dll != null)
        {
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add(dll);
        }
        else
        {
            string project = ResolveToolProject(repositoryRoot);
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(project);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Debug");
            psi.ArgumentList.Add("--");
        }

        foreach (string arg in exportArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet for export-apk.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
        {
            return new ToolResult(false, 0, null, 0, [], "export.tool", Trim(stderr));
        }

        return Parse(stdout, process.ExitCode);
    }

    private static ToolResult Parse(string stdout, int exitCode)
    {
        string line = stdout.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        if (line.Length == 0)
        {
            return new ToolResult(false, 0, null, 0, [], "export.tool", "Ew4.Tool export-apk produced no output.");
        }

        JsonObject? doc = JsonNode.Parse(line) as JsonObject;
        if (doc == null)
        {
            return new ToolResult(false, 0, null, 0, [], "export.tool", "Invalid JSON from Ew4.Tool.");
        }

        if (doc["ok"]?.GetValue<bool>() == false)
        {
            return new ToolResult(
                false,
                0,
                null,
                0,
                [],
                doc["code"]?.GetValue<string>() ?? "export.tool",
                doc["message"]?.GetValue<string>() ?? "export-apk failed");
        }

        if (exitCode != 0)
        {
            return new ToolResult(false, 0, null, 0, [], "export.tool", "export-apk exited with code " + exitCode);
        }

        var compat = new List<CompatEntry>();
        if (doc["compat"] is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                if (item is not JsonObject o)
                {
                    continue;
                }

                compat.Add(new CompatEntry(
                    o["path"]?.GetValue<string>() ?? "",
                    o["field"]?.GetValue<string>() ?? "",
                    o["reason"]?.GetValue<string>() ?? "",
                    o["impact"]?.GetValue<string>() ?? ""));
            }
        }

        return new ToolResult(
            true,
            doc["exportId"]?.GetValue<long>() ?? 0,
            doc["outPath"]?.GetValue<string>(),
            doc["filesPatched"]?.GetValue<int>() ?? 0,
            compat,
            null,
            null);
    }

    private static string? ResolveToolDll(string repositoryRoot)
    {
        string? env = Environment.GetEnvironmentVariable("EW4_TOOL_DLL");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return env;
        }

        foreach (string configuration in new[] { "Debug", "Release" })
        {
            string candidate = Path.Combine(repositoryRoot, "build", "Ew4.Tool", configuration, "net10.0", "Ew4.Tool.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ResolveToolProject(string repositoryRoot) =>
        Environment.GetEnvironmentVariable("EW4_TOOL_PROJECT")
        ?? Path.Combine(repositoryRoot, "src", "Ew4.Tool", "Ew4.Tool.csproj");

    private static string ResolveDotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private static string Trim(string text)
    {
        text = text.Trim();
        return text.Length > 500 ? text[..500] + "…" : text;
    }
}
