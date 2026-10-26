using System;

namespace Ew4.Interface.Runtime;

/// <summary>Runtime 当前内容上下文；Engine 与 Editor 共用。</summary>
public sealed record ContentBinding(
    string Revision,
    ContentBindingKind Kind,
    string ModId,
    string? WorkspaceId = null)
{
    public void EnsureWorkspace()
    {
        if (Kind != ContentBindingKind.WorkspacePreview || string.IsNullOrWhiteSpace(WorkspaceId))
        {
            throw new InvalidOperationException("Editor workspace binding is required.");
        }
    }
}
