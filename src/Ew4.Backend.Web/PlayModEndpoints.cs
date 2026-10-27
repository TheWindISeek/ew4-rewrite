using System.Text.Json;
using System.Text.RegularExpressions;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Ew4.Service.Host;
using Microsoft.AspNetCore.Http.Features;

namespace Ew4.Backend.Web;

public static class PlayModEndpoints
{
    private static readonly Regex ExportApkFileName = new(@"^[a-z0-9][a-z0-9_.-]{0,80}-\d{9,12}\.apk$", RegexOptions.CultureInvariant);

    public static void MapPlayModApi(this WebApplication app)
    {
        app.MapGet("/api/export-apk/download/{fileName}", (string fileName, Ew4InstallOptions install) =>
        {
            if (!ExportApkFileName.IsMatch(fileName))
            {
                return Results.NotFound();
            }

            string path = Path.Combine(install.StateDirectory, "export-apk", fileName);
            if (!File.Exists(path))
            {
                return Results.NotFound();
            }

            return Results.File(path, "application/vnd.android.package-archive", fileName);
        });
        app.MapMethods("/api/mods", ["GET", "POST"], async (HttpRequest request, IGameOperations game) =>
        {
            if (PlayEndpoints.IsForbiddenCrossSite(request))
            {
                return Results.StatusCode(403);
            }

            try
            {
                using var reader = new StreamReader(request.Body);
                string body = request.Query["q"].FirstOrDefault() ?? await reader.ReadToEndAsync();
                var kind = string.IsNullOrWhiteSpace(body)
                    ? PlatformModOperationKind.ListMods
                    : PlatformModOperationKind.MutateMods;
                OperationResponse response = game.Invoke(
                    new PlatformModOperationDescriptor(kind),
                    new PlatformJsonPayload(string.IsNullOrWhiteSpace(body) ? null : body));
                return Results.Text(response.TextContent ?? "", response.ContentType);
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or JsonException)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        app.MapPost("/api/mod-prepare", async (HttpRequest request, IGameOperations game) =>
        {
            string? temp = null;
            try
            {
                IFormFile file = (await request.ReadFormAsync()).Files.GetFile("file")
                    ?? throw new InvalidDataException("请选择本地资源包");
                if (file.Length > 1024L * 1024 * 1024)
                {
                    throw new InvalidDataException("文件超过1GiB");
                }

                temp = Path.Combine(Path.GetTempPath(), "ew4-inspect-" + Guid.NewGuid().ToString("N"));
                await using (FileStream output = File.Create(temp))
                {
                    await file.CopyToAsync(output);
                }

                OperationResponse response = game.Invoke(
                    new PlatformModOperationDescriptor(PlatformModOperationKind.PrepareImport),
                    new PlatformFilePayload(temp, file.FileName));
                temp = null;
                return Results.Text(response.TextContent ?? "", response.ContentType);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
            finally
            {
                if (temp != null)
                {
                    File.Delete(temp);
                }
            }
        }).DisableAntiforgery();

        app.MapPost("/api/mod-import", async (HttpRequest request, IGameOperations game) =>
        {
            try
            {
                var form = await request.ReadFormAsync();
                OperationResponse response = game.Invoke(
                    new PlatformModOperationDescriptor(PlatformModOperationKind.CommitImport),
                    new PlatformFormCommitPayload(form["token"].ToString(), form["id"].ToString(), form["name"].ToString()));
                return Results.Text(response.TextContent ?? "", response.ContentType);
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        }).DisableAntiforgery();
    }
}
