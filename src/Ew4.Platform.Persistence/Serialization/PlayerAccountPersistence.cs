using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Io;

namespace Ew4.Platform.Persistence.Serialization;

/// <summary>本地 <c>account.json</c> 读写（原子替换）。</summary>
public static class PlayerAccountPersistence
{
    public const int DefaultReadBudgetBytes = 16 * 1024 * 1024;

    public static bool Exists(string path) => PhysicalFiles.FileExists(path);

    public static byte[] Read(string path, int maximumBytes = DefaultReadBudgetBytes)
    {
        if (!PhysicalFiles.FileExists(path))
        {
            throw new RuleViolation("account.missing", "Local account file is missing.");
        }

        long length = new FileInfo(path).Length;
        if (length > maximumBytes)
        {
            throw new RuleViolation("account.size", "Account exceeds read budget");
        }

        return PhysicalFiles.ReadAllBytes(path);
    }

    public static void WriteAtomic(string path, byte[] jsonUtf8) => WorldSave.WriteAtomicBytes(path, jsonUtf8);
}
