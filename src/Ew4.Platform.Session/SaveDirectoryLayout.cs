using System;
using System.IO;
using Ew4.Platform.Content;
using Ew4.Platform.Session.Layout;

namespace Ew4.Platform.Session;

public static class SaveDirectoryLayout
{
    public static string Root(string stateDirectory, ResolvedContent content, string? workspacePreviewId = null)
    {
        if (!string.IsNullOrEmpty(workspacePreviewId))
        {
            return Path.Combine(Path.GetFullPath(stateDirectory), SessionPaths.User.Saves, "ws-" + workspacePreviewId);
        }

        string fingerprint = content.Fingerprint;
        string shortId = fingerprint.Length >= 12 ? fingerprint[..12] : fingerprint;
        return Path.Combine(Path.GetFullPath(stateDirectory), SessionPaths.User.Saves, content.Manifest.Id, shortId);
    }

    public static string AutoSavePath(string root) => Path.Combine(root, SessionPaths.User.Session);

    public static string SlotPath(string root, SaveSlotId slot)
    {
        if (slot.Index is < 0 or > 6 || slot.Mode is not (0 or 1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        if (slot.Mode == 0)
        {
            return slot.Index == 0
                ? AutoSavePath(root)
                : Path.Combine(root, "slot-" + slot.Index + ".json");
        }

        return Path.Combine(root, SessionPaths.User.Saves, "mode-" + slot.Mode, "slot-" + slot.Index + ".json");
    }

    public static string AccountPath(string root) => Path.Combine(root, SessionPaths.User.Account);
}
