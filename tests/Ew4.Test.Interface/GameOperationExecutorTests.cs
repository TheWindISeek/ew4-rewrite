using Ew4.Interface.Editor;
using Ew4.Interface.Host;
using Ew4.Interface.Mod;
using Ew4.Interface.Operations;
using Ew4.Interface.Runtime;
using Ew4.Platform.Mod.Snapshot;
using Ew4.Platform.Persistence.Packages;
using Ew4.Service.Host;
using Ew4.Service.Play;
using Ew4.Service.Plans;
using Xunit;

namespace Ew4.Test.Interface;

public sealed class GameOperationExecutorTests
{
    [Fact]
    public void Invoke_platform_list_mods_uses_mod_operations()
    {
        var mod = new RecordingModOperations();
        var executor = CreateExecutor(mod: mod);
        OperationResponse response = executor.Invoke(
            new PlatformModOperationDescriptor(PlatformModOperationKind.ListMods),
            new PlatformJsonPayload(null));

        Assert.Contains("\"mods\"", response.TextContent, StringComparison.Ordinal);
        Assert.Equal(1, mod.GetPanelStateCallCount);
    }

    [Fact]
    public void Invoke_editor_workspace_list_uses_workspace_bridge()
    {
        var workspace = new RecordingWorkspaceOperations();
        var executor = CreateExecutor(workspace: workspace);
        OperationResponse response = executor.Invoke(
            new EditorOperationDescriptor(EditorOperationKind.WorkspaceList),
            new EditorWorkspacePayload());

        Assert.Equal("[]", response.TextContent);
        Assert.Equal(1, workspace.ListCallCount);
    }

    [Fact]
    public void Invoke_editor_campaign_failure_is_400_with_code_and_message()
    {
        var executor = CreateExecutor();
        OperationResponse response = executor.Invoke(
            new EditorOperationDescriptor(EditorOperationKind.CampaignGet),
            new EditorWorkspacePayload(WorkspaceId: "ws", CampaignId: "c1"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("editor.nope", response.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Invoke_preview_status_runs_as_plan()
    {
        var executor = CreateExecutor();
        OperationResponse response = executor.Invoke(
            new EditorOperationDescriptor(EditorOperationKind.PreviewStatus),
            new EditorWorkspacePayload());

        Assert.Equal("{}", response.TextContent);
    }

    [Fact]
    public void Invoke_static_asset_delegates_to_static_service()
    {
        var staticAssets = new PlayStaticAssetService(new StubRuntimeHost(), new StubWebPaths());
        var executor = CreateExecutor(staticAssets: staticAssets);
        OperationResponse response = executor.Invoke(
            new StaticHttpAssetDescriptor("/conquest-pages.json"),
            new StaticAssetPayload());

        Assert.Equal(404, response.StatusCode);
    }

    private static GameOperationExecutor CreateExecutor(
        IModOperations? mod = null,
        IEditorWorkspaceOperations? workspace = null,
        PlayStaticAssetService? staticAssets = null)
    {
        mod ??= new RecordingModOperations();
        workspace ??= new RecordingWorkspaceOperations();
        staticAssets ??= new PlayStaticAssetService(new StubRuntimeHost(), new StubWebPaths());
        var runtime = new StubRuntimeHost();
        var play = new PlayHost(new Ew4.Platform.Engine.Engine());
        var registry = new PlanRegistry();
        new EditorPlans(new StubCampaignEditor(), new StubEditorOperations(), new StubPreviewBinding(), workspace).Register(registry);
        new PlayPlans(runtime, play).Register(registry);
        new ModPlans(mod).Register(registry);
        registry.Validate();

        return new GameOperationExecutor(runtime, new PlanRunner(registry), staticAssets);
    }

    private sealed class RecordingModOperations : IModOperations
    {
        public int GetPanelStateCallCount { get; private set; }

        public string GetPanelStateJson()
        {
            GetPanelStateCallCount++;
            return "{\"mods\":[]}";
        }

        public void Activate(string? modId) => throw new NotSupportedException();

        public void Remove(string modId) => throw new NotSupportedException();

        public ModImportPrepared StageUploadedFile(string sourcePath, string fileName) => throw new NotSupportedException();

        public void Discard(string? token) => throw new NotSupportedException();

        public void Commit(string token, string id, string name) => throw new NotSupportedException();
    }

    private sealed class RecordingWorkspaceOperations : IEditorWorkspaceOperations
    {
        public int ListCallCount { get; private set; }

        public string ListWorkspacesJson()
        {
            ListCallCount++;
            return "[]";
        }

        public string CreateWorkspaceJson(string id, string name, string? fromModId, string? mode = null) => throw new NotSupportedException();

        public string CapabilitiesJson(string workspaceId) => throw new NotSupportedException();

        public string MapListJson(string workspaceId) => throw new NotSupportedException();

        public string MapDescribeJson(string workspaceId, int mapId) => throw new NotSupportedException();

        public string MapPreviewJson(string workspaceId, int mapId, MapCropQuery? crop) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceId) => throw new NotSupportedException();

        public string ValidateJson(string workspaceId, Ew4.Interface.Common.ValidationLevel level) => throw new NotSupportedException();

        public string ExportJson(string workspaceId, Ew4.Interface.Common.ValidationLevel level, string? fileName, bool standalone = false) =>
            throw new NotSupportedException();

        public string ListCampaignsJson(string workspaceId) => throw new NotSupportedException();

        public byte[] ReadFile(string workspaceId, string relativePath) => throw new NotSupportedException();

        public string WriteFileJson(string workspaceId, string relativePath, byte[] body) => throw new NotSupportedException();
    }

    private sealed class StubRuntimeHost : IRuntimeHost
    {
        public IModSnapshot? ActiveSnapshot => null;

        public HostPreviewState PreviewState => new(false, null, null);

        public ModStatus Status() => new(Array.Empty<ModInfo>(), null, false);

        public void Activate(string? modId) => throw new NotSupportedException();

        public void Remove(string modId) => throw new NotSupportedException();

        public void EnterWorkspacePreview(string workspaceRoot, string workspaceId) => throw new NotSupportedException();

        public void RefreshWorkspacePreview(string workspaceRoot, string workspaceId) => throw new NotSupportedException();

        public void ExitWorkspacePreview() => throw new NotSupportedException();
    }

    private sealed class StubWebPaths : IGameWebPaths
    {
        public string WwwRootDirectory => Path.Combine(Path.GetTempPath(), "ew4-test-wwwroot-missing");
    }

    private sealed class StubBindingSource : IContentBindingSource
    {
        public ContentBinding? TryGetBinding() => null;
    }

    private sealed class StubCampaignEditor : ICampaignEditorOperations
    {
        public EditorOutcome<string> CreateWorkspaceCampaignJson(string workspaceId, byte[] createUtf8) =>
            throw new NotSupportedException();

        public EditorOutcome<byte[]> GetWorkspaceCampaignJson(string workspaceId, string campaignId) =>
            EditorOutcome<byte[]>.Fail("editor.nope", "no such campaign");

        public EditorOutcome<string> SaveWorkspaceCampaignJson(string workspaceId, string campaignId, byte[] jsonUtf8, bool smoke) =>
            throw new NotSupportedException();

        public EditorOutcome<string> RoundtripWorkspaceJson(string workspaceId, string campaignId) =>
            throw new NotSupportedException();

        public EditorOutcome<string> ApplyWorkspaceCampaignEditJson(string workspaceId, string campaignId, byte[] editUtf8) =>
            throw new NotSupportedException();

        public EditorOutcome<byte[]> BtlBytesToDocumentJson(string campaignId, byte[] btlBytes) =>
            throw new NotSupportedException();
    }

    private sealed class StubEditorOperations : IEditorOperations
    {
        public CommanderTableView ListCommanders(string workspaceId) => throw new NotSupportedException();

        public CommanderEntry GetCommander(string workspaceId, int legacyId) => throw new NotSupportedException();

        public CommanderSkillCatalogView SkillCatalog(string workspaceId) => throw new NotSupportedException();

        public EditorOutcome<CommanderEditOutcome> ApplyCommanderEdit(string workspaceId, CommanderEdit edit) =>
            throw new NotSupportedException();
    }

    private sealed class StubPreviewBinding : IPreviewBindingOperations
    {
        public void Enter(string workspaceId) => throw new NotSupportedException();

        public void Refresh(string workspaceId) => throw new NotSupportedException();

        public void Exit() => throw new NotSupportedException();

        public string GetStateJson() => "{}";
    }
}
