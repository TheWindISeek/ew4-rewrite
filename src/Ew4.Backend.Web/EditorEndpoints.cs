using System.Text;
using Ew4.Interface.Editor;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Ew4.Backend.Web;

internal static class EditorEndpoints
{
    public static void MapEditorApi(this WebApplication app)
    {
        var workspaces = app.MapGroup("/api/editor/workspaces");
        workspaces.MapGet("", Handler(EditorOperationKind.WorkspaceList));
        workspaces.MapPost("", Handler(EditorOperationKind.WorkspaceCreate));
        workspaces.MapDelete("/{workspaceId}", Handler(EditorOperationKind.WorkspaceDelete));
        workspaces.MapPost("/{workspaceId}/validate", Handler(EditorOperationKind.WorkspaceValidate));
        workspaces.MapPost("/{workspaceId}/export", Handler(EditorOperationKind.WorkspaceExport));
        workspaces.MapGet("/{workspaceId}/capabilities", Handler(EditorOperationKind.WorkspaceCapabilities));
        workspaces.MapGet("/{workspaceId}/maps", Handler(EditorOperationKind.MapList));
        workspaces.MapGet("/{workspaceId}/maps/{mapId:int}", Handler(EditorOperationKind.MapDescribe));
        workspaces.MapGet("/{workspaceId}/maps/{mapId:int}/preview", Handler(EditorOperationKind.MapPreview));
        workspaces.MapGet("/{workspaceId}/campaigns", Handler(EditorOperationKind.WorkspaceCampaignList));
        workspaces.MapGet("/{workspaceId}/files/{**path}", Handler(EditorOperationKind.WorkspaceFileRead));
        workspaces.MapPut("/{workspaceId}/files/{**path}", Handler(EditorOperationKind.WorkspaceFileWrite));
        workspaces.MapPost("/{workspaceId}/preview/enter", Handler(EditorOperationKind.PreviewEnter));
        workspaces.MapPost("/{workspaceId}/preview/refresh", Handler(EditorOperationKind.PreviewRefresh));

        var workspaceCampaign = app.MapGroup("/api/editor/workspaces/{workspaceId}/campaign");
        workspaceCampaign.MapPost("", Handler(EditorOperationKind.CampaignCreate));
        workspaceCampaign.MapGet("/{campaignId}", Handler(EditorOperationKind.CampaignGet));
        workspaceCampaign.MapPut("/{campaignId}", Handler(EditorOperationKind.CampaignPut));
        workspaceCampaign.MapPost("/{campaignId}/roundtrip", Handler(EditorOperationKind.CampaignRoundtrip));
        workspaceCampaign.MapPost("/{campaignId}/edit", Handler(EditorOperationKind.CampaignEdit));

        var commanders = app.MapGroup("/api/editor/workspaces/{workspaceId}/commanders");
        commanders.MapGet("", Handler(EditorOperationKind.CommanderList));
        commanders.MapGet("/skills", Handler(EditorOperationKind.CommanderSkills));
        commanders.MapGet("/{legacyId:int}", Handler(EditorOperationKind.CommanderGet));
        commanders.MapPost("/{legacyId:int}/edit", Handler(EditorOperationKind.CommanderEdit));

        app.MapPost("/api/editor/preview/exit", Handler(EditorOperationKind.PreviewExit));
        app.MapGet("/api/editor/preview", Handler(EditorOperationKind.PreviewStatus));
        app.MapPost("/api/editor/campaign/btl-to-json", Handler(EditorOperationKind.BtlToJson));
    }

    private static Func<HttpContext, Task<IResult>> Handler(EditorOperationKind kind) =>
        ctx => Invoke(ctx, kind);

    private static async Task<IResult> Invoke(HttpContext context, EditorOperationKind kind)
    {
        try
        {
            IGameOperations game = context.RequestServices.GetRequiredService<IGameOperations>();
            EditorWorkspacePayload payload = await BuildPayload(context, kind);
            OperationResponse response = game.Invoke(new EditorOperationDescriptor(kind), payload);
            return ToResult(response);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException)
        {
            return Results.Json(new { code = "client.failure", message = error.Message }, statusCode: 400);
        }
    }

    private static async Task<EditorWorkspacePayload> BuildPayload(HttpContext context, EditorOperationKind kind)
    {
        HttpRequest request = context.Request;
        string? workspaceId = context.Request.RouteValues.TryGetValue("workspaceId", out object? ws) ? ws?.ToString() : null;
        string? campaignId = context.Request.RouteValues.TryGetValue("campaignId", out object? cid) ? cid?.ToString() : null;
        string? path = context.Request.RouteValues.TryGetValue("path", out object? p) ? p?.ToString() : null;
        int? legacyId = context.Request.RouteValues.TryGetValue("legacyId", out object? lid) && int.TryParse(lid?.ToString(), out int parsed)
            ? parsed
            : null;

        byte[]? body = null;
        if (kind is EditorOperationKind.WorkspaceFileWrite
            or EditorOperationKind.CampaignPut
            or EditorOperationKind.CampaignEdit
            or EditorOperationKind.CampaignCreate
            or EditorOperationKind.CommanderEdit
            or EditorOperationKind.BtlToJson)
        {
            body = await ReadBody(request);
        }

        if (kind == EditorOperationKind.WorkspaceCreate)
        {
            CreateWorkspaceRequest? create = await request.ReadFromJsonAsync<CreateWorkspaceRequest>();
            return new EditorWorkspacePayload(
                WorkspaceId: create?.Id,
                WorkspaceName: create?.Name,
                FromModId: create?.FromModId,
                Mode: create?.Mode);
        }

        if (kind == EditorOperationKind.BtlToJson && request.HasFormContentType)
        {
            IFormCollection form = await request.ReadFormAsync();
            campaignId = form["id"].ToString();
            IFormFile? file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(campaignId) || file == null)
            {
                throw new InvalidOperationException("Multipart requires form fields 'id' and file 'file'.");
            }

            await using Stream stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            body = memory.ToArray();
        }
        else if (kind == EditorOperationKind.BtlToJson)
        {
            throw new InvalidOperationException("btl-to-json requires multipart form fields 'id' and 'file'.");
        }

        bool smoke = bool.TryParse(request.Query["smoke"], out bool smokeValue) && smokeValue;
        string? validationLevel = request.Query["level"].FirstOrDefault();
        string? fileName = request.Query["fileName"].FirstOrDefault();
        int? mapId = request.RouteValues.TryGetValue("mapId", out object? mid) && int.TryParse(mid?.ToString(), out int parsedMap) ? parsedMap : null;
        MapCropQuery? crop = int.TryParse(request.Query["x"], out int cx) && int.TryParse(request.Query["y"], out int cy)
            && int.TryParse(request.Query["w"], out int cw) && int.TryParse(request.Query["h"], out int ch)
            ? new MapCropQuery(cx, cy, cw, ch) : null;
        bool standalone = bool.TryParse(request.Query["standalone"], out bool standaloneValue) && standaloneValue;

        return new EditorWorkspacePayload(
            WorkspaceId: workspaceId,
            RelativePath: path,
            Body: body,
            ValidationLevel: validationLevel,
            FileName: fileName,
            CampaignId: campaignId,
            Smoke: smoke,
            LegacyId: legacyId,
            Standalone: standalone,
            MapId: mapId,
            Crop: crop);
    }

    private static IResult ToResult(OperationResponse response)
    {
        if (response.StatusCode == StatusCodes.Status204NoContent)
        {
            return Results.NoContent();
        }

        if (response.StatusCode == StatusCodes.Status400BadRequest)
        {
            return Results.Text(response.TextContent ?? "", response.ContentType, statusCode: StatusCodes.Status400BadRequest);
        }

        if (response.BinaryContent != null)
        {
            return Results.Bytes(response.BinaryContent, response.ContentType);
        }

        if (response.StatusCode == StatusCodes.Status201Created)
        {
            return Results.Text(response.TextContent ?? "", response.ContentType, statusCode: StatusCodes.Status201Created);
        }

        return Results.Text(response.TextContent ?? "", response.ContentType);
    }

    private static async Task<byte[]> ReadBody(HttpRequest request)
    {
        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory);
        return memory.ToArray();
    }

    private sealed record CreateWorkspaceRequest(string Id, string Name, string? FromModId, string? Mode = null);
}
