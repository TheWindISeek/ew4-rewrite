using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Io;

namespace Ew4.Platform.Persistence.Serialization;

/// <summary>游玩槽位文件（内层 JSON + 可选 <see cref="SlotSaveEnvelope"/>）。</summary>
public static class PlaySlotPersistence
{
    public static bool Exists(string path) => PhysicalFiles.FileExists(path);

    public static DateTimeOffset GetLastWriteTime(string path) =>
        new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);

    public static byte[] ReadInnerJson(string path, SlotSaveEnvelope.Binding? envelope, SaveReadLimits? limits = null)
    {
        limits ??= new();
        if (!PhysicalFiles.FileExists(path))
        {
            throw new RuleViolation("save.empty", "This save slot is empty");
        }

        long length = new FileInfo(path).Length;
        if (length > limits.MaximumBytes)
        {
            throw new RuleViolation("save.size", "Save exceeds reader budget");
        }

        return envelope != null
            ? SlotSaveEnvelope.ReadSlotJson(path, envelope, limits)
            : PhysicalFiles.ReadAllBytes(path);
    }

    public static void WriteInnerJson(string path, byte[] slotJson, SlotSaveEnvelope.Binding? envelope)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            PhysicalFiles.EnsureDirectory(parent);
        }

        if (envelope != null)
        {
            SlotSaveEnvelope.WriteAtomic(path, slotJson, envelope);
        }
        else
        {
            WorldSave.WriteAtomicBytes(path, slotJson);
        }
    }
}
