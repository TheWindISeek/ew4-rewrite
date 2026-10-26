using System.Text.Json;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Microsoft.AspNetCore.Http.Features;

namespace Ew4.Backend.Web;

public static class PlayModEndpoints
{
    public static void MapPlayModApi(this WebApplication app)
    {
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
