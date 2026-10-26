using Ew4.Platform.Persistence.Layout;

namespace Ew4.Service.Host;

public sealed class Ew4InstallOptions
{
    public required InstallLayout Install { get; init; }

    public required string RepositoryRoot { get; init; }

    public string ModsDirectory => Install.ModsDirectory;

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
