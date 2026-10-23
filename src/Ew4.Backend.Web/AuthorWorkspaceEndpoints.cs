using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Runtime;
using Microsoft.AspNetCore.Mvc;

namespace Ew4.Backend.Web;

internal static class AuthorWorkspaceEndpoints
{
    public static void MapAuthorWorkspaceApi(this WebApplication app)
    {
        var workspaces = app.MapGroup("/api/author/workspaces");
        workspaces.MapGet("", ListWorkspaces);
        workspaces.MapPost("", CreateWorkspace);
        workspaces.MapDelete("/{workspaceId}", DeleteWorkspace);
        workspaces.MapPost("/{workspaceId}/validate", ValidateWorkspace);
        workspaces.MapPost("/{workspaceId}/export", ExportWorkspace);
        workspaces.MapGet("/{workspaceId}/scenarios", ListScenarios);
        workspaces.MapGet("/{workspaceId}/files/{**path}", ReadFile);
        workspaces.MapPut("/{workspaceId}/files/{**path}", WriteFile);
        workspaces.MapPost("/{workspaceId}/preview/enter", EnterPreview);
        workspaces.MapPost("/{workspaceId}/preview/refresh", RefreshPreview);
        app.MapPost("/api/author/preview/exit", ExitPreview);
        app.MapGet("/api/author/preview", GetPreviewStatus);
    }

    private static IResult ListWorkspaces(IWorkspaceService workspaces)
    {
        IReadOnlyList<WorkspaceInfo> list = workspaces.List();
        return Results.Json(list);
    }

    private static IResult CreateWorkspace(CreateWorkspaceRequest request, IWorkspaceService workspaces)
    {
        try
        {
            WorkspaceInfo info = workspaces.Create(request.FromModId ?? ModId.Base, request.Id, request.Name);
            return Results.Json(info, statusCode: StatusCodes.Status201Created);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult DeleteWorkspace(string workspaceId, IWorkspaceService workspaces)
    {
        try
        {
            workspaces.Delete(workspaceId);
            return Results.NoContent();
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult ValidateWorkspace(
        string workspaceId,
        [FromQuery] ValidationLevel level,
        IWorkspaceService workspaces)
    {
        try
        {
            IReadOnlyList<Diagnostic> diagnostics = workspaces.Validate(workspaceId, level);
            return Results.Json(diagnostics.Select(AuthorHttpJson.DiagnosticDto.From));
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult ExportWorkspace(
        string workspaceId,
        [FromQuery] ValidationLevel level,
        [FromQuery] string? fileName,
        IWorkspaceService workspaces)
    {
        try
        {
            ExportResult result = workspaces.Export(
                workspaceId,
                new ExportOptions(level, fileName));
            return Results.Json(new
            {
                result.Success,
                result.ArchivePath,
                diagnostics = result.Diagnostics.Select(AuthorHttpJson.DiagnosticDto.From),
            });
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult ListScenarios(string workspaceId, IWorkspaceService workspaces)
    {
        try
        {
            IModSource pack = workspaces.OpenMergedMod(workspaceId);
            var campaign = new List<string>();
            var conquest = new List<string>();
            var tutorial = new List<string>();
            var other = new List<string>();

            foreach (ScenarioIndex index in ScenarioCatalog.List(pack))
            {
                if (index.JsonPath.Contains(".ew4scenario.", StringComparison.Ordinal))
                {
                    continue;
                }

                int mode = ScenarioStartHelpers.ResolveMode(index.Id);
                switch (mode)
                {
                    case 1:
                        campaign.Add(index.Id);
                        break;
                    case 2:
                        conquest.Add(index.Id);
                        break;
                    case 4:
                        tutorial.Add(index.Id);
                        break;
                    default:
                        other.Add(index.Id);
                        break;
                }
            }

            campaign.Sort(StringComparer.Ordinal);
            conquest.Sort(StringComparer.Ordinal);
            tutorial.Sort(StringComparer.Ordinal);
            other.Sort(StringComparer.Ordinal);

            return Results.Json(new
            {
                campaign,
                conquest,
                tutorial,
                other,
            });
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult ReadFile(string workspaceId, string path, IWorkspaceService workspaces)
    {
        try
        {
            byte[] bytes = workspaces.ReadFile(workspaceId, path);
            return Results.Bytes(bytes, "application/octet-stream");
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static async Task<IResult> WriteFile(string workspaceId, string path, HttpRequest request, IWorkspaceService workspaces)
    {
        try
        {
            using var memory = new MemoryStream();
            await request.Body.CopyToAsync(memory);
            WriteResult result = workspaces.WriteFile(workspaceId, path, memory.ToArray());
            return Results.Json(new
            {
                result.Path,
                diagnostics = result.Diagnostics.Select(AuthorHttpJson.DiagnosticDto.From),
            });
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult EnterPreview(
        string workspaceId,
        IWorkspaceService workspaces,
        HostApp hostApp)
    {
        try
        {
            hostApp.EnterWorkspacePreview(workspaces.ResolveWorkspaceRoot(workspaceId), workspaceId);
            return Results.Json(hostApp.PreviewState);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult RefreshPreview(
        string workspaceId,
        IWorkspaceService workspaces,
        HostApp hostApp)
    {
        try
        {
            hostApp.RefreshWorkspacePreview(workspaces.ResolveWorkspaceRoot(workspaceId), workspaceId);
            return Results.Json(hostApp.PreviewState);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult ExitPreview(HostApp hostApp)
    {
        hostApp.ExitWorkspacePreview();
        return Results.Json(hostApp.PreviewState);
    }

    private static IResult GetPreviewStatus(HostApp hostApp) => Results.Json(hostApp.PreviewState);

    private sealed record CreateWorkspaceRequest(string Id, string Name, string? FromModId);
}
