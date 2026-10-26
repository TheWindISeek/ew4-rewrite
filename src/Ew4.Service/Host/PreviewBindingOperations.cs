using System.Text.Json;
using Ew4.Interface.Runtime;
using Ew4.Platform.Persistence.Workspaces;

namespace Ew4.Service.Host;

public sealed class PreviewBindingOperations : IPreviewBindingOperations
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IRuntimeHost _runtime;
    private readonly WorkspaceStore _workspaces;

    public PreviewBindingOperations(IRuntimeHost runtime, WorkspaceStore workspaces)
    {
        _runtime = runtime;
        _workspaces = workspaces;
    }

    public string GetStateJson() => JsonSerializer.Serialize(_runtime.PreviewState, Json);

    public void Enter(string workspaceId)
    {
        string root = _workspaces.ResolveWorkspaceDirectory(workspaceId);
        _runtime.EnterWorkspacePreview(root, workspaceId);
    }

    public void Refresh(string workspaceId)
    {
        string root = _workspaces.ResolveWorkspaceDirectory(workspaceId);
        _runtime.RefreshWorkspacePreview(root, workspaceId);
    }

    public void Exit() => _runtime.ExitWorkspacePreview();
}
