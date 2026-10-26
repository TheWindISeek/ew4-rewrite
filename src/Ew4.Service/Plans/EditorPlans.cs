using Ew4.Interface.Common;
using System.Text.Json;
using Ew4.Interface.Editor;
using Ew4.Interface.Host;
using Ew4.Interface.Operations;
using Ew4.Interface.Plans;
using Ew4.Interface.Runtime;
using Ew4.Service.Host;

namespace Ew4.Service.Plans;

/// <summary>Editor 域的 Step 与 Plan：战役文档、将领表、预览绑定。</summary>
public sealed class EditorPlans : IPlanModule
{
    public static readonly PlanKey<EditorWorkspacePayload> Workspace = new("editor.workspace");

    private readonly ICampaignEditorOperations _campaign;
    private readonly IEditorOperations _commander;
    private readonly IPreviewBindingOperations _preview;
    private readonly IEditorWorkspaceOperations _workspace;

    private readonly IStep _parse;
    private readonly IStep _ensureBinding;

    public EditorPlans(ICampaignEditorOperations campaign, IEditorOperations commander, IPreviewBindingOperations preview,
        IEditorWorkspaceOperations workspace)
    {
        _campaign = campaign;
        _commander = commander;
        _preview = preview;
        _workspace = workspace;
        _parse = Steps.Of("editor.parse-request", [], [Workspace], ctx =>
            ctx.Set(Workspace, ctx.Get(PlanKeys.Payload) as EditorWorkspacePayload
                ?? throw new ArgumentException("EditorWorkspacePayload required.")));
        _ensureBinding = Steps.Of("preview.ensure-binding", [Workspace], [],
            ctx => _preview.Enter(WorkspaceId(ctx)));
    }

    public void Register(PlanRegistry registry)
    {
        RegisterWorkspace(registry);

        Campaign(registry, EditorOperationKind.CampaignCreate, "editor.campaign-create", ctx =>
            OperationResponse.CreatedJson(Steps.Unwrap(_campaign.CreateWorkspaceCampaignJson(WorkspaceId(ctx), Body(ctx)))));
        Campaign(registry, EditorOperationKind.CampaignGet, "editor.campaign-get", ctx =>
            Bytes(Steps.Unwrap(_campaign.GetWorkspaceCampaignJson(WorkspaceId(ctx), CampaignId(ctx)))));
        Campaign(registry, EditorOperationKind.CampaignPut, "editor.campaign-put", ctx =>
            OperationResponse.Json(Steps.Unwrap(_campaign.SaveWorkspaceCampaignJson(
                WorkspaceId(ctx), CampaignId(ctx), Body(ctx), ctx.Get(Workspace).Smoke))));
        Campaign(registry, EditorOperationKind.CampaignEdit, "editor.campaign-edit", ctx =>
            OperationResponse.Json(Steps.Unwrap(_campaign.ApplyWorkspaceCampaignEditJson(WorkspaceId(ctx), CampaignId(ctx), Body(ctx)))));
        Campaign(registry, EditorOperationKind.CampaignRoundtrip, "editor.campaign-roundtrip", ctx =>
            OperationResponse.Json(Steps.Unwrap(_campaign.RoundtripWorkspaceJson(WorkspaceId(ctx), CampaignId(ctx)))));
        Campaign(registry, EditorOperationKind.BtlToJson, "editor.btl-to-json", ctx =>
            Bytes(Steps.Unwrap(_campaign.BtlBytesToDocumentJson(
                CampaignId(ctx), Body(ctx)))), resource: false);

        Commander(registry, EditorOperationKind.CommanderList, "editor.commander-list", ctx =>
            OperationResponse.Json(EditorRuntimeJson.CommanderTable(_commander.ListCommanders(WorkspaceId(ctx)))));
        Commander(registry, EditorOperationKind.CommanderSkills, "editor.commander-skills", ctx =>
            OperationResponse.Json(EditorRuntimeJson.SkillCatalog(_commander.SkillCatalog(WorkspaceId(ctx)))));
        Commander(registry, EditorOperationKind.CommanderGet, "editor.commander-get", ctx =>
            OperationResponse.Json(EditorRuntimeJson.CommanderEntry(_commander.GetCommander(WorkspaceId(ctx), LegacyId(ctx)))));
        Commander(registry, EditorOperationKind.CommanderEdit, "editor.commander-edit", ctx =>
        {
            CommanderEditLoadout edit = EditorRuntimeJson.ParseCommanderEdit(LegacyId(ctx), Body(ctx));
            return OperationResponse.Json(EditorRuntimeJson.CommanderEditOutcome(
                Steps.Unwrap(_commander.ApplyCommanderEdit(WorkspaceId(ctx), edit))));
        });

        Preview(registry, EditorOperationKind.PreviewEnter, ctx => _preview.Enter(WorkspaceId(ctx)));
        Preview(registry, EditorOperationKind.PreviewRefresh, ctx => _preview.Refresh(WorkspaceId(ctx)));
        Preview(registry, EditorOperationKind.PreviewExit, _ => _preview.Exit());
        Preview(registry, EditorOperationKind.PreviewStatus, _ => { });
    }

    private void RegisterWorkspace(PlanRegistry registry)
    {
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceList, ctx => OperationResponse.Json(_workspace.ListWorkspacesJson()), resource: false);
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceCreate, ctx =>
        {
            var w = ctx.Get(Workspace);
            string id = WorkspaceId(ctx);
            return OperationResponse.CreatedJson(_workspace.CreateWorkspaceJson(id, w.WorkspaceName ?? w.FileName ?? id, w.FromModId, w.Mode));
        });
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceDelete, ctx =>
        {
            _workspace.DeleteWorkspace(WorkspaceId(ctx));
            return OperationResponse.NoContent();
        });
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceValidate, ctx =>
            OperationResponse.Json(_workspace.ValidateJson(WorkspaceId(ctx), Level(ctx))));
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceExport, ctx =>
            OperationResponse.Json(_workspace.ExportJson(WorkspaceId(ctx), Level(ctx), ctx.Get(Workspace).FileName, ctx.Get(Workspace).Standalone)));
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceCapabilities, ctx =>
            OperationResponse.Json(_workspace.CapabilitiesJson(WorkspaceId(ctx))));
        WorkspaceRequest(registry, EditorOperationKind.MapList, ctx => OperationResponse.Json(_workspace.MapListJson(WorkspaceId(ctx))));
        WorkspaceRequest(registry, EditorOperationKind.MapDescribe, ctx =>
            OperationResponse.Json(_workspace.MapDescribeJson(WorkspaceId(ctx), MapId(ctx))));
        WorkspaceRequest(registry, EditorOperationKind.MapPreview, ctx =>
            OperationResponse.Json(_workspace.MapPreviewJson(WorkspaceId(ctx), MapId(ctx), ctx.Get(Workspace).Crop)));
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceCampaignList, ctx =>
            OperationResponse.Json(_workspace.ListCampaignsJson(WorkspaceId(ctx))));
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceFileRead, ctx =>
            OperationResponse.Bytes(_workspace.ReadFile(WorkspaceId(ctx), Path(ctx))));
        WorkspaceRequest(registry, EditorOperationKind.WorkspaceFileWrite, ctx =>
            OperationResponse.Json(_workspace.WriteFileJson(WorkspaceId(ctx), Path(ctx), ctx.Get(Workspace).Body ?? Array.Empty<byte>())));
    }

    private void WorkspaceRequest(PlanRegistry registry, EditorOperationKind kind, Func<PlanContext, OperationResponse> run, bool resource = true) =>
        Campaign(registry, kind, "editor." + kind.ToString().ToLowerInvariant(), run, resource);

    private static ValidationLevel Level(PlanContext ctx) =>
        Enum.TryParse(ctx.Get(Workspace).ValidationLevel, ignoreCase: true, out ValidationLevel parsed) ? parsed : ValidationLevel.SmokeStart;

    private static int MapId(PlanContext ctx) =>
        ctx.Get(Workspace).MapId ?? throw new ArgumentException("mapId required.");

    private static string Path(PlanContext ctx) =>
        ctx.Get(Workspace).RelativePath ?? throw new ArgumentException("path required.");

    private void Campaign(PlanRegistry registry, EditorOperationKind kind, string stepId,
        Func<PlanContext, OperationResponse> run, bool resource = true)
    {
        PlanBuilder plan = registry.Request(RequestNames.For(kind)).Step(_parse).Step(Respond(stepId, run));
        if (resource)
        {
            plan.Resource(payload => (payload as EditorWorkspacePayload)?.WorkspaceId is { } id ? "workspace:" + id : null);
        }

        plan.Done();
    }

    private void Commander(PlanRegistry registry, EditorOperationKind kind, string stepId, Func<PlanContext, OperationResponse> run) =>
        registry.Request(RequestNames.For(kind))
            .Resource("binding")
            .Step(_parse)
            .Step(_ensureBinding)
            .Step(Respond(stepId, run))
            .Done();

    private void Preview(PlanRegistry registry, EditorOperationKind kind, Action<PlanContext> act) =>
        registry.Request(RequestNames.For(kind))
            .Resource("binding")
            .Step(_parse)
            .Step(Respond("preview." + kind.ToString().Replace("Preview", "").ToLowerInvariant(), ctx =>
            {
                act(ctx);
                return OperationResponse.Json(_preview.GetStateJson());
            }))
            .Done();

    private static IStep Respond(string id, Func<PlanContext, OperationResponse> run) =>
        Steps.Of(id, [Workspace], [PlanKeys.Response], ctx => ctx.Set(PlanKeys.Response, run(ctx)));

    private static OperationResponse Bytes(byte[] json) => new(null, "application/json", json);

    private static string WorkspaceId(PlanContext ctx) =>
        ctx.Get(Workspace).WorkspaceId ?? throw new ArgumentException("workspaceId required.");

    private static string CampaignId(PlanContext ctx) =>
        ctx.Get(Workspace).CampaignId ?? throw new ArgumentException("campaignId required.");

    private static byte[] Body(PlanContext ctx) =>
        ctx.Get(Workspace).Body ?? throw new ArgumentException("body required.");

    private static int LegacyId(PlanContext ctx) =>
        ctx.Get(Workspace).LegacyId ?? throw new ArgumentException("legacyId required.");
}
