using Ew4.Interface.Mod;

namespace Ew4.Service.Host;

/// <summary>进程宿主门面：Pack 激活与创作预览，供 Service 编排。</summary>
public interface IRuntimeHost
{
    IModSnapshot? ActiveSnapshot { get; }

    HostPreviewState PreviewState { get; }

    ModStatus Status();

    void Activate(string? modId);

    void Remove(string modId);

    void EnterWorkspacePreview(string workspaceRoot, string workspaceId);

    void RefreshWorkspacePreview(string workspaceRoot, string workspaceId);

    void ExitWorkspacePreview();
}
