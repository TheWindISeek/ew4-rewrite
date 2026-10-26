using System.Text.Json;
using Ew4.Interface.Host;
using Ew4.Interface.Mod;
using Ew4.Interface.Operations;
using Ew4.Interface.Plans;

namespace Ew4.Service.Plans;

/// <summary>Platform 域：mod 面板（列出、激活/删除/导入/丢弃、上传暂存、提交安装）。</summary>
public sealed class ModPlans : IPlanModule
{
    private const string Resource = "mods";

    private readonly IModOperations _mod;
    private readonly IStep _renderPanel;

    public ModPlans(IModOperations mod)
    {
        _mod = mod;
        _renderPanel = Steps.Of("mod.render-panel", [], [PlanKeys.Response],
            ctx => ctx.Set(PlanKeys.Response, OperationResponse.Json(_mod.GetPanelStateJson())));
    }

    public void Register(PlanRegistry registry)
    {
        registry.Request(RequestNames.For(PlatformModOperationKind.ListMods))
            .Resource(Resource)
            .Step(_renderPanel)
            .Done();

        registry.Request(RequestNames.For(PlatformModOperationKind.MutateMods))
            .Resource(Resource)
            .Step(Steps.Of("mod.mutate", ctx => Mutate((ctx.Get(PlanKeys.Payload) as PlatformJsonPayload)?.Json)))
            .Step(_renderPanel)
            .Done();

        registry.Request(RequestNames.For(PlatformModOperationKind.PrepareImport))
            .Resource(Resource)
            .Step(Steps.Of("mod.stage-upload", [], [PlanKeys.Response], ctx =>
            {
                var file = ctx.Get(PlanKeys.Payload) as PlatformFilePayload
                    ?? throw new ArgumentException("PlatformFilePayload required.");
                ModImportPrepared prepared = _mod.StageUploadedFile(file.SourcePath, file.FileName);
                using var identity = JsonDocument.Parse(prepared.IdentityJson);
                ctx.Set(PlanKeys.Response, OperationResponse.Json(JsonSerializer.Serialize(new
                {
                    token = prepared.Token,
                    identity = identity.RootElement.Clone(),
                })));
            }))
            .Done();

        registry.Request(RequestNames.For(PlatformModOperationKind.CommitImport))
            .Resource(Resource)
            .Step(Steps.Of("mod.commit-import", ctx =>
            {
                var form = ctx.Get(PlanKeys.Payload) as PlatformFormCommitPayload
                    ?? throw new ArgumentException("PlatformFormCommitPayload required.");
                _mod.Commit(form.Token, form.Id, form.Name);
            }))
            .Step(_renderPanel)
            .Done();
    }

    private void Mutate(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string? Text(string name) => root.TryGetProperty(name, out JsonElement el) ? el.GetString() : null;

        switch (Text("op"))
        {
            case "activate":
                _mod.Activate(Text("key"));
                break;
            case "delete":
                if (!string.IsNullOrEmpty(Text("key")))
                {
                    _mod.Remove(Text("key")!);
                }

                break;
            case "import":
                if (!string.IsNullOrEmpty(Text("token")) && !string.IsNullOrEmpty(Text("id")) && !string.IsNullOrEmpty(Text("name")))
                {
                    _mod.Commit(Text("token")!, Text("id")!, Text("name")!);
                }

                break;
            case "discard":
                _mod.Discard(Text("token"));
                break;
        }
    }
}
