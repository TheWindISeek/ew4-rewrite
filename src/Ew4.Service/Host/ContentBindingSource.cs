using Ew4.Interface.Runtime;
using Ew4.Interface.Mod;

namespace Ew4.Service.Host;

public static class ContentBindingSource
{
    public static ContentBinding? FromHost(IRuntimeHost host)
    {
        IModSnapshot? snapshot = host.ActiveSnapshot;
        if (snapshot == null)
        {
            return null;
        }

        HostPreviewState preview = host.PreviewState;
        if (preview.Active && !string.IsNullOrWhiteSpace(preview.WorkspaceId))
        {
            return new ContentBinding(
                snapshot.Revision,
                ContentBindingKind.WorkspacePreview,
                snapshot.PackId,
                preview.WorkspaceId);
        }

        return new ContentBinding(snapshot.Revision, ContentBindingKind.InstalledMod, snapshot.PackId);
    }
}
