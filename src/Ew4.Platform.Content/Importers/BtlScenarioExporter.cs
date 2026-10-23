using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Platform.Content.Importers;

/// <summary>
/// 将 BTL 字节经 <see cref="ScenarioBtlCodec"/> 写回磁盘（与编辑器保存同一路径）。
/// </summary>
public static class BtlScenarioExporter
{
    public static void WriteRoundtrip(string destinationPath, byte[] btlBytes, string scenarioId)
    {
        ScenarioDocument document = ScenarioBtlCodec.Import(scenarioId, btlBytes);
        byte[] exported = ScenarioBtlCodec.Export(document, reencodeLayers: false);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.WriteAllBytes(destinationPath, exported);
    }
}
