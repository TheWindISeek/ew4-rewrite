using System;
using System.IO;
using Ew4.Platform.Persistence.Io;
using Ew4.Platform.Persistence.Layout;

namespace Ew4.Platform.Persistence;

public static class SaveDirectoryLayout
{
    public static string Root(string stateDirectory, string modId, string contentFingerprint, string? workspacePreviewId = null)
    {
        if (!string.IsNullOrEmpty(workspacePreviewId))
        {
            return Path.Combine(Path.GetFullPath(stateDirectory), SessionPaths.User.Saves, "ws-" + workspacePreviewId);
        }

        string shortId = contentFingerprint.Length >= 12 ? contentFingerprint[..12] : contentFingerprint;
        return Path.Combine(Path.GetFullPath(stateDirectory), SessionPaths.User.Saves, modId, shortId);
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

    public static bool SlotFileExists(string root, SaveSlotId slot) =>
        PhysicalFiles.FileExists(SlotPath(root, slot));

    /// <summary>
    /// 战役/征服模式槽位 0：若按模式路径尚无文件但根目录仍有旧版 <c>session.json</c>，返回自动存档路径供上层校验场景模式后读档。
    /// </summary>
    public static string SlotPathWithLegacyAutosave(string root, SaveSlotId slot, bool tryLegacyAutosave)
    {
        string path = SlotPath(root, slot);
        if (!tryLegacyAutosave || slot.Mode == 0 || slot.Index != 0)
        {
            return path;
        }

        if (PhysicalFiles.FileExists(path))
        {
            return path;
        }

        string autosave = AutoSavePath(root);
        return PhysicalFiles.FileExists(autosave) ? autosave : path;
    }
}
