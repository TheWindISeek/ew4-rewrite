using System.Text.Json;
using Ew4.Platform.Content;
using Ew4.Service;
using Microsoft.AspNetCore.Http.Features;

namespace Ew4.Backend.Web;

public static class PlayModEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void MapPlayModApi(this WebApplication app)
    {
        app.MapMethods("/api/mods", ["GET", "POST"], async (HttpRequest request, ModCatalogService catalog, ModImportService imports) =>
        {
            if (PlayEndpoints.IsForbiddenCrossSite(request))
            {
                return Results.StatusCode(403);
            }

            try
            {
                using var reader = new StreamReader(request.Body);
                string body = request.Query["q"].FirstOrDefault() ?? await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var document = JsonDocument.Parse(body);
                    string? op = document.RootElement.TryGetProperty("op", out JsonElement opEl) ? opEl.GetString() : null;
                    if (op == "activate")
                    {
                        string? key = document.RootElement.TryGetProperty("key", out JsonElement keyEl) ? keyEl.GetString() : null;
                        catalog.Activate(key);
                    }
                    else if (op == "delete")
                    {
                        string? key = document.RootElement.TryGetProperty("key", out JsonElement keyEl) ? keyEl.GetString() : null;
                        if (!string.IsNullOrEmpty(key))
                        {
                            catalog.Remove(key);
                        }
                    }
                    else if (op == "import")
                    {
                        string? token = document.RootElement.TryGetProperty("token", out JsonElement tokenEl) ? tokenEl.GetString() : null;
                        string? id = document.RootElement.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                        string? name = document.RootElement.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        {
                            imports.Commit(token, id, name);
                        }
                    }
                    else if (op == "discard")
                    {
                        string? token = document.RootElement.TryGetProperty("token", out JsonElement tokenEl) ? tokenEl.GetString() : null;
                        imports.Discard(token);
                    }
                }

                return Results.Text(
                    JsonSerializer.Serialize(ModPanelApi.Serialize(catalog.GetPanelState()), Json),
                    "application/json; charset=utf-8");
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or JsonException or ContentError)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        app.MapPost("/api/mod-prepare", async (HttpRequest request, ModImportService imports) =>
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

                PreparedModImport prepared = imports.StageUploadedFile(temp, file.FileName);
                temp = null;
                return Results.Json(new { token = prepared.Token, identity = prepared.Identity });
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ContentError)
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

        app.MapPost("/api/mod-import", async (HttpRequest request, ModImportService imports, ModCatalogService catalog) =>
        {
            try
            {
                var form = await request.ReadFormAsync();
                imports.Commit(form["token"].ToString(), form["id"].ToString(), form["name"].ToString());
                return Results.Text(
                    JsonSerializer.Serialize(ModPanelApi.Serialize(catalog.GetPanelState()), Json),
                    "application/json; charset=utf-8");
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or ContentError)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        }).DisableAntiforgery();
    }
}
