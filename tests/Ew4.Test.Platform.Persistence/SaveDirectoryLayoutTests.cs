using Ew4.Platform.Persistence;
using Xunit;

namespace Ew4.Test.Platform.Persistence;

public sealed class SaveDirectoryLayoutTests
{
    [Fact]
    public void SlotPathWithLegacyAutosave_returns_autosave_when_mode_slot_missing()
    {
        string root = Path.Combine(Path.GetTempPath(), "ew4-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string autosave = SaveDirectoryLayout.AutoSavePath(root);
        File.WriteAllText(autosave, "{}");

        string resolved = SaveDirectoryLayout.SlotPathWithLegacyAutosave(
            root,
            new SaveSlotId(Mode: 1, Index: 0),
            tryLegacyAutosave: true);

        Assert.Equal(autosave, resolved);
    }

    [Fact]
    public void SlotPathWithLegacyAutosave_keeps_canonical_when_present()
    {
        string root = Path.Combine(Path.GetTempPath(), "ew4-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "saves", "mode-1"));
        string canonical = SaveDirectoryLayout.SlotPath(root, new SaveSlotId(1, 0));
        File.WriteAllText(canonical, "{}");
        File.WriteAllText(SaveDirectoryLayout.AutoSavePath(root), "{}");

        string resolved = SaveDirectoryLayout.SlotPathWithLegacyAutosave(
            root,
            new SaveSlotId(1, 0),
            tryLegacyAutosave: true);

        Assert.Equal(canonical, resolved);
    }
}
