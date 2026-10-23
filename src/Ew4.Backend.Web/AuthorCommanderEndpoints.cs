using Ew4.Editor.Commander;
using Ew4.Platform.Content;
using Microsoft.AspNetCore.Mvc;

namespace Ew4.Backend.Web;

internal static class AuthorCommanderEndpoints
{
    public static void MapAuthorCommanderApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/author/workspaces/{workspaceId}/commanders");
        group.MapGet("", ListCommanders);
        group.MapGet("/skills", ListSkills);
        group.MapGet("/{legacyId:int}", GetCommander);
        group.MapPost("/{legacyId:int}/edit", EditCommander);
    }

    private static IResult ListCommanders(string workspaceId, ICommanderEditor editor)
    {
        try
        {
            CommanderTableView view = editor.List(workspaceId);
            return Results.Json(view);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult ListSkills(string workspaceId, ICommanderEditor editor)
    {
        try
        {
            return Results.Json(editor.SkillCatalog(workspaceId));
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult GetCommander(string workspaceId, int legacyId, ICommanderEditor editor)
    {
        try
        {
            return Results.Json(editor.Get(workspaceId, legacyId));
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult EditCommander(
        string workspaceId,
        int legacyId,
        [FromBody] CommanderEditRequest request,
        ICommanderEditor editor)
    {
        try
        {
            CommanderEdit edit = request.ToEdit(legacyId);
            CommanderEditResult result = editor.Apply(workspaceId, edit);
            return Results.Json(result);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private sealed class CommanderEditRequest
    {
        public int? Skill1 { get; init; }

        public int? Skill2 { get; init; }

        public int? Skill3 { get; init; }

        public int? Skill4 { get; init; }

        public int? Infantry { get; init; }

        public int? Cavalry { get; init; }

        public int? Artillery { get; init; }

        public int? Warship { get; init; }

        public int? Fort { get; init; }

        public CommanderEdit ToEdit(int legacyId) =>
            new CommanderEditLoadout(
                legacyId,
                Skill1 ?? 0,
                Skill2 ?? 0,
                Skill3 ?? 0,
                Skill4 ?? 0,
                Infantry ?? 0,
                Cavalry ?? 0,
                Artillery ?? 0,
                Warship ?? 0,
                Fort ?? 0);
    }
}
