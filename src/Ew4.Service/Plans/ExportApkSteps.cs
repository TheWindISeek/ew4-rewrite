using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Interface.Common;
using Ew4.Service.Host;
using Ew4.Service.Play;
using Ew4.Service.Tooling;

namespace Ew4.Service.Plans;

/// <summary>Web：<c>POST /api/export-apk</c> — 委托 <c>Ew4.Tool export-apk</c>（见 <see cref="ExportApkToolInvoker"/>）。</summary>
public static class ExportApkSteps
{
    public static string Run(Ew4InstallOptions install, PlayHost play, string body)
    {
        try
        {
            if (!ExportApkCapability.Supported)
            {
                return Fail(
                    "export.unsupported",
                    "导出 Mod APK 仅在开发机或 Web 安装包中提供；Android 游戏客户端内不支持，请使用 scripts/ew4.sh export-apk。");
            }

            if (play.StateRoot is not { } stateRoot)
            {
                throw new OperationException("export.inactive", "No content pack is active.");
            }

            JsonObject request = string.IsNullOrWhiteSpace(body) ? [] : JsonNode.Parse(body) as JsonObject
                ?? throw new OperationException("export.request", "Body must be an object.");
            string op = request["op"]?.GetValue<string>() ?? "export";

            string? modId = request["modId"]?.GetValue<string>();
            bool allowLossy = request["allowLossy"]?.GetValue<bool>() ?? true;
            long? exportId = request["exportId"]?.GetValue<long>();

            string repo = install.RepositoryRoot;
            string modsDir = install.ModsDirectory;
            string baseDir = Path.Combine(repo, "mods", "base");
            if (!Directory.Exists(baseDir))
            {
                baseDir = Path.Combine(modsDir, "base");
            }

            if (op == "check")
            {
                string dryOut = Path.Combine(Path.GetTempPath(), "ew4-export-check-" + Guid.NewGuid().ToString("N") + ".apk");
                ExportApkToolInvoker.ToolResult tool = ExportApkToolInvoker.Run(
                    repo,
                    modsDir,
                    baseDir,
                    stateRoot,
                    dryOut,
                    modId,
                    exportId,
                    dryRun: true,
                    allowLossy: allowLossy);
                if (!tool.Ok)
                {
                    return Fail(tool.Code ?? "export.tool", tool.Message ?? "export-apk check failed");
                }

                var list = new JsonArray();
                foreach (ExportApkToolInvoker.CompatEntry issue in tool.Compat)
                {
                    list.Add(new JsonObject
                    {
                        ["path"] = issue.Path,
                        ["field"] = issue.Field,
                        ["reason"] = issue.Reason,
                        ["impact"] = issue.Impact,
                    });
                }

                return new JsonObject
                {
                    ["ok"] = true,
                    ["data"] = new JsonObject { ["compat"] = list, ["count"] = tool.Compat.Count },
                }.ToJsonString();
            }

            if (op != "export")
            {
                throw new OperationException("export.request", "Unknown op: " + op);
            }

            string exportDir = Path.Combine(stateRoot, "export-apk");
            Directory.CreateDirectory(exportDir);
            long id = exportId ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string safeMod = (modId ?? "active").Replace('/', '_');
            string fileName = safeMod + "-" + id + ".apk";
            string outPath = Path.Combine(exportDir, fileName);

            ExportApkToolInvoker.ToolResult result = ExportApkToolInvoker.Run(
                repo,
                modsDir,
                baseDir,
                stateRoot,
                outPath,
                modId,
                id,
                dryRun: false,
                allowLossy: allowLossy);
            if (!result.Ok)
            {
                return Fail(result.Code ?? "export.tool", result.Message ?? "export-apk failed");
            }

            return new JsonObject
            {
                ["ok"] = true,
                ["data"] = new JsonObject
                {
                    ["exportId"] = result.ExportId,
                    ["filesPatched"] = result.FilesPatched,
                    ["compat"] = result.Compat.Count,
                    ["fileName"] = fileName,
                    ["downloadUrl"] = "/api/export-apk/download/" + Uri.EscapeDataString(fileName),
                },
            }.ToJsonString();
        }
        catch (Exception error) when (error is OperationException or JsonException or InvalidOperationException)
        {
            string code = error is OperationException op ? op.Code : "export.request";
            return Fail(code, error.Message);
        }
    }

    private static string Fail(string code, string message) =>
        new JsonObject { ["ok"] = false, ["code"] = code, ["message"] = message }.ToJsonString();
}
