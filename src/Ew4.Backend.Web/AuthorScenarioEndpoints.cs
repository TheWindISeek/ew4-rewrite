using System.Text.Json;
using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;
using Microsoft.AspNetCore.Mvc;

namespace Ew4.Backend.Web;

internal static class AuthorScenarioEndpoints
{
    public static void MapAuthorScenarioApi(this WebApplication app)
    {
        var workspaceScenario = app.MapGroup("/api/author/workspaces/{workspaceId}/scenario");
        workspaceScenario.MapGet("/{scenarioId}", GetWorkspaceScenario);
        workspaceScenario.MapPut("/{scenarioId}", PutWorkspaceScenario);
        workspaceScenario.MapPost("/{scenarioId}/roundtrip", RoundtripWorkspaceScenario);
        workspaceScenario.MapPost("/{scenarioId}/edit", ApplyScenarioEdit);

        var legacy = app.MapGroup("/api/author/scenario");
        legacy.MapGet("/{scenarioId}", GetScenarioLegacy);
        legacy.MapPut("/{scenarioId}", PutScenarioLegacy);
        legacy.MapPost("/{scenarioId}/roundtrip", RoundtripLegacy);
        app.MapPost("/api/author/scenario/btl-to-json", PostBtlToJson);
    }

    private static IResult GetWorkspaceScenario(
        string workspaceId,
        string scenarioId,
        IScenarioEditor editor)
    {
        try
        {
            return DocumentJson(editor.Open(workspaceId, scenarioId));
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static async Task<IResult> PutWorkspaceScenario(
        string workspaceId,
        string scenarioId,
        HttpRequest request,
        [FromQuery] bool smoke,
        IWorkspaceService workspaces,
        ContentResolver resolver,
        FilePackRepository repository,
        IEngine engine,
        IScenarioBuilder builder,
        IScenarioEditToolkit toolkit)
    {
        try
        {
            return await SaveScenario(
                workspaceId,
                scenarioId,
                request,
                smoke,
                workspaces,
                resolver,
                repository,
                engine,
                builder,
                toolkit);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult RoundtripWorkspaceScenario(
        string workspaceId,
        string scenarioId,
        IWorkspaceService workspaces,
        IScenarioEditToolkit toolkit)
    {
        try
        {
            return Roundtrip(workspaces.OpenMergedPack(workspaceId), scenarioId, toolkit);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult ApplyScenarioEdit(
        string workspaceId,
        string scenarioId,
        ScenarioEditRequest request,
        IScenarioEditor editor)
    {
        try
        {
            ScenarioEdit edit = request.ToEdit();
            ScenarioEditResult result = editor.Apply(workspaceId, scenarioId, edit);
            return Results.Json(new
            {
                result.ScenarioId,
                result.Saved.BtlPath,
                result.Saved.DocumentPath,
            });
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult GetScenarioLegacy(
        string scenarioId,
        [FromQuery] string? workspace,
        AuthorHostOptions hostOptions,
        FilePackRepository repository,
        HostApp hostApp,
        IScenarioEditToolkit toolkit)
    {
        try
        {
            IPackSource pack = ResolveLegacyPack(workspace, hostOptions, repository, hostApp);
            return DocumentJson(toolkit.OpenFromPack(pack, scenarioId));
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static async Task<IResult> PutScenarioLegacy(
        string scenarioId,
        HttpRequest request,
        [FromQuery] string? workspace,
        [FromQuery] bool smoke,
        AuthorHostOptions hostOptions,
        IWorkspaceService workspaceService,
        ContentResolver resolver,
        FilePackRepository repository,
        IEngine engine,
        IScenarioBuilder builder,
        IScenarioEditToolkit toolkit)
    {
        try
        {
            string workspaceId = string.IsNullOrWhiteSpace(workspace)
                ? hostOptions.DefaultWorkspaceId
                : workspace;
            return await SaveScenario(
                workspaceId,
                scenarioId,
                request,
                smoke,
                workspaceService,
                resolver,
                repository,
                engine,
                builder,
                toolkit);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static IResult RoundtripLegacy(
        string scenarioId,
        [FromQuery] string? workspace,
        AuthorHostOptions hostOptions,
        FilePackRepository repository,
        HostApp hostApp,
        IScenarioEditToolkit toolkit)
    {
        try
        {
            IPackSource pack = ResolveLegacyPack(workspace, hostOptions, repository, hostApp);
            return Roundtrip(pack, scenarioId, toolkit);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static async Task<IResult> PostBtlToJson(HttpRequest request, IScenarioEditToolkit toolkit)
    {
        try
        {
            string scenarioId;
            byte[] btlBytes;
            if (request.HasFormContentType)
            {
                IFormCollection form = await request.ReadFormAsync();
                scenarioId = form["id"].ToString();
                IFormFile? file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(scenarioId) || file == null)
                {
                    throw new ContentError("author.btl-to-json", "Multipart requires form fields 'id' and file 'file'.");
                }

                await using Stream stream = file.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                btlBytes = memory.ToArray();
            }
            else
            {
                scenarioId = request.Query["id"].ToString();
                string? path = request.Query["path"];
                if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(path))
                {
                    throw new ContentError(
                        "author.btl-to-json",
                        "Query requires 'id' and 'path', or POST multipart with 'id' and 'file'.");
                }

                btlBytes = await File.ReadAllBytesAsync(Path.GetFullPath(path));
            }

            ScenarioDocument document = toolkit.ImportBtl(scenarioId, btlBytes);
            return DocumentJson(document);
        }
        catch (ContentError ex)
        {
            return AuthorHttpJson.Problem(ex);
        }
    }

    private static async Task<IResult> SaveScenario(
        string workspaceId,
        string scenarioId,
        HttpRequest request,
        bool smoke,
        IWorkspaceService workspaces,
        ContentResolver resolver,
        FilePackRepository repository,
        IEngine engine,
        IScenarioBuilder builder,
        IScenarioEditToolkit toolkit)
    {
        byte[] body = await ReadBody(request);
        ScenarioDocument document = toolkit.DeserializeJson(body);
        if (!string.Equals(document.Id, scenarioId, StringComparison.Ordinal))
        {
            throw new ContentError(
                "scenario.id",
                $"Route scenarioId '{scenarioId}' does not match document id '{document.Id}'.");
        }

        ScenarioSaveResult saved = toolkit.SaveToWorkspace(workspaces.ResolveWorkspaceRoot(workspaceId), document);
        AuthorHttpJson.DiagnosticDto[]? diagnostics = null;
        if (smoke)
        {
            ResolvedContent resolved = ScenarioSmokeValidator.ResolveWorkspace(
                resolver,
                repository,
                workspaces.ResolveWorkspaceRoot(workspaceId));
            IReadOnlyList<Diagnostic> smokeDiagnostics =
                ScenarioSmokeValidator.SmokeStart(resolved, scenarioId, engine, builder);
            diagnostics = smokeDiagnostics.Select(AuthorHttpJson.DiagnosticDto.From).ToArray();
        }

        return Results.Json(new { saved.BtlPath, saved.DocumentPath, diagnostics });
    }

    private static IResult Roundtrip(IPackSource pack, string scenarioId, IScenarioEditToolkit toolkit)
    {
        byte[] raw = ScenarioPackAccess.ResolveBtlBytes(pack, scenarioId);
        ScenarioBtlRoundtrip.Result result = toolkit.ValidateBtlRoundtrip(scenarioId, raw);
        return Results.Json(new
        {
            result.OriginalClone,
            result.Document,
            result.DocumentJson,
            result.Layers,
            result.LayerMismatchSection,
        });
    }

    private static IPackSource ResolveLegacyPack(
        string? workspace,
        AuthorHostOptions hostOptions,
        FilePackRepository repository,
        HostApp hostApp)
    {
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return AuthorPackResolver.OpenActivePack(repository, hostApp);
        }

        return AuthorPackResolver.OpenWorkspacePack(repository, hostOptions.ResolveWorkspaceRoot(workspace));
    }

    private static IResult DocumentJson(ScenarioDocument document) =>
        Results.Bytes(ScenarioEditToolkit.Default.SerializeJson(document), "application/json");

    private static async Task<byte[]> ReadBody(HttpRequest request)
    {
        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory);
        return memory.ToArray();
    }

    private sealed class ScenarioEditRequest
    {
        public string Type { get; set; } = "";
        public int Index { get; set; }
        public short Cell { get; set; }
        public long Money { get; set; }
        public long Industry { get; set; }
        public long Food { get; set; }

        public JsonElement Fields { get; set; }

        public ScenarioEdit ToEdit() => Type switch
        {
            "armyCell" => new ScenarioEditArmyCell(Index, Cell),
            "siteCell" => new ScenarioEditSiteCell(Index, Cell),
            "factionResources" => new ScenarioEditFactionResources(Index, Money, Industry, Food),
            "army" => new ScenarioEditArmyFields(Index, ScenarioEditFieldJson.ParseArmy(Fields)),
            "site" => new ScenarioEditSiteFields(Index, ScenarioEditFieldJson.ParseSite(Fields)),
            "faction" => new ScenarioEditFactionFields(Index, ScenarioEditFieldJson.ParseFaction(Fields)),
            "event" => new ScenarioEditEventFields(Index, ScenarioEditFieldJson.ParseEvent(Fields)),
            "map" => new ScenarioEditMapInfo(ScenarioEditFieldJson.ParseMap(Fields)),
            _ => throw new ContentError("scenario.edit", "Unknown edit type: " + Type),
        };
    }
}
