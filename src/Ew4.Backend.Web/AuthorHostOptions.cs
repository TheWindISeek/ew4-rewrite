using Ew4.Platform.Runtime.Layout;

namespace Ew4.Backend.Web;

public sealed class AuthorHostOptions
{
    public required InstallLayout Install { get; init; }

    public required string RepositoryRoot { get; init; }

    public string ModsDirectory => Install.ModsDirectory;

    public string PacksDirectory => ModsDirectory;

    /// <summary>用户数据根（<c>install.user</c>）：<c>active.json</c>、存档等。</summary>
    public string StateDirectory => Install.UserDirectory;

    public string WorkspacesDirectory => Install.WorkspacesDirectory;

    public string PendingImportsDirectory => Install.PendingImportsDirectory;

    public string DefaultWorkspaceId { get; init; } = "default";

    public string ResolveWorkspaceRoot(string? workspaceId = null)
    {
        string id = string.IsNullOrWhiteSpace(workspaceId) ? DefaultWorkspaceId : workspaceId.Trim();
        if (id.Contains('/') || id.Contains('\\') || id.Contains(".."))
        {
            throw new ArgumentException("Invalid workspace id: " + id, nameof(workspaceId));
        }

        return Path.Combine(WorkspacesDirectory, id);
    }
}
