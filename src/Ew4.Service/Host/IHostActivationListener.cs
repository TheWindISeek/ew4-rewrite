using Ew4.Interface.Mod;
using Ew4.Platform.Content;

namespace Ew4.Service.Host;

public sealed record HostActivationInfo(ResolvedContent Content, bool WorkspacePreview, string? WorkspaceId, string SaveRoot, string StateRoot)
{
    public IModSnapshot Snapshot => Content;
}

/// <summary>
/// Pack 激活或预览切换后的通知（Play 层重置局内状态等）。
/// </summary>
public interface IHostActivationListener
{
    void OnActivated(HostActivationInfo info);
}
