using Ew4.Platform.Persistence.Io;

namespace Ew4.Platform.Persistence.Import;

/// <summary>战役 BTL 导入落盘（唯一磁盘边界；内容由 Content 生成 JSON/BTL 字节）。</summary>
public static class BtlScenarioImportWriter
{
    public static void WriteScenarioJson(string scenariosDirectory, string scenarioId, string jsonUtf8) =>
        PhysicalFiles.WriteAllText(PhysicalFiles.Combine(scenariosDirectory, scenarioId + ".json"), jsonUtf8);

    public static void EnsureScenariosDirectory(string scenariosDirectory) =>
        PhysicalFiles.EnsureDirectory(scenariosDirectory);

    public static void WriteBtlBytes(string destinationPath, byte[] bytes) =>
        PhysicalFiles.WriteAllBytes(destinationPath, bytes);
}
